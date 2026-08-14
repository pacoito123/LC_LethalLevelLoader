using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using DunGen.Graph;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Reflection;
using LethalLevelLoader.Tools;

namespace LethalLevelLoader
{
    public sealed class LethalLevelLoaderNetworkManager : NetworkBehaviour
    {
        public static GameObject networkingManagerPrefab;
        private static LethalLevelLoaderNetworkManager _instance;
        public static LethalLevelLoaderNetworkManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<LethalLevelLoaderNetworkManager>(FindObjectsInactive.Exclude);
                if (_instance == null)
                    DebugHelper.LogError("LethalLevelLoaderNetworkManager Could Not Be Found! Returning Null!", DebugType.User);
                return _instance;
            }
            set => _instance = value;
        }
        public static NetworkManager networkManager;

        private static readonly List<GameObject> queuedNetworkPrefabs = new List<GameObject>();
        public static bool networkHasStarted;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (_instance != null && _instance != this) // Using '_instance' to avoid the FindObjectOfType() search + error log.
            {
                Destroy(Instance);
            }

            Instance = this;

            gameObject.name = "LethalLevelLoaderNetworkManager";
            DebugHelper.Log("LethalLevelLoaderNetworkManager Spawned.", DebugType.User);
        }

        [Rpc(SendTo.Server)]
        public void GetRandomExtendedDungeonFlowServerRpc()
        {
            DebugHelper.Log("Getting Random DungeonFlows!", DebugType.User);

            List<ExtendedDungeonFlowWithRarity> availableExtendedFlowsList = DungeonManager.GetValidExtendedDungeonFlows(LevelManager.CurrentExtendedLevel, debugResults: true);

            List<StringContainer> dungeonFlowNames = new List<StringContainer>();
            List<int> rarities = new List<int>();

            if (availableExtendedFlowsList.Count == 0)
            {
                DebugHelper.LogError("Loading Facility DungeonFlow to prevent infinite loading!", DebugType.User);
                StringContainer newStringContainer = new StringContainer
                {
                    SomeText = PatchedContent.ExtendedDungeonFlows[0].DungeonFlow.name
                };
                dungeonFlowNames.Add(newStringContainer);
                rarities.Add(300);
            }
            else
            {
                List<DungeonFlow> dungeonFlowTypes = Patches.RoundManager.GetDungeonFlows();
                foreach (ExtendedDungeonFlowWithRarity extendedDungeonFlowWithRarity in availableExtendedFlowsList)
                {
                    StringContainer newStringContainer = new StringContainer
                    {
                        SomeText = dungeonFlowTypes[dungeonFlowTypes.IndexOf(extendedDungeonFlowWithRarity.extendedDungeonFlow.DungeonFlow)].name
                    };
                    dungeonFlowNames.Add(newStringContainer);

                    rarities.Add(extendedDungeonFlowWithRarity.rarity);
                }
            }

            SetRandomExtendedDungeonFlowClientRpc(dungeonFlowNames.ToArray(), rarities.ToArray());
        }

        [Rpc(SendTo.Server)]
        public void GetUpdatedLevelCurrentWeatherServerRpc()
        {
            List<StringContainer> levelNames = new List<StringContainer>();
            List<LevelWeatherType> weatherTypes = new List<LevelWeatherType>();
            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
            {
                StringContainer stringContainer = new StringContainer
                {
                    SomeText = extendedLevel.name
                };
                levelNames.Add(stringContainer);
                weatherTypes.Add(extendedLevel.SelectableLevel.currentWeather);
            }

            SetUpdatedLevelCurrentWeatherClientRpc(levelNames.ToArray(), weatherTypes.ToArray());
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void SetUpdatedLevelCurrentWeatherClientRpc(StringContainer[] levelNames, LevelWeatherType[] weatherTypes)
        {
            Dictionary<ExtendedLevel, LevelWeatherType> syncedLevelCurrentWeathers = new Dictionary<ExtendedLevel, LevelWeatherType>();

            for (int i = 0; i < levelNames.Length; i++)
                foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                    if (levelNames[i].SomeText == extendedLevel.name)
                        syncedLevelCurrentWeathers.Add(extendedLevel, weatherTypes[i]);

            foreach (KeyValuePair<ExtendedLevel, LevelWeatherType> syncedWeather in syncedLevelCurrentWeathers)
            {
                if (syncedWeather.Key.SelectableLevel.currentWeather != syncedWeather.Value)
                {
                    DebugHelper.LogWarning("Client Had Differing Current Weather Value For ExtendedLevel: " + syncedWeather.Key.NumberlessPlanetName + ", Syncing!", DebugType.User);
                    syncedWeather.Key.SelectableLevel.currentWeather = syncedWeather.Value;
                }
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void SetRandomExtendedDungeonFlowClientRpc(StringContainer[] dungeonFlowNames, int[] rarities) // TODO: Streamline
        {
            DebugHelper.Log("Setting Random DungeonFlows!", DebugType.User);
            List<IntWithRarity> dungeonFlowsList = new List<IntWithRarity>();

            Dictionary<string, int> dungeonFlowIds = new Dictionary<string, int>();
            int counter = 0;
            foreach (DungeonFlow dungeonFlow in Patches.RoundManager.GetDungeonFlows())
            {
                dungeonFlowIds.Add(dungeonFlow.name, counter);
                counter++;
            }
            for (int i = 0; i < dungeonFlowNames.Length; i++)
            {
                IntWithRarity intWithRarity = new IntWithRarity(dungeonFlowIds[dungeonFlowNames[i].SomeText], rarities[i], null);
                dungeonFlowsList.Add(intWithRarity);
            }
            List<IntWithRarity> cachedDungeonFlowsList = [.. LevelManager.CurrentExtendedLevel.SelectableLevel.dungeonFlowTypes];
            LevelManager.CurrentExtendedLevel.SelectableLevel.dungeonFlowTypes = dungeonFlowsList.ToArray();
            Patches.RoundManager.GenerateNewFloor();
            LevelManager.CurrentExtendedLevel.SelectableLevel.dungeonFlowTypes = cachedDungeonFlowsList.ToArray();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void GetDungeonFlowSizeServerRpc(RpcParams rpcParams = default)
        {
            SetDungeonFlowSizeClientRpc(DungeonLoader.GetClampedDungeonSize(), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void SetDungeonFlowSizeClientRpc(float hostSize, RpcParams rpcParams)
        {
            Patches.RoundManager.dungeonGenerator.Generator.LengthMultiplier = hostSize;
            Patches.RoundManager.dungeonGenerator.Generate();
        }

        [Rpc(SendTo.Server)]
        internal void SetExtendedLevelValuesServerRpc(ExtendedLevelData extendedLevelData)
        {
            if (PatchedContent.TryGetExtendedContent(extendedLevelData.UniqueIdentifier, out ExtendedLevel extendedLevel))
                SetExtendedLevelValuesClientRpc(extendedLevelData);
            else
                DebugHelper.Log("Failed To Send Level Info!", DebugType.User);
        }
        [Rpc(SendTo.ClientsAndHost)]
        internal void SetExtendedLevelValuesClientRpc(ExtendedLevelData extendedLevelData)
        {
            if (PatchedContent.TryGetExtendedContent(extendedLevelData.UniqueIdentifier, out ExtendedLevel extendedLevel))
                extendedLevelData.ApplySavedValues(extendedLevel);
            else
                DebugHelper.Log("Failed To Apply Saved Level Info!", DebugType.User);
        }


        public static void RegisterNetworkPrefab(GameObject prefab)
        {
            if (networkHasStarted == false)
                queuedNetworkPrefabs.Add(prefab);
            else
                DebugHelper.LogWarning("Attempted To Register NetworkPrefab: " + prefab + " After GameNetworkManager Has Started!", DebugType.User);
        }

        public static T SetupNetworkManagerObject<T>() where T : NetworkBehaviour
        {
            GameObject newPrefab = new GameObject(nameof(T));
            newPrefab.hideFlags = HideFlags.HideAndDontSave;

            T instancedBehaviour = newPrefab.AddComponent<T>();

            NetworkObject networkObject = newPrefab.AddComponent<NetworkObject>();
            byte[] hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(Assembly.GetCallingAssembly().GetName().Name + nameof(T)));
            networkObject.GlobalObjectIdHash = BitConverter.ToUInt32(hash, 0);
            networkObject.DontDestroyWithOwner = true;
            networkObject.SceneMigrationSynchronization = true;
            networkObject.DestroyWithScene = true;
            DontDestroyOnLoad(newPrefab);

            NetworkManager.Singleton.AddNetworkPrefab(newPrefab);

            return (instancedBehaviour);
        }

        internal static void RegisterPrefabs(NetworkManager networkManager)
        {
            //DebugHelper.Log("Game NetworkManager Start");

            List<GameObject> addedNetworkPrefabs = new List<GameObject>();

            foreach (NetworkPrefab networkPrefab in networkManager.NetworkConfig.Prefabs.m_Prefabs)
                addedNetworkPrefabs.Add(networkPrefab.Prefab);

            int debugCounter = 0;

            foreach (GameObject queuedNetworkPrefab in queuedNetworkPrefabs)
            {
                if (!addedNetworkPrefabs.Contains(queuedNetworkPrefab))
                {
                    //DebugHelper.Log("Trying To Register Prefab: " + queuedNetworkPrefab);
                    networkManager.AddNetworkPrefab(queuedNetworkPrefab);
                    addedNetworkPrefabs.Add(queuedNetworkPrefab);
                }
                else
                    debugCounter++;
            }

            foreach (GameObject addedNetworkPrefab in addedNetworkPrefabs)
                ContentRestorer.RestoreAudioAssetReferencesInParent(addedNetworkPrefab);

            DebugHelper.Log("Skipped Registering " + debugCounter + " NetworkObjects As They Were Already Registered.", DebugType.User);

            networkHasStarted = true;
        }


        public class StringContainer : INetworkSerializable
        {
            public string SomeText;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                if (serializer.IsWriter)
                {
                    serializer.GetFastBufferWriter().WriteValueSafe(SomeText);
                }
                else
                {
                    serializer.GetFastBufferReader().ReadValueSafe(out SomeText);
                }
            }
        }
    }
}
