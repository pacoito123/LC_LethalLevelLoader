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
            OriginalContent.ItemGroups.AddRange(foundItemGroups);
        }

        internal static void TryScrapeVanillaUnlockableItems(StartOfRound startOfRound)
        {
            if (OriginalContent.FootstepSurfaces.Count == 0)
                OriginalContent.UnlockableItems.AddRange(startOfRound.unlockablesList.unlockables);
        }

        internal static void TryScrapeVanillaFootstepSurfaces(StartOfRound startOfRound)
        {
            if (OriginalContent.FootstepSurfaces.Count == 0)
                OriginalContent.FootstepSurfaces.AddRange(startOfRound.footstepSurfaces);
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

                OriginalContent.SelectableLevels.AddRange(startOfRound.levels);
                OriginalContent.MoonsCatalogue.AddRange(TerminalManager.Terminal.moonsCatalogueList);
            }
        }

        internal static void ObtainSpecialContentReferences()
        {
            foreach (GrabbableObject sceneGrabbableObject in Patches.StartOfRound.shipAnimator.GetComponentsInChildren<GrabbableObject>(includeInactive: false))
                if (sceneGrabbableObject.itemProperties != null && sceneGrabbableObject.itemProperties.spawnPrefab != null)
                    if (!OriginalContent.Items.Contains(sceneGrabbableObject.itemProperties))
                        OriginalContent.Items.Add(sceneGrabbableObject.itemProperties);

            for (int i = 0; i < OriginalContent.Enemies.Count; i++)
            {
                EnemyType enemyType = OriginalContent.Enemies[i];
                if (enemyType == null || enemyType.enemyPrefab == null) continue;
                if (enemyType.enemyPrefab.TryGetComponent(out NutcrackerEnemyAI nutcracker))
                {
                    if (nutcracker.gunPrefab != null && nutcracker.gunPrefab.TryGetComponent(out GrabbableObject shotgun) && shotgun.itemProperties != null)
                        if (!OriginalContent.Items.Contains(shotgun.itemProperties))
                            OriginalContent.Items.Add(shotgun.itemProperties);
                    if (nutcracker.shotgunShellPrefab != null && nutcracker.shotgunShellPrefab.TryGetComponent(out GrabbableObject shell) && shell.itemProperties != null)
                        if (!OriginalContent.Items.Contains(shell.itemProperties))
                            OriginalContent.Items.Add(shell.itemProperties);
                }
                else if (enemyType.enemyPrefab.TryGetComponent(out ButlerEnemyAI butler))
                {
                    if (butler.knifePrefab != null && butler.knifePrefab.TryGetComponent(out GrabbableObject knife) && knife.itemProperties != null)
                        if (!OriginalContent.Items.Contains(knife.itemProperties))
                            OriginalContent.Items.Add(knife.itemProperties);
                    if (butler.butlerBeesEnemyType != null && butler.butlerBeesEnemyType.enemyPrefab != null)
                        if (!OriginalContent.Enemies.Contains(butler.butlerBeesEnemyType))
                            OriginalContent.Enemies.Add(butler.butlerBeesEnemyType);
                }
                else if (enemyType.enemyPrefab.TryGetComponent(out RedLocustBees bees))
                {
                    if (bees.hivePrefab != null && bees.hivePrefab.TryGetComponent(out GrabbableObject hive) && hive.itemProperties != null)
                        if (!OriginalContent.Items.Contains(hive.itemProperties))
                            OriginalContent.Items.Add(hive.itemProperties);
                }
                else if (enemyType.enemyPrefab.TryGetComponent(out GiantKiwiAI sapsucker))
                {
                    if (sapsucker.eggPrefab != null && sapsucker.eggPrefab.TryGetComponent(out GrabbableObject egg) && egg.itemProperties != null)
                        if (!OriginalContent.Items.Contains(egg.itemProperties))
                            OriginalContent.Items.Add(egg.itemProperties);
                }
                else if (enemyType.enemyPrefab.TryGetComponent(out CadaverGrowthAI cadaverGrowth))
                {
                    if (cadaverGrowth.bloomEnemyType != null && cadaverGrowth.bloomEnemyType.enemyPrefab != null)
                        if (!OriginalContent.Enemies.Contains(cadaverGrowth.bloomEnemyType))
                            OriginalContent.Enemies.Add(cadaverGrowth.bloomEnemyType);
                }
            }

            foreach (SpawnableEnemyWithRarity enemyWithRarity in Patches.RoundManager.WeedEnemies)
                if (enemyWithRarity != null && enemyWithRarity.enemyType != null && enemyWithRarity.enemyType.enemyPrefab != null)
                    if (!OriginalContent.Enemies.Contains(enemyWithRarity.enemyType))
                        OriginalContent.Enemies.Add(enemyWithRarity.enemyType);
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
            OriginalContent.AudioMixerGroups.AddRange(extractedMixerGroups.Values);

            for (int i = 0; i < allMixerSnapshots.Length; i++)
            {
                AudioMixerSnapshot mixerSnapshot = allMixerSnapshots[i];
                if (mixerSnapshot != null && !string.IsNullOrEmpty(mixerSnapshot.name))
                    extractedMixerSnapshots.TryAdd(mixerSnapshot.name, mixerSnapshot);
            }
            OriginalContent.AudioMixerSnapshots.AddRange(extractedMixerSnapshots.Values);
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
            OriginalContent.ReverbPresets.AddRange(extractedPresets.Values);
        }

        internal static void ExtractSelectableLevelReferences(SelectableLevel selectableLevel)
        {
            if (selectableLevel == null) return;

            foreach (SpawnableEnemyWithRarity enemyWithRarity in selectableLevel.Enemies)
                TryAddReference(OriginalContent.Enemies, enemyWithRarity.enemyType);

            foreach (SpawnableEnemyWithRarity enemyWithRarity in selectableLevel.OutsideEnemies)
                TryAddReference(OriginalContent.Enemies, enemyWithRarity.enemyType);

            foreach (SpawnableEnemyWithRarity enemyWithRarity in selectableLevel.DaytimeEnemies)
                TryAddReference(OriginalContent.Enemies, enemyWithRarity.enemyType);

            if (selectableLevel.specialEnemyRarity != null && selectableLevel.specialEnemyRarity.overrideEnemy != null)
                TryAddReference(OriginalContent.Enemies, selectableLevel.specialEnemyRarity.overrideEnemy);

            foreach (IndoorMapHazard indoorMapHazard in selectableLevel.indoorMapHazards)
                TryAddReference(OriginalContent.IndoorMapHazards, indoorMapHazard.hazardType);

            foreach (SpawnableOutsideObjectWithRarity spawnableOutsideObject in selectableLevel.spawnableOutsideObjects)
                TryAddReference(OriginalContent.SpawnableOutsideObjects, spawnableOutsideObject.spawnableObject);

            TryAddReference(OriginalContent.LevelAmbienceLibraries, selectableLevel.levelAmbienceClips);

            foreach (IntWithRarity dungeonWithRarity in selectableLevel.dungeonFlowTypes)
                if (dungeonWithRarity.overrideLevelAmbience != null)
                    TryAddReference(OriginalContent.LevelAmbienceLibraries, dungeonWithRarity.overrideLevelAmbience);
        }

        internal static void TryAddReference<T>(List<T> referenceList, T reference) where T : UnityEngine.Object
        {
            if (!referenceList.Contains(reference))
                referenceList.Add(reference);
        }
    }
}
