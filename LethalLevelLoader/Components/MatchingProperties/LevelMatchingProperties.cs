using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "LevelMatchingProperties", menuName = "Lethal Level Loader/Utility/LevelMatchingProperties", order = 12)]
    public class LevelMatchingProperties : MatchingProperties
    {
        [Space(5)] public List<StringWithRarity> levelTags = new List<StringWithRarity>();
        [Space(5)] public List<Vector2WithRarity> currentRoutePrice = new List<Vector2WithRarity>();
        [Space(5)] public List<StringWithRarity> currentWeather = new List<StringWithRarity>();
        [Space(5)] public List<StringWithRarity> planetNames = new List<StringWithRarity>();

        public static new LevelMatchingProperties Create(ExtendedContent extendedContent)
        {
            LevelMatchingProperties levelMatchingProperties = CreateInstance<LevelMatchingProperties>();
            levelMatchingProperties.name = extendedContent.name + "LevelMatchingProperties";
            return (levelMatchingProperties);
        }

        public int GetDynamicRarity(ExtendedLevel extendedLevel)
        {
            int returnRarity = 0;

            if (levelTags.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedTags(extendedLevel.ContentTags, levelTags), extendedLevel.name, "Content Tags");
            if (authorNames.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedString(extendedLevel.AuthorName, authorNames), extendedLevel.name, "Author Name");
            if (modNames.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedStrings(extendedLevel.ExtendedMod.ModNameAliases, modNames), extendedLevel.name, "Mod Name");
            if (currentRoutePrice.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingWithinRanges(extendedLevel.RoutePrice, currentRoutePrice), extendedLevel.name, "Route Price");
            if (planetNames.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedString(extendedLevel.NumberlessPlanetName, planetNames), extendedLevel.name, "Planet Name");
            if (currentWeather.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedString($"{extendedLevel.SelectableLevel.currentWeather}", currentWeather), extendedLevel.name, "Current Weather");

            return (returnRarity);
        }

        public void ApplyValues(List<StringWithRarity> newModNames = null, List<StringWithRarity> newAuthorNames = null, List<StringWithRarity> newLevelTags = null, List<Vector2WithRarity> newRoutePrices = null, List<StringWithRarity> newCurrentWeathers = null, List<StringWithRarity> newPlanetNames = null)
        {
            if (newModNames?.Count > 0)
                modNames = [.. newModNames];
            if (newAuthorNames?.Count > 0)
                authorNames = [.. newAuthorNames];
            if (newLevelTags?.Count > 0)
                levelTags = [.. newLevelTags];
            if (newRoutePrices?.Count > 0)
                currentRoutePrice = [.. newRoutePrices];
            if (newCurrentWeathers?.Count > 0)
                currentWeather = [.. newCurrentWeathers];
            if (newPlanetNames?.Count > 0)
                planetNames = [.. newPlanetNames];
        }
    }
}
