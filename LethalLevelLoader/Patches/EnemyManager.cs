using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public class EnemyManager
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
            int highestVanillaEnemyScanNodeCreatureID = -1;
            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.VanillaExtendedEnemyTypes)
                if (extendedEnemyType.EnemyID > highestVanillaEnemyScanNodeCreatureID)
                    highestVanillaEnemyScanNodeCreatureID = extendedEnemyType.EnemyID;

            int counter = 1; //we want this to be 1
            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.CustomExtendedEnemyTypes)
            {
                ScanNodeProperties enemyScanNode = extendedEnemyType.EnemyType.enemyPrefab.GetComponentInChildren<ScanNodeProperties>();
                if (enemyScanNode != null)
                {
                    extendedEnemyType.ScanNodeProperties = enemyScanNode;
                    extendedEnemyType.ScanNodeProperties.creatureScanID = (highestVanillaEnemyScanNodeCreatureID + counter);
                    extendedEnemyType.EnemyID = (highestVanillaEnemyScanNodeCreatureID + counter);
                    DebugHelper.Log("Setting Custom EnemyType: " + extendedEnemyType.EnemyType.enemyName + " ID To: " + (highestVanillaEnemyScanNodeCreatureID + counter), DebugType.Developer);
                }
                counter++;
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

    struct EnemyData
    {
        public EnemyAI enemyAI;
        public GameObject gamePrefab;
        public GameObject networkPrefab;
    }

    enum SpawnableEnemyType
    {
        None = -1,
        Inside,
        Outside,
        Daytime
    }
}
