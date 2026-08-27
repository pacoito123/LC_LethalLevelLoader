using BepInEx;
using BepInEx.Logging;
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
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "imabatby.lethallevelloader";
        public const string ModName = "LethalLevelLoader";
        public const string ModVersion = "1.7.12";

        internal static readonly Harmony Harmony = new Harmony(ModGUID);
        internal static new readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public static event Action onBeforeSetup;
        public static event Action onSetupComplete; //Happens on the first lobby in a session
        public static event Action onLobbyInitialized; //Happens per lobby in a session
        public static bool IsSetupComplete { get; private set; }
        public static bool IsLobbyInitialized { get; internal set; }

        private void Awake()
        {
            ConfigLoader.BindGeneralConfigs();

            Harmony.PatchAll(typeof(Patches));
            Harmony.PatchAll(typeof(EventPatches));
            Harmony.PatchAll(typeof(SafetyPatches));
            Harmony.PatchAll(typeof(SavePatches));

            LethalLevelLoaderPatcher.onChainloaderFinish += HandleAdditionalCompatibilities;

            NetworkScenePatcher.Patch();

            // Allow using NetworkVariables with bool types:
            NetworkVariableSerializationTypes.InitializeSerializer_UnmanagedByMemcpy<bool>();
            NetworkVariableSerializationTypes.InitializeEqualityChecker_UnmanagedIEquatable<bool>();
            // ...

            GameObject assetBundleLoaderObject = new GameObject("LethalLevelLoader AssetBundleLoader");
            AssetBundleLoader assetBundleLoader = assetBundleLoaderObject.AddComponent<AssetBundleLoader>();
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

            DebugHelper.Log($"{ModName} loaded!!", DebugType.User);
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

            try
            {
                if (DawnLibCompatibility.Enabled)
                    Harmony.PatchAll(typeof(DawnLibCompatibility));
            }
            catch (Exception ex)
            {
                DebugHelper.LogError(ex, DebugType.User);
            }

            if (LethalLibCompatibility.Enabled)
                Harmony.PatchAll(typeof(LethalLibCompatibility));

            if (LethalPerformanceCompatibility.Enabled)
                Harmony.PatchAll(typeof(LethalPerformanceCompatibility));

            if (DeepSewersCompatibility.Enabled)
                DeepSewersCompatibility.FixDeepSewersGeneration();

            DebugHelper.Log("Additional LethalLevelLoader Compatibilities Done.", DebugType.User);
        }
    }
}