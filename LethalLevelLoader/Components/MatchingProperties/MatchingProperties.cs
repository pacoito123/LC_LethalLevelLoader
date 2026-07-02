using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public class MatchingProperties : ScriptableObject
    {
        [Space(5)] public List<StringWithRarity> modNames = new List<StringWithRarity>();
        [Space(5)] public List<StringWithRarity> authorNames = new List<StringWithRarity>();

        public static MatchingProperties Create(ExtendedContent extendedContent)
        {
            MatchingProperties matchingProperties = CreateInstance<MatchingProperties>();
            matchingProperties.name = extendedContent.name + "MatchingProperties";
            return (matchingProperties);
        }

        internal static bool UpdateRarity(ref int currentValue, int newValue, string debugActionObject = null, string debugActionReason = null)
        {
            if (newValue > currentValue)
            {
                if (!string.IsNullOrEmpty(debugActionReason))
                {
                    if (!string.IsNullOrEmpty(debugActionObject))
                        DebugHelper.Log("Raised Rarity Of: " + debugActionObject + " From (" + currentValue + ") To (" + newValue + ") Due To Matching " + debugActionReason, DebugType.Developer);
                    else
                        DebugHelper.Log("Raised Rarity From (" + currentValue + ") To (" + newValue + ") Due To Matching " + debugActionReason, DebugType.Developer);
                }
                currentValue = newValue;
                return (true);
            }
            return (false);
        }

        internal static int GetHighestRarityViaMatchingWithinRanges(int comparingValue, List<Vector2WithRarity> matchingVectors)
        {
            int returnInt = 0;
            foreach (Vector2WithRarity vectorWithRarity in matchingVectors)
                if (vectorWithRarity.Rarity > returnInt)
                    if ((comparingValue >= vectorWithRarity.Min) && (comparingValue <= vectorWithRarity.Max))
                        returnInt = vectorWithRarity.Rarity;
            return (returnInt);
        }

        internal static int GetHighestRarityViaMatchingNormalizedString(string comparingString, List<StringWithRarity> matchingStrings)
        {
            int returnInt = 0;
            foreach (StringWithRarity stringWithRarity in matchingStrings)
                if (stringWithRarity.Rarity > returnInt && stringWithRarity.Name.EqualsSanitized(comparingString))
                    returnInt = stringWithRarity.Rarity;
            return (returnInt);
        }

        internal static int GetHighestRarityViaMatchingNormalizedTags(List<ContentTag> comparingTags, List<StringWithRarity> matchingStrings)
        {
            int returnInt = 0;
            foreach (ContentTag comparingTag in comparingTags)
            {
                int rarity = GetHighestRarityViaMatchingNormalizedString(comparingTag.contentTagName, matchingStrings);
                if (rarity > returnInt)
                    returnInt = rarity;
            }
            return (returnInt);
        }

        internal static int GetHighestRarityViaMatchingNormalizedStrings(List<string> comparingStrings, List<StringWithRarity> matchingStrings)
        {
            int returnInt = 0;
            foreach (string comparingString in comparingStrings)
            {
                int rarity = GetHighestRarityViaMatchingNormalizedString(comparingString, matchingStrings);
                if (rarity > returnInt)
                    returnInt = rarity;
            }
            return (returnInt);
        }
    }
}
