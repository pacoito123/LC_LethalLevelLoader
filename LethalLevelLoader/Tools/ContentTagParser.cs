using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace LethalLevelLoader
{
    internal static class ContentTagParser
    {
        private static readonly Dictionary<string, string[]> importedItemContentTagDictionary = [];
        private static readonly Dictionary<string, string[]> importedLevelContentTagDictionary = [];
        private static readonly Dictionary<string, string[]> importedEnemyContentTagDictionary = [];

        internal static void ApplyVanillaContentTags()
        {
            ApplyImportedItemContentTags();
            ApplyImportedSelectableLevelContentTags();
            ApplyImportedEnemyTypeContentTags();
        }

        internal static void ImportVanillaContentTags()
        {
            ParseContentFile("Items", importedItemContentTagDictionary, 4);
            ParseContentFile("SelectableLevels", importedLevelContentTagDictionary, 4);
            ParseContentFile("Enemies", importedEnemyContentTagDictionary, 4);
        }

        internal static void ParseContentFile(string fileName, Dictionary<string, string[]> importedContentTagDict, int startingLine)
        {
            DebugHelper.Log("Parsing Contents Of Content CSV Located At: " + fileName, DebugType.Developer);
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LethalLevelLoader.VanillaContentTags." + fileName + ".csv");
            using StreamReader reader = new(stream, Encoding.UTF8);
            int lineCount = 0;
            string line;
            try
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (++lineCount < startingLine) continue;
                    if (TryParseLine(line, out string contentName, out string[] contentTags))
                    {
                        importedContentTagDict[contentName] = contentTags;
                        DebugParsedLine(contentName, contentTags);
                    }
                }
            }
            catch (Exception e)
            {
                DebugHelper.LogError($"Could Not Parse File '{fileName}', CSV Tags Will Not Be Applied: {e}", DebugType.User);
            }
        }

        internal static void ApplyImportedItemContentTags()
        {
            int counter = 0;
            List<ExtendedItem> allVanillaItems = [.. PatchedContent.VanillaMod.ExtendedItems];
            foreach (KeyValuePair<string, string[]> importedItemData in importedItemContentTagDictionary)
            {
                int foundIndex = allVanillaItems.FindIndex(extendedItem => importedItemData.Key.ContainsSanitized([extendedItem.Item.name, extendedItem.Item.itemName], bothWays: true));
                if (foundIndex >= 0)
                {
                    ExtendedItem extendedItem = allVanillaItems[foundIndex];
                    DebugHelper.Log($"Applying CSV Tags For Imported Item #{++counter} / {importedItemContentTagDictionary.Count}: {importedItemData.Key} To ExtendedItem: {extendedItem.Item.itemName}({extendedItem.Item.name})", DebugType.Developer);
                    extendedItem.ContentTags = ContentTagManager.CreateNewContentTags(["Vanilla", .. importedItemData.Value]);
                    allVanillaItems.RemoveAt(foundIndex);
                }
                else
                    DebugHelper.LogWarning($"Could Not Apply CSV Tags For Imported Item #{++counter} / {importedItemContentTagDictionary.Count}: {importedItemData.Key}", DebugType.Developer);
            }
        }

        internal static void ApplyImportedSelectableLevelContentTags()
        {
            int counter = 0;
            List<ExtendedLevel> allVanillaLevels = [.. PatchedContent.VanillaMod.ExtendedLevels];
            foreach (KeyValuePair<string, string[]> importedLevelData in importedLevelContentTagDictionary)
            {
                int foundIndex = allVanillaLevels.FindIndex(extendedLevel => importedLevelData.Key.ContainsSanitized([extendedLevel.SelectableLevel.name, extendedLevel.SelectableLevel.PlanetName], bothWays: true));
                if (foundIndex >= 0)
                {
                    ExtendedLevel extendedLevel = allVanillaLevels[foundIndex];
                    DebugHelper.Log($"Applying CSV Tags For Imported Level #{++counter} / {importedLevelContentTagDictionary.Count}: {importedLevelData.Key} To ExtendedLevel: {extendedLevel.SelectableLevel.PlanetName}({extendedLevel.SelectableLevel.name})", DebugType.Developer);
                    extendedLevel.ContentTags = ContentTagManager.CreateNewContentTags(["Vanilla", .. importedLevelData.Value]);
                    allVanillaLevels.RemoveAt(foundIndex);
                }
                else
                    DebugHelper.LogWarning($"Could Not Apply CSV Tags For Imported Level #{++counter} / {importedLevelContentTagDictionary.Count}: {importedLevelData.Key}", DebugType.Developer);
            }
        }

        internal static void ApplyImportedEnemyTypeContentTags()
        {
            int counter = 0;
            List<ExtendedEnemyType> allVanillaEnemies = [.. PatchedContent.VanillaMod.ExtendedEnemyTypes];
            foreach (KeyValuePair<string, string[]> importedEnemyData in importedEnemyContentTagDictionary)
            {
                int foundIndex = allVanillaEnemies.FindIndex(extendedEnemy => importedEnemyData.Key.ContainsSanitized([extendedEnemy.EnemyType.name, extendedEnemy.EnemyType.enemyName], bothWays: true));
                if (foundIndex >= 0)
                {
                    ExtendedEnemyType extendedEnemy = allVanillaEnemies[foundIndex];
                    DebugHelper.Log($"Applying CSV Tags For Imported Enemy #{++counter} / {importedEnemyContentTagDictionary.Count}: {importedEnemyData.Key} To ExtendedEnemyType: {extendedEnemy.EnemyType.enemyName}({extendedEnemy.EnemyType.name})", DebugType.Developer);
                    extendedEnemy.ContentTags = ContentTagManager.CreateNewContentTags(["Vanilla", .. importedEnemyData.Value]);
                    allVanillaEnemies.RemoveAt(foundIndex);
                }
                else
                    DebugHelper.LogWarning($"Could Not Apply CSV Tags For Imported Enemy #{++counter} / {importedEnemyContentTagDictionary.Count}: {importedEnemyData.Key}", DebugType.Developer);
            }
        }

        internal static bool TryParseLine(string line, out string contentName, out string[] contentTags)
        {
            contentName = null;
            contentTags = null;

            if (string.IsNullOrEmpty(line)) return (false);
            string[] cells = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length < 2) return (false);

            contentName = cells[0];
            contentTags = new string[cells.Length - 2];

            for (int i = 0; i < contentTags.Length; i++)
                contentTags[i] = cells[i + 2];

            return (true);
        }

        internal static void DebugParsedLine(string contentName, string[] contentTags)
        {
            if (!string.IsNullOrEmpty(contentName) && contentTags?.Length > 0)
                DebugHelper.Log($"ContentName: {contentName} | ContentTags: {string.Join(", ", contentTags)}", DebugType.Developer);
        }
    }
}
