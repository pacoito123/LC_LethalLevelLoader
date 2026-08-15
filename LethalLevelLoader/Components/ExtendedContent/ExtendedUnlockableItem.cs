using LethalLevelLoader.Tools;
using Unity.Netcode;
using UnityEngine;

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "ExtendedUnlockableItem", menuName = "Lethal Level Loader/Extended Content/ExtendedUnlockableItem", order = 21)]
    public class ExtendedUnlockableItem : ExtendedContent
    {
        [field: Header("General Settings")]

        [field: SerializeField] public UnlockableItem UnlockableItem { get; set; }

        [field: SerializeField] public int ItemCost { get; set; }

        [field: Space(5)]
        [field: Header("Terminal Store & Info Override Settings")]

        [field: TextArea(2, 20)]
        [field: SerializeField] public string OverrideInfoNodeDescription { get; set; } = string.Empty;
        [field: TextArea(2, 20)]
        [field: SerializeField] public string OverrideBuyNodeDescription { get; set; } = string.Empty;
        [field: TextArea(2, 20)]
        [field: SerializeField] public string OverrideBuyConfirmNodeDescription { get; set; } = string.Empty;

        public int UnlockableItemID { get; set; } = -1;

        public TerminalNode BuyNode { get; internal set; }
        public TerminalNode BuyConfirmNode { get; internal set; }
        public TerminalNode BuyInfoNode { get; internal set; }

        public void Initialize()
        {
            name = UnlockableItem.unlockableName + "ExtendedDungeonFlow";

            if (UnlockableItem.prefabObject == null)
                ContentRestorer.RestoreAudioAssetReferencesInParent(UnlockableItem.prefabObject);
        }

        internal static ExtendedUnlockableItem Create(UnlockableItem newUnlockableItem)
        {
            ExtendedUnlockableItem extendedUnlockableItem = CreateInstance<ExtendedUnlockableItem>();
            extendedUnlockableItem.UnlockableItem = newUnlockableItem;
            extendedUnlockableItem.name = newUnlockableItem.unlockableName.Sanitized(toLower: false, removeWhitespace: true) + "ExtendedUnlockableItem";

            return (extendedUnlockableItem);
        }

        internal override (bool result, string log) TryValidateContent()
        {
            if (UnlockableItem.unlockableType == 1 && !UnlockableItem.alreadyUnlocked)
            {
                if (UnlockableItem.prefabObject == null)
                    return ((false, "Unlockable Item Prefab Was Null Or Empty"));
                else if (!UnlockableItem.prefabObject.TryGetComponent(out NetworkObject _))
                    return ((false, "Unlockable Item Prefab Is Missing NetworkObject Component"));
                else if (!UnlockableItem.prefabObject.TryGetComponent(out AutoParentToShip _))
                    return ((false, "Unlockable Item Prefab Is Missing AutoParentToShip Component"));
            }
            else if (UnlockableItem.unlockableType == 0 && UnlockableItem.suitMaterial == null)
                return ((false, "Unlockable Suit Is Missing Suit Material"));
            return (base.TryValidateContent());
        }
    }
}