using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Dawn;
using HarmonyLib;
using LethalLevelLoader.Tools;
using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using Application = UnityEngine.Application;

namespace LethalLevelLoader
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(LethalLib.Plugin.ModGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(LethalModDataLib.PluginInfo.PLUGIN_GUID)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "imabatby.lethallevelloader";
        public const string ModName = "LethalLevelLoader";
        public const string ModVersion = "1.5.6";

        internal static Plugin Instance;

        internal static AssetBundle MainAssets;
        internal static readonly Harmony Harmony = new Harmony(ModGUID);

        internal static BepInEx.Logging.ManualLogSource logger;

        public static event Action onBeforeSetup;
        public static event Action onSetupComplete; //Happens on the first lobby in a session
        public static event Action onLobbyInitialized; //Happens per lobby in a session
        public static bool IsSetupComplete { get; private set; } = false;
        public static bool IsLobbyInitialized { get; internal set; } = false;

        internal static GameObject networkManagerPrefab;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;

            logger = Logger;

            Logger.LogInfo($"LethalLevelLoader loaded!!");

            Harmony.PatchAll(typeof(LethalLevelLoaderNetworkManager));
            Harmony.PatchAll(typeof(DungeonLoader));

            Harmony.PatchAll(typeof(Patches));
            Harmony.PatchAll(typeof(EventPatches));
            Harmony.PatchAll(typeof(SafetyPatches));

            TrySoftPatch(LethalLib.Plugin.ModGUID, typeof(LethalLibPatches));

            NetworkScenePatcher.Patch();
            Patches.InitMonoModHooks();

            // Allow using NetworkVariables with bool types:
            NetworkVariableSerializationTypes.InitializeSerializer_UnmanagedByMemcpy<bool>();
            NetworkVariableSerializationTypes.InitializeEqualityChecker_UnmanagedIEquatable<bool>();
            // ...

            GameObject assetBundleLoaderObject = new GameObject("LethalLevelLoader AssetBundleLoader");
            AssetBundleLoader assetBundleLoader = assetBundleLoaderObject.AddComponent<AssetBundleLoader>();
            //assetBundleLoader.LoadBundles();
            if (Application.isEditor)
                DontDestroyOnLoad(assetBundleLoaderObject);
            else
                assetBundleLoaderObject.hideFlags = HideFlags.HideAndDontSave;

            GameObject newAssetBundleLoaderObject = new GameObject("LethalCore-AssetBundleLoader");
            AssetBundles.AssetBundleLoader newAssetBundleLoader = newAssetBundleLoaderObject.AddComponent<AssetBundles.AssetBundleLoader>();
            if (Application.isEditor)
                DontDestroyOnLoad(newAssetBundleLoaderObject);
            else
                newAssetBundleLoaderObject.hideFlags = HideFlags.HideAndDontSave;

            LethalBundleManager.Start();
            //LethalBundleManager.TryLoadLethalBundles();

            //AssetBundleLoader.onBundlesFinishedLoading += AssetBundleLoader.LoadContentInBundles;

            ConfigLoader.BindGeneralConfigs();
        }

        internal static void OnBeforeSetupInvoke()
        {
            IsLobbyInitialized = false;
            onBeforeSetup?.Invoke();
        }

        internal static void CompleteSetup()
        {
            DebugHelper.Log("LethalLevelLoader Has Finished Initializing.", DebugType.User);
            Plugin.IsSetupComplete = true;
            onSetupComplete?.Invoke();
        }

        internal static void LobbyInitialized()
        {
            IsLobbyInitialized = true;
            onLobbyInitialized?.Invoke();
        }

        internal static void TrySoftPatch(string pluginName, Type type)
        {
            if (Chainloader.PluginInfos.ContainsKey(pluginName))
            {
                Harmony.CreateClassProcessor(type, true).Patch();
                DebugHelper.Log(pluginName + "found, enabling compatability patches.", DebugType.User);
            }

        }
    }
}