using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LethalLevelLoader
{
    public class ConfigHelper
    {
        public const char indexSeparator = ',';
        public const char keyPairSeparator = ':';
        public const char vectorSeparator = '-';
        public const string emptyDefaultValues = "Default Values Were Empty";

        public static List<StringWithRarity> ConvertToStringWithRarityList(string inputString, Vector2 clampRarity)
        {
            string[] splitStrings = SplitStringsByIndexSeparator(inputString);
            if (splitStrings.Length == 0) return [];

            List<StringWithRarity> returnList = new List<StringWithRarity>(splitStrings.Length);
            for (int i = 0; i < splitStrings.Length; i++)
            {
                (string, string) splitStringData = SplitStringByKeyPairSeparator(splitStrings[i]);
                string levelName = splitStringData.Item1;
                if (!int.TryParse(splitStringData.Item2, out int rarity)) { } // TODO: Log invalid string.
                if (clampRarity != Vector2.zero)
                    rarity = Math.Clamp(rarity, Mathf.RoundToInt(clampRarity.x), Mathf.RoundToInt(clampRarity.y));
                returnList.Add(new StringWithRarity(levelName, rarity));
            }
            return (returnList);
        }

        public static List<Vector2WithRarity> ConvertToVector2WithRarityList(string inputString, Vector2 clampRarity)
        {
            string[] splitStrings = SplitStringsByIndexSeparator(inputString);
            if (splitStrings.Length == 0) return [];

            List<Vector2WithRarity> returnList = new List<Vector2WithRarity>(splitStrings.Length);
            for (int i = 0; i < splitStrings.Length; i++)
            {
                (string, string) splitStringData = SplitStringByKeyPairSeparator(splitStrings[i]);
                (string, string) splitVectorData = SplitStringByVectorSeparator(splitStringData.Item1);
                if (!float.TryParse(splitVectorData.Item1, out float x)) { } // TODO: Log invalid strings.
                if (!float.TryParse(splitVectorData.Item2, out float y)) { }
                if (!int.TryParse(splitStringData.Item2, out int rarity)) { }
                if (clampRarity != Vector2.zero)
                    rarity = Math.Clamp(rarity, Mathf.RoundToInt(clampRarity.x), Mathf.RoundToInt(clampRarity.y));
                returnList.Add(new Vector2WithRarity(new Vector2(x, y), rarity));
            }
            return (returnList);
        }

        public static List<SpawnableEnemyWithRarity> ConvertToSpawnableEnemyWithRarityList(string inputString, Vector2 clampRarity)
        {
            StringWithRarity[] splitStrings = ConvertToStringWithRarityList(inputString, clampRarity).ToArray();
            if (splitStrings.Length == 0) return [];

            List<SpawnableEnemyWithRarity> returnList = new List<SpawnableEnemyWithRarity>(splitStrings.Length);
            foreach (StringWithRarity stringWithRarity in splitStrings)
            {
                foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.ExtendedEnemyTypes)
                {
                    EnemyType enemyType = extendedEnemyType.EnemyType;
                    bool matched = stringWithRarity.Name.ContainsSanitized(enemyType.enemyName, bothWays: true);

                    if (!matched && enemyType.enemyPrefab != null)
                    {
                        ScanNodeProperties enemyScanNode = enemyType.enemyPrefab.GetComponentInChildren<ScanNodeProperties>(includeInactive: false);
                        matched = (enemyScanNode != null) && stringWithRarity.Name.ContainsSanitized(enemyScanNode.headerText, bothWays: true);
                    }

                    if (matched)
                    {
                        // DebugHelper.Log("Vanilla Enemy Name: " + SanitizeString(item.itemName) + " , Parsed Item Name: " + SanitizeString(stringWithRarity.Name), DebugType.Developer);
                        returnList.Add(new SpawnableEnemyWithRarity()
                        {
                            enemyType = enemyType,
                            rarity = stringWithRarity.Rarity
                        });
                        break;
                    }
                }
            }
            return (returnList);
        }

        public static List<SpawnableItemWithRarity> ConvertToSpawnableItemWithRarityList(string inputString, Vector2 clampRarity)
        {
            StringWithRarity[] splitStrings = ConvertToStringWithRarityList(inputString, clampRarity).ToArray();
            if (splitStrings.Length == 0) return [];

            List<SpawnableItemWithRarity> returnList = new List<SpawnableItemWithRarity>(splitStrings.Length);
            foreach (StringWithRarity stringWithRarity in splitStrings)
            {
                foreach (ExtendedItem extendedItem in PatchedContent.ExtendedItems)
                {
                    Item item = extendedItem.Item;
                    if (stringWithRarity.Name.ContainsSanitized(item.itemName, bothWays: true))
                    {
                        // DebugHelper.Log("Vanilla Item Name: " + SanitizeString(item.itemName) + " , Parsed Item Name: " + SanitizeString(stringWithRarity.Name), DebugType.Developer);
                        returnList.Add(new SpawnableItemWithRarity()
                        {
                            spawnableItem = item,
                            rarity = stringWithRarity.Rarity
                        });
                        break;
                    }
                }
            }
            return (returnList);
        }

        public static string SpawnableEnemiesWithRaritiesToString(SpawnableEnemyWithRarity[] spawnableEnemies)
        {
            if (spawnableEnemies.Length == 0) return emptyDefaultValues;
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < spawnableEnemies.Length; i++)
            {
                SpawnableEnemyWithRarity enemy = spawnableEnemies[i];
                str.Append(enemy.enemyType.enemyName + keyPairSeparator + enemy.rarity);

                if (i != spawnableEnemies.Length - 1)
                    str.Append(indexSeparator);
            }
            return str.ToString();
        }

        public static string SpawnableItemsWithRaritiesToString(SpawnableItemWithRarity[] spawnableItems)
        {
            if (spawnableItems.Length == 0) return emptyDefaultValues;
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < spawnableItems.Length; i++)
            {
                SpawnableItemWithRarity item = spawnableItems[i];
                str.Append(item.spawnableItem.itemName + keyPairSeparator + item.rarity);

                if (i != spawnableItems.Length - 1)
                    str.Append(indexSeparator);
            }
            return str.ToString();
        }

        public static string StringWithRaritiesToString(StringWithRarity[] names)
        {
            if (names.Length == 0) return emptyDefaultValues;
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                StringWithRarity name = names[i];
                str.Append(name.Name + keyPairSeparator + name.Rarity);

                if (i != names.Length - 1)
                    str.Append(indexSeparator);
            }
            return str.ToString();
        }

        public static string Vector2WithRaritiesToString(Vector2WithRarity[] values)
        {
            if (values.Length == 0) return emptyDefaultValues;
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                Vector2WithRarity value = values[i];
                str.Append(value.Min + vectorSeparator + value.Max + keyPairSeparator + value.Rarity);

                if (i != values.Length - 1)
                    str.Append(indexSeparator);
            }
            return str.ToString();
        }

        public static string[] SplitStringsByIndexSeparator(string inputString)
        {
            return SplitStringByCharacter(inputString, indexSeparator);
        }

        public static (string, string) SplitStringByKeyPairSeparator(string inputString)
        {
            return SplitStringPairByCharacter(inputString, keyPairSeparator);
        }

        public static (string, string) SplitStringByVectorSeparator(string inputString)
        {
            return SplitStringPairByCharacter(inputString, vectorSeparator);
        }

        public static (string, string) SplitStringPairByCharacter(string inputString, char separator)
        {
            string[] possiblePair = SplitStringByCharacter(inputString, separator);
            return (possiblePair.Length == 2) ? (possiblePair[0], possiblePair[1]) : (inputString, string.Empty);
        }

        public static string[] SplitStringByCharacter(string inputString, char separator)
        {
            string[] splitString = inputString.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < splitString.Length; i++)
                splitString[i] = splitString[i].Trim();
            return splitString;
        }
    }
}
