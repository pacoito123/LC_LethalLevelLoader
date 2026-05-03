using DunGen;
using DunGen.Adapters;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLevelLoader.Compatibility;
using LethalLevelLoader.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using NetworkManager = Unity.Netcode.NetworkManager;

namespace LethalLevelLoader
{
    internal static class Patches
    {
        internal const int priority = 200;

        internal static string delayedSceneLoadingName = string.Empty;

        internal static List<string> allSceneNamesCalledToLoad = new List<string>();

        internal static Dictionary<Camera, float> playerCameras = new Dictionary<Camera, float>();

        internal static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        //Caching this because I need it for checks while the local client is disconnecting which may make direct comparisons inconsistent.
        internal static ulong currentClientId;

        //Singletons and such for these are set in each classes Awake function, But they all are accessible on the first awake function of the earliest one of these four managers awake function, so i grab them directly via findobjectoftype to safely access them as early as possible.
        public static StartOfRound StartOfRound { get; internal set; }
        public static RoundManager RoundManager { get; internal set; }
        public static Terminal Terminal { get; internal set; }
        public static TimeOfDay TimeOfDay { get; internal set; }

        public static ExtendedEvent OnBeforeVanillaContentCollected = new ExtendedEvent();
        public static ExtendedEvent OnAfterVanillaContentCollected = new ExtendedEvent();
        public static ExtendedEvent OnAfterCustomContentRestored = new ExtendedEvent();

        [HarmonyPriority(priority)]
        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        internal static void PreInitSceneScriptAwake_Prefix(PreInitSceneScript __instance)
        {
            if (Plugin.IsSetupComplete == false)
            {
                //AssetBundleLoader.CreateLoadingBundlesHeaderText(__instance);
                if (__instance.TryGetComponent(out AudioSource audioSource))
                    OriginalContent.AudioMixers.Add(audioSource.outputAudioMixerGroup.audioMixer);

                //AssetBundleLoader.LoadBundles(__instance);
                //AssetBundleLoader.onBundlesFinishedLoading += AssetBundleLoader.LoadContentInBundles;

                /*if (LethalBundleManager.CurrentStatus == LethalBundleManager.ModProcessingStatus.Complete)
if (AssetBundleLoader.noBundlesFound == true)
{
    CurrentLoadingStatus = LoadingStatus.Complete;
    AssetBundleLoader.OnBundlesFinishedLoadingInvoke();
}*/


                ContentTagParser.ImportVanillaContentTags();
            }
        }

        [HarmonyPriority(priority)]
        [HarmonyPatch(typeof(PreInitSceneScript), "ChooseLaunchOption")]
        [HarmonyPrefix]
        internal static bool PreInitSceneScriptChooseLaunchOption_Prefix()
        {
            //return ((AssetBundleLoader.loadedFilesTotal - AssetBundleLoader.loadingAssetBundles.Count) == AssetBundleLoader.loadedFilesTotal);
            return true;
        }

        [HarmonyPriority(priority)]
        [HarmonyPatch(typeof(SceneManager), "LoadScene", new Type[] { typeof(string) })]
        [HarmonyPrefix]
        internal static bool SceneManagerLoadScene(string sceneName)
        {
            if (allSceneNamesCalledToLoad.Count == 0)
                allSceneNamesCalledToLoad.Add(SceneManager.GetActiveScene().name);
            if (SceneManager.GetSceneByName(sceneName) != null)
                allSceneNamesCalledToLoad.Add(sceneName);

            if (sceneName == "MainMenu" && !allSceneNamesCalledToLoad.Contains("InitSceneLaunchOptions"))
            {
                DebugHelper.LogError("SceneManager has been told to load Main Menu without ever loading InitSceneLaunchOptions. This will break LethalLevelLoader. This is likely due to a \"Skip to Main Menu\" mod.", DebugType.User);
                return (false);
            }

            if (LethalBundleManager.CurrentStatus == LethalBundleManager.ModProcessingStatus.Loading)
            {
                DebugHelper.LogWarning("SceneManager has attempted to load " + sceneName + " Scene before AssetBundles have finished loading. Pausing request until LethalLevelLoader is ready to proceed.", DebugType.User);
                delayedSceneLoadingName = sceneName;
                LethalBundleManager.OnFinishedProcessing.RemoveListener(LoadMainMenu);
                LethalBundleManager.OnFinishedProcessing.AddListener(LoadMainMenu);

                return (false);
            }
            return (true);
        }

        internal static void LoadMainMenu()
        {
            DebugHelper.LogWarning("Proceeding with the loading of " + delayedSceneLoadingName + " Scene as LethalLevelLoader has finished loading AssetBundles.", DebugType.User);
            if (delayedSceneLoadingName != string.Empty)
                SceneManager.LoadScene(delayedSceneLoadingName);
            delayedSceneLoadingName = string.Empty;
        }

