using System.Collections.Generic;

namespace LethalLevelLoader
{
    public static class UnlockableItemManager
    {
        internal static void PatchVanillaUnlockableItemLists()
        {
            if (PatchedContent.CustomExtendedUnlockableItems.Count == 0) return;

            List<UnlockableItem> unlockableItems = Patches.StartOfRound.unlockablesList.unlockables;
            foreach (ExtendedUnlockableItem extendedUnlockableItem in PatchedContent.CustomExtendedUnlockableItems)
            {
                if (extendedUnlockableItem.ContentType is ContentType.External) continue;

                extendedUnlockableItem.UnlockableItemID = unlockableItems.Count;
                unlockableItems.Add(extendedUnlockableItem.UnlockableItem);

                if (extendedUnlockableItem.UnlockableItem.unlockableType == 1)
                {
                    if (extendedUnlockableItem.UnlockableItem.prefabObject == null || extendedUnlockableItem.UnlockableItem.alreadyUnlocked) continue;

                    if (extendedUnlockableItem.UnlockableItem.prefabObject.TryGetComponent(out AutoParentToShip autoParentToShip))
                        autoParentToShip.unlockableID = extendedUnlockableItem.UnlockableItemID;

                    PlaceableShipObject placeableShipObject = extendedUnlockableItem.UnlockableItem.prefabObject.GetComponentInChildren<PlaceableShipObject>(includeInactive: false);
                    if (placeableShipObject != null)
                    {
                        placeableShipObject.parentObject = autoParentToShip;
                        placeableShipObject.unlockableID = extendedUnlockableItem.UnlockableItemID;
                    }
                }
            }
        }
    }
}