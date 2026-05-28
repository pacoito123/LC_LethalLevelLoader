using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LethalLevelLoader
{
    public static class LevelManager
    {
        public static ExtendedLevel CurrentExtendedLevel
        {
            get
            {
                if (Patches.StartOfRound == null || Patches.StartOfRound.currentLevel == null) return (null);
                if (field == null || Patches.StartOfRound.currentLevel != field.SelectableLevel)
                    if (PatchedContent.TryGetExtendedContent(Patches.StartOfRound.currentLevel, out field))
                        DebugHelper.Log($"Level switched to: {field.SelectableLevel.PlanetName}", DebugType.IAmBatby);
                return (field);
            }
        }
        public static LevelEvents GlobalLevelEvents = new LevelEvents();

        public static List<DayHistory> dayHistoryList = new List<DayHistory>();
        public static int daysTotal;
        public static int quotasTotal;

        public static int invalidSaveLevelID = -1;

        public static readonly Dictionary<string, int> dynamicRiskLevelDictionary = new Dictionary<string, int>()
        {
            {"D-",  0},
            {"D",   0},
            {"D+",  0},
            {"C-",  0},
            {"C",   0},
            {"C+",  0},
            {"B-",  0},
            {"B",   0},
            {"B+",  0},
            {"A-",  0},
            {"A",   0},
            {"A+",  0},
            {"S-",  0},
            {"S",   0},
            {"S+",  0},
            {"S++", 0},
            {"S+++",0}
        };

        internal static void PatchVanillaLevelLists()
        {
            // Filter 'External' moons from vanilla lists to avoid duplicate entries (assumes they are being added in some other way).
            List<SelectableLevel> selectableLevels = new(PatchedContent.ExtendedLevels.Count);
            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                if (extendedLevel.ContentType is not ContentType.External)
                    selectableLevels.Add(extendedLevel.SelectableLevel);
            Patches.StartOfRound.levels = [.. selectableLevels];
            TerminalManager.Terminal.moonsCatalogueList = [.. selectableLevels];
        }

        internal static void ObtainShipAnimatorClips(StartOfRound startOfRound)
        {
            Animator shipAnimator = startOfRound.shipAnimator;
            RuntimeAnimatorController animatorController = shipAnimator.runtimeAnimatorController;

            /* List<GameObject> childObjects = new List<GameObject>();
            foreach (Transform child in shipAnimator.GetComponentsInChildren<Transform>(includeInactive: true))
                if (!childObjects.Contains(child.gameObject))
                    childObjects.Add(child.gameObject); */

            for (int i = 0; i < animatorController.animationClips.Length; i++)
            {
                if (string.Equals(animatorController.animationClips[i].name, "HangarShipLandB", StringComparison.Ordinal))
                    LevelLoader.defaultShipFlyToMoonClip = animatorController.animationClips[i];
                if (string.Equals(animatorController.animationClips[i].name, "ShipLeave", StringComparison.Ordinal))
                    LevelLoader.defaultShipFlyFromMoonClip = animatorController.animationClips[i];
            }
        }

        internal static void ObtainTimeOfDayClips(TimeOfDay timeOfDay)
        {
            LevelLoader.timeOfDayCues = timeOfDay.timeOfDayCues;
            if (LevelLoader.timeOfDayCues?.Length == 4)
            {
                LevelLoader.defaultStartOfDayMusic = LevelLoader.timeOfDayCues[0];
                LevelLoader.defaultMidDayMusic = LevelLoader.timeOfDayCues[1];
                LevelLoader.defaultLateDayMusic = LevelLoader.timeOfDayCues[2];
                LevelLoader.defaultNightMusic = LevelLoader.timeOfDayCues[3];
            }
        }

        internal static void ObtainGrassShaderReference()
        {
            // Appears to grab the first instance of a shader with that name, any additional ones that may be loaded by custom bundles are therefore ignored.
            Shader wavingGrass = Shader.Find("Shader Graphs/WavingGrass");
            if (wavingGrass != null)
            {
                LevelLoader.vanillaWavingGrassShader = wavingGrass;
                LevelLoader.vanillaWavingGrassShaderKeywords = [
                    new LocalKeyword(wavingGrass, "_ALPHATEST_ON"),
                    new LocalKeyword(wavingGrass, "_DISABLE_SSR"),
                    new LocalKeyword(wavingGrass, "_DISABLE_SSR_TRANSPARENT")
                ];
            }
        }

        public static bool TryGetExtendedLevel(SelectableLevel selectableLevel, out ExtendedLevel returnExtendedLevel, ContentType levelType = ContentType.Any) =>
            PatchedContent.TryGetExtendedContent(selectableLevel, out returnExtendedLevel) && (levelType is ContentType.Any || levelType == returnExtendedLevel.ContentType);

        public static ExtendedLevel GetExtendedLevel(SelectableLevel selectableLevel) => PatchedContent.TryGetExtendedContent(selectableLevel, out ExtendedLevel extendedLevel) ? extendedLevel : null;

        public static void PopulateDynamicRiskLevelDictionary()
        {
            Dictionary<string, List<int>> vanillaRiskLevelDictionary = new Dictionary<string, List<int>>();

            foreach (ExtendedLevel vanillaLevel in PatchedContent.VanillaExtendedLevels)
            {
                DebugHelper.Log($"Risk Level Of {vanillaLevel.NumberlessPlanetName} Is: {vanillaLevel.SelectableLevel.riskLevel}", DebugType.Developer);
                if (!string.IsNullOrEmpty(vanillaLevel.SelectableLevel.riskLevel) && !vanillaLevel.SelectableLevel.riskLevel.Contains("Safe", StringComparison.Ordinal))
                {
                    if (vanillaRiskLevelDictionary.TryGetValue(vanillaLevel.SelectableLevel.riskLevel, out List<int> dynamicDifficultyRatingList))
                        dynamicDifficultyRatingList.Add(vanillaLevel.CalculatedDifficultyRating);
                    else
                        vanillaRiskLevelDictionary.Add(vanillaLevel.SelectableLevel.riskLevel, [vanillaLevel.CalculatedDifficultyRating]);
                }
            }

            foreach (KeyValuePair<string, List<int>> vanillaRiskLevel in vanillaRiskLevelDictionary)
            {
                string debugString = $"Vanilla Risk Level Group ({vanillaRiskLevel.Key}): ";
                if (vanillaRiskLevel.Value != null)
                {
                    int riskLevelSum = 0;
                    foreach (int riskLevel in vanillaRiskLevel.Value)
                        riskLevelSum += riskLevel;

                    debugString += $" Average - {riskLevelSum / (float)vanillaRiskLevel.Value.Count}, Values - ";
                    foreach (int calculatedDifficulty in vanillaRiskLevel.Value)
                        debugString += $"{calculatedDifficulty}, ";
                }
                DebugHelper.Log(debugString, DebugType.Developer);
            }

            foreach (KeyValuePair<string, int> dynamicRiskLevelPair in new Dictionary<string, int>(dynamicRiskLevelDictionary))
                foreach (KeyValuePair<string, List<int>> vanillaRiskLevel in vanillaRiskLevelDictionary)
                    if (dynamicRiskLevelPair.Key.Equals(vanillaRiskLevel.Key, StringComparison.Ordinal))
                    {
                        int riskLevelSum = 0;
                        foreach (int riskLevel in vanillaRiskLevel.Value)
                            riskLevelSum += riskLevel;
                        int average = Mathf.RoundToInt((float)riskLevelSum / vanillaRiskLevel.Value.Count);

                        DebugHelper.Log($"Setting RiskLevel {vanillaRiskLevel.Key} To {average}", DebugType.Developer);
                        dynamicRiskLevelDictionary[dynamicRiskLevelPair.Key] = average;
                    }

            DebugHelper.Log("Starting To Assign - and + Risk Levels", DebugType.Developer);
            string[] keys = [.. dynamicRiskLevelDictionary.Keys];
            for (int i = 0; i < keys.Length; i++)
            {
                string previousFullRiskLevel = string.Empty;
                string nextFullRiskLevel = string.Empty;
                string currentFullRiskLevel;

                string key = keys[i];
                if (string.IsNullOrEmpty(key)) continue;
                DebugHelper.Log($"Trying To Assign Value To Risk Level: {key}", DebugType.Developer);

                if (key.Contains('-', StringComparison.Ordinal))
                {
                    if (i > 0)
                        previousFullRiskLevel = keys[i - 1];
                    currentFullRiskLevel = keys[i];

                    dynamicRiskLevelDictionary[key] = Mathf.RoundToInt((i == 0) ? (dynamicRiskLevelDictionary[currentFullRiskLevel] / 2f)
                        : (Mathf.Lerp(dynamicRiskLevelDictionary[previousFullRiskLevel], dynamicRiskLevelDictionary[currentFullRiskLevel], 0.66f)));

                }
                else if (key.Contains('+', StringComparison.Ordinal) && !key.Equals("S+", StringComparison.Ordinal))
                {
                    currentFullRiskLevel = keys[i - 1];
                    if (!key.Contains('S', StringComparison.Ordinal))
                        nextFullRiskLevel = keys[i + 1];

                    int pluses = key.Split('+', StringSplitOptions.None).Length - 1;
                    dynamicRiskLevelDictionary[key] = (pluses > 1) ? (dynamicRiskLevelDictionary[currentFullRiskLevel] * pluses)
                        : (Mathf.RoundToInt(Mathf.Lerp(dynamicRiskLevelDictionary[currentFullRiskLevel], dynamicRiskLevelDictionary[nextFullRiskLevel], 0.33f)));
                }

                DebugHelper.Log($"Risk Level: {key} Was Assigned Calculated Difficulty Of: {dynamicRiskLevelDictionary[key]}", DebugType.Developer);
            }
        }

        public static void AssignCalculatedRiskLevels()
        {
            Dictionary<int, string> assignmentRiskLevelDictionary = new Dictionary<int, string>();
            List<int> orderedCalculatedDifficultyList = [.. dynamicRiskLevelDictionary.Values];
            orderedCalculatedDifficultyList.Sort();

            foreach (int calculatedDifficultyValue in orderedCalculatedDifficultyList)
                foreach (KeyValuePair<string, int> calculatedRiskLevel in dynamicRiskLevelDictionary)
                    if (calculatedRiskLevel.Value == calculatedDifficultyValue)
                        assignmentRiskLevelDictionary.Add(calculatedDifficultyValue, calculatedRiskLevel.Key);

            foreach (KeyValuePair<int, string> calculatedRiskLevel in assignmentRiskLevelDictionary)
                DebugHelper.Log($"Ordered Calculated Risk Level: ({calculatedRiskLevel.Value}) - {calculatedRiskLevel.Key}", DebugType.Developer);

            foreach (ExtendedLevel customLevel in PatchedContent.CustomExtendedLevels)
            {
                if (customLevel.OverrideDynamicRiskLevelAssignment == false)
                {
                    int customLevelCalculatedDifficultyRating = customLevel.CalculatedDifficultyRating;
                    int closestCalculatedRiskLevelRating = customLevelCalculatedDifficultyRating;
                    foreach (int rating in orderedCalculatedDifficultyList)
                        if (Math.Abs(customLevelCalculatedDifficultyRating - rating) < closestCalculatedRiskLevelRating)
                            closestCalculatedRiskLevelRating = rating;
                    if (closestCalculatedRiskLevelRating > 0)
                        customLevel.SelectableLevel.riskLevel = assignmentRiskLevelDictionary[closestCalculatedRiskLevelRating];
                }
            }

            List<ExtendedLevel> extendedLevelsOrdered = [.. PatchedContent.ExtendedLevels];
            extendedLevelsOrdered.Sort(new ExtendedLevel.ExtendedLevelDifficultyComparer());

            foreach (ExtendedLevel extendedLevel in extendedLevelsOrdered)
                DebugHelper.Log(extendedLevel.NumberlessPlanetName + " (" + extendedLevel.SelectableLevel.riskLevel + ") " + " (" + extendedLevel.CalculatedDifficultyRating + ")", DebugType.Developer);
        }

        public static void LogDayHistory()
        {
            //Heavy early returns here because this runs from a DunGen patch and needs to be safe for unconventional Unity-Editor generation usage.
            if (Plugin.IsSetupComplete == false || Patches.StartOfRound == null || Patches.RoundManager == null || Patches.TimeOfDay == null)
            {
                DebugHelper.LogWarning("Game Seems Uninitialized, Exiting LogDayHistory Early!", DebugType.Developer);
                return;
            }

            DayHistory newDayHistory = new DayHistory();
            daysTotal++;

            newDayHistory.allViableOptions = DungeonManager.GetValidExtendedDungeonFlows(CurrentExtendedLevel, false).ConvertAll(i => i.extendedDungeonFlow);
            newDayHistory.extendedLevel = CurrentExtendedLevel;
            newDayHistory.extendedDungeonFlow = DungeonManager.CurrentExtendedDungeonFlow;
            newDayHistory.day = daysTotal;
            newDayHistory.quota = Patches.TimeOfDay.timesFulfilledQuota;
            newDayHistory.weatherEffect = Patches.StartOfRound.currentLevel.currentWeather;

            string debugString = "Created New Day History Log! PlanetName: ";
            if (newDayHistory.extendedLevel != null)
                debugString += newDayHistory.extendedLevel.NumberlessPlanetName + " ,";
            else
                debugString += "MISSING EXTENDEDLEVEL ,";
            if (newDayHistory.extendedDungeonFlow != null)
                debugString += newDayHistory.extendedDungeonFlow.DungeonName + " ,";
            else
                debugString += "MISSING EXTENDEDDUNGEONFLOW ,";
            debugString += "Quota: " + newDayHistory.quota + " , Day: " + newDayHistory.day + " , Weather: " + newDayHistory.weatherEffect.ToString();

            DebugHelper.Log(debugString, DebugType.User);
            dayHistoryList.Add(newDayHistory);
        }

        public static int CalculateExtendedLevelDifficultyRating(ExtendedLevel extendedLevel, bool debugResults = false)
        {
            int returnRating = 0;
            string debugString = "Calculated Difficulty Rating For ExtendedLevel: " + extendedLevel.NumberlessPlanetName + "(" + extendedLevel.SelectableLevel.riskLevel + ")" + " ----- ";

            int baselineRouteValue = extendedLevel.RoutePrice;
            baselineRouteValue += extendedLevel.SelectableLevel.maxTotalScrapValue;
            returnRating += baselineRouteValue;
            debugString += "Baseline Route Value: " + baselineRouteValue + ", ";

            int scrapValue = 0;
            foreach (SpawnableItemWithRarity spawnableScrap in extendedLevel.SelectableLevel.spawnableScrap)
            {
                if (spawnableScrap.spawnableItem != null)
                {
                    if (((spawnableScrap.spawnableItem.minValue + spawnableScrap.spawnableItem.maxValue) * 5) != 0 && spawnableScrap.rarity != 0)
                    {
                        if ((spawnableScrap.rarity / 10) != 0)
                            scrapValue += (spawnableScrap.spawnableItem.maxValue - spawnableScrap.spawnableItem.minValue) / (spawnableScrap.rarity / 10);
                    }
                }
            }
            returnRating += scrapValue;
            debugString += "Scrap Value: " + scrapValue + ", ";

            int enemySpawnValue = (extendedLevel.SelectableLevel.maxEnemyPowerCount + extendedLevel.SelectableLevel.maxOutsideEnemyPowerCount + extendedLevel.SelectableLevel.maxDaytimeEnemyPowerCount) * 15;
            enemySpawnValue *= 2;
            returnRating += enemySpawnValue;
            debugString += "Enemy Spawn Value: " + enemySpawnValue + ", ";

            static float sumEnemyValues(List<SpawnableEnemyWithRarity> enemyList)
            {
                float enemyValue = 0;
                foreach (SpawnableEnemyWithRarity enemyWithRarity in enemyList)
                    if (enemyWithRarity.rarity > 0 && enemyWithRarity.enemyType != null)
                        if (enemyWithRarity.rarity / 10 != 0)
                            enemyValue += (enemyWithRarity.enemyType.PowerLevel * 100) / (enemyWithRarity.rarity / 10);
                return enemyValue;
            }
            float enemyValue = sumEnemyValues(extendedLevel.SelectableLevel.Enemies);
            enemyValue += sumEnemyValues(extendedLevel.SelectableLevel.OutsideEnemies);
            enemyValue += sumEnemyValues(extendedLevel.SelectableLevel.DaytimeEnemies);

            returnRating += Mathf.RoundToInt(enemyValue);
            debugString += "Enemy Value: " + enemyValue + ", ";

            debugString += "Calculated Difficulty Value: " + returnRating + ", ";

            //returnRating = Mathf.RoundToInt(returnRating * Mathf.Lerp(1, extendedLevel.selectableLevel.factorySizeMultiplier, 0.25f));
            returnRating += Mathf.RoundToInt(returnRating * (extendedLevel.SelectableLevel.factorySizeMultiplier * 0.5f));

            debugString += "Factory Size Multiplier: " + extendedLevel.SelectableLevel.factorySizeMultiplier + ", ";

            debugString += "Multiplied Calculated Difficulty Value: " + returnRating;

            if (debugResults == true)
                DebugHelper.Log(debugString, DebugType.Developer);
            return (returnRating);
        }
    }

    public class DayHistory
    {
        public int quota;
        public int day;
        public ExtendedLevel extendedLevel;
        public List<ExtendedDungeonFlow> allViableOptions;
        public ExtendedDungeonFlow extendedDungeonFlow;
        public LevelWeatherType weatherEffect;
    }
}