using DunGen.Graph;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace LethalLevelLoader
{
    public static class PatchedContent
    {
        public static ExtendedMod VanillaMod
        {
            get
            {
                if (field == null)
                    field = ExtendedMod.Create("LethalCompany", "Zeekerss");
                return (field);
            }
        }

        public static List<string> AllLevelSceneNames { get; } = new List<string>();

        public static List<ExtendedMod> ExtendedMods { get; } = new List<ExtendedMod>();
        private static Dictionary<string, ExtendedContent> UniqueIdentifiersDictionary { get; } = new Dictionary<string, ExtendedContent>();
        private static Dictionary<SelectableLevel, ExtendedLevel> ExtendedLevelDictionary { get; } = new Dictionary<SelectableLevel, ExtendedLevel>();
        private static Dictionary<DungeonFlow, ExtendedDungeonFlow> ExtendedDungeonFlowDictionary { get; } = new Dictionary<DungeonFlow, ExtendedDungeonFlow>();
        private static Dictionary<Item, ExtendedItem> ExtendedItemDictionary { get; } = new Dictionary<Item, ExtendedItem>();
        private static Dictionary<EnemyType, ExtendedEnemyType> ExtendedEnemyTypeDictionary { get; } = new Dictionary<EnemyType, ExtendedEnemyType>();
        private static Dictionary<BuyableVehicle, ExtendedBuyableVehicle> ExtendedBuyableVehicleDictionary { get; } = new Dictionary<BuyableVehicle, ExtendedBuyableVehicle>();
        private static Dictionary<UnlockableItem, ExtendedUnlockableItem> ExtendedUnlockableItemDictionary { get; } = new Dictionary<UnlockableItem, ExtendedUnlockableItem>();
        private static Dictionary<FootstepSurface, ExtendedFootstepSurface> ExtendedFootstepSurfaceDictionary { get; } = new Dictionary<FootstepSurface, ExtendedFootstepSurface>();


        public static List<ExtendedLevel> ExtendedLevels { get; } = new List<ExtendedLevel>();

        public static List<ExtendedLevel> VanillaExtendedLevels
        {
            get
            {
                List<ExtendedLevel> list = new List<ExtendedLevel>();
                foreach (ExtendedLevel level in ExtendedLevels)
                    if (level.ContentType is ContentType.Vanilla)
                        list.Add(level);
                return (list);
            }
        }

        public static List<ExtendedLevel> CustomExtendedLevels
        {
            get
            {
                List<ExtendedLevel> list = new List<ExtendedLevel>();
                foreach (ExtendedLevel level in ExtendedLevels)
                    if (level.ContentType is ContentType.Custom or ContentType.External)
                        list.Add(level);
                return (list);
            }
        }



        public static List<SelectableLevel> SelectableLevels
        {
            get
            {
                List<SelectableLevel> list = new List<SelectableLevel>();
                foreach (ExtendedLevel level in ExtendedLevels)
                    list.Add(level.SelectableLevel);
                return (list);
            }
        }

        public static List<SelectableLevel> MoonsCatalogue
        {
            get
            {
                List<SelectableLevel> list = [.. OriginalContent.MoonsCatalogue];
                foreach (ExtendedLevel level in ExtendedLevels)
                    if (level.ContentType is ContentType.Custom or ContentType.External)
                        list.Add(level.SelectableLevel);
                return (list);
            }
        }



        public static List<ExtendedDungeonFlow> ExtendedDungeonFlows { get; } = new List<ExtendedDungeonFlow>();

        public static List<ExtendedDungeonFlow> VanillaExtendedDungeonFlows
        {
            get
            {
                List<ExtendedDungeonFlow> list = new List<ExtendedDungeonFlow>();
                foreach (ExtendedDungeonFlow dungeon in ExtendedDungeonFlows)
                    if (dungeon.ContentType is ContentType.Vanilla)
                        list.Add(dungeon);
                return (list);
            }
        }

        public static List<ExtendedDungeonFlow> CustomExtendedDungeonFlows
        {
            get
            {
                List<ExtendedDungeonFlow> list = new List<ExtendedDungeonFlow>();
                foreach (ExtendedDungeonFlow dungeon in ExtendedDungeonFlows)
                    if (dungeon.ContentType is ContentType.Custom or ContentType.External)
                        list.Add(dungeon);
                return (list);
            }
        }



        public static List<ExtendedWeatherEffect> ExtendedWeatherEffects { get; } = new List<ExtendedWeatherEffect>();

        public static List<ExtendedWeatherEffect> VanillaExtendedWeatherEffects
        {
            get
            {
                List<ExtendedWeatherEffect> list = [];
                foreach (ExtendedWeatherEffect effect in ExtendedWeatherEffects)
                    if (effect.contentType is ContentType.Vanilla)
                        list.Add(effect);
                return (list);
            }
        }

        public static List<ExtendedWeatherEffect> CustomExtendedWeatherEffects
        {
            get
            {
                List<ExtendedWeatherEffect> list = [];
                foreach (ExtendedWeatherEffect effect in ExtendedWeatherEffects)
                    if (effect.contentType is ContentType.Custom or ContentType.External)
                        list.Add(effect);
                return (list);
            }
        }



        public static List<ExtendedItem> ExtendedItems { get; } = new List<ExtendedItem>();

        public static List<ExtendedItem> CustomExtendedItems
        {
            get
            {
                List<ExtendedItem> returnList = new List<ExtendedItem>();
                foreach (ExtendedItem item in ExtendedItems)
                    if (item.ContentType is ContentType.Custom or ContentType.External)
                        returnList.Add(item);
                return (returnList);
            }
        }



        public static List<ExtendedEnemyType> ExtendedEnemyTypes { get; } = new List<ExtendedEnemyType>();

        public static List<ExtendedEnemyType> CustomExtendedEnemyTypes
        {
            get
            {
                List<ExtendedEnemyType> returnList = new List<ExtendedEnemyType>();
                foreach (ExtendedEnemyType extendedEnemyType in ExtendedEnemyTypes)
                    if (extendedEnemyType.ContentType is ContentType.Custom or ContentType.External)
                        returnList.Add(extendedEnemyType);
                return (returnList);
            }
        }

        public static List<ExtendedEnemyType> VanillaExtendedEnemyTypes
        {
            get
            {
                List<ExtendedEnemyType> returnList = new List<ExtendedEnemyType>();
                foreach (ExtendedEnemyType extendedEnemyType in ExtendedEnemyTypes)
                    if (extendedEnemyType.ContentType is ContentType.Vanilla)
                        returnList.Add(extendedEnemyType);
                return (returnList);
            }
        }

        public static List<ExtendedBuyableVehicle> ExtendedBuyableVehicles { get; } = new List<ExtendedBuyableVehicle>();

        public static List<ExtendedBuyableVehicle> CustomExtendedBuyableVehicles
        {
            get
            {
                List<ExtendedBuyableVehicle> returnList = new List<ExtendedBuyableVehicle>();
                foreach (ExtendedBuyableVehicle extendedBuyableVehicle in ExtendedBuyableVehicles)
                    if (extendedBuyableVehicle.ContentType is ContentType.Custom or ContentType.External)
                        returnList.Add(extendedBuyableVehicle);
                return (returnList);
            }
        }

        public static List<ExtendedBuyableVehicle> VanillaExtendedBuyableVehicles
        {
            get
            {
                List<ExtendedBuyableVehicle> returnList = new List<ExtendedBuyableVehicle>();
                foreach (ExtendedBuyableVehicle extendedBuyableVehicle in ExtendedBuyableVehicles)
                    if (extendedBuyableVehicle.ContentType is ContentType.Vanilla)
                        returnList.Add(extendedBuyableVehicle);
                return (returnList);
            }
        }



        public static List<ExtendedUnlockableItem> ExtendedUnlockableItems { get; } = new List<ExtendedUnlockableItem>();

        public static List<ExtendedUnlockableItem> CustomExtendedUnlockableItems
        {
            get
            {
                List<ExtendedUnlockableItem> returnList = new List<ExtendedUnlockableItem>();
                foreach (ExtendedUnlockableItem extendedUnlockableItem in ExtendedUnlockableItems)
                    if (extendedUnlockableItem.ContentType is ContentType.Custom or ContentType.External)
                        returnList.Add(extendedUnlockableItem);
                return (returnList);
            }
        }

        public static List<ExtendedUnlockableItem> VanillaExtendedUnlockableItems
        {
            get
            {
                List<ExtendedUnlockableItem> returnList = new List<ExtendedUnlockableItem>();
                foreach (ExtendedUnlockableItem extendedUnlockableItem in ExtendedUnlockableItems)
                    if (extendedUnlockableItem.ContentType is ContentType.Vanilla)
                        returnList.Add(extendedUnlockableItem);
                return (returnList);
            }
        }



        public static List<ExtendedFootstepSurface> ExtendedFootstepSurfaces { get; } = new List<ExtendedFootstepSurface>();

        public static List<ExtendedFootstepSurface> CustomExtendedFootstepSurfaces
        {
            get
            {
                List<ExtendedFootstepSurface> returnList = new List<ExtendedFootstepSurface>();
                foreach (ExtendedFootstepSurface extendedFootstepSurface in ExtendedFootstepSurfaces)
                    if (extendedFootstepSurface.ContentType is ContentType.Custom or ContentType.External)
                        returnList.Add(extendedFootstepSurface);
                return (returnList);
            }
        }

        public static List<ExtendedFootstepSurface> VanillaExtendedFootstepSurfaces
        {
            get
            {
                List<ExtendedFootstepSurface> returnList = new List<ExtendedFootstepSurface>();
                foreach (ExtendedFootstepSurface extendedFootstepSurface in ExtendedFootstepSurfaces)
                    if (extendedFootstepSurface.ContentType is ContentType.Vanilla)
                        returnList.Add(extendedFootstepSurface);
                return (returnList);
            }
        }


        public static void RegisterExtendedDungeonFlow(ExtendedDungeonFlow extendedDungeonFlow)
        {
            extendedDungeonFlow.ConvertObsoleteValues();
            if (string.IsNullOrEmpty(extendedDungeonFlow.name))
            {
                DebugHelper.LogWarning("Tried to register ExtendedDungeonFlow with missing name! Setting to DungeonFlow name for safety!", DebugType.Developer);
                extendedDungeonFlow.name = extendedDungeonFlow.DungeonFlow.name;
            }
            //AssetBundleLoader.RegisterNewExtendedContent(extendedDungeonFlow, extendedDungeonFlow.name);
            LethalBundleManager.RegisterNewExtendedContent(extendedDungeonFlow, null);
        }

        public static void RegisterExtendedLevel(ExtendedLevel extendedLevel)
        {
            //AssetBundleLoader.RegisterNewExtendedContent(extendedLevel, extendedLevel.name);
            LethalBundleManager.RegisterNewExtendedContent(extendedLevel, null);
        }

        public static void RegisterExtendedMod(ExtendedMod extendedMod)
        {
            DebugHelper.Log("Registering ExtendedMod: " + extendedMod.ModName + " Manually.", DebugType.IAmBatby);
            //AssetBundleLoader.RegisterExtendedMod(extendedMod);
            LethalBundleManager.RegisterExtendedMod(extendedMod, null);
        }

        internal static void SortExtendedMods()
        {
            ExtendedMods.Sort(new ExtendedMod.ExtendedModComparer());
            foreach (ExtendedMod extendedMod in ExtendedMods)
                extendedMod.SortRegisteredContent();
        }

        internal static void PopulateContentDictionaries()
        {
            // Remove duplicate/invalid UUIDs from lists while adding them to content dictionaries.
            ExtendedLevels.RemoveAll(static extendedLevel => !TryAddUUID(extendedLevel) || !TryAdd(ExtendedLevelDictionary, extendedLevel.SelectableLevel, extendedLevel));
            ExtendedDungeonFlows.RemoveAll(static extendedDungeonFlow => !TryAddUUID(extendedDungeonFlow) || !TryAdd(ExtendedDungeonFlowDictionary, extendedDungeonFlow.DungeonFlow, extendedDungeonFlow));
            ExtendedItems.RemoveAll(static extendedItem => !TryAddUUID(extendedItem) || !TryAdd(ExtendedItemDictionary, extendedItem.Item, extendedItem));
            ExtendedEnemyTypes.RemoveAll(static extendedEnemyType => !TryAddUUID(extendedEnemyType) || !TryAdd(ExtendedEnemyTypeDictionary, extendedEnemyType.EnemyType, extendedEnemyType));
            ExtendedBuyableVehicles.RemoveAll(static extendedBuyableVehicle => !TryAddUUID(extendedBuyableVehicle) || !TryAdd(ExtendedBuyableVehicleDictionary, extendedBuyableVehicle.BuyableVehicle, extendedBuyableVehicle));
            ExtendedUnlockableItems.RemoveAll(static extendedUnlockableItem => !TryAddUUID(extendedUnlockableItem) || !TryAdd(ExtendedUnlockableItemDictionary, extendedUnlockableItem.UnlockableItem, extendedUnlockableItem));
            ExtendedFootstepSurfaces.RemoveAll(static extendedFootstepSurface => !TryAddUUID(extendedFootstepSurface) || !TryAdd(ExtendedFootstepSurfaceDictionary, extendedFootstepSurface.FootstepSurface, extendedFootstepSurface));
        }

        internal static bool TryAddUUID(ExtendedContent extendedContent) => TryAdd(UniqueIdentifiersDictionary, extendedContent.UniqueIdentificationName, extendedContent);

        internal static bool TryAdd<T1, T2>(Dictionary<T1, T2> dict, T1 key, T2 value) where T2 : ExtendedContent
        {
            if (dict.TryAdd(key, value))
                return (true);
            if (value.ExtendedMod != VanillaMod)
                DebugHelper.LogError($"Could not add key '{key}' to {typeof(T2).Name} dictionary.", DebugType.Developer);
            return (false);
        }

        public static bool TryGetExtendedContent(SelectableLevel selectableLevel, out ExtendedLevel extendedLevel)
        {
            return (ExtendedLevelDictionary.TryGetValue(selectableLevel, out extendedLevel));
        }

        public static bool TryGetExtendedContent(DungeonFlow dungeonFlow, out ExtendedDungeonFlow extendedDungeonFlow)
        {
            return (ExtendedDungeonFlowDictionary.TryGetValue(dungeonFlow, out extendedDungeonFlow));
        }

        public static bool TryGetExtendedContent(Item item, out ExtendedItem extendedItem)
        {
            return (ExtendedItemDictionary.TryGetValue(item, out extendedItem));
        }

        public static bool TryGetExtendedContent(EnemyType enemyType, out ExtendedEnemyType extendedEnemyType)
        {
            return (ExtendedEnemyTypeDictionary.TryGetValue(enemyType, out extendedEnemyType));
        }

        public static bool TryGetExtendedContent(BuyableVehicle buyableVehicle, out ExtendedBuyableVehicle extendedBuyableVehicle)
        {
            return (ExtendedBuyableVehicleDictionary.TryGetValue(buyableVehicle, out extendedBuyableVehicle));
        }

        public static bool TryGetExtendedContent(UnlockableItem unlockableItem, out ExtendedUnlockableItem extendedUnlockableItem)
        {
            return (ExtendedUnlockableItemDictionary.TryGetValue(unlockableItem, out extendedUnlockableItem));
        }

        public static bool TryGetExtendedContent(FootstepSurface footstepSurface, out ExtendedFootstepSurface extendedFootstepSurface)
        {
            return (ExtendedFootstepSurfaceDictionary.TryGetValue(footstepSurface, out extendedFootstepSurface));
        }

        public static bool TryGetExtendedContent<T>(string uniqueIdentifierName, out T extendedContent) where T : ExtendedContent
        {
            extendedContent = null;
            if (UniqueIdentifiersDictionary.TryGetValue(uniqueIdentifierName, out ExtendedContent content) && content is T result)
                extendedContent = result;
            return (extendedContent != null);
        }
    }

    public static class OriginalContent
    {
        //Levels

        public static List<SelectableLevel> SelectableLevels { get; } = new List<SelectableLevel>();

        public static List<SelectableLevel> MoonsCatalogue { get; } = new List<SelectableLevel>();

        //Dungeons

        public static List<DungeonFlow> DungeonFlows { get; } = new List<DungeonFlow>();

        //Items

        public static List<Item> Items { get; } = new List<Item>();

        public static List<ItemGroup> ItemGroups { get; } = new List<ItemGroup>();

        //Unlockable Items

        public static List<UnlockableItem> UnlockableItems { get; } = new List<UnlockableItem>();

        //Footstep Surfaces

        public static List<FootstepSurface> FootstepSurfaces { get; } = new List<FootstepSurface>();

        //Enemies

        public static List<EnemyType> Enemies { get; } = new List<EnemyType>();

        //Spawnable Objects

        public static List<SpawnableOutsideObject> SpawnableOutsideObjects { get; } = new List<SpawnableOutsideObject>();

        public static List<IndoorMapHazardType> IndoorMapHazards { get; } = new List<IndoorMapHazardType>();

        //Audio

        public static List<AudioMixer> AudioMixers { get; } = new List<AudioMixer>();

        public static List<AudioMixerGroup> AudioMixerGroups { get; } = new List<AudioMixerGroup>();

        public static List<AudioMixerSnapshot> AudioMixerSnapshots { get; } = new List<AudioMixerSnapshot>();

        public static List<LevelAmbienceLibrary> LevelAmbienceLibraries { get; } = new List<LevelAmbienceLibrary>();

        public static List<ReverbPreset> ReverbPresets { get; } = new List<ReverbPreset>();

        //Terminal

        public static List<TerminalKeyword> TerminalKeywords { get; } = new List<TerminalKeyword>();

        public static List<TerminalNode> TerminalNodes { get; } = new List<TerminalNode>();
    }
}
