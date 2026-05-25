using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class VehiclesManager
    {
        internal static void PatchVanillaVehiclesLists()
        {
            List<BuyableVehicle> buyableVehicles = new List<BuyableVehicle>(PatchedContent.ExtendedBuyableVehicles.Count);
            List<GameObject> vehiclePrefabs = new List<GameObject>(PatchedContent.ExtendedBuyableVehicles.Count);
            foreach (ExtendedBuyableVehicle extendedVehicle in PatchedContent.ExtendedBuyableVehicles)
            {
                if (extendedVehicle != null && extendedVehicle.BuyableVehicle != null && extendedVehicle.BuyableVehicle.vehiclePrefab != null)
                {
                    buyableVehicles.Add(extendedVehicle.BuyableVehicle);
                    vehiclePrefabs.Add(extendedVehicle.BuyableVehicle.vehiclePrefab);
                }
            }
            Patches.Terminal.buyableVehicles = [.. buyableVehicles];
            Patches.StartOfRound.VehiclesList = [.. vehiclePrefabs];
        }

        internal static void SetBuyableVehicleIDs()
        {
            foreach (ExtendedBuyableVehicle extendedBuyableVehicle in PatchedContent.ExtendedBuyableVehicles)
                extendedBuyableVehicle.VehicleID = -1;

            int vehicleID = 0;
            foreach (ExtendedBuyableVehicle vanillaBuyableVehicle in PatchedContent.VanillaExtendedBuyableVehicles)
            {
                vanillaBuyableVehicle.VehicleID = vehicleID;
                vehicleID++;
            }

            foreach (ExtendedBuyableVehicle customBuyableVehicle in PatchedContent.CustomExtendedBuyableVehicles)
            {
                customBuyableVehicle.VehicleID = vehicleID;
                vehicleID++;
            }

            foreach (ExtendedBuyableVehicle extendedBuyableVehicle in PatchedContent.ExtendedBuyableVehicles)
                if (extendedBuyableVehicle.BuyableVehicle.vehiclePrefab.TryGetComponent(out VehicleController vehicleController))
                    vehicleController.vehicleID = extendedBuyableVehicle.VehicleID;
        }
    }
}
