using LethalLevelLoader.Tools;
using Unity.Netcode;
using UnityEngine;

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "ExtendedBuyableVehicle", menuName = "Lethal Level Loader/Extended Content/ExtendedBuyableVehicle", order = 21)]
    public class ExtendedBuyableVehicle : ExtendedContent
    {
        [field: SerializeField] public BuyableVehicle BuyableVehicle { get; set; }
        [field: SerializeField] public string TerminalKeywordName { get; set; } = string.Empty;

        public int VehicleID { get; set; }

        [field: Header("Terminal Store & Info Override Settings")]

        [field: SerializeField] public TerminalNode VehicleBuyNode { get; set; }
        [field: SerializeField] public TerminalNode VehicleBuyConfirmNode { get; set; }
        [field: SerializeField] public TerminalNode VehicleInfoNode { get; set; }

        internal static ExtendedBuyableVehicle Create(BuyableVehicle newBuyableVehicle)
        {
            ExtendedBuyableVehicle newExtendedBuyableVehicle = CreateInstance<ExtendedBuyableVehicle>();
            newExtendedBuyableVehicle.name = newBuyableVehicle.vehiclePrefab.name;
            newExtendedBuyableVehicle.BuyableVehicle = newBuyableVehicle;

            return (newExtendedBuyableVehicle);
        }

        public void Initialize()
        {
            ContentRestorer.RestoreAudioAssetReferencesInParent(BuyableVehicle.vehiclePrefab);
            if (BuyableVehicle.secondaryPrefab != null)
                ContentRestorer.RestoreAudioAssetReferencesInParent(BuyableVehicle.secondaryPrefab);
        }

        internal override (bool result, string log) TryValidateContent()
        {
            if (BuyableVehicle == null)
                return ((false, "BuyableVehicle Was Null Or Empty"));
            else if (BuyableVehicle.vehiclePrefab == null)
                return ((false, "Vehicle Prefab Was Null Or Empty"));
            else if (!BuyableVehicle.vehiclePrefab.TryGetComponent<NetworkObject>(out _))
                return ((false, "Vehicle Prefab Is Missing NetworkObject Component"));
            else if (BuyableVehicle.secondaryPrefab != null && !BuyableVehicle.secondaryPrefab.TryGetComponent<NetworkObject>(out _))
                return ((false, "Vehicle Secondary Prefab Is Missing NetworkObject Component"));
            else
                return (base.TryValidateContent());
        }
    }
}
