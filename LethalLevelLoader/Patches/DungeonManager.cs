using DunGen.Graph;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class DungeonManager
    {
        public static ExtendedDungeonFlow CurrentExtendedDungeonFlow
        {
            get
            {
                if (Patches.RoundManager == null || Patches.RoundManager.dungeonGenerator == null || Patches.RoundManager.dungeonGenerator.Generator == null || Patches.RoundManager.dungeonGenerator.Generator.DungeonFlow == null)
                    return (null);
                if (field == null || Patches.RoundManager.dungeonGenerator.Generator.DungeonFlow != field.DungeonFlow)
                    if (PatchedContent.TryGetExtendedContent(Patches.RoundManager.dungeonGenerator.Generator.DungeonFlow, out field))
                        DebugHelper.Log($"Dungeon switched to: {field.DungeonFlow.name}", DebugType.IAmBatby);
                return (field);
            }
        }
        public static readonly DungeonEvents GlobalDungeonEvents = new DungeonEvents();

        internal static void PatchVanillaDungeonLists()
        {
            if (PatchedContent.CustomExtendedDungeonFlows.Count == 0) return;

            List<IndoorMapType> indoorMapTypes = [.. Patches.RoundManager.dungeonFlowTypes];
            List<AudioClip> firstTimeDungeonAudios = [.. Patches.RoundManager.firstTimeDungeonAudios];
            foreach (ExtendedDungeonFlow extendedDungeonFlow in PatchedContent.CustomExtendedDungeonFlows)
            {
                if (extendedDungeonFlow.ContentType is ContentType.External) continue;
                extendedDungeonFlow.DungeonID = indoorMapTypes.Count;
                IndoorMapType newIndoorMapType = new(extendedDungeonFlow.DungeonFlow, extendedDungeonFlow.MapTileSize, extendedDungeonFlow.FirstTimeDungeonAudio)
                {
                    restrictBounds = extendedDungeonFlow.RestrictBounds,
                    cullingTileDepth = extendedDungeonFlow.CullingTileDepth
                };
                indoorMapTypes.Add(newIndoorMapType);
                if (extendedDungeonFlow.FirstTimeDungeonAudio != null && !firstTimeDungeonAudios.Contains(extendedDungeonFlow.FirstTimeDungeonAudio))
                    firstTimeDungeonAudios.Add(extendedDungeonFlow.FirstTimeDungeonAudio);
            }
            Patches.RoundManager.dungeonFlowTypes = [.. indoorMapTypes];
            Patches.RoundManager.firstTimeDungeonAudios = [.. firstTimeDungeonAudios];
        }

        public static List<ExtendedDungeonFlowWithRarity> GetValidExtendedDungeonFlows(ExtendedLevel extendedLevel, bool debugResults)
        {
            DebugStopwatch.StartStopWatch("Get Valid ExtendedDungeonFlows");
            List<ExtendedDungeonFlowWithRarity> returnExtendedDungeonFlowsList = new List<ExtendedDungeonFlowWithRarity>();
            List<ExtendedDungeonFlowWithRarity> potentialExtendedDungeonFlowsList = new List<ExtendedDungeonFlowWithRarity>();

            foreach (ExtendedDungeonFlow vanillaDungeonFlow in PatchedContent.VanillaExtendedDungeonFlows)
                potentialExtendedDungeonFlowsList.Add(new ExtendedDungeonFlowWithRarity(vanillaDungeonFlow, 0));

            //Add Custom DungeonFlows
            foreach (ExtendedDungeonFlow customDungeonFlow in PatchedContent.CustomExtendedDungeonFlows)
                potentialExtendedDungeonFlowsList.Add(new ExtendedDungeonFlowWithRarity(customDungeonFlow, 0));

            foreach (ExtendedDungeonFlowWithRarity customDungeonFlow in new List<ExtendedDungeonFlowWithRarity>(potentialExtendedDungeonFlowsList))
            {
                customDungeonFlow.rarity = customDungeonFlow.extendedDungeonFlow.LevelMatchingProperties.GetDynamicRarity(extendedLevel);
                if (customDungeonFlow.rarity != 0)
                    returnExtendedDungeonFlowsList.Add(customDungeonFlow);
            }

            if (debugResults == true)
            {
                string debugString = "ExtendedLevel <-> ExtendedDungeonFlow Dynamic Matching Report." + "\n\n";

                debugString += "Info For ExtendedLevel: " + extendedLevel.name + " | Planet Name: " + extendedLevel.NumberlessPlanetName + " | Content Tags: ";
                foreach (ContentTag tag in extendedLevel.ContentTags)
                    debugString += tag.contentTagName + ", ";
                debugString = debugString.TrimEnd([',', ' ']);
                debugString += " | Route Price: " + extendedLevel.RoutePrice + " | Current Weather: " + extendedLevel.SelectableLevel.currentWeather.ToString();
                debugString += '\n';

                List<ExtendedDungeonFlow> viableDungeonFlows = returnExtendedDungeonFlowsList.ConvertAll(d => d.extendedDungeonFlow);
                debugString += "Unviable ExtendedDungeonFlows: ";
                foreach (ExtendedDungeonFlowWithRarity extendedDungeonFlowWithRarity in potentialExtendedDungeonFlowsList)
                    if (!viableDungeonFlows.Contains(extendedDungeonFlowWithRarity.extendedDungeonFlow))
                        debugString += extendedDungeonFlowWithRarity.extendedDungeonFlow.DungeonName + ", ";
                debugString = debugString.TrimEnd([',', ' ']);
                debugString += '\n';

                returnExtendedDungeonFlowsList.Sort(new ExtendedDungeonFlowWithRarity.ExtendedDungeonFlowWithRarityComparer(ascending: false));

                debugString += "Viable ExtendedDungeonFlows: ";
                foreach (ExtendedDungeonFlowWithRarity extendedDungeonFlowWithRarity in returnExtendedDungeonFlowsList)
                    debugString += extendedDungeonFlowWithRarity.extendedDungeonFlow.DungeonName + " (" + extendedDungeonFlowWithRarity.rarity + ")" + ", ";
                debugString = debugString.TrimEnd([',', ' ']);

                DebugHelper.Log(debugString + '\n', DebugType.User);
            }

            DebugStopwatch.StopStopWatch("Get Valid ExtendedDungeonFlows");

            return (returnExtendedDungeonFlowsList);
        }

        internal static void RefreshDungeonFlowIDs()
        {
            //DebugHelper.Log("Re-Adjusting DungeonFlowTypes Array For Late Arriving Vanilla DungeonFlow", DebugType.User);

            List<DungeonFlow> cachedDungeonFlowTypes = new List<DungeonFlow>();
            List<IndoorMapType> indoorMapTypes = new List<IndoorMapType>();
            foreach (ExtendedDungeonFlow vanillaDungeonFlow in PatchedContent.VanillaExtendedDungeonFlows)
            {
                vanillaDungeonFlow.DungeonID = cachedDungeonFlowTypes.Count;
                cachedDungeonFlowTypes.Add(vanillaDungeonFlow.DungeonFlow);
            }
            foreach (ExtendedDungeonFlow customDungeonFlow in PatchedContent.CustomExtendedDungeonFlows)
            {
                customDungeonFlow.DungeonID = cachedDungeonFlowTypes.Count;
                cachedDungeonFlowTypes.Add(customDungeonFlow.DungeonFlow);
            }

            foreach (DungeonFlow dungeonFlow in cachedDungeonFlowTypes)
            {
                IndoorMapType newIndoorMapType = new IndoorMapType(dungeonFlow, 1f, null);
                indoorMapTypes.Add(newIndoorMapType);
            }
            Patches.RoundManager.dungeonFlowTypes = [.. indoorMapTypes];
        }

        internal static bool TryGetExtendedDungeonFlow(DungeonFlow dungeonFlow, out ExtendedDungeonFlow returnExtendedDungeonFlow, ContentType contentType = ContentType.Any) =>
            PatchedContent.TryGetExtendedContent(dungeonFlow, out returnExtendedDungeonFlow) && (contentType is ContentType.Any || contentType == returnExtendedDungeonFlow.ContentType);

        internal static bool TryGetExtendedDungeonFlow(IndoorMapType indoorMapType, out ExtendedDungeonFlow returnExtendedDungeonFlow, ContentType contentType = ContentType.Any) => (TryGetExtendedDungeonFlow(indoorMapType.dungeonFlow, out returnExtendedDungeonFlow, contentType));
    }
}