using DunGen;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LethalLevelLoader.Tools
{
    internal static class ContentRestorer
    {
        // Keep a dictionary of commonly-used NetworkPrefabs to restore (e.g. 'EntranceTeleportA') for faster lookup.
        internal static readonly Dictionary<string, GameObject> cachedNetworkPrefabs = [];
        internal static readonly HashSet<Object> objectsToDestroy = [];

        internal static void RestoreVanillaDungeonAssetReferences(ExtendedDungeonFlow extendedDungeonFlow)
        {
            if (extendedDungeonFlow == null)
            {
                DebugHelper.LogError("Tried To Restore Vanilla Assets For Null ExtendedDungeonFlow! Returning!", DebugType.User);
                return;
            }
            if (extendedDungeonFlow.DungeonFlow == null)
            {
                DebugHelper.LogError("Tried To Restore Vanilla Assets For ExtendedDungeonFlow " + extendedDungeonFlow.DungeonName + " But DungeonFlow Was Null! Returning!", DebugType.User);
                return;
            }

            List<RandomScrapSpawn> tileScrapSpawns = [];
            foreach (Tile tile in extendedDungeonFlow.AllTiles)
            {
                tile.GetComponentsInChildren(includeInactive: true, tileScrapSpawns);
                foreach (RandomScrapSpawn randomScrapSpawn in tileScrapSpawns)
                    TryRestoreRandomScrapSpawn(randomScrapSpawn);
                RestoreAudioAssetReferencesInParent(tile.gameObject);
            }

            foreach (RandomMapObject randomMapObject in extendedDungeonFlow.DungeonFlow.GetRandomMapObjects(extendedDungeonFlow.AllTiles))
                RestoreRandomMapObject(randomMapObject);
        }

        internal static void RestoreVanillaLevelAssetReferences(ExtendedLevel extendedLevel)
        {
            if (extendedLevel == null)
            {
                DebugHelper.LogError("Tried To Restore Vanilla Assets For Null ExtendedLevel! Returning!", DebugType.User);
                return;
            }
            if (extendedLevel.SelectableLevel == null)
            {
                DebugHelper.LogError("Tried To Restore Vanilla Assets For ExtendedLevel " + extendedLevel.NumberlessPlanetName + " But SelectableLevel Was Null! Returning!", DebugType.User);
                return;
            }

            DebugHelper.Log($"Restoring Vanilla References for SelectableLevel: {extendedLevel.SelectableLevel.name}", DebugType.IAmBatby);

            foreach (SpawnableItemWithRarity spawnableItem in extendedLevel.SelectableLevel.spawnableScrap)
            {
                if (spawnableItem == null || spawnableItem.spawnableItem == null || spawnableItem.spawnableItem.spawnPrefab != null) continue;
                Item vanillaItem = OriginalContent.Items.Find(item => string.Equals(item.name, spawnableItem.spawnableItem.name, StringComparison.Ordinal));
                if (vanillaItem != null)
                    spawnableItem.spawnableItem = RestoreAsset(spawnableItem.spawnableItem, vanillaItem);
            }
            int removedScrap = extendedLevel.SelectableLevel.spawnableScrap.RemoveAll(item => item == null || item.spawnableItem == null || item.spawnableItem.spawnPrefab == null);
            if (removedScrap > 0)
                DebugHelper.LogWarning($"Removed '{removedScrap}' missing or empty scrap spawns in SelectableLevel: {extendedLevel.SelectableLevel.name}", DebugType.User);

            static bool ShouldRemoveEnemyRarity(SpawnableEnemyWithRarity enemyRarity)
            {
                if (enemyRarity == null || enemyRarity.enemyType == null) return true;
                if (enemyRarity.enemyType.enemyPrefab != null) return false;

                EnemyType vanillaEnemy = OriginalContent.Enemies.Find(enemy => string.Equals(enemy.name, enemyRarity.enemyType.name, StringComparison.Ordinal));
                if (vanillaEnemy == null) return true;

                enemyRarity.enemyType = RestoreAsset(enemyRarity.enemyType, vanillaEnemy);
                return false;
            }
            int removedEnemies = extendedLevel.SelectableLevel.Enemies.RemoveAll(ShouldRemoveEnemyRarity);
            removedEnemies += extendedLevel.SelectableLevel.OutsideEnemies.RemoveAll(ShouldRemoveEnemyRarity);
            removedEnemies += extendedLevel.SelectableLevel.DaytimeEnemies.RemoveAll(ShouldRemoveEnemyRarity);
            if (removedEnemies > 0)
                DebugHelper.LogWarning($"Removed '{removedEnemies}' missing or empty enemy spawns in SelectableLevel: {extendedLevel.SelectableLevel.name}", DebugType.User);

            OverrideEnemyRarity specialEnemy = extendedLevel.SelectableLevel.specialEnemyRarity;
            if (specialEnemy != null && specialEnemy.overrideEnemy != null && specialEnemy.overrideEnemy.enemyPrefab == null)
            {
                EnemyType vanillaEnemy = OriginalContent.Enemies.Find(enemy => string.Equals(enemy.name, specialEnemy.overrideEnemy.name, StringComparison.Ordinal));
                if (vanillaEnemy == null)
                {
                    extendedLevel.SelectableLevel.specialEnemyRarity = null;
                    DebugHelper.LogWarning($"Removed missing or empty OverrideEnemyRarity in SelectableLevel: {extendedLevel.SelectableLevel.name}", DebugType.User);
                }
                else
                    specialEnemy.overrideEnemy = RestoreAsset(specialEnemy.overrideEnemy, vanillaEnemy);
            }

            if ((extendedLevel.SelectableLevel.indoorMapHazards == null || extendedLevel.SelectableLevel.indoorMapHazards.Length == 0)
                && extendedLevel.SelectableLevel.spawnableMapObjects?.Length > 0) // Pre-v80
            {
                List<IndoorMapHazard> indoorMapHazards = new(extendedLevel.SelectableLevel.spawnableMapObjects.Length);
                foreach (SpawnableMapObject spawnableMapObject in extendedLevel.SelectableLevel.spawnableMapObjects)
                {
                    if (spawnableMapObject == null || spawnableMapObject.prefabToSpawn == null || spawnableMapObject.prefabToSpawn.TryGetComponent(out NetworkObject _)) continue;
                    IndoorMapHazardType vanillaHazard = OriginalContent.IndoorMapHazards.Find(mapHazard => string.Equals(mapHazard.prefabToSpawn.name, spawnableMapObject.prefabToSpawn.name, StringComparison.Ordinal));
                    if (vanillaHazard != null)
                    {
                        IndoorMapHazard indoorMapHazard = new()
                        {
                            hazardType = vanillaHazard,
                            numberToSpawn = spawnableMapObject.numberToSpawn
                        };
                        indoorMapHazards.Add(indoorMapHazard);
                    }
                }
                extendedLevel.SelectableLevel.indoorMapHazards = [.. indoorMapHazards];
                if (indoorMapHazards.Count > 0)
                    DebugHelper.Log($"Converted '{indoorMapHazards.Count}' SpawnableMapObjects to IndoorMapHazard spawns in SelectableLevel: {extendedLevel.SelectableLevel.name}", DebugType.Developer);
            }
            else if (extendedLevel.SelectableLevel.indoorMapHazards?.Length > 0)
            {
                List<IndoorMapHazard> indoorMapHazards = [.. extendedLevel.SelectableLevel.indoorMapHazards];
                foreach (IndoorMapHazard indoorMapHazard in indoorMapHazards)
                {
                    if (indoorMapHazard == null || indoorMapHazard.hazardType == null || indoorMapHazard.hazardType.prefabToSpawn != null) continue;
                    IndoorMapHazardType vanillaHazard = OriginalContent.IndoorMapHazards.Find(mapHazard => string.Equals(mapHazard.name, indoorMapHazard.hazardType.name, StringComparison.Ordinal));
                    if (vanillaHazard != null)
                        indoorMapHazard.hazardType = RestoreAsset(indoorMapHazard.hazardType, vanillaHazard);
                }
                int removedMapHazards = indoorMapHazards.RemoveAll(mapHazard => mapHazard == null || mapHazard.hazardType == null || mapHazard.hazardType.prefabToSpawn == null);
                if (removedMapHazards > 0)
                {
                    extendedLevel.SelectableLevel.indoorMapHazards = [.. indoorMapHazards];
                    DebugHelper.LogWarning($"Removed '{removedMapHazards}' missing or empty IndoorMapHazard spawns in SelectableLevel: {extendedLevel.SelectableLevel.name}", DebugType.User);
                }
            }

            List<SpawnableOutsideObjectWithRarity> spawnableOutsideObjects = [.. extendedLevel.SelectableLevel.spawnableOutsideObjects];
            foreach (SpawnableOutsideObjectWithRarity spawnableOutsideObject in spawnableOutsideObjects)
            {
                if (spawnableOutsideObject == null || spawnableOutsideObject.spawnableObject == null || spawnableOutsideObject.spawnableObject.prefabToSpawn != null) continue;
                SpawnableOutsideObject vanillaOutsideObject = OriginalContent.SpawnableOutsideObjects.Find(outsideObject => string.Equals(outsideObject.name, spawnableOutsideObject.spawnableObject.name, StringComparison.Ordinal));
                if (vanillaOutsideObject != null)
                    spawnableOutsideObject.spawnableObject = RestoreAsset(spawnableOutsideObject.spawnableObject, vanillaOutsideObject);
            }
            int removedOutsideObjects = spawnableOutsideObjects.RemoveAll(outsideObject => outsideObject == null || outsideObject.spawnableObject == null || outsideObject.spawnableObject.prefabToSpawn == null);
            if (removedOutsideObjects > 0)
            {
                extendedLevel.SelectableLevel.spawnableOutsideObjects = [.. spawnableOutsideObjects];
                DebugHelper.LogWarning($"Removed '{removedOutsideObjects}' missing or empty SpawnableOutsideObject spawns in SelectableLevel: {extendedLevel.SelectableLevel.name}", DebugType.User);
            }

            if (extendedLevel.SelectableLevel.levelAmbienceClips != null)
            {
                LevelAmbienceLibrary vanillaAmbienceLibrary = OriginalContent.LevelAmbienceLibraries.Find(ambienceLibrary => string.Equals(ambienceLibrary.name, extendedLevel.SelectableLevel.levelAmbienceClips.name, StringComparison.Ordinal));
                if (vanillaAmbienceLibrary != null)
                    extendedLevel.SelectableLevel.levelAmbienceClips = RestoreAsset(extendedLevel.SelectableLevel.levelAmbienceClips, vanillaAmbienceLibrary);
            }
        }

        internal static void RestoreVanillaItemAssetReferences()
        {
            EnemyType vanillaRadMechEnemyType = OriginalContent.Enemies.Find(enemy => string.Equals(enemy.enemyName, "RadMech", StringComparison.Ordinal));
            if (vanillaRadMechEnemyType != null)
            {
                foreach (LungProp lungProp in Resources.FindObjectsOfTypeAll<LungProp>())
                {
                    if (lungProp.radMechEnemyType == vanillaRadMechEnemyType) continue;
                    if (lungProp.radMechEnemyType == null)
                    {
                        lungProp.radMechEnemyType = vanillaRadMechEnemyType;
                        continue;
                    }
                    if (string.Equals(lungProp.radMechEnemyType.name, "RadMech", StringComparison.Ordinal))
                    {
                        if (lungProp.radMechEnemyType.enemyPrefab != null || lungProp.radMechEnemyType.nestSpawnPrefab != null)
                            DebugHelper.LogWarning("LungProp " + lungProp.name + " Bundles An Additional RadMech Enemy! Fields radMechEnemyType And nestSpawnPrefab Should Be Empty!", DebugType.User);
                        lungProp.radMechEnemyType = RestoreAsset(lungProp.radMechEnemyType, vanillaRadMechEnemyType);
                    }
                }
            }
            else
                DebugHelper.LogError("Could Not Find Vanilla RadMech Enemy Reference To Assign To LungProp Items!", DebugType.User);

            EnemyType vanillaMaskedEnemyType = OriginalContent.Enemies.Find(enemy => string.Equals(enemy.enemyName, "Masked", StringComparison.Ordinal));
            if (vanillaMaskedEnemyType != null)
            {
                foreach (HauntedMaskItem hauntedMask in Resources.FindObjectsOfTypeAll<HauntedMaskItem>())
                {
                    if (hauntedMask.mimicEnemy == vanillaMaskedEnemyType) continue;
                    if (hauntedMask.mimicEnemy == null)
                    {
                        hauntedMask.mimicEnemy = vanillaMaskedEnemyType;
                        continue;
                    }
                    if (string.Equals(hauntedMask.mimicEnemy.name, "MaskedPlayerEnemy", StringComparison.Ordinal))
                    {
                        if (hauntedMask.mimicEnemy.enemyPrefab != null)
                            DebugHelper.LogWarning("HauntedMaskItem " + hauntedMask.name + " Bundles An Additional Masked Enemy! Field mimicEnemy Should Be Empty!", DebugType.User);
                        hauntedMask.mimicEnemy = RestoreAsset(hauntedMask.mimicEnemy, vanillaMaskedEnemyType);
                    }
                }
            }
            else
                DebugHelper.LogError("Could Not Find Vanilla Masked Enemy Reference To Assign To HauntedMaskItem Items!", DebugType.User);
        }

        internal static void RestoreAudioAssetReferencesInParent(GameObject parent)
        {
            //DebugHelper.Log("Validating & Restoring AudioSources");
            foreach (AudioSource audioSource in parent.GetComponentsInChildren<AudioSource>(includeInactive: true))
            {
                //DebugHelper.Log("Trying To Find And Restore Audio Assets In: " + audioSource.gameObject.name);
                if (audioSource.outputAudioMixerGroup == null)
                {
                    if (audioSource.gameObject.name != null)
                        DebugHelper.LogWarning("Audio Restoration Warning: " + audioSource.gameObject.name + " Has Missing AudioMixerGroup", DebugType.Developer);
                }
                else
                    TryRestoreAudioSource(audioSource);
            }

            foreach (AudioReverbTrigger audioReverbTrigger in parent.GetComponentsInChildren<AudioReverbTrigger>(includeInactive: true))
            {
                if (audioReverbTrigger.reverbPreset == null)
                    DebugHelper.LogWarning("Audio Restoration Warning: " + audioReverbTrigger.gameObject.name + " Has Missing ReverbPreset", DebugType.Developer);
                else
                {
                    foreach (ReverbPreset reverbPreset in OriginalContent.ReverbPresets)
                        if (reverbPreset != null && reverbPreset.name != null && audioReverbTrigger.reverbPreset.name == reverbPreset.name)
                        {
                            DebugHelper.Log("Restoring ReverbPreset: " + audioReverbTrigger.reverbPreset.name + " In AudioReverbTrigger: " + audioReverbTrigger.gameObject.name, DebugType.Developer);
                            audioReverbTrigger.reverbPreset = RestoreAsset(audioReverbTrigger.reverbPreset, reverbPreset);
                        }
                }
                foreach (switchToAudio audioChange in audioReverbTrigger.audioChanges)
                {
                    if (audioChange.audio == null)
                        DebugHelper.LogWarning("Audio Restoration Warning: " + audioReverbTrigger.gameObject.name + " Has Missing AudioChange AudioSource", DebugType.Developer);
                    else
                        TryRestoreAudioSource(audioChange.audio);
                    if (audioChange.changeToClip == null)
                        DebugHelper.LogWarning("Audio Restoration Warning: " + audioReverbTrigger.gameObject.name + " Has Missing AudioChange AudioClip", DebugType.Developer);
                }
            }
        }

        internal static void TryRestoreAudioSource(AudioSource audioSource)
        {
            if (audioSource.outputAudioMixerGroup == null) return;

            AudioMixerGroup targetMixerGroup = audioSource.outputAudioMixerGroup;
            AudioMixer targetMixer = audioSource.outputAudioMixerGroup.audioMixer;

            AudioMixerGroup restoredMixerGroup = null;
            AudioMixer restoredMixer = null;

            foreach (AudioMixer vanillaMixer in OriginalContent.AudioMixers)
                if (targetMixer.name == vanillaMixer.name)
                    restoredMixer = RestoreAsset(targetMixer, vanillaMixer, destroyOnReplace: false);

            foreach (AudioMixerGroup vanillaMixerGroup in OriginalContent.AudioMixerGroups)
                if (targetMixerGroup.name == vanillaMixerGroup.name)
                    restoredMixerGroup = RestoreAsset(targetMixerGroup, vanillaMixerGroup, destroyOnReplace: false);

            if (restoredMixerGroup != null && restoredMixer != null)
            {
                //if (audioSource.clip != null)
                //DebugHelper.Log("Restoring Audio Assets On AudioSource: " + audioSource.gameObject.name + ", AudioSource contained AudioClip: " + audioSource.clip.name);
                //else
                //DebugHelper.Log("Restoring Audio Assets On AudioSource: " + audioSource.gameObject.name);
                audioSource.outputAudioMixerGroup = restoredMixerGroup;
            }
        }

        internal static void TryRestoreShader(Material customMaterial, Shader vanillaShader, LocalKeyword[] enabledKeywords = null)
        {
            if (vanillaShader == null || customMaterial == null || customMaterial.shader == null)
                return;

            if (customMaterial.shader == vanillaShader || string.IsNullOrEmpty(customMaterial.shader.name))
                return;

            if (string.Equals(customMaterial.shader.name, vanillaShader.name, StringComparison.Ordinal))
            {
                customMaterial.shader = vanillaShader;

                if (enabledKeywords != null)
                    customMaterial.enabledKeywords = enabledKeywords;
            }
        }

        internal static bool TryRestoreNetworkPrefab(GameObject prefab, out GameObject registeredPrefab)
        {
            registeredPrefab = null;
            if (prefab == null || string.IsNullOrEmpty(prefab.name)) return (false);
            if (!cachedNetworkPrefabs.TryGetValue(prefab.name, out registeredPrefab))
            {
                foreach (NetworkPrefab networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.m_Prefabs)
                {
                    if (string.Equals(networkPrefab.Prefab.name, prefab.name, StringComparison.Ordinal))
                    {
                        cachedNetworkPrefabs.Add(prefab.name, networkPrefab.Prefab);
                        registeredPrefab = RestoreAsset(prefab, networkPrefab.Prefab);
                        break;
                    }
                }
            }
            return (registeredPrefab != null);
        }

        internal static bool TryRestoreSpawnSyncedObject(SpawnSyncedObject spawnSyncedObject)
        {
            if (spawnSyncedObject != null && TryRestoreNetworkPrefab(spawnSyncedObject.spawnPrefab, out GameObject registeredPrefab))
            {
                spawnSyncedObject.spawnPrefab = registeredPrefab;
                return (true);
            }
            return (false);
        }

        internal static bool TryRestoreRandomScrapSpawn(RandomScrapSpawn randomScrapSpawn)
        {
            if (randomScrapSpawn != null && randomScrapSpawn.spawnableItems != null && !string.IsNullOrEmpty(randomScrapSpawn.spawnableItems.name))
            {
                ItemGroup vanillaItemGroup = OriginalContent.ItemGroups.Find(itemGroup => string.Equals(itemGroup.name, randomScrapSpawn.spawnableItems.name, StringComparison.Ordinal));
                if (vanillaItemGroup != null)
                {
                    randomScrapSpawn.spawnableItems = RestoreAsset(randomScrapSpawn.spawnableItems, vanillaItemGroup);
                    return (true);
                }
            }
            return (false);
        }

        internal static void RestoreRandomMapObject(RandomMapObject randomMapObject)
        {
            if (randomMapObject == null) return;
            randomMapObject.spawnablePrefabs ??= [];

            for (int i = 0; i < randomMapObject.spawnablePrefabs.Count; i++)
            {
                GameObject spawnablePrefab = randomMapObject.spawnablePrefabs[i];
                if (spawnablePrefab == null)
                {
                    DebugHelper.LogWarning("Map Object Restoration Warning: " + randomMapObject.name + " Has Missing RandomMapObject", DebugType.Developer);
                    randomMapObject.spawnablePrefabs.RemoveAt(i--);
                    continue;
                }

                IndoorMapHazardType vanillaHazardType = OriginalContent.IndoorMapHazards.Find(hazardType => hazardType != null && hazardType.prefabToSpawn != null && hazardType.prefabToSpawn.name == spawnablePrefab.name);
                if (vanillaHazardType != null)
                    randomMapObject.spawnablePrefabs[i] = RestoreAsset(spawnablePrefab, vanillaHazardType.prefabToSpawn, destroyOnReplace: false);
            }
        }

        internal static void RestoreBridgeTrigger(BridgeTrigger bridgeTrigger)
        {
            if (bridgeTrigger == null) return;
            if (bridgeTrigger.giantTypes == null || bridgeTrigger.giantTypes.Length == 0)
            {
                bridgeTrigger.giantTypes = [.. EnemyManager.GiantEnemyTypes];
                return;
            }
            List<EnemyType> giantTypes = [.. bridgeTrigger.giantTypes];
            for (int i = 0; i < giantTypes.Count; i++)
            {
                EnemyType giantType = bridgeTrigger.giantTypes[i];
                if (giantType == null)
                {
                    DebugHelper.LogWarning("Bridge Trigger Object Restoration Warning: " + bridgeTrigger.name + " Has Missing EnemyType", DebugType.Developer);
                    giantTypes.RemoveAt(i--);
                    continue;
                }
                if (EnemyManager.GiantEnemyTypes.Contains(giantType)) continue;
                bool found = false;
                foreach (EnemyType enemyType in EnemyManager.GiantEnemyTypes)
                    if (string.Equals(giantType.name, enemyType.name, StringComparison.Ordinal))
                    {
                        giantTypes[i] = RestoreAsset(giantTypes[i], enemyType);
                        found = true;
                        break;
                    }
                if (!found)
                {
                    DebugHelper.LogWarning("Bridge Trigger Object Restoration Warning: " + bridgeTrigger.name + " Has Missing EnemyType " + giantTypes[i].name, DebugType.Developer);
                    giantTypes.RemoveAt(i--);
                }
            }
        }

        internal static T RestoreAsset<T>(Object currentAsset, T newAsset, bool debugAction = false, bool destroyOnReplace = true) where T : Object
        {
            if (currentAsset != null && newAsset != null)
            {
                if (currentAsset == newAsset)
                    return newAsset;

                if (debugAction == true)
                    DebugHelper.Log("Restoring " + currentAsset.GetType().ToString() + ": Old Asset Name: " + currentAsset.name + " , New Asset Name: ", DebugType.Developer);

                if (destroyOnReplace == true)
                    objectsToDestroy.Add(currentAsset);
            }
            else
                DebugHelper.LogWarning("Asset Restoration Failed, Null Reference Found!", DebugType.Developer);
            return (newAsset);
        }

        internal static void DestroyRestoredAssets(bool debugAction = false)
        {
            foreach (Object objectToDestroy in objectsToDestroy)
            {
                if (objectToDestroy == null) continue;
                if (debugAction == true)
                    DebugHelper.Log("Destroying: " + objectToDestroy.name, DebugType.Developer);
                Object.DestroyImmediate(objectToDestroy);
            }
            objectsToDestroy.Clear();
        }
    }
}
