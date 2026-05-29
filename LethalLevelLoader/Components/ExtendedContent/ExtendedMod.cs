using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public enum ModMergeSetting { MatchingAuthorName, MatchingModName, Disabled }
    [CreateAssetMenu(fileName = "ExtendedMod", menuName = "Lethal Level Loader/ExtendedMod", order = 30)]
    public class ExtendedMod : ScriptableObject
    {
        [field: SerializeField] public string ModName { get; internal set; } = "Unspecified";
        [field: SerializeField] public string AuthorName { get; internal set; } = "Unknown";
        public List<string> ModNameAliases { get; internal set; } = new List<string>();
        [field: SerializeField] public ModMergeSetting ModMergeSetting { get; internal set; } = ModMergeSetting.MatchingAuthorName;

        [field: SerializeField]
        public List<ExtendedLevel> ExtendedLevels { get; private set; } = new List<ExtendedLevel>();

        [field: SerializeField]
        public List<ExtendedDungeonFlow> ExtendedDungeonFlows { get; private set; } = new List<ExtendedDungeonFlow>();

        [field: SerializeField]
        public List<ExtendedItem> ExtendedItems { get; private set; } = new List<ExtendedItem>();

        [field: SerializeField]
        public List<ExtendedEnemyType> ExtendedEnemyTypes { get; private set; } = new List<ExtendedEnemyType>();

        [field: SerializeField]
        public List<ExtendedWeatherEffect> ExtendedWeatherEffects { get; private set; } = new List<ExtendedWeatherEffect>();

        [field: SerializeField]
        public List<ExtendedFootstepSurface> ExtendedFootstepSurfaces { get; private set; } = new List<ExtendedFootstepSurface>();

        [field: SerializeField]
        public List<ExtendedStoryLog> ExtendedStoryLogs { get; private set; } = new List<ExtendedStoryLog>();

        [field: SerializeField]
        public List<ExtendedBuyableVehicle> ExtendedBuyableVehicles { get; private set; } = new List<ExtendedBuyableVehicle>();

        [field: SerializeField]
        public List<ExtendedUnlockableItem> ExtendedUnlockableItems { get; private set; } = new List<ExtendedUnlockableItem>();

        [field: SerializeField]
        public List<string> StreamingLethalBundleNames { get; private set; } = new List<string>();

        public static ContentTag VanillaContentTag
        {
            get
            {
                if (field == null)
                    field = ContentTag.Create("Vanilla");
                return field;
            }
        }

        public static ContentTag CustomContentTag
        {
            get
            {
                if (field == null)
                    field = ContentTag.Create("Custom");
                return field;
            }
        }

        public List<ExtendedContent> ExtendedContents
        {
            get
            {
                List<ExtendedContent> returnList =
                [
                    .. ExtendedLevels,
                    .. ExtendedDungeonFlows,
                    .. ExtendedItems,
                    .. ExtendedEnemyTypes,
                    .. ExtendedWeatherEffects,
                    .. ExtendedFootstepSurfaces,
                    .. ExtendedStoryLogs,
                    .. ExtendedBuyableVehicles,
                    .. ExtendedUnlockableItems,
                ];
                returnList.Sort(new ExtendedContent.ExtendedContentComparer());
                return (returnList);
            }
        }

        internal static ExtendedMod Create(string modName)
        {
            ExtendedMod newExtendedMod = CreateInstance<ExtendedMod>();
            newExtendedMod.ModName = modName;
            newExtendedMod.name = modName.Sanitized(toLower: false) + "Mod";
            DebugHelper.Log("Created New ExtendedMod: " + newExtendedMod.ModName, DebugType.Developer);
            return (newExtendedMod);
        }

        public static ExtendedMod Create(string modName, string authorName)
        {
            ExtendedMod newExtendedMod = CreateInstance<ExtendedMod>();
            newExtendedMod.ModName = modName;
            newExtendedMod.name = modName.Sanitized(toLower: false) + "Mod";
            newExtendedMod.AuthorName = authorName;
            if (Plugin.Instance != null)
                DebugHelper.Log("Created New ExtendedMod: " + newExtendedMod.ModName + " by " + authorName, DebugType.Developer);
            return (newExtendedMod);
        }

        public static ExtendedMod Create(string modName, string authorName, ExtendedContent[] extendedContents)
        {
            ExtendedMod newExtendedMod = CreateInstance<ExtendedMod>();
            newExtendedMod.ModName = modName;
            newExtendedMod.name = modName.Sanitized(toLower: false) + "Mod";
            newExtendedMod.AuthorName = authorName;

            foreach (ExtendedContent extendedContent in extendedContents)
                newExtendedMod.RegisterExtendedContent(extendedContent);

            if (Plugin.Instance != null)
                DebugHelper.Log("Created New ExtendedMod: " + newExtendedMod.ModName + " by " + authorName, DebugType.Developer);

            return (newExtendedMod);
        }

        internal void RegisterExtendedContent(ExtendedContent newExtendedContent)
        {
            if (newExtendedContent != null)
            {
                if (!ExtendedContents.Contains(newExtendedContent))
                {
                    if (newExtendedContent is ExtendedLevel extendedLevel)
                        RegisterExtendedContent(extendedLevel);
                    else if (newExtendedContent is ExtendedDungeonFlow extendedDungeonFlow)
                        RegisterExtendedContent(extendedDungeonFlow);
                    else if (newExtendedContent is ExtendedItem extendedItem)
                        RegisterExtendedContent(extendedItem);
                    else if (newExtendedContent is ExtendedEnemyType extendedEnemyType)
                        RegisterExtendedContent(extendedEnemyType);
                    else if (newExtendedContent is ExtendedWeatherEffect extendedWeatherEffect)
                        RegisterExtendedContent(extendedWeatherEffect);
                    else if (newExtendedContent is ExtendedFootstepSurface extendedFootstepSurface)
                        RegisterExtendedContent(extendedFootstepSurface);
                    else if (newExtendedContent is ExtendedStoryLog extendedStoryLog)
                        RegisterExtendedContent(extendedStoryLog);
                    else if (newExtendedContent is ExtendedBuyableVehicle extendedBuyableVehicle)
                        RegisterExtendedContent(extendedBuyableVehicle);
                    else if (newExtendedContent is ExtendedUnlockableItem extendedUnlockableItem)
                        RegisterExtendedContent(extendedUnlockableItem);
                    else
                        throw new ArgumentException(newExtendedContent.name + " (" + newExtendedContent.GetType().Name + ") " + " Could Not Be Registered To ExtendedMod: " + ModName + " Due To Unimplemented Registration Check!", nameof(newExtendedContent));
                }
                else
                    throw new ArgumentException(newExtendedContent.name + " (" + newExtendedContent.GetType().Name + ") " + " Could Not Be Registered To ExtendedMod: " + ModName + " Due To Already Being Registered To This Mod!", nameof(newExtendedContent));
            }
            else
                throw new ArgumentNullException(nameof(newExtendedContent), "Null ExtendedContent Could Not Be Registered To ExtendedMod: " + ModName + " Due To Failed Validation Check!");
        }

        internal void RegisterExtendedContent<T>(T extendedContent, List<T> extendedContentList) where T : ExtendedContent
        {
            if (extendedContent == null)
            {
                DebugHelper.LogError($"{extendedContent.GetType()} Was Null", DebugType.User);
                return;
            }
            if (extendedContentList == null)
            {
                DebugHelper.LogError($"{extendedContent.GetType()} Content List Was Null", DebugType.User);
                return;
            }

            extendedContent.ConvertObsoleteValues();
            TryThrowInvalidContentException(extendedContent, extendedContent.TryValidateContent());

            extendedContentList.Add(extendedContent);
            extendedContent.ExtendedMod = this;
        }

        internal void RegisterExtendedContent(ExtendedLevel extendedLevel) => RegisterExtendedContent(extendedLevel, ExtendedLevels);
        internal void RegisterExtendedContent(ExtendedDungeonFlow extendedDungeonFlow) => RegisterExtendedContent(extendedDungeonFlow, ExtendedDungeonFlows);
        internal void RegisterExtendedContent(ExtendedItem extendedItem) => RegisterExtendedContent(extendedItem, ExtendedItems);
        internal void RegisterExtendedContent(ExtendedEnemyType extendedEnemyType) => RegisterExtendedContent(extendedEnemyType, ExtendedEnemyTypes);
        internal void RegisterExtendedContent(ExtendedWeatherEffect extendedWeatherEffect) => RegisterExtendedContent(extendedWeatherEffect, ExtendedWeatherEffects);
        internal void RegisterExtendedContent(ExtendedFootstepSurface extendedFootstepSurface) => RegisterExtendedContent(extendedFootstepSurface, ExtendedFootstepSurfaces);
        internal void RegisterExtendedContent(ExtendedStoryLog extendedStoryLog) => RegisterExtendedContent(extendedStoryLog, ExtendedStoryLogs);
        internal void RegisterExtendedContent(ExtendedBuyableVehicle extendedBuyableVehicle) => RegisterExtendedContent(extendedBuyableVehicle, ExtendedBuyableVehicles);
        internal void RegisterExtendedContent(ExtendedUnlockableItem extendedUnlockableItem) => RegisterExtendedContent(extendedUnlockableItem, ExtendedUnlockableItems);

        internal void TryThrowInvalidContentException(ExtendedContent extendedContent, (bool, string) result)
        {
            if (result.Item1 == false)
            {
                if (extendedContent == null)
                    throw new ArgumentNullException(nameof(extendedContent), "Null ExtendedContent Could Not Be Registered To ExtendedMod: " + ModName + " Due To Failed Validation Check! " + result.Item2);

                throw new ArgumentException(extendedContent.name + " (" + extendedContent.GetType().Name + ") " + " Could Not Be Registered To ExtendedMod: " + ModName + " Due To Failed Validation Check! " + result.Item2, nameof(extendedContent));
            }
        }

        internal void UnregisterExtendedContent(ExtendedContent currentExtendedContent)
        {
            if (currentExtendedContent is ExtendedLevel extendedLevel)
                ExtendedLevels.Remove(extendedLevel);
            else if (currentExtendedContent is ExtendedDungeonFlow extendedDungeonFlow)
                ExtendedDungeonFlows.Remove(extendedDungeonFlow);
            else if (currentExtendedContent is ExtendedItem extendedItem)
                ExtendedItems.Remove(extendedItem);
            else if (currentExtendedContent is ExtendedUnlockableItem extendedUnlockableItem)
                ExtendedUnlockableItems.Remove(extendedUnlockableItem);

            currentExtendedContent.ExtendedMod = null;
            DebugHelper.LogWarning("Unregistered ExtendedContent: " + currentExtendedContent.name + " In ExtendedMod: " + ModName, DebugType.Developer);
        }

        internal void UnregisterAllExtendedContent()
        {
            ExtendedLevels.Clear();
            ExtendedDungeonFlows.Clear();
            ExtendedItems.Clear();
            ExtendedEnemyTypes.Clear();
            ExtendedWeatherEffects.Clear();
            ExtendedFootstepSurfaces.Clear();
            ExtendedStoryLogs.Clear();
            ExtendedBuyableVehicles.Clear();
            ExtendedUnlockableItems.Clear();
        }

        internal void SortRegisteredContent()
        {
            ExtendedContent.ExtendedContentComparer extendedComparer = new ExtendedContent.ExtendedContentComparer();
            ExtendedLevels.Sort(extendedComparer);
            ExtendedDungeonFlows.Sort(extendedComparer);
            ExtendedItems.Sort(extendedComparer);
            ExtendedEnemyTypes.Sort(extendedComparer);
            ExtendedWeatherEffects.Sort(extendedComparer);
            ExtendedFootstepSurfaces.Sort(extendedComparer);
            ExtendedStoryLogs.Sort(extendedComparer);
            ExtendedBuyableVehicles.Sort(extendedComparer);
            ExtendedUnlockableItems.Sort(extendedComparer);
        }

        internal struct ExtendedModComparer : IComparer<ExtendedMod>
        {
            public readonly int Compare(ExtendedMod a, ExtendedMod b) => a.ModName.CompareTo(b.ModName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