        [HarmonyPatch(typeof(GameNetworkManager), "Start"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void GameNetworkManagerStart_Prefix(GameNetworkManager __instance)
        {
            if (LethalBundleManager.HasFinalisedFoundContent == false)
                LethalBundleManager.FinialiseFoundContent();
            if (Plugin.IsSetupComplete == false)
            {
                LethalLevelLoaderNetworkManager.networkManager = __instance.GetComponent<NetworkManager>();
                NetworkBundleManager.networkManager = __instance.GetComponent<NetworkManager>();
                foreach (NetworkPrefab networkPrefab in __instance.GetComponent<NetworkManager>().NetworkConfig.Prefabs.Prefabs)
                    if (networkPrefab.Prefab.name.Contains("EntranceTeleport"))
                        if (networkPrefab.Prefab.GetComponent<AudioSource>() != null)
                            OriginalContent.AudioMixers.Add(networkPrefab.Prefab.GetComponent<AudioSource>().outputAudioMixerGroup.audioMixer);

                GameObject networkManagerPrefab = PrefabHelper.CreateNetworkPrefab("LethalLevelLoaderNetworkManagerTest");
                networkManagerPrefab.AddComponent<LethalLevelLoaderNetworkManager>();
                //networkManagerPrefab.GetComponent<NetworkObject>().DontDestroyWithOwner = true;
                networkManagerPrefab.GetComponent<NetworkObject>().SceneMigrationSynchronization = true;
                networkManagerPrefab.GetComponent<NetworkObject>().DestroyWithScene = false;
                //GameObject.DontDestroyOnLoad(networkManagerPrefab);
                LethalLevelLoaderNetworkManager.networkingManagerPrefab = networkManagerPrefab;

                LethalLevelLoaderNetworkManager.RegisterNetworkPrefab(networkManagerPrefab);

                DebugHelper.Log("Creating NetworkBundleManager", DebugType.IAmBatby);
                GameObject networkBundleManagerPrefab = PrefabHelper.CreateNetworkPrefab("NetworkBundleManager");
                networkBundleManagerPrefab.AddComponent<NetworkBundleManager>();
                //networkBundleManagerPrefab.GetComponent<NetworkObject>().DontDestroyWithOwner = true;
                networkBundleManagerPrefab.GetComponent<NetworkObject>().SceneMigrationSynchronization = true;
                networkBundleManagerPrefab.GetComponent<NetworkObject>().DestroyWithScene = false;
                //GameObject.DontDestroyOnLoad(networkBundleManagerPrefab);
                NetworkBundleManager.networkingManagerPrefab = networkBundleManagerPrefab;

                LethalLevelLoaderNetworkManager.RegisterNetworkPrefab(networkBundleManagerPrefab);

                AssetBundleLoader.NetworkRegisterCustomContent(__instance.GetComponent<NetworkManager>());
                LethalLevelLoaderNetworkManager.RegisterPrefabs(__instance.GetComponent<NetworkManager>());
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SaveGameValues"), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void GameNetworkManagerSaveGameValues_Postfix(GameNetworkManager __instance)
        {
            // Vanilla checks
            if (!__instance.isHostingGame || !StartOfRound.Instance.inShipPhase || StartOfRound.Instance.isChallengeFile)
                return;
            SaveManager.SaveGameValues();
        }

        [HarmonyPatch(typeof(StartOfRound), "Awake"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void StartOfRoundAwake_Prefix(StartOfRound __instance)
        {
            Plugin.OnBeforeSetupInvoke();
            //Reference Setup
            StartOfRound = __instance;
            RoundManager = UnityEngine.Object.FindFirstObjectByType<RoundManager>(FindObjectsInactive.Exclude);
            Terminal = UnityEngine.Object.FindFirstObjectByType<Terminal>(FindObjectsInactive.Exclude);
            TimeOfDay = UnityEngine.Object.FindFirstObjectByType<TimeOfDay>(FindObjectsInactive.Exclude);

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneLoaded += EventPatches.OnSceneLoaded;

            currentClientId = NetworkManager.Singleton.LocalClientId;

            //Removing the broken cardboard box item please understand 
            //Scrape Vanilla For Content References
            if (Plugin.IsSetupComplete == false)
            {
                StartOfRound.allItemsList.itemsList.RemoveAt(2);

                OnBeforeVanillaContentCollected.Invoke();

                DebugStopwatch.StartStopWatch("Scrape Vanilla Content");
                ContentExtractor.TryScrapeVanillaItems(StartOfRound);
                ContentExtractor.TryScrapeVanillaUnlockableItems(StartOfRound);
                ContentExtractor.TryScrapeVanillaFootstepSurfaces(StartOfRound);
                ContentExtractor.TryScrapeVanillaContent(StartOfRound, RoundManager);
                ContentExtractor.ObtainSpecialItemReferences();

                OnAfterVanillaContentCollected.Invoke();
            }

            //Startup LethalLevelLoader's Network Manager Instance
            if (LethalLevelLoaderNetworkManager.networkManager.IsServer || LethalLevelLoaderNetworkManager.networkManager.IsHost)
            {
                GameObject.Instantiate(LethalLevelLoaderNetworkManager.networkingManagerPrefab).GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
                GameObject.Instantiate(NetworkBundleManager.networkingManagerPrefab).GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
            }

            //Add the facility's firstTimeDungeonAudio additionally to RoundManager's list to fix a base game bug.
            RoundManager.firstTimeDungeonAudios = RoundManager.firstTimeDungeonAudios.ToList().AddItem(RoundManager.firstTimeDungeonAudios[0]).ToArray();
            DebugStopwatch.StartStopWatch("Fix AudioSource Settings");
            //Disable Spatialization In All AudioSources To Fix Log Spam Bug.
            foreach (AudioSource audioSource in Resources.FindObjectsOfTypeAll<AudioSource>())
                audioSource.spatialize = false;

            playerCameras.Clear();
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.targetTexture != null && camera.targetTexture.name == "PlayerScreen")
                    playerCameras.Add(camera, camera.farClipPlane);

            if (DungeonLoader.defaultKeyPrefab == null)
                DungeonLoader.defaultKeyPrefab = RoundManager.keyPrefab;

            if (Plugin.IsSetupComplete == false)
            {
                //Terminal Specific Reference Setup
                TerminalManager.CacheTerminalReferences();

                LevelManager.ObtainShipAnimatorClips(StartOfRound);
                LevelManager.ObtainTimeOfDayClips(TimeOfDay);
                LevelManager.ObtainGrassShaderReference();

                DebugStopwatch.StartStopWatch("Create Vanilla ExtendedContent");
                //Create & Initialize ExtendedContent Objects For Vanilla Content.
                AssetBundleLoader.CreateVanillaExtendedDungeonFlows();
                AssetBundleLoader.CreateVanillaExtendedLevels(StartOfRound);
                AssetBundleLoader.CreateVanillaExtendedItems();
                AssetBundleLoader.CreateVanillaExtendedEnemyTypes();
                AssetBundleLoader.CreateVanillaExtendedBuyableVehicles();
                AssetBundleLoader.CreateVanillaExtendedUnlockableItems();
                AssetBundleLoader.CreateVanillaExtendedFootstepSurfaces();

                DebugStopwatch.StartStopWatch("Initialize Custom ExtendedContent"); // this is not used
                //Initialize ExtendedContent Objects For Custom Content.
                AssetBundleLoader.InitializeBundles();

                PatchedContent.PopulateContentDictionaries();

                if (DawnLibCompatibility.Enabled)
                    DawnLibCompatibility.RegisterDawnExtendedLevels(); // Create ExtendedLevel for DawnLib moons.

                foreach (ExtendedLevel extendedLevel in PatchedContent.CustomExtendedLevels)
                    extendedLevel.SetLevelID();

                //Some Debugging.
                string debugString = "LethalLevelLoader Loaded The Following ExtendedLevels:" + "\n";
                foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                    debugString += (PatchedContent.ExtendedLevels.IndexOf(extendedLevel) + 1) + ". " + extendedLevel.SelectableLevel.PlanetName + " (" + extendedLevel.ContentType + ")" + "\n";
                DebugHelper.Log(debugString, DebugType.User);

                debugString = "LethalLevelLoader Loaded The Following ExtendedDungeonFlows:" + "\n";
                foreach (ExtendedDungeonFlow extendedDungeonFlow in PatchedContent.ExtendedDungeonFlows)
                    debugString += (PatchedContent.ExtendedDungeonFlows.IndexOf(extendedDungeonFlow) + 1) + ". " + extendedDungeonFlow.DungeonName + " (" + extendedDungeonFlow.DungeonFlow.name + ") (" + extendedDungeonFlow.ContentType + ")" + "\n";
                DebugHelper.Log(debugString, DebugType.User);



                DebugStopwatch.StartStopWatch("Restore Content");
                //Restore Custom Content References To Vanilla Content
                foreach (ExtendedLevel customLevel in PatchedContent.CustomExtendedLevels)
                    ContentRestorer.RestoreVanillaLevelAssetReferences(customLevel);

                foreach (ExtendedDungeonFlow customDungeonFlow in PatchedContent.CustomExtendedDungeonFlows)
                    ContentRestorer.RestoreVanillaDungeonAssetReferences(customDungeonFlow);

                ContentRestorer.RestoreVanillaItemAssetReferences(); // LungProp, HauntedMaskItem
                // ContentRestorer.RestoreVanillaEnemyAssetReferences(); // ButlerEnemyAI, CadaverGrowthAI, GiantKiwiAI

                //Destroy Placeholder Custom Content References That Have Now Been Restored
                ContentRestorer.DestroyRestoredAssets();

                OnAfterCustomContentRestored.Invoke();

                DebugStopwatch.StartStopWatch("Dynamic Risk Level");

                //Use Vanilla SelectableLevel's To Populate Information About Moon Difficulty.
                LevelManager.PopulateDynamicRiskLevelDictionary();

                //Assign Risk Level's To Custom SelectableLevel's Using The Populated Vanilla Information As Reference
                LevelManager.AssignCalculatedRiskLevels();

                DebugStopwatch.StartStopWatch("Apply, Merge & Populate Content Tags");

                //Apply ContentTags To Vanilla ExtendedContent Objects.
                ContentTagParser.ApplyVanillaContentTags();

                //Iterate Through All ExtendedMod Objects And Merge Any Reoccurring ContentTagName In The Same ExtendedMod.
                ContentTagManager.MergeAllExtendedModTags();

                //Populate Information About All Current ContentTag's Used In ExtendedContent For Developer Use.
                ContentTagManager.PopulateContentTagData();

                //Debugging.
                DebugHelper.DebugAllContentTags();
                ItemManager.GetExtendedItemPriceData();
                ItemManager.GetExtendedItemWeightData();
            }

            DebugStopwatch.StartStopWatch("Bind Configs");
            //Bind User Configuration Information.
            ConfigLoader.BindConfigs();

            DebugStopwatch.StartStopWatch("Patch Base game Lists");
            //Patch The Base game References To SelectableLevel's To Include Enabled Custom SelectableLevels.
            LevelManager.PatchVanillaLevelLists();

            //Patch The Base game References To DungeonFlows's To Include Enabled Custom DungeonFlows.
            DungeonManager.PatchVanillaDungeonLists();

            //Patch The Base game References To EnemyTypes's To Include Enabled Custom EnemyTypes.
            EnemyManager.UpdateEnemyIDs(); //Might only need to do once?

            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.CustomExtendedEnemyTypes)
                TerminalManager.CreateEnemyTypeTerminalData(extendedEnemyType); //Might only need to do once?

            if (Plugin.IsSetupComplete == false)
                EnemyManager.AddCustomEnemyTypesToTestAllEnemiesLevel(); //Might only need to do once?

            DebugStopwatch.StartStopWatch("ExtendedItem Injection");

            //Dynamically Inject Custom Item's Into SelectableLevel's Based On Level & Dungeon MatchingProperties.
            ItemManager.RefreshDynamicItemRarityOnAllExtendedLevels();

            DebugStopwatch.StartStopWatch("ExtendedEnemyType Injection");

            //Dynamically Inject Custom EnemyType's Into SelectableLevel's Based On Level & Dungeon MatchingProperties.
            EnemyManager.RefreshDynamicEnemyTypeRarityOnAllExtendedLevels();

            DebugStopwatch.StartStopWatch("ExtendedBuyableVehicle Injection");

            VehiclesManager.PatchVanillaVehiclesLists();
            VehiclesManager.SetBuyableVehicleIDs();

            foreach (ExtendedBuyableVehicle customExtendedBuyableVehicle in PatchedContent.CustomExtendedBuyableVehicles)
                TerminalManager.CreateBuyableVehicleTerminalData(customExtendedBuyableVehicle);

            if (Plugin.IsSetupComplete == false) // Only needs to be done once.
            {
                DebugStopwatch.StartStopWatch("ExtendedUnlockableItem Injection");

                UnlockableItemManager.PatchVanillaUnlockableItemLists();
                UnlockableItemManager.SetUnlockableItemIDs();

                foreach (ExtendedUnlockableItem customExtendedUnlockableItem in PatchedContent.CustomExtendedUnlockableItems)
                    TerminalManager.CreateUnlockableItemTerminalData(customExtendedUnlockableItem);
            }

            DebugStopwatch.StartStopWatch("ExtendedFootstepSurface Injection");
            if (Plugin.IsSetupComplete == false)
                FootstepSurfaceManager.MergeExtendedFootstepSurfaces();
            FootstepSurfaceManager.PatchVanillaFootstepSurfaceLists();

            DebugStopwatch.StartStopWatch("Create ExtendedLevelGroups & Filter Assets");

            //Populate SelectableLevel Data To Be Used In Overhaul Of The Terminal Moons Catalogue.
            TerminalManager.CreateExtendedLevelGroups();

            if (Plugin.IsSetupComplete == false)
            {
                //Populate SelectableLevel Data To Be Used In Overhaul Of The Terminal Moons Catalogue.
                TerminalManager.CreateMoonsFilterTerminalAssets();

                foreach (CompatibleNoun routeNode in TerminalManager.routeKeyword.compatibleNouns)
                    TerminalManager.AddTerminalNodeEventListener(routeNode.result, TerminalManager.OnBeforeRouteNodeLoaded, TerminalManager.LoadNodeActionType.Before);

                //Create Terminal Data For Custom StoryLog's And Patch Base game References To StoryLog's To Include Custom StoryLogs.
                TerminalManager.CreateTerminalDataForAllExtendedStoryLogs();

                TerminalManager.AddTerminalNodeEventListener(TerminalManager.moonsKeyword.specialKeywordResult, TerminalManager.RefreshMoonsCataloguePage, TerminalManager.LoadNodeActionType.After);
            }
            else
            {
                // Populate Terminal lists with already-existing ExtendedContent:
                foreach (ExtendedMod extendedMod in PatchedContent.ExtendedMods)
                {
                    // Load ExtendedItem store page entries:
                    if (extendedMod.ExtendedItems.Count > 0)
                    {
                        List<Item> allBuyableItems = [.. Terminal.buyableItemsList];

                        foreach (ExtendedItem extendedItem in extendedMod.ExtendedItems)
                            if (extendedItem.IsBuyableItem)
                                allBuyableItems.Add(extendedItem.Item);

                        Terminal.buyableItemsList = [.. allBuyableItems];
                    }
                    // ...

                    // Load ExtendedEnemyType beastiary entries.
                    if (extendedMod.ExtendedEnemyTypes.Count > 0)
                        foreach (ExtendedEnemyType extendedEnemy in extendedMod.ExtendedEnemyTypes)
                            Terminal.enemyFiles.Add(extendedEnemy.EnemyInfoNode);

                    // Load ExtendedStoryLog journal entries.
                    if (extendedMod.ExtendedStoryLogs.Count > 0)
                        foreach (ExtendedStoryLog extendedStoryLog in extendedMod.ExtendedStoryLogs)
                            Terminal.logEntryFiles.Add(extendedStoryLog.assignedNode);

                    // Load ExtendedBuyableVehicle store page entries:
                    if (extendedMod.ExtendedBuyableVehicles.Count > 0)
                    {
                        List<BuyableVehicle> allVehicles = [.. Terminal.buyableVehicles];

                        foreach (ExtendedBuyableVehicle extendedBuyableVehicle in extendedMod.ExtendedBuyableVehicles)
                            allVehicles.Add(extendedBuyableVehicle.BuyableVehicle);

                        Terminal.buyableVehicles = [.. allVehicles];
                    }
                    // ...
                }
                // ...
            }

            DebugStopwatch.StartStopWatch("Initialize Save");

            if (LethalLevelLoaderNetworkManager.networkManager.IsServer)
                SaveManager.InitializeSave();

            DebugStopwatch.StopStopWatch("Initialize Save");
            if (Plugin.IsSetupComplete == false)
            {
                AssetBundleLoader.CreateVanillaExtendedWeatherEffects(TimeOfDay);
                WeatherManager.RefreshVanillaWeatherEffects(TimeOfDay);
                WeatherManager.PopulateExtendedLevelEnabledExtendedWeatherEffects();
                Plugin.CompleteSetup();
                StartOfRound.SetPlanetsWeather();
            }
            else
                WeatherManager.RefreshVanillaWeatherEffects(TimeOfDay); // Refresh weather stuff on every lobby reload.

            Plugin.LobbyInitialized();
        }

        [HarmonyPatch(typeof(StartOfRound), "SetPlanetsWeather"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool StartOfRoundSetPlanetsWeather_Prefix(int connectedPlayersOnServer)
        {
            if (Plugin.IsSetupComplete == false)
            {
                DebugHelper.LogWarning("Exiting SetPlanetsWeather() Early To Avoid Weather Being Set Before Custom Levels Are Registered.", DebugType.User);
                return (false);
            }
            return (true);
        }

        [HarmonyPatch(typeof(StartOfRound), "SetPlanetsWeather"), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void StartOfRoundSetPlanetsWeather_Postfix()
        {
            if (IsServer)
                LethalLevelLoaderNetworkManager.Instance.GetUpdatedLevelCurrentWeatherServerRpc();
        }

        public static bool hasInitiallyChangedLevel;
        [HarmonyPatch(typeof(StartOfRound), "ChangeLevel"), HarmonyPrefix, HarmonyPriority(priority)]
        public static bool StartOfRoundChangeLevel_Prefix(ref int levelID)
        {
            if (LethalLevelLoaderNetworkManager.networkManager.IsServer == false) return (true);

            //Because Level ID's can change between modpack adjustments and such, we save the name of the level instead and find and load that up instead of the saved ID the base game uses.
            if (hasInitiallyChangedLevel == false && !string.IsNullOrEmpty(SaveManager.currentSaveFile.CurrentLevelName))
                foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                    if (extendedLevel.SelectableLevel.name == SaveManager.currentSaveFile.CurrentLevelName)
                    {
                        DebugHelper.Log("Loading Previously Saved SelectableLevel: " + extendedLevel.SelectableLevel.PlanetName, DebugType.User);
                        levelID = StartOfRound.levels.ToList().IndexOf(extendedLevel.SelectableLevel);
                        hasInitiallyChangedLevel = true;
                        return (true);
                    }


            //If we can't find the previous current level, that probably means the game is going to try and use an ID bigger than the current array, or reference the wrong level, so we reset it back to experimentation here.
            if (hasInitiallyChangedLevel == false && !string.IsNullOrEmpty(SaveManager.currentSaveFile.CurrentLevelName) && !SaveManager.currentSaveFile.CurrentLevelName.Contains("Experimentation") && (levelID >= StartOfRound.levels.Length || levelID > OriginalContent.SelectableLevels.Count))
                levelID = 0;

            hasInitiallyChangedLevel = true;
            return (true);
        }


        [HarmonyPatch(typeof(StartOfRound), "ChangeLevel"), HarmonyPostfix, HarmonyPriority(priority)]
        public static void StartOfRoundChangeLevel_Postfix(int levelID)
        {
            NetworkBundleManager.Instance.Refresh();
            if (IsServer && RoundManager.currentLevel != null && SaveManager.currentSaveFile.CurrentLevelName != RoundManager.currentLevel.PlanetName)
            {
                DebugHelper.Log("Saving Current SelectableLevel: " + RoundManager.currentLevel.PlanetName, DebugType.User);
                SaveManager.currentSaveFile.CurrentLevelName = RoundManager.currentLevel.name;
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "LoadShipGrabbableItems"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void StartOfRoundLoadShipGrabbableItems_Prefix()
        {
            SaveManager.LoadShipGrabbableItems();
        }

        [HarmonyPatch(typeof(Terminal), "ParseWord"), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void TerminalParseWord_Postfix(Terminal __instance, ref TerminalKeyword __result, string playerWord)
        {
            if (__result != null)
            {
                TerminalKeyword newKeyword = TerminalManager.TryFindAlternativeNoun(__instance, __result, playerWord);
                if (newKeyword != null)
                    __result = newKeyword;
            }
        }

        internal static bool ranLethalLevelLoaderTerminalEvent;

        [HarmonyPatch(typeof(Terminal), "RunTerminalEvents"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool TerminalRunTerminalEvents_Prefix(Terminal __instance, TerminalNode node)
        {
            return (TerminalManager.OnBeforeLoadNewNode(ref node));
        }

        [HarmonyPatch(typeof(Terminal), "LoadNewNode"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool TerminalLoadNewNode_Prefix(Terminal __instance, ref TerminalNode node)
        {
            TerminalManager.moonsInCataloguePage = 0;
            return (TerminalManager.OnBeforeLoadNewNode(ref node));
        }

        [HarmonyPatch(typeof(Terminal), "LoadNewNode"), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void TerminalLoadNewNode_Postfix(Terminal __instance, ref TerminalNode node)
        {
            TerminalManager.OnLoadNewNode(ref node);
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ScrollMouse_performed"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool TerminalScrollMouse_Prefix(PlayerControllerB __instance, InputAction.CallbackContext context)
        {
            if (!__instance.inTerminalMenu || TerminalManager.moonsInCataloguePage == 0) return true;

            float scrollAmount = 15 / (float)TerminalManager.moonsInCataloguePage; // Scroll 15 moons at a time, instead of a third of the page.
            float scrollDirection = context.ReadValue<float>();

            __instance.terminalScrollVertical.value += scrollAmount * scrollDirection;
            return false;
        }

        //Called via SceneManager event.
        internal static void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel == null || currentLevel.IsLevelLoaded == false || currentLevel.ContentType is ContentType.External) return;
            foreach (GameObject rootObject in SceneManager.GetSceneByName(currentLevel.SelectableLevel.sceneName).GetRootGameObjects())
                ContentRestorer.RestoreAudioAssetReferencesInParent(rootObject);
            LevelLoader.RefreshShipAnimatorClips(currentLevel);
            LevelLoader.RefreshWeatherEffects(currentLevel);
            LevelLoader.RefreshTimeOfDayMusic(currentLevel);
            if (currentLevel.UseTerrainFootsteps)
            {
                TerrainManager.BakeTerrainFootsteps();
                SceneManager.sceneUnloaded += TerrainManager.CleanupTerrainFootsteps;
            }
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void GenerateNewLevelClientRpc_Prefix(RoundManager __instance)
        {
            // Don't run on the server.
            if (__instance.__rpc_exec_stage is not NetworkBehaviour.__RpcExecStage.Execute) return;

            RestoreRuntimeDungeon();
        }

        private static void RestoreRuntimeDungeon()
        {
            GameObject dungeonGenerator = GameObject.FindGameObjectWithTag("DungeonGenerator");
            if (dungeonGenerator == null)
            {
                DebugHelper.LogFatal("Could not find a GameObject with a DungeonGenerator tag in the current moon!", DebugType.User);
                return;
            }

            Transform levelGenerationContainer = dungeonGenerator.transform.GetParent();
            if (!dungeonGenerator.TryGetComponent(out RuntimeDungeon _))
            {
                DebugHelper.LogWarning("RuntimeDungeon component missing! Creating a replacement to allow landing...", DebugType.User);

                RuntimeDungeon dungeon = dungeonGenerator.AddComponent<RuntimeDungeon>();
                UnityNavMeshAdapter navMeshAdapter = dungeonGenerator.AddComponent<UnityNavMeshAdapter>();

                for (int i = 0; i < levelGenerationContainer.childCount; i++)
                {
                    // Try to find LevelGenerationRoot in the hierarchy.
                    if (levelGenerationContainer.GetChild(i).name.Contains("Root", StringComparison.InvariantCultureIgnoreCase))
                    {
                        dungeon.Root = levelGenerationContainer.GetChild(i).gameObject;
                        break;
                    }
                }

                if (dungeon.Root == null)
                {
                    DebugHelper.LogWarning("Could not locate LevelGenerationRoot GameObject, creating one as well...", DebugType.User);

                    Transform newDungeonRoot = new GameObject("LevelGenerationRoot").transform;
                    newDungeonRoot.SetParent(levelGenerationContainer, worldPositionStays: false);
                    newDungeonRoot.localPosition = new(12, -218, 12);

                    dungeon.Root = newDungeonRoot.gameObject;
                }

                navMeshAdapter.BakeMode = UnityNavMeshAdapter.RuntimeNavMeshBakeMode.FullDungeonBake;
                navMeshAdapter.LayerMask = LayerMask.GetMask("Default", "Room", "Colliders", "NavigationSurface"); // 35072

                dungeon.Generator.AllowTilePooling = true; // Yippee!
                dungeon.Generator.GenerateAsynchronously = true;

                DebugHelper.Log("RuntimeDungeon created, proceeding as usual!", DebugType.User);
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> StartOfRoundStartGame_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions).MatchForward(useEnd: false,
                new(OpCodes.Call, typeof(NetworkBehaviour).GetProperty(nameof(NetworkBehaviour.NetworkManager), BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty).GetGetMethod()),
                new(OpCodes.Callvirt, typeof(NetworkManager).GetProperty(nameof(NetworkManager.SceneManager), BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty).GetGetMethod()),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(StartOfRound).GetField(nameof(StartOfRound.currentLevel))),
                new(OpCodes.Ldfld, typeof(SelectableLevel).GetField(nameof(SelectableLevel.sceneName))),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Callvirt, typeof(NetworkSceneManager).GetMethod(nameof(NetworkSceneManager.LoadScene), BindingFlags.Public)),
                new(OpCodes.Pop))
            .Insert( // Insert call to select a random scene immediately before the current scene begins to load, and after generating the seed for the current round.
                new(OpCodes.Ldfld, typeof(StartOfRound).GetField(nameof(StartOfRound.randomMapSeed))),
                new(OpCodes.Call, typeof(Patches).GetMethod(nameof(PerformSceneSelection), BindingFlags.Static | BindingFlags.NonPublic)),
                new(OpCodes.Ldarg_0))
            .InstructionEnumeration();
        }

        private static void PerformSceneSelection(int randomMapSeed)
        {
            ExtendedLevel extendedLevel = LevelManager.CurrentExtendedLevel;
            if (!IsServer || extendedLevel == null) return;

            extendedLevel.SelectableLevel.sceneName = string.Empty;
            System.Random levelRandom = new(randomMapSeed);

            int counter = 1;
            foreach (StringWithRarity sceneSelection in extendedLevel.SceneSelections)
            {
                DebugHelper.Log("Scene Selection #" + counter + " \"" + sceneSelection.Name + "\" (" + sceneSelection.Rarity + ")", DebugType.Developer);
                counter++;
            }

            int[] sceneSelections = extendedLevel.SceneSelections.Select(s => s.Rarity).ToArray();
            int selectedSceneIndex = RoundManager.GetRandomWeightedIndex(sceneSelections, levelRandom);
            extendedLevel.SelectableLevel.sceneName = extendedLevel.SceneSelections[selectedSceneIndex].Name;
            DebugHelper.Log("Selected SceneName: " + extendedLevel.SelectableLevel.sceneName + " For ExtendedLevel: " + extendedLevel.NumberlessPlanetName, DebugType.Developer);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SceneManager_OnLoadComplete1)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void StartOfRoundOnLoadComplete_Prefix(string sceneName)
        {
            ExtendedLevel extendedLevel = LevelManager.CurrentExtendedLevel;
            if (extendedLevel == null || extendedLevel.SelectableLevel.sceneName == sceneName) return;

            if (extendedLevel.SceneSelections.Select(scene => scene.Name).Contains(sceneName)) // Check if a valid scene loaded.
                extendedLevel.SelectableLevel.sceneName = sceneName; // Update current level's scene name, so the round can end properly.
            else if (sceneName != "SampleSceneRelay")
                DebugHelper.LogFatal($"Critical Failure! Scene '{sceneName}' has no selection entry for ExtendedLevel {extendedLevel.NumberlessPlanetName}!", DebugType.User);
        }

        [HarmonyPatch(typeof(DungeonGenerator), "Generate"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void DungeonGeneratorGenerate_Prefix(DungeonGenerator __instance)
        {
            if (LevelManager.CurrentExtendedLevel != null)
                DungeonLoader.PrepareDungeon();
            LevelManager.LogDayHistory();

            if (RoundManager != null && (RoundManager.dungeonGenerator == null || RoundManager.dungeonGenerator.Generator?.DungeonFlow == null))
                DebugHelper.LogFatal("Critical Failure! DungeonGenerator DungeonFlow Is Null!", DebugType.User);
        }

        // Base game has a bug where it stops listening before it gets the Complete call, so this just fixes the base game function.
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.Generator_OnGenerationStatusChanged)), HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> OnGenerationStatusChanged_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new CodeMatcher(instructions).MatchForward(useEnd: true, new CodeMatch(OpCodes.Bne_Un));
            if (matcher.IsInvalid) return instructions;
            object jumpEnd = matcher.Operand; // Copy instruction to jump to.

            matcher.MatchForward(useEnd: false, new CodeMatch(OpCodes.Brtrue));
            if (matcher.IsInvalid) return instructions;
            matcher.Operand = jumpEnd; // Set instruction to jump to.

            return matcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc)), HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GenerateNewLevelClientRpc_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions).End()
            .MatchBack(useEnd: true,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(RoundManager).GetMethod(nameof(RoundManager.GenerateNewFloor), BindingFlags.Instance | BindingFlags.Public)))
            .SetInstruction(new(OpCodes.Call, typeof(Patches).GetMethod(nameof(InjectHostDungeonFlowSelection), BindingFlags.Static | BindingFlags.NonPublic)))
            .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewFloor)), HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GenerateNewFloor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions).End()
            .MatchBack(useEnd: true,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(RoundManager).GetField(nameof(RoundManager.dungeonGenerator), BindingFlags.Instance | BindingFlags.Public)),
                new(OpCodes.Callvirt, typeof(RuntimeDungeon).GetMethod(nameof(RuntimeDungeon.Generate), BindingFlags.Instance | BindingFlags.Public)))
            .SetInstruction(new(OpCodes.Call, typeof(Patches).GetMethod(nameof(InjectHostDungeonSizeSelection), BindingFlags.Static | BindingFlags.Public)))
            .InstructionEnumeration();
        }

        //Called via Transpiler.
        public static void InjectHostDungeonSizeSelection(RoundManager roundManager)
        {
            if (LevelManager.CurrentExtendedLevel != null)
                LethalLevelLoaderNetworkManager.Instance.GetDungeonFlowSizeServerRpc();
            else
                roundManager.dungeonGenerator.Generate();
        }

        //Called via Transpiler.
        internal static void InjectHostDungeonFlowSelection(RoundManager roundManager)
        {
            if (LevelManager.CurrentExtendedLevel != null)
                DungeonLoader.SelectDungeon();
            else
                roundManager.GenerateNewFloor();
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SetLockedDoors)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerSetLockedDoors_Prefix()
        {
            RoundManager.keyPrefab = DungeonManager.CurrentExtendedDungeonFlow.OverrideKeyPrefab != null ? DungeonManager.CurrentExtendedDungeonFlow.OverrideKeyPrefab : DungeonLoader.defaultKeyPrefab;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnOutsideHazards)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerSpawnOutsideHazards_Prefix()
        {
            RoundManager.quicksandPrefab = LevelManager.CurrentExtendedLevel.OverrideQuicksandPrefab != null ? LevelManager.CurrentExtendedLevel.OverrideQuicksandPrefab : LevelLoader.defaultQuicksandPrefab;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerFinishGeneratingNewLevelClientRpc_Prefix(RoundManager __instance, ref bool __state)
        {
            __state = __instance.__rpc_exec_stage is NetworkBehaviour.__RpcExecStage.Execute;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void RoundManagerFinishGeneratingNewLevelClientRpc_Postfix(RoundManager __instance, bool __state)
        {
            // Don't run on the server.
            if (__state) return;

            if (__instance.currentLevel == null || string.IsNullOrEmpty(__instance.currentLevel.sceneName)) return;
            Scene scene = SceneManager.GetSceneByName(__instance.currentLevel.sceneName);
            if (!scene.isLoaded) return;

            // LevelLoader.RefreshFootstepSurfaces();
            // LevelLoader.BakeSceneColliderMaterialData(scene);
            LevelLoader.TryRestoreShaders(scene);
            ApplyCameraDistanceOverride();
        }

        internal static void ApplyCameraDistanceOverride()
        {
            float newDistance = 0;
            if (LevelManager.CurrentExtendedLevel.OverrideCameraMaxDistance > 400f || (DungeonManager.CurrentExtendedDungeonFlow != null && DungeonManager.CurrentExtendedDungeonFlow.OverrideCameraMaxDistance > 400f))
                newDistance = Mathf.Max(LevelManager.CurrentExtendedLevel.OverrideCameraMaxDistance, DungeonManager.CurrentExtendedDungeonFlow.OverrideCameraMaxDistance);
            foreach (KeyValuePair<Camera, float> cameraPair in playerCameras)
                cameraPair.Key.farClipPlane = Mathf.Max(cameraPair.Value, newDistance);
        }

        [HarmonyPatch(typeof(StoryLog), "Start"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void StoryLogStart_Prefix(StoryLog __instance)
        {
            foreach (ExtendedStoryLog extendedStoryLog in LevelManager.CurrentExtendedLevel.ExtendedMod.ExtendedStoryLogs)
                if (extendedStoryLog.sceneName == __instance.gameObject.scene.name)
                {
                    if (__instance.storyLogID == extendedStoryLog.storyLogID)
                    {
                        DebugHelper.Log("Updating " + extendedStoryLog.storyLogTitle + "ID", DebugType.Developer);
                        __instance.storyLogID = extendedStoryLog.newStoryLogID;
                    }
                }
        }

        private static readonly HashSet<IndoorMapHazard> temporaryIndoorMapHazards = [];
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnMapObjects)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerSpawnMapObjects_Prefix(SelectableLevel ___currentLevel)
        {
            temporaryIndoorMapHazards.UnionWith(DungeonManager.CurrentExtendedDungeonFlow.IndoorMapHazards);
            ___currentLevel.indoorMapHazards ??= [];
            ___currentLevel.indoorMapHazards = [.. ___currentLevel.indoorMapHazards, .. temporaryIndoorMapHazards];
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnMapObjects)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void RoundManagerSpawnMapObjects_Postfix(SelectableLevel ___currentLevel)
        {
            ___currentLevel.indoorMapHazards = Array.FindAll(___currentLevel.indoorMapHazards, mapHazard => !temporaryIndoorMapHazards.Contains(mapHazard));
            temporaryIndoorMapHazards.Clear();
        }

        [HarmonyPatch(typeof(RoundManager), "GeneratedFloorPostProcessing"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerGeneratedFloorPostProcessing_Prefix()
        {
            if (Settings.injectDynamicMatchingWeights)
            {
                ItemManager.InjectCustomItemsIntoLevelViaDynamicRarity(LevelManager.CurrentExtendedLevel, DungeonManager.CurrentExtendedDungeonFlow);
                EnemyManager.InjectCustomEnemyTypesIntoLevelViaDynamicRarity(LevelManager.CurrentExtendedLevel, DungeonManager.CurrentExtendedDungeonFlow);
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.GetCurrentMaterialStandingOn)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> SwapActiveTerrain_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo terrainGetComponentInfo = typeof(Component).GetMethod(nameof(Component.GetComponent), 1, []).MakeGenericMethod(typeof(Terrain));
            CodeMatcher codeMatcher = new CodeMatcher(instructions).MatchForward(useEnd: false,
                new(OpCodes.Callvirt, terrainGetComponentInfo),
                new(OpCodes.Ldnull),
                new(OpCodes.Call));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match GetComponent<Terrain>() call.", DebugType.User);
                return instructions;
            }

            Type genericType = Type.MakeGenericMethodParameter(0).MakeByRefType();
            MethodInfo terrainTryGetComponentInfo = typeof(Component).GetMethod(nameof(Component.TryGetComponent), 1, [genericType]).MakeGenericMethod(typeof(Terrain));
            _ = codeMatcher.RemoveInstructions(3)
            .InsertAndAdvance(
                new(OpCodes.Ldloca_S, (sbyte)0),
                new(OpCodes.Callvirt, terrainTryGetComponentInfo))
            .MatchForward(useEnd: false, new CodeMatch(OpCodes.Stloc_0));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match Terrain local variable assignment.", DebugType.User);
                return instructions;
            }

            _ = codeMatcher.SetOpcodeAndAdvance(OpCodes.Pop) // Not removing Terrain.activeTerrain call before this in case any other Transpiler expects it to still be there.
            .MatchForward(useEnd: false, new CodeMatch(OpCodes.Stloc_1));

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match TerrainData local variable assignment.", DebugType.User);
                return instructions;
            }

            MethodInfo swapTerrainAlphaMapInfo = typeof(Patches).GetMethod(nameof(SwapTerrainAlphaMap), BindingFlags.Static | BindingFlags.NonPublic);
            return codeMatcher.Insert(
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, swapTerrainAlphaMapInfo))
            .InstructionEnumeration();
        }

        private static void SwapTerrainAlphaMap(Terrain terrain)
        {
            if (TerrainManager.CurrentTerrain == terrain) return;
            TerrainManager.CurrentTerrain = terrain;

            if (!TerrainManager.TerrainAlphaMaps.TryGetValue(terrain, out float[,,] alphaMaps))
            {
                TerrainData terrainData = terrain.terrainData;
                alphaMaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
                TerrainManager.TerrainAlphaMaps[terrain] = alphaMaps;
            }

            StartOfRound.Instance.currentTerrainAlphaMaps = alphaMaps;
            StartOfRound.Instance.gotCurrentTerrainAlphamaps = alphaMaps != null;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.GetCurrentMaterialStandingOn)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> SwapFootstepSurface_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            MethodInfo startOfRoundGetter = typeof(StartOfRound).GetProperty(nameof(StartOfRound.Instance), BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            FieldInfo currentLevelInfo = typeof(StartOfRound).GetField(nameof(StartOfRound.currentLevel), BindingFlags.Instance | BindingFlags.Public);
            FieldInfo levelIDInfo = typeof(SelectableLevel).GetField(nameof(SelectableLevel.levelID), BindingFlags.Instance | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).MatchForward(useEnd: false,
                new(OpCodes.Call, startOfRoundGetter),
                new(OpCodes.Ldfld, currentLevelInfo),
                new(OpCodes.Ldfld, levelIDInfo),
                new(OpCodes.Ldc_I4_2)); // Match immediately before Vow level check.

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match Vow level check.", DebugType.User);
                return instructions;
            }

            MethodInfo tryGetFootstepSurfaceIndexInfo = typeof(FootstepSurfaceManager).GetMethod(nameof(FootstepSurfaceManager.TryGetFootstepSurfaceIndex), BindingFlags.Static | BindingFlags.Public);
            FieldInfo currentFootstepSurfaceIndexInfo = typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.currentFootstepSurfaceIndex), BindingFlags.Instance | BindingFlags.Public);
            FieldInfo standingOnTerrainInfo = typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.standingOnTerrain), BindingFlags.Instance | BindingFlags.NonPublic);
            return codeMatcher.CreateLabel(out Label vanillaFootstepsTarget)
            .Insert( // Insert call to 'FootstepSurfaceManager.TryGetFootstepSurfaceIndex()' and jump to vanilla footstep target if false.
                new(OpCodes.Ldloc_1),
                new(OpCodes.Ldloc_3),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldflda, currentFootstepSurfaceIndexInfo),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldflda, standingOnTerrainInfo),
                new(OpCodes.Call, tryGetFootstepSurfaceIndexInfo),
                new(OpCodes.Brfalse, vanillaFootstepsTarget),
                new(OpCodes.Ret))
            .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.GetCurrentMaterialStandingOn)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> RestoreOldFootsteps_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            FieldInfo currentFootstepSurfaceIndexInfo = typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.currentFootstepSurfaceIndex), BindingFlags.Instance | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, currentFootstepSurfaceIndexInfo),
                new(OpCodes.Ldc_I4_S, (sbyte)12), // Match immediately before Gunkfish slime footstep check.
                new(OpCodes.Beq));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match Gunkfish slime footstep check.", DebugType.User);
                return instructions;
            }

            _ = codeMatcher.CreateLabel(out Label terrainFootstepOverrideTarget)
            .MatchBack(useEnd: true,
                new(OpCodes.Ldarg_1), // Match immediately after 'checkStandingOnTerrain' check.
                new(OpCodes.Brfalse),
                new(OpCodes.Ret));

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match 'checkStandingOnTerrain' check.", DebugType.User);
                return instructions;
            }

            MethodInfo currentExtendedLevelGetter = typeof(LevelManager).GetProperty(nameof(LevelManager.CurrentExtendedLevel), BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            MethodInfo useTerrainFootstepsGetter = typeof(ExtendedLevel).GetProperty(nameof(ExtendedLevel.UseTerrainFootsteps), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            return codeMatcher.Insert( // Insert call to 'LevelManager.CurrentExtendedLevel.UseTerrainFootsteps' and jump to Gunkfish slime footstep check if false.
                new(OpCodes.Call, currentExtendedLevelGetter),
                new(OpCodes.Callvirt, useTerrainFootstepsGetter),
                new(OpCodes.Brfalse, terrainFootstepOverrideTarget))
            .CreateLabel(out Label checkStandingOnTerrainTarget)
            .Advance(-2)
            .SetOperandAndAdvance(checkStandingOnTerrainTarget) // Update target position of the matched 'brfalse' instruction.
            .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientConnect)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void StartOfRoundOnClientConnect_Postfix()
        {
            NetworkBundleManager.Instance.OnClientsChangedRefresh();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientDisconnect)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void StartOfRoundOnClientDisconnect_Postfix(ulong clientId)
        {
            if (clientId != currentClientId)
                NetworkBundleManager.Instance.OnClientsChangedRefresh();
        }

        [HarmonyPatch(typeof(NetworkConnectionManager), nameof(NetworkConnectionManager.OnClientDisconnectFromServer)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void NetworkConnectionManagerOnClientDisconnectFromServer_Postfix(ulong clientId)
        {
            if (clientId != currentClientId)
                NetworkBundleManager.Instance.OnClientsChangedRefresh();
        }


        internal const string disabledText = "[ At least one player is loading custom moon! ]";
        internal const string routingText = "Routing...";

        [HarmonyPatch(typeof(StartMatchLever), nameof(StartMatchLever.Update)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> StartMatchLever_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            CodeMatch[] matches = [new(OpCodes.Call, typeof(GameNetworkManager).GetProperty(nameof(GameNetworkManager.Instance), BindingFlags.Static | BindingFlags.Public).GetGetMethod()),
                new(OpCodes.Ldfld, typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.gameHasStarted), BindingFlags.Instance | BindingFlags.Public))];

            return new CodeMatcher(instructions, generator).MatchForward(false, matches)
            .Advance(3)
            .InsertAndAdvance( // Set lever as uninteractable for clients while the game hasn't started yet.
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(StartMatchLever).GetField(nameof(StartMatchLever.triggerScript), BindingFlags.Instance | BindingFlags.Public)),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Stfld, typeof(InteractTrigger).GetField(nameof(InteractTrigger.interactable), BindingFlags.Instance | BindingFlags.Public)))
            .MatchForward(false, matches)
            .CreateLabel(out Label readyTarget)
            .Insert(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(StartMatchLever).GetField(nameof(StartMatchLever.triggerScript), BindingFlags.Instance | BindingFlags.Public)),
                new(OpCodes.Call, typeof(Patches).GetMethod(nameof(CheckLever), BindingFlags.Static | BindingFlags.NonPublic)),
                new(OpCodes.Brtrue, readyTarget),
                new(OpCodes.Ret)) // Return early to avoid tooltip being replaced with "Start game/Land ship", if bundle is not yet loaded.
            .InstructionEnumeration();
        }

        private static bool CheckLever(InteractTrigger trigger)
        {
            trigger.interactable = NetworkBundleManager.AllowedToLoadLevel && !StartOfRound.travellingToNewLevel;

            if (!trigger.interactable)
            {
                trigger.disabledHoverTip = StartOfRound.travellingToNewLevel ? routingText : disabledText;

                return false;
            }

            return true;
        }

        /* //DunGen Optimization Patches (Credit To LadyRaphtalia, Author Of Scarlet Devil Mansion)
        [HarmonyPatch(typeof(DoorwayPairFinder), "GetDoorwayPairs"), HarmonyPrefix, HarmonyPriority(priority)] // TODO: Check if still needed
        public static bool GetDoorwayPairsPatch(ref DoorwayPairFinder __instance, int? maxCount, ref Queue<DoorwayPair> __result)
        {

            __instance.tileOrder = __instance.CalculateOrderedListOfTiles();
            var doorwayPairs = __instance.PreviousTile == null ?
              __instance.GetPotentialDoorwayPairsForFirstTile() :
              __instance.GetPotentialDoorwayPairsForNonFirstTile();

            var num = doorwayPairs.Count();
            if (maxCount != null)
                num = Mathf.Min(num, maxCount.Value);
            __result = new Queue<DoorwayPair>(num);

            var newList = OrderDoorwayPairs(doorwayPairs, num);
            foreach (var item in newList)
                __result.Enqueue(item);

            return false;
        }

        private class DoorwayPairComparer : IComparer<DoorwayPair>
        {
            public int Compare(DoorwayPair x, DoorwayPair y)
            {
                var tileWeight = y.TileWeight.CompareTo(x.TileWeight);
                if (tileWeight == 0) return y.DoorwayWeight.CompareTo(x.DoorwayWeight);
                return tileWeight;
            }
        }

        private static IEnumerable<DoorwayPair> OrderDoorwayPairs(IEnumerable<DoorwayPair> list, int num)
        {
            return list.OrderBy(x => x, new DoorwayPairComparer()).Take(num);
        } */

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadPlanetsMoldSpreadData))]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetSavedGameValues))]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveGameValues))]
        [HarmonyPatch(typeof(MoldSpreadManager), nameof(MoldSpreadManager.Start)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> MoldSaveData_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo gameObjectGetter = typeof(GameObject).GetProperty(nameof(GameObject.gameObject), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo objectNameGetter = typeof(UnityEngine.Object).GetProperty(nameof(UnityEngine.Object.name), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            FieldInfo levelIDInfo = typeof(SelectableLevel).GetField(nameof(SelectableLevel.levelID), BindingFlags.Instance | BindingFlags.Public);

            return new CodeMatcher(instructions).MatchForward(useEnd: true,
                new CodeMatch(OpCodes.Ldstr))
            .Repeat(matcher =>
                {
                    string saveKey = $"{matcher.Operand}";
                    if (saveKey.StartsWith("Level{0}"))
                    {
                        matcher.SearchForward(ci => ci.Is(OpCodes.Ldfld, levelIDInfo)) // Skip to `SelectableLevel.levelID`.
                            .SetAndAdvance(OpCodes.Callvirt, gameObjectGetter)
                            .SetAndAdvance(OpCodes.Callvirt, objectNameGetter);
                        return;
                    }
                    matcher.Advance(1);
                })
            .InstructionEnumeration();
        }
    }
}