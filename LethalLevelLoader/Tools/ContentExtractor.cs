using DunGen.Graph;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace LethalLevelLoader
{
    public class ContentExtractor
    {
        internal static void TryScrapeVanillaItems(StartOfRound startOfRound)
        {
            HashSet<ItemGroup> foundItemGroups = [];
            foreach (Item item in startOfRound.allItemsList.itemsList)
            {
                if (item.spawnPrefab != null)
                {
                    TryAddReference(OriginalContent.Items, item);
                    foundItemGroups.UnionWith(item.spawnPositionTypes);
                }
            }
            OriginalContent.ItemGroups = [.. foundItemGroups];
        }

        internal static void TryScrapeVanillaUnlockableItems(StartOfRound startOfRound)
        {
            OriginalContent.UnlockableItems = [.. startOfRound.unlockablesList.unlockables];
        }

        internal static void TryScrapeVanillaFootstepSurfaces(StartOfRound startOfRound)
        {
            if (OriginalContent.FootstepSurfaces.Count == 0)
                OriginalContent.FootstepSurfaces = [.. startOfRound.footstepSurfaces];
        }

        internal static void TryScrapeVanillaContent(StartOfRound startOfRound, RoundManager roundManager)
        {
            if (Plugin.IsSetupComplete == false)
            {
                if (startOfRound != null)
                {
                    foreach (IndoorMapType indoorFlowType in roundManager.dungeonFlowTypes)
                        TryAddReference(OriginalContent.DungeonFlows, indoorFlowType.dungeonFlow);

                    foreach (SelectableLevel selectableLevel in startOfRound.levels)
                        ExtractSelectableLevelReferences(selectableLevel);

                    /* foreach (IndoorMapType indoorFlowType in roundManager.dungeonFlowTypes)
                        ExtractDungeonFlowReferences(indoorFlowType.dungeonFlow); */
                }
                if (TerminalManager.Terminal.currentNode != null)
                    TryAddReference(OriginalContent.TerminalNodes, TerminalManager.Terminal.currentNode);

                foreach (TerminalNode terminalNode in TerminalManager.Terminal.terminalNodes.terminalNodes)
                    TryAddReference(OriginalContent.TerminalNodes, terminalNode);


                foreach (TerminalNode terminalNode in TerminalManager.Terminal.terminalNodes.specialNodes)
                    TryAddReference(OriginalContent.TerminalNodes, terminalNode);

                foreach (TerminalNode terminalNode in TerminalManager.Terminal.enemyFiles)
                    TryAddReference(OriginalContent.TerminalNodes, terminalNode);


                foreach (TerminalNode terminalNode in TerminalManager.Terminal.logEntryFiles)
                    TryAddReference(OriginalContent.TerminalNodes, terminalNode);


                foreach (TerminalNode terminalNode in TerminalManager.Terminal.ShipDecorSelection)
                    TryAddReference(OriginalContent.TerminalNodes, terminalNode);
                foreach (TerminalKeyword terminalKeyword in TerminalManager.Terminal.terminalNodes.allKeywords)
                {
                    TryAddReference(OriginalContent.TerminalKeywords, terminalKeyword);
                    if (terminalKeyword.compatibleNouns != null)
                        foreach (CompatibleNoun compatibleNoun in terminalKeyword.compatibleNouns)
                            if (compatibleNoun.result != null)
                                TryAddReference(OriginalContent.TerminalNodes, compatibleNoun.result);
                    if (terminalKeyword.specialKeywordResult != null)
                        TryAddReference(OriginalContent.TerminalNodes, terminalKeyword.specialKeywordResult);
                }
                foreach (TerminalNode terminalNode in new List<TerminalNode>(OriginalContent.TerminalNodes))
                    if (terminalNode.terminalOptions != null)
                        foreach (CompatibleNoun compatibleNoun in terminalNode.terminalOptions)
                            if (compatibleNoun.result != null)
                                TryAddReference(OriginalContent.TerminalNodes, compatibleNoun.result);

                ExtractMemoryLoadedAudioMixerGroups();
                ExtractMemoryLoadedReverbPresets();

                OriginalContent.SelectableLevels = [.. startOfRound.levels];
                OriginalContent.MoonsCatalogue = [.. TerminalManager.Terminal.moonsCatalogueList];

            }
            //DebugHelper.DebugScrapedVanillaContent();
        }

        internal static void TryScrapeCustomContent()
        {
            /*
            foreach (EnemyType enemyType in Resources.FindObjectsOfTypeAll(typeof(EnemyType)))
                if (!OriginalContent.Enemies.Contains(enemyType))
                    PatchedContent.Enemies.Add(enemyType);

            foreach (Item item in Resources.FindObjectsOfTypeAll(typeof(Item)))
                if (!OriginalContent.Items.Contains(item))
                    PatchedContent.Items.Add(item);*/
        }

        internal static void ObtainSpecialItemReferences()
        {
            foreach (GrabbableObject sceneGrabbableObject in Patches.StartOfRound.shipAnimator.gameObject.GetComponentsInChildren<GrabbableObject>())
                if (sceneGrabbableObject.itemProperties != null && !OriginalContent.Items.Contains(sceneGrabbableObject.itemProperties))
                    if (sceneGrabbableObject.itemProperties.spawnPrefab != null)
                        OriginalContent.Items.Add(sceneGrabbableObject.itemProperties);

            foreach (EnemyType enemyType in OriginalContent.Enemies)
            {
                if (enemyType.name == "Nutcracker_0")
                {
                    NutcrackerEnemyAI nutcrackerEnemy = enemyType.enemyPrefab.GetComponent<NutcrackerEnemyAI>();
                    if (nutcrackerEnemy != null)
                    {
                        OriginalContent.Items.Add(nutcrackerEnemy.gunPrefab.GetComponent<GrabbableObject>().itemProperties);
                        OriginalContent.Items.Add(nutcrackerEnemy.shotgunShellPrefab.GetComponent<GrabbableObject>().itemProperties);
                    }
                }
                else if (enemyType.name == "Butler_0")
                {
                    ButlerEnemyAI butlerEnemy = enemyType.enemyPrefab.GetComponent<ButlerEnemyAI>();
                    if (butlerEnemy != null)
                        OriginalContent.Items.Add(butlerEnemy.knifePrefab.GetComponent<GrabbableObject>().itemProperties);
                }
                else if (enemyType.name == "RedLocustBees")
                {
                    RedLocustBees beesEnemy = enemyType.enemyPrefab.GetComponent<RedLocustBees>();
                    if (beesEnemy != null)
                        OriginalContent.Items.Add(beesEnemy.hivePrefab.GetComponent<GrabbableObject>().itemProperties);
                }
            }
        }

        internal static void ExtractMemoryLoadedAudioMixerGroups()
        {
            AudioMixerGroup[] allMixerGroups = Resources.FindObjectsOfTypeAll<AudioMixerGroup>();
            AudioMixerSnapshot[] allMixerSnapshots = Resources.FindObjectsOfTypeAll<AudioMixerSnapshot>();

            Dictionary<string, AudioMixerGroup> extractedMixerGroups = new Dictionary<string, AudioMixerGroup>(allMixerGroups.Length);
            Dictionary<string, AudioMixerSnapshot> extractedMixerSnapshots = new Dictionary<string, AudioMixerSnapshot>(allMixerSnapshots.Length);

            for (int i = 0; i < allMixerGroups.Length; i++)
            {
                AudioMixerGroup mixerGroup = allMixerGroups[i];
                if (mixerGroup != null && !string.IsNullOrEmpty(mixerGroup.name))
                    extractedMixerGroups.TryAdd(mixerGroup.name, mixerGroup);
            }
            OriginalContent.AudioMixerGroups = [.. extractedMixerGroups.Values];

            for (int i = 0; i < allMixerSnapshots.Length; i++)
            {
                AudioMixerSnapshot mixerSnapshot = allMixerSnapshots[i];
                if (mixerSnapshot != null && !string.IsNullOrEmpty(mixerSnapshot.name))
                    extractedMixerSnapshots.TryAdd(mixerSnapshot.name, mixerSnapshot);
            }
            OriginalContent.AudioMixerSnapshots = [.. extractedMixerSnapshots.Values];
        }

        internal static void ExtractMemoryLoadedReverbPresets()
        {
            // A little bit less bad...
            ReverbPreset[] allReverbPresets = Resources.FindObjectsOfTypeAll<ReverbPreset>();
            Dictionary<string, ReverbPreset> extractedPresets = new Dictionary<string, ReverbPreset>(allReverbPresets.Length);

            for (int i = allReverbPresets.Length - 1; i >= 0; i--) // Vanilla reverb presets are at the end of the list, due to interiors loading theirs first.
            {
                ReverbPreset reverbPreset = allReverbPresets[i];
                if (reverbPreset != null && reverbPreset.name != null)
                    extractedPresets.TryAdd(reverbPreset.name, reverbPreset);
            }
            OriginalContent.ReverbPresets = [.. extractedPresets.Values];
        }

        internal static void ExtractSelectableLevelReferences(SelectableLevel selectableLevel)
        {
            foreach (SpawnableEnemyWithRarity enemyWithRarity in selectableLevel.Enemies)
                TryAddReference(OriginalContent.Enemies, enemyWithRarity.enemyType);

            foreach (SpawnableEnemyWithRarity enemyWithRarity in selectableLevel.OutsideEnemies)
                TryAddReference(OriginalContent.Enemies, enemyWithRarity.enemyType);

            foreach (SpawnableEnemyWithRarity enemyWithRarity in selectableLevel.DaytimeEnemies)
                TryAddReference(OriginalContent.Enemies, enemyWithRarity.enemyType);

            foreach (IndoorMapHazard indoorMapHazard in selectableLevel.indoorMapHazards)
                TryAddReference(OriginalContent.IndoorMapHazards, indoorMapHazard.hazardType);

            foreach (SpawnableOutsideObjectWithRarity spawnableOutsideObject in selectableLevel.spawnableOutsideObjects)
                TryAddReference(OriginalContent.SpawnableOutsideObjects, spawnableOutsideObject.spawnableObject);

            TryAddReference(OriginalContent.LevelAmbienceLibraries, selectableLevel.levelAmbienceClips);
        }

        internal static void ExtractDungeonFlowReferences(DungeonFlow dungeonFlow)
        {
            /*foreach (Tile tile in dungeonFlow.GetTiles())
                foreach (RandomScrapSpawn randomScrapSpawn in tile.gameObject.GetComponentsInChildren<RandomScrapSpawn>())
                    TryAddReference(OriginalContent.ItemGroups, randomScrapSpawn.spawnableItems);*/
        }

        internal static void TryAddReference<T>(List<T> referenceList, T reference) where T : UnityEngine.Object
        {
            if (!referenceList.Contains(reference))
                referenceList.Add(reference);
        }
    }
}
