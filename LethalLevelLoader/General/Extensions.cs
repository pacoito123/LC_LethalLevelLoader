using DunGen;
using DunGen.Graph;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class Extensions
    {
        private static readonly Regex sanitizeRegex = new Regex(@"(\s*[^\p{L}])", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex skipToLetterRegex = new Regex(@"(^[^\p{L}]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex stripSpecialCharactersRegex = new Regex(@"([^\p{L}\d\s])", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IEnumerable<Tile> GetTiles(this DungeonFlow dungeonFlow)
        {
            if (dungeonFlow == null) return [];
            HashSet<Tile> tilesList = new HashSet<Tile>();
            HashSet<TileSet> tileSetsList = new HashSet<TileSet>();

            foreach (GraphNode dungeonNode in dungeonFlow.Nodes)
                foreach (TileSet dungeonTileSet in dungeonNode?.TileSets)
                    if (dungeonTileSet != null && tileSetsList.Add(dungeonTileSet))
                        tilesList.UnionWith(GetTilesInTileSet(dungeonTileSet));

            foreach (TileInjectionRule tileInjectionRule in dungeonFlow.TileInjectionRules)
                if (tileInjectionRule?.TileSet != null && tileSetsList.Add(tileInjectionRule.TileSet))
                    tilesList.UnionWith(GetTilesInTileSet(tileInjectionRule.TileSet));

            foreach (GraphLine dungeonLine in dungeonFlow.Lines)
                foreach (DungeonArchetype dungeonArchetype in dungeonLine?.DungeonArchetypes)
                {
                    HashSet<TileSet> archetypeTileSets = [.. dungeonArchetype.TileSets];
                    archetypeTileSets.UnionWith(dungeonArchetype.BranchCapTileSets);

                    foreach (TileSet dungeonTileSet in archetypeTileSets)
                        if (dungeonTileSet != null && tileSetsList.Add(dungeonTileSet))
                            tilesList.UnionWith(GetTilesInTileSet(dungeonTileSet));
                }

            return (tilesList);
        }

        public static IEnumerable<Tile> GetTilesInTileSet(this TileSet tileSet)
        {
            if (tileSet == null) return [];
            HashSet<Tile> tilesList = new HashSet<Tile>();

            if (tileSet.TileWeights != null && tileSet.TileWeights.Weights != null)
                foreach (GameObjectChance dungeonTileWeight in tileSet.TileWeights.Weights)
                    if (dungeonTileWeight != null && dungeonTileWeight.Value != null)
                    {
                        Tile tile = dungeonTileWeight.Value.GetComponentInChildren<Tile>(includeInactive: false);
                        if (tile != null)
                            tilesList.Add(tile);
                    }

            return (tilesList);
        }

        public static IEnumerable<RandomMapObject> GetRandomMapObjects(this DungeonFlow dungeonFlow)
        {
            if (dungeonFlow == null) return [];
            return dungeonFlow.GetRandomMapObjects(dungeonFlow.GetTiles());
        }

        public static IEnumerable<RandomMapObject> GetRandomMapObjects(this DungeonFlow dungeonFlow, IEnumerable<Tile> allTiles)
        {
            if (dungeonFlow == null) return [];
            List<RandomMapObject> returnList = new List<RandomMapObject>();

            List<RandomMapObject> tileRandomMapObjects = new List<RandomMapObject>();
            foreach (Tile dungeonTile in allTiles)
            {
                dungeonTile.GetComponentsInChildren(includeInactive: true, tileRandomMapObjects);
                returnList.AddRange(tileRandomMapObjects);
            }

            return (returnList);
        }

        public static IEnumerable<SpawnSyncedObject> GetSpawnSyncedObjects(this DungeonFlow dungeonFlow)
        {
            if (dungeonFlow == null) return [];
            return dungeonFlow.GetSpawnSyncedObjects(dungeonFlow.GetTiles());
        }

        public static IEnumerable<SpawnSyncedObject> GetSpawnSyncedObjects(this DungeonFlow dungeonFlow, IEnumerable<Tile> allTiles)
        {
            if (dungeonFlow == null) return [];
            HashSet<SpawnSyncedObject> returnList = new HashSet<SpawnSyncedObject>();

            foreach (Tile dungeonTile in allTiles)
            {
                foreach (Doorway dungeonDoorway in dungeonTile.gameObject.GetComponentsInChildren<Doorway>())
                {
                    foreach (GameObjectWeight doorwayTileWeight in dungeonDoorway.ConnectorPrefabWeights)
                        returnList.UnionWith(doorwayTileWeight.GameObject.GetComponentsInChildren<SpawnSyncedObject>());
                    foreach (GameObjectWeight doorwayTileWeight in dungeonDoorway.BlockerPrefabWeights)
                        returnList.UnionWith(doorwayTileWeight.GameObject.GetComponentsInChildren<SpawnSyncedObject>());
                }
                returnList.UnionWith(dungeonTile.gameObject.GetComponentsInChildren<SpawnSyncedObject>());
            }

            return (returnList);
        }

        public static void AddReferences(this CompatibleNoun compatibleNoun, TerminalKeyword firstNoun, TerminalNode firstResult)
        {
            compatibleNoun.noun = firstNoun;
            compatibleNoun.result = firstResult;
        }

        public static void AddCompatibleNoun(this TerminalKeyword terminalKeyword, TerminalKeyword newNoun, TerminalNode newResult)
        {
            terminalKeyword.compatibleNouns ??= [];
            CompatibleNoun newCompatibleNoun = new CompatibleNoun(newNoun, newResult);
            terminalKeyword.compatibleNouns = [.. terminalKeyword.compatibleNouns.AddItem(newCompatibleNoun)];
        }

        public static void AddCompatibleNoun(this TerminalNode terminalNode, TerminalKeyword newNoun, TerminalNode newResult)
        {
            terminalNode.terminalOptions ??= [];
            CompatibleNoun newCompatibleNoun = new CompatibleNoun(newNoun, newResult);
            terminalNode.terminalOptions = [.. terminalNode.terminalOptions.AddItem(newCompatibleNoun)];
        }

        public static void Add(this IntWithRarity intWithRarity, int id, int rarity)
        {
            intWithRarity.id = id;
            intWithRarity.rarity = rarity;
        }

        public static bool ContainsSanitized(this string input, string[] comparisons, bool bothWays = false)
        {
            foreach (string comparison in comparisons)
                if (!string.IsNullOrEmpty(comparison) && input.ContainsSanitized(comparison, bothWays))
                    return true;
            return false;
        }

        public static bool ContainsSanitized(this string input, string comparison, bool bothWays = false)
        {
            (string, string) sanitized = (input.Sanitized(), comparison.Sanitized());
            return sanitized.Item1.Contains(sanitized.Item2, StringComparison.Ordinal) || (bothWays && sanitized.Item2.Contains(sanitized.Item1, StringComparison.Ordinal));
        }

        public static string Sanitized(this string input, bool toLower = true)
        {
            string sanitizedInput = sanitizeRegex.Replace(input, string.Empty);
            return toLower ? sanitizedInput.ToLowerInvariant() : sanitizedInput;
        }

        public static string RemoveWhitespace(this string input)
        {
            return string.Join(string.Empty, input.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }

        public static string SkipToLetters(this string input)
        {
            return skipToLetterRegex.Replace(input, string.Empty);
        }

        public static string StripSpecialCharacters(this string input)
        {
            return stripSpecialCharactersRegex.Replace(input, string.Empty).Trim();
        }

        public static string Truncate(this string input, int length)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return (input.Length <= length) ? input : input[..length];
        }

        public static List<DungeonFlow> GetDungeonFlows(this RoundManager roundManager)
        {
            List<DungeonFlow> dungeonFlows = new List<DungeonFlow>(roundManager.dungeonFlowTypes.Length);
            for (int i = 0; i < roundManager.dungeonFlowTypes.Length; i++)
                if (roundManager.dungeonFlowTypes[i] != null && roundManager.dungeonFlowTypes[i].dungeonFlow != null)
                    dungeonFlows.Add(roundManager.dungeonFlowTypes[i].dungeonFlow);
            return (dungeonFlows);
        }

        public static T TryAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (!gameObject.TryGetComponent(out T component))
                component = gameObject.AddComponent<T>();
            return component;
        }
    }
}