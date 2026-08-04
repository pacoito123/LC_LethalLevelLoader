using System.Collections.Generic;
using Unity.Netcode;

namespace LethalLevelLoader
{
    public class LLLSaveFile(string currentSaveFileName)
    {
        public string SaveLocation => currentSaveFileName + ".moddata";

        public string CurrentLevelName { get; internal set; } = string.Empty;

        public int parityStepsTaken;
        public Dictionary<int, AllItemsListItemData> itemSaveData = new Dictionary<int, AllItemsListItemData>();
        public List<ExtendedLevelData> extendedLevelSaveData = new List<ExtendedLevelData>();

        public void Save()
        {
            string prefix = Plugin.ModName + '.' + nameof(LLLSaveFile) + '.';
            ES3.Save(nameof(parityStepsTaken), prefix + nameof(parityStepsTaken), SaveLocation);
            ES3.Save(nameof(itemSaveData), prefix + nameof(itemSaveData), SaveLocation);
            ES3.Save(nameof(extendedLevelSaveData), prefix + nameof(extendedLevelSaveData), SaveLocation);
        }

        public void Load()
        {
            string prefix = Plugin.ModName + '.' + nameof(LLLSaveFile) + '.';
            parityStepsTaken = ES3.Load(prefix + nameof(parityStepsTaken), SaveLocation, 0);
            itemSaveData = ES3.Load(prefix + nameof(itemSaveData), SaveLocation, new Dictionary<int, AllItemsListItemData>());
            extendedLevelSaveData = ES3.Load(prefix + nameof(extendedLevelSaveData), SaveLocation, new List<ExtendedLevelData>());
        }

        public void Reset()
        {
            CurrentLevelName = string.Empty;
            parityStepsTaken = 0;
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
