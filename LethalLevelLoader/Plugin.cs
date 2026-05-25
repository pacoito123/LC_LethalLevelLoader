using BepInEx;
using HarmonyLib;
using LethalLevelLoader.Compatibility;
using LethalLevelLoader.Patcher;
using LethalLevelLoader.Tools;
using System;
using Unity.Netcode;
using UnityEngine;
using Application = UnityEngine.Application;

namespace LethalLevelLoader
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(LethalModDataLib.PluginInfo.PLUGIN_GUID)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "imabatby.lethallevelloader";
        public const string ModName = "LethalLevelLoader";
        public const string ModVersion = "1.7.3";

        internal static Plugin Instance;

        internal static readonly Harmony Harmony = new Harmony(ModGUID);

        internal static BepInEx.Logging.ManualLogSource logger;

        public static event Action onBeforeSetup;
        public static event Action onSetupComplete; //Happens on the first lobby in a session
        public static event Action onLobbyInitialized; //Happens per lobby in a session
        public static bool IsSetupComplete { get; private set; }
        public static bool IsLobbyInitialized { get; internal set; }

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

            LethalLevelLoaderPatcher.onChainloaderFinish += HandleAdditionalCompatibilities;

            NetworkScenePatcher.Patch();

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

            ConfigLoader.BindGeneralConfigs();

            LethalBundleManager.Start();
            //LethalBundleManager.TryLoadLethalBundles();

            //AssetBundleLoader.onBundlesFinishedLoading += AssetBundleLoader.LoadContentInBundles;
        }

        internal static void OnBeforeSetupInvoke()
        {
            IsLobbyInitialized = false;
            onBeforeSetup?.Invoke();
        }

        internal static void CompleteSetup()
        {
            DebugHelper.Log("LethalLevelLoader Has Finished Initializing.", DebugType.User);
            IsSetupComplete = true;
            onSetupComplete?.Invoke();
        }

        internal static void LobbyInitialized()
        {
            IsLobbyInitialized = true;
            onLobbyInitialized?.Invoke();
        }

        private static void HandleAdditionalCompatibilities()
        {
            Harmony.PatchAll(typeof(TimeOfDayPatches));

            if (LethalLibCompatibility.Enabled)
                Harmony.PatchAll(typeof(LethalLibCompatibility));

            if (DeepSewersCompatibility.Enabled)
                DeepSewersCompatibility.FixDeepSewersGeneration();

            DebugHelper.Log("Additional LethalLevelLoader Compatibilities Done.", DebugType.User);
        }
    }
}