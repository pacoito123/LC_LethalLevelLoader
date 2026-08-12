using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class EnemyManager
    {
        public static HashSet<EnemyType> TinyEnemyTypes { get; } = [];
        public static HashSet<EnemyType> GiantEnemyTypes { get; } = [];
        public static HashSet<EnemyType> MediumEnemyTypes { get; } = [];

        public static void RefreshDynamicEnemyTypeRarityOnAllExtendedLevels()
        {
            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                InjectCustomEnemyTypesIntoLevelViaDynamicRarity(extendedLevel);
        }

        public static void InjectCustomEnemyTypesIntoLevelViaDynamicRarity(ExtendedLevel extendedLevel, ExtendedDungeonFlow extendedDungeonFlow = null, bool debugResults = false)
        {
            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.CustomExtendedEnemyTypes)
            {
                InjectEnemyOfTypeIntoLevel(extendedEnemyType, SpawnableEnemyType.Inside, extendedLevel, extendedDungeonFlow, debugResults);
                InjectEnemyOfTypeIntoLevel(extendedEnemyType, SpawnableEnemyType.Outside, extendedLevel, extendedDungeonFlow, debugResults);
                InjectEnemyOfTypeIntoLevel(extendedEnemyType, SpawnableEnemyType.Daytime, extendedLevel, extendedDungeonFlow, debugResults);
            }
        }

        private static void InjectEnemyOfTypeIntoLevel(ExtendedEnemyType extendedEnemy, SpawnableEnemyType spawnableEnemyType, ExtendedLevel extendedLevel, ExtendedDungeonFlow extendedDungeonFlow = null, bool debugResults = false)
        {
            if (spawnableEnemyType is SpawnableEnemyType.None) return;

            List<SpawnableEnemyWithRarity> enemyPool = spawnableEnemyType switch
            {
                SpawnableEnemyType.Inside => extendedLevel.SelectableLevel.Enemies,
                SpawnableEnemyType.Outside => extendedLevel.SelectableLevel.OutsideEnemies,
                SpawnableEnemyType.Daytime => extendedLevel.SelectableLevel.DaytimeEnemies,
                _ or SpawnableEnemyType.None => null,
            };

            LevelMatchingProperties levelProperties = spawnableEnemyType switch
            {
                SpawnableEnemyType.Inside => extendedEnemy.InsideLevelMatchingProperties,
                SpawnableEnemyType.Outside => extendedEnemy.OutsideLevelMatchingProperties,
                SpawnableEnemyType.Daytime => extendedEnemy.DaytimeLevelMatchingProperties,
                _ or SpawnableEnemyType.None => null,
            };

            DungeonMatchingProperties dungeonProperties = spawnableEnemyType switch
            {
                SpawnableEnemyType.Inside => extendedEnemy.InsideDungeonMatchingProperties,
                SpawnableEnemyType.Outside => extendedEnemy.OutsideDungeonMatchingProperties,
                SpawnableEnemyType.Daytime => extendedEnemy.DaytimeDungeonMatchingProperties,
                _ or SpawnableEnemyType.None => null,
            };

            string debugString = string.Empty;
            int enemyIndex = enemyPool.FindIndex(enemy => enemy.enemyType == extendedEnemy.EnemyType);

            int levelRarity = levelProperties.GetDynamicRarity(extendedLevel);
            int dungeonRarity = (extendedDungeonFlow != null) ? dungeonProperties.GetDynamicRarity(extendedDungeonFlow) : 0;

            int returnRarity = Math.Max(levelRarity, dungeonRarity);

            if (enemyIndex != -1)
            {
                if (returnRarity > 0)
                {
                    enemyPool[enemyIndex].rarity = returnRarity;
                    if (debugResults == true)
                        debugString = "Updated " + spawnableEnemyType + " Rarity Of ExtendedEnemyType: " + extendedEnemy.EnemyType.enemyName + " To: " + returnRarity + " On Moon: " + extendedLevel.NumberlessPlanetName;
                }
                else
                {
                    enemyPool.RemoveAt(enemyIndex);
                    if (debugResults == true)
                        debugString = "Removed " + spawnableEnemyType + " ExtendedEnemyType: " + extendedEnemy.EnemyType.enemyName + " From Moon: " + extendedLevel.NumberlessPlanetName;
                }
            }
            else if (returnRarity > 0)
            {
                SpawnableEnemyWithRarity newSpawnableEnemy = new SpawnableEnemyWithRarity(extendedEnemy.EnemyType, returnRarity);
                enemyPool.Add(newSpawnableEnemy);
                if (debugResults == true)
                    debugString = "Added " + spawnableEnemyType + " ExtendedEnemyType: " + extendedEnemy.EnemyType.enemyName + " To Moon: " + extendedLevel.NumberlessPlanetName + " With A Rarity Of: " + returnRarity;
            }

            if (debugResults == true && !string.IsNullOrEmpty(debugString))
                DebugHelper.Log(debugString, DebugType.Developer);
        }

        internal static void UpdateEnemyIDs()
        {
            int highestEnemyScanNodeCreatureID = -1;
            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.ExtendedEnemyTypes)
                if (extendedEnemyType.ContentType is not ContentType.Custom && extendedEnemyType.EnemyID > highestEnemyScanNodeCreatureID)
                    highestEnemyScanNodeCreatureID = extendedEnemyType.EnemyID;

            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.CustomExtendedEnemyTypes)
            {
                if (extendedEnemyType == null || extendedEnemyType.ContentType is not ContentType.Custom) continue;
                extendedEnemyType.EnemyID = ++highestEnemyScanNodeCreatureID;
                if (extendedEnemyType.EnemyType.enemyPrefab != null)
                {
                    ScanNodeProperties[] allEnemyScanNodes = extendedEnemyType.EnemyType.enemyPrefab.GetComponentsInChildren<ScanNodeProperties>(includeInactive: true);
                    for (int i = 0; i < allEnemyScanNodes.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(allEnemyScanNodes[i].headerText) && allEnemyScanNodes[i].headerText.ContainsSanitized(extendedEnemyType.EnemyDisplayName, bothWays: true))
                            extendedEnemyType.ScanNodeProperties = allEnemyScanNodes[i];
                        allEnemyScanNodes[i].creatureScanID = extendedEnemyType.EnemyID;
                    }
                }
                if (string.IsNullOrEmpty(extendedEnemyType.EnemyDisplayName))
                {
                    extendedEnemyType.EnemyDisplayName = (extendedEnemyType.ScanNodeProperties != null) ? extendedEnemyType.ScanNodeProperties.headerText : extendedEnemyType.EnemyType.enemyName;
                    DebugHelper.LogWarning($"EnemyDisplayName field empty for '{extendedEnemyType.name}'! Using '{extendedEnemyType.EnemyDisplayName}' as fallback...", DebugType.User);
                }
                DebugHelper.Log($"Set Enemy ID '{extendedEnemyType.EnemyID}' For Custom EnemyType: {extendedEnemyType.EnemyType.enemyName}", DebugType.Developer);
            }
        }

        internal static void AddCustomEnemyTypesToTestAllEnemiesLevel()
        {
            QuickMenuManager quickMenuManager = UnityEngine.Object.FindAnyObjectByType<QuickMenuManager>(FindObjectsInactive.Exclude);

            if (quickMenuManager != null)
            {
                foreach (ExtendedEnemyType customEnemyType in PatchedContent.CustomExtendedEnemyTypes)
                {
                    SpawnableEnemyWithRarity spawnableEnemyWithRarity = new SpawnableEnemyWithRarity(customEnemyType.EnemyType, 300);
                    quickMenuManager.testAllEnemiesLevel.Enemies.Add(spawnableEnemyWithRarity);
                    quickMenuManager.testAllEnemiesLevel.OutsideEnemies.Add(spawnableEnemyWithRarity);
                    quickMenuManager.testAllEnemiesLevel.DaytimeEnemies.Add(spawnableEnemyWithRarity);
                }
            }
        }

        internal static void PopulateEnemySizeLists()
        {
            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.ExtendedEnemyTypes)
            {
                if (extendedEnemyType == null || extendedEnemyType.EnemyType == null) continue;
                switch (extendedEnemyType.EnemyType.EnemySize)
                {
                    case EnemySize.Tiny:
                        TinyEnemyTypes.Add(extendedEnemyType.EnemyType);
                        break;
                    case EnemySize.Giant:
                        GiantEnemyTypes.Add(extendedEnemyType.EnemyType);
                        break;
                    case EnemySize.Medium:
                        MediumEnemyTypes.Add(extendedEnemyType.EnemyType);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    internal enum SpawnableEnemyType
    {
        None = -1,
        Inside,
        Outside,
        Daytime
    }
}
