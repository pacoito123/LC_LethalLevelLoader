using DunGen;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static DunGen.Graph.DungeonFlow;

namespace LethalLevelLoader
{
    [Serializable]
    public class ExtendedDungeonFlowWithRarity(ExtendedDungeonFlow newExtendedDungeonFlow, int newRarity)
    {
        public ExtendedDungeonFlow extendedDungeonFlow = newExtendedDungeonFlow;
        public int rarity = newRarity;

        public bool UpdateRarity(int newRarity) { if (newRarity > rarity) { rarity = newRarity; return (true); } return (false); }

        internal struct ExtendedDungeonFlowWithRarityComparer(bool ascending = true) : IComparer<ExtendedDungeonFlowWithRarity>
        {
            public readonly int Compare(ExtendedDungeonFlowWithRarity a, ExtendedDungeonFlowWithRarity b) => ascending ? a.rarity - b.rarity : b.rarity - a.rarity;
        }
    }

    public static class DungeonLoader
    {
        internal static GameObject defaultKeyPrefab;

        internal static void SelectDungeon()
        {
            Patches.RoundManager.dungeonGenerator.Generator.DungeonFlow = null;
            if (LethalLevelLoaderNetworkManager.Instance.IsServer)
                LethalLevelLoaderNetworkManager.Instance.GetRandomExtendedDungeonFlowServerRpc();
        }

        internal static void PrepareDungeon()
        {
            DungeonGenerator dungeonGenerator = Patches.RoundManager.dungeonGenerator.Generator;
            dungeonGenerator.retryCount = 50; //I shouldn't really do this but I'm curious if it silently helps some custom interiors

            ExtendedDungeonFlow currentExtendedDungeonFlow = DungeonManager.CurrentExtendedDungeonFlow;
            if (currentExtendedDungeonFlow == null || currentExtendedDungeonFlow.ContentType is ContentType.External) return;
            if (currentExtendedDungeonFlow.IsDynamicOutOfBoundsTriggerEnabled)
                dungeonGenerator.OnGenerationStatusChanged += PatchOutOfBoundsTriggers;

            ExtendedLevel currentExtendedLevel = LevelManager.CurrentExtendedLevel;
            PatchFireEscapes(dungeonGenerator, currentExtendedLevel);
            PatchDynamicGlobalProps(dungeonGenerator, currentExtendedDungeonFlow);
        }

        public static float GetClampedDungeonSize()
        {
            ExtendedDungeonFlow extendedDungeonFlow = DungeonManager.CurrentExtendedDungeonFlow;
            ExtendedLevel extendedLevel = LevelManager.CurrentExtendedLevel;
            float calculatedMultiplier = CalculateDungeonMultiplier(extendedLevel, DungeonManager.CurrentExtendedDungeonFlow);
            if (DungeonManager.CurrentExtendedDungeonFlow != null && DungeonManager.CurrentExtendedDungeonFlow.IsDynamicDungeonSizeRestrictionEnabled == true)
            {
                if (calculatedMultiplier > extendedDungeonFlow.DynamicDungeonSizeMax)
                    calculatedMultiplier = Mathf.Lerp(calculatedMultiplier, extendedDungeonFlow.DynamicDungeonSizeMax, extendedDungeonFlow.DynamicDungeonSizeLerpRate); //This is how vanilla does it.
                else if (calculatedMultiplier < extendedDungeonFlow.DynamicDungeonSizeMin)
                    calculatedMultiplier = Mathf.Lerp(calculatedMultiplier, extendedDungeonFlow.DynamicDungeonSizeMin, extendedDungeonFlow.DynamicDungeonSizeLerpRate);//This is how vanilla does it.
                DebugHelper.Log("Current ExtendedLevel: " + extendedLevel.NumberlessPlanetName + " ExtendedLevel DungeonSize Is: " + extendedLevel.SelectableLevel.factorySizeMultiplier + " | Overriding DungeonSize To: " + calculatedMultiplier, DebugType.User);
            }
            else
                DebugHelper.Log("CurrentLevel: " + extendedLevel.NumberlessPlanetName + " DungeonSize Is: " + extendedLevel.SelectableLevel.factorySizeMultiplier + " | Leaving DungeonSize As: " + calculatedMultiplier, DebugType.User);
            return (calculatedMultiplier);
        }

        public static float CalculateDungeonMultiplier(ExtendedLevel extendedLevel, ExtendedDungeonFlow extendedDungeonFlow)
        {
            foreach (IndoorMapType indoorMapType in Patches.RoundManager.dungeonFlowTypes)
                if (indoorMapType.dungeonFlow == extendedDungeonFlow.DungeonFlow)
                    return (extendedLevel.SelectableLevel.factorySizeMultiplier / indoorMapType.MapTileSize * Patches.RoundManager.mapSizeMultiplier);
            return 1f;
        }

