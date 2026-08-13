using System.Collections.Generic;
using Unity.Netcode;

namespace LethalLevelLoader
{
    public class LLLSaveFile(string currentSaveFileName)
    {
        public string SaveLocation => currentSaveFileName + ".moddata";

        public string CurrentLevelName { get; internal set; } = string.Empty;

        public Dictionary<int, AllItemsListItemData> itemSaveData = new Dictionary<int, AllItemsListItemData>();
        public List<ExtendedLevelData> extendedLevelSaveData = new List<ExtendedLevelData>();

        public void Save()
        {
            string prefix = Plugin.ModName + '.' + nameof(LLLSaveFile) + '.';
            ES3.Save(prefix + nameof(CurrentLevelName), CurrentLevelName, SaveLocation);
            ES3.Save(prefix + nameof(itemSaveData), itemSaveData, SaveLocation);
            ES3.Save(prefix + nameof(extendedLevelSaveData), extendedLevelSaveData, SaveLocation);
        }

        public void Load()
        {
            string prefix = Plugin.ModName + '.' + nameof(LLLSaveFile) + '.';
            CurrentLevelName = ES3.Load(prefix + nameof(CurrentLevelName), SaveLocation, string.Empty);
            itemSaveData = ES3.Load(prefix + nameof(itemSaveData), SaveLocation, new Dictionary<int, AllItemsListItemData>());
            extendedLevelSaveData = ES3.Load(prefix + nameof(extendedLevelSaveData), SaveLocation, new List<ExtendedLevelData>());
        }

        public void Reset()
        {
            CurrentLevelName = string.Empty;
            itemSaveData = new Dictionary<int, AllItemsListItemData>();
            extendedLevelSaveData = new List<ExtendedLevelData>();
        }
    }

    public struct AllItemsListItemData(string newItemObjectName, string newItemName, string newModName, string newModAuthor, int newAllItemsListIndex, int newModItemsListIndex, int newItemNameDuplicateIndex, bool newIsScrap, bool newSaveItemVariable)
    {
        public string itemObjectName = newItemObjectName;
        public string itemName = newItemName;
        public string modName = newModName;
        public string modAuthor = newModAuthor;
        public int allItemsListIndex = newAllItemsListIndex;
        public int modItemsListIndex = newModItemsListIndex;
        public int itemNameDuplicateIndex = newItemNameDuplicateIndex;
        public bool isScrap = newIsScrap;
        public bool saveItemVariable = newSaveItemVariable;
    }

    public struct ExtendedLevelData(ExtendedLevel extendedLevel) : INetworkSerializable
    {
        public readonly string UniqueIdentifier => uniqueIdentifier;
        public string uniqueIdentifier = extendedLevel.UniqueIdentificationName;
        public bool isHidden = extendedLevel.IsRouteHidden;
        public bool isLocked = extendedLevel.IsRouteLocked;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref uniqueIdentifier);
            serializer.SerializeValue(ref isHidden);
            serializer.SerializeValue(ref isLocked);
        }

        public readonly void ApplySavedValues(ExtendedLevel extendedLevel)
        {
            extendedLevel.IsRouteHidden = isHidden;
            extendedLevel.IsRouteLocked = isLocked;
        }
    }
}
