using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "DungeonMatchingProperties", menuName = "Lethal Level Loader/Utility/DungeonMatchingProperties", order = 13)]
    public class DungeonMatchingProperties : MatchingProperties
    {
        [Space(5)] public List<StringWithRarity> dungeonTags = new List<StringWithRarity>();
        [Space(5)] public List<StringWithRarity> dungeonNames = new List<StringWithRarity>();

        public static new DungeonMatchingProperties Create(ExtendedContent extendedContent)
        {
            DungeonMatchingProperties dungeonMatchingProperties = CreateInstance<DungeonMatchingProperties>();
            dungeonMatchingProperties.name = extendedContent.name + "DungeonMatchingProperties";
            return (dungeonMatchingProperties);
        }
        public int GetDynamicRarity(ExtendedDungeonFlow extendedDungeonFlow)
        {
            int returnRarity = 0;

            if (dungeonTags.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedTags(extendedDungeonFlow.ContentTags, dungeonTags), extendedDungeonFlow.name, "Content Tags");
            if (authorNames.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedString(extendedDungeonFlow.AuthorName, authorNames), extendedDungeonFlow.name, "Author Name");
            if (modNames.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedStrings(extendedDungeonFlow.ExtendedMod.ModNameAliases, modNames), extendedDungeonFlow.name, "Mod Name Name");
            if (dungeonNames.Count > 0)
                UpdateRarity(ref returnRarity, GetHighestRarityViaMatchingNormalizedString(extendedDungeonFlow.DungeonFlow.name, dungeonNames), extendedDungeonFlow.name, "Dungeon Name");

            return (returnRarity);
        }

        public void ApplyValues(List<StringWithRarity> newModNames = null, List<StringWithRarity> newAuthorNames = null, List<StringWithRarity> newDungeonTags = null, List<StringWithRarity> newDungeonNames = null)
        {
            if (newModNames?.Count > 0)
                modNames = [.. newModNames];
            if (newAuthorNames?.Count > 0)
                authorNames = [.. newAuthorNames];
            if (newDungeonTags?.Count > 0)
                dungeonTags = [.. newDungeonTags];
            if (newDungeonNames?.Count > 0)
                dungeonNames = [.. newDungeonNames];
        }
    }
}