        internal static List<EntranceTeleport> GetEntranceTeleports()
        {
            List<EntranceTeleport> entranceTeleports = new List<EntranceTeleport>();
            foreach (GameObject rootObject in LevelLoader.currentLevelScene.GetRootGameObjects())
                foreach (EntranceTeleport entranceTeleport in rootObject.GetComponentsInChildren<EntranceTeleport>())
                    entranceTeleports.Add(entranceTeleport);
            return (entranceTeleports);
        }

        internal static void PatchFireEscapes(DungeonGenerator dungeonGenerator, ExtendedLevel extendedLevel)
        {
            string debugString = "Fire Exit Patch Report, Details Below;\n\n";

            List<EntranceTeleport> allEntranceTeleports = GetEntranceTeleports();
            if (allEntranceTeleports.Count == 0)
            {
                DebugHelper.LogFatal("No EntranceTeleports Found In The Scene!", DebugType.User);
                foreach (GlobalPropSettings globalPropSettings in dungeonGenerator.DungeonFlow.GlobalProps)
                    if (globalPropSettings.ID == 1231)
                    {
                        globalPropSettings.Count = new(0, 0);
                        break;
                    }
                return;
            }

            List<EntranceTeleport> mainEntrances = allEntranceTeleports.FindAll(entrance => entrance.entranceId == 0);
            if (mainEntrances.Count == 0)
                DebugHelper.LogError("No EntranceTeleport For Main Entrance Found In The Scene! A Fire Exit Will Act As Main Entrance Instead...", DebugType.User);
            else if (mainEntrances.Count > 1)
                DebugHelper.LogError($"'{mainEntrances.Count}' EntranceTeleports For Main Entrance Found In The Scene! Only One Will Act As Main Entrance...", DebugType.User);

            for (int i = 0; i < allEntranceTeleports.Count; i++)
                allEntranceTeleports[i].entranceId = i;

            debugString += $"EntranceTeleports Found, {extendedLevel.NumberlessPlanetName} Contains {allEntranceTeleports.Count} Entrances! ( {allEntranceTeleports.Count - 1} Fire Escapes)\n";
            debugString += $"Main Entrance: {allEntranceTeleports[0].name} (Entrance ID: {allEntranceTeleports[0].entranceId})\n";
            foreach (EntranceTeleport entranceTeleport in allEntranceTeleports)
                if (entranceTeleport.entranceId != 0)
                    debugString += $"Alternate Entrance: {entranceTeleport.name} (Entrance ID: {entranceTeleport.entranceId})\n";

            foreach (GlobalPropSettings globalPropSettings in dungeonGenerator.DungeonFlow.GlobalProps)
                if (globalPropSettings.ID == 1231)
                {
                    debugString += $"Found Fire Escape GlobalProp: (ID: 1231), Modifying Spawn rate Count From ({globalPropSettings.Count.Min},{globalPropSettings.Count.Max}) To ({allEntranceTeleports.Count - 1},{allEntranceTeleports.Count - 1})\n";
                    globalPropSettings.Count = new IntRange(allEntranceTeleports.Count - 1, allEntranceTeleports.Count - 1); //-1 Because .Count includes the Main Entrance.
                    break;
                }

            DebugHelper.Log(debugString + '\n', DebugType.User);
        }

        public static void PatchDynamicGlobalProps(DungeonGenerator dungeonGenerator, ExtendedDungeonFlow extendedDungeonFlow)
        {
            foreach (GlobalPropCountOverride globalPropOverride in extendedDungeonFlow.GlobalPropCountOverridesList)
                foreach (GlobalPropSettings globalProp in dungeonGenerator.DungeonFlow.GlobalProps)
                    if (globalPropOverride.globalPropID == globalProp.ID)
                    {
                        globalProp.Count.Min *= Mathf.RoundToInt(Mathf.Lerp(1, (dungeonGenerator.LengthMultiplier / Patches.RoundManager.mapSizeMultiplier), globalPropOverride.globalPropCountScaleRate));
                        globalProp.Count.Max *= globalProp.Count.Max * Mathf.RoundToInt(Mathf.Lerp(1, (dungeonGenerator.LengthMultiplier / Patches.RoundManager.mapSizeMultiplier), globalPropOverride.globalPropCountScaleRate));
                    }
        }

        public static void PatchOutOfBoundsTriggers(DungeonGenerator generator, GenerationStatus status)
        {
            if (status != GenerationStatus.Complete) return;
            generator.OnGenerationStatusChanged -= PatchOutOfBoundsTriggers;

            float lowestPoint = generator.CurrentDungeon.transform.TransformPoint(generator.CurrentDungeon.Bounds.min).y;
            foreach (GameObject rootObject in SceneManager.GetSceneByName(Patches.StartOfRound.currentLevel.sceneName).GetRootGameObjects())
                foreach (OutOfBoundsTrigger trigger in rootObject.GetComponentsInChildren<OutOfBoundsTrigger>(includeInactive: true))
                {
                    Vector3 position = trigger.transform.position;
                    position.y = lowestPoint;
                    trigger.transform.position = position;
                }
        }
    }
}
