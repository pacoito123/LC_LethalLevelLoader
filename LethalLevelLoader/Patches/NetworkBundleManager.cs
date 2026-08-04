using LethalLevelLoader.AssetBundles;
using LethalLevelLoader.Compatibility;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalLevelLoader
{
    internal sealed class NetworkBundleManager : NetworkBehaviour, IDisposable
    {
        public static GameObject networkingManagerPrefab;
        private static NetworkBundleManager _instance;
        public static NetworkBundleManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<NetworkBundleManager>(FindObjectsInactive.Exclude);
                if (_instance == null)
                    DebugHelper.LogError("NetworkBundleManager Could Not Be Found! Returning Null!", DebugType.User);
                return _instance;
            }
            set => _instance = value;
        }
        public static NetworkManager networkManager;

        public List<NetworkSceneInfo> networkSceneInfos;
        internal Dictionary<string, List<AssetBundleGroup>> assetBundleGroupSceneDict = new Dictionary<string, List<AssetBundleGroup>>();

        //internal List<AssetBundleGroup> currentRouteRequestedBundles = new List<AssetBundleGroup>();
        internal ExtendedLevel currentRouteRequestor;

        private readonly NetworkVariable<bool> allowedToLoadLevel = new NetworkVariable<bool>();
        internal static bool AllowedToLoadLevel => Instance != null && Instance.allowedToLoadLevel.Value;

        //private Dictionary<ulong, bool> playersReadyDict = new Dictionary<ulong, bool>();

        private readonly NetworkList<bool> playersLoadStatus = new NetworkList<bool>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (_instance != null && _instance != this) // Using '_instance' to avoid the FindObjectOfType() search + error log.
            {
                // Transfer currently-loaded bundle to the new bundle manager instance.
                currentRouteRequestor = Instance.currentRouteRequestor;

                Destroy(Instance);
            }

            Instance = this;

            gameObject.name = "NetworkBundleManager";
            DebugHelper.Log("NetworkBundleManger Has Spawned!", DebugType.IAmBatby);

            if (Plugin.IsLobbyInitialized == true)
                Initialize();
            else
            {
                Plugin.onLobbyInitialized -= Initialize;
                Plugin.onLobbyInitialized += Initialize;
            }
            AssetBundles.AssetBundleLoader.OnBundleLoaded.AddListener(Instance.RefreshLoadStatus);
            AssetBundles.AssetBundleLoader.OnBundleUnloaded.AddListener(Instance.RefreshLoadStatus);

            if (DawnLibCompatibility.Enabled)
                allowedToLoadLevel.OnValueChanged += (previousValue, newValue) => DawnLibCompatibility.RefreshLocalClientBundleState(newValue ? 4 : 0); // Done, Queued
        }

        public override void OnDestroy()
        {
            AssetBundles.AssetBundleLoader.OnBundleLoaded.RemoveListener(Instance.RefreshLoadStatus);
            AssetBundles.AssetBundleLoader.OnBundleUnloaded.RemoveListener(Instance.RefreshLoadStatus);
            base.OnDestroy();
        }

        //This should run anytime the client joins a lobby
        private void Initialize()
        {
            Plugin.onLobbyInitialized -= Initialize; // Unsubscribe immediately from the event, to ensure it's only called once per instance.
            DebugHelper.Log("NetworkBundleManager Initializing.", DebugType.User);
            GenerateSceneDict();
            GenerateAssetBundleGroupDict();
            Refresh();
        }

        //Called on Plugin.onSetupComplete
        //Called by StartOfRound.ChangeLevel.Postfix
        internal void Refresh()
        {
            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;

            List<AssetBundleGroup> newGroups = GetRouteGroups(currentLevel);
            List<AssetBundleGroup> previousGroups = new List<AssetBundleGroup>();
            if (currentRouteRequestor != null)
                previousGroups = GetRouteGroups(currentRouteRequestor);

            // Only unload when about to load a different level, OR a level not registered by LLL.
            if (currentLevel != null && (currentRouteRequestor != currentLevel || currentLevel.ContentType is ContentType.External))
            {
                foreach (AssetBundleGroup bundleGroup in previousGroups)
                    if (!newGroups.Contains(bundleGroup))
                        bundleGroup.TryUnloadGroup();

                currentRouteRequestor = currentLevel;
            }

            if (currentLevel != null)
                TerminalManager.simulateKeyword.specialKeywordResult = currentLevel.SimulateNode;

            foreach (AssetBundleGroup bundleGroup in newGroups)
                if (!previousGroups.Contains(bundleGroup))
                    bundleGroup.TryLoadGroup();

            if (IsServer)
                RequestLoadStatusRefreshServerRpc();
        }

        //Called by StartOfRound.OnClientConnect.Postfix
        //Called by StartOfRound.OnClientDisconnect.Postfix
        internal void OnClientsChangedRefresh()
        {
            if (!IsServer || (NetworkManager != null && NetworkManager.ShutdownInProgress)) return;
            RequestLoadStatusRefreshServerRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        internal void RequestLoadStatusRefreshServerRpc()
        {
            DebugHelper.Log("Refeshing Loaded Bundles Status!", DebugType.User);
            playersLoadStatus.Clear();
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                playersLoadStatus.Add(false);
            RequestLoadStatusRefreshClientRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RequestLoadStatusRefreshClientRpc()
        {
            RefreshLoadStatus();
        }

        //Called by RequestLoadStatusRefreshClientRpc
        //Called by OnBundleLoaded
        //Called by OnBundleUnloaded
        private void RefreshLoadStatus()
        {
            bool loadedStatus = true;
            foreach (AssetBundleGroup routeGroup in GetRouteGroups(LevelManager.CurrentExtendedLevel))
                if (routeGroup.LoadedStatus != AssetBundleGroupLoadedStatus.Loaded)
                {
                    loadedStatus = false;
                    if (routeGroup.LoadingStatus != AssetBundleGroupLoadingStatus.Loading)
                    {
                        if (DawnLibCompatibility.Enabled)
                            DawnLibCompatibility.RefreshLocalClientBundleState(2); // Loading
                        routeGroup.TryLoadGroup();
                    }
                }
            DebugHelper.Log("Sending LoadedStatus: " + loadedStatus + " To Server!", DebugType.User);
            SetLoadedStatusServerRpc(NetworkManager.LocalClientId, loadedStatus);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SetLoadedStatusServerRpc(ulong clientID, bool status)
        {
            int index = NetworkManager.ConnectionManager.ConnectedClientIds.FindIndex(id => id == clientID);
            if (playersLoadStatus.Count <= index)
            {
                DebugHelper.LogError("Tried To Set LoadedStatus When List Is Invalid (ClientID: " + clientID + ", Index: " + index + "), Resetting.", DebugType.User);
                RequestLoadStatusRefreshServerRpc();
                return;
            }

            playersLoadStatus[index] = status;
            int progress = 0;
            foreach (bool loadStatus in playersLoadStatus)
                if (loadStatus == true)
                    progress++;
            allowedToLoadLevel.Value = (progress == playersLoadStatus.Count);
            DebugHelper.Log("LoadedStatus Is Currently: (" + progress + " / " + playersLoadStatus.Count + ")", DebugType.User);
        }

        private List<AssetBundleGroup> GetRouteGroups(ExtendedLevel route)
        {
            if (route == null)
                return ([]);

            HashSet<AssetBundleGroup> returnList = new HashSet<AssetBundleGroup>();
            foreach (StringWithRarity sceneSelection in route.SceneSelections)
                if (assetBundleGroupSceneDict.TryGetValue(sceneSelection.Name, out List<AssetBundleGroup> groups))
                    returnList.UnionWith(groups);
            return ([.. returnList]);
        }

        private void GenerateSceneDict()
        {
            networkSceneInfos = new List<NetworkSceneInfo>();
            Dictionary<int, string> levelSceneDict = NetworkScenePatcher.GetLevelSceneDict();
            List<string> scenePaths = [.. levelSceneDict.Values];
            for (int i = 0; i < levelSceneDict.Count; i++)
                if (scenePaths.Count > i)
                    networkSceneInfos.Add(new NetworkSceneInfo(i, scenePaths[i]));
        }

        private void GenerateAssetBundleGroupDict()
        {
            assetBundleGroupSceneDict = new Dictionary<string, List<AssetBundleGroup>>();
            foreach (AssetBundleGroup group in AssetBundles.AssetBundleLoader.Instance.AssetBundleGroups)
                foreach (string groupSceneName in group.GetSceneNames())
                {
                    if (!assetBundleGroupSceneDict.TryGetValue(groupSceneName, out List<AssetBundleGroup> bundleList))
                        assetBundleGroupSceneDict.Add(groupSceneName, [group]);
                    else if (!bundleList.Contains(group))
                        bundleList.Add(group);
                }
        }

        public void Dispose()
        {
            allowedToLoadLevel.Dispose();
            playersLoadStatus.Dispose();
        }

        /*
        internal void LogStuff()
        {
            DebugHelper.Log("NetworkBundleManager Spawned!", DebugType.IAmBatby);

            DebugHelper.Log("NetworkSceneInfos!", DebugType.IAmBatby);
            foreach (NetworkSceneInfo info in networkSceneInfos)
                DebugHelper.Log("Level Scene Index: " + info.LevelSceneIndex + ", Scene Index: " + info.SceneIndex + ", Scene Path: " + info.LevelScenePath + ", Origin: " + info.Origin + ", IsLoaded: " + info.IsLoaded, DebugType.IAmBatby);

            DebugHelper.Log("AssetBundleInfos!", DebugType.User);
            foreach (AssetBundleInfo bundleInfo in AssetBundleLoader.AssetBundleInfos)
                DebugHelper.Log("Path: " + bundleInfo.DirectoryPath + ", IsLoaded: " + bundleInfo.IsLoaded + ", IsSceneBundle: " + bundleInfo.IsSceneBundle, DebugType.IAmBatby);
        }
        */
    }
}
