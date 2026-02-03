using DunGen;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLevelLoader.Compatibility;
using LethalLevelLoader.Tools;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
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
            RoundManager = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
            Terminal = UnityEngine.Object.FindFirstObjectByType<Terminal>();
            TimeOfDay = UnityEngine.Object.FindFirstObjectByType<TimeOfDay>();

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

                DebugStopwatch.StartStopWatch("Create Vanilla ExtendedContent");
                //Create & Initialize ExtendedContent Objects For Vanilla Content.
                AssetBundleLoader.CreateVanillaExtendedDungeonFlows();
                AssetBundleLoader.CreateVanillaExtendedLevels(StartOfRound);
                AssetBundleLoader.CreateVanillaExtendedItems();
                AssetBundleLoader.CreateVanillaExtendedEnemyTypes();
                AssetBundleLoader.CreateVanillaExtendedBuyableVehicles();
                AssetBundleLoader.CreateVanillaExtendedUnlockableItems(StartOfRound);

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

            LevelLoader.defaultFootstepSurfaces = new List<FootstepSurface>(StartOfRound.footstepSurfaces).ToArray();

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
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> StartOfRoundStartGame_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions).MatchForward(useEnd: false,
                new(OpCodes.Call, AccessTools.Method(typeof(NetworkManager), "get_NetworkManager")),
                new(OpCodes.Callvirt, AccessTools.Method(typeof(NetworkSceneManager), "get_SceneManager")),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.currentLevel))),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(SelectableLevel), nameof(SelectableLevel.sceneName))),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Callvirt, AccessTools.Method(typeof(NetworkSceneManager), nameof(NetworkSceneManager.LoadScene))),
                new(OpCodes.Pop))
            .Insert( // Insert call to select a random scene immediately before the current scene begins to load, and after generating the seed for the current round.
                new(OpCodes.Ldfld, AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.randomMapSeed))),
                new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(PerformSceneSelection))),
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

            if (extendedLevel.SceneSelections.Select(scene => sceneName).Contains(sceneName)) // Check if a valid scene loaded.
                extendedLevel.SelectableLevel.sceneName = sceneName; // Update current level's scene name, so the round can end properly.
            else
                DebugHelper.LogError($"Critical Failure! Scene '{sceneName}' has no selection entry for ExtendedLevel {extendedLevel.NumberlessPlanetName}!", DebugType.User);
        }

        [HarmonyPatch(typeof(DungeonGenerator), "Generate"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void DungeonGeneratorGenerate_Prefix(DungeonGenerator __instance)
        {
            if (LevelManager.CurrentExtendedLevel != null)
                DungeonLoader.PrepareDungeon();
            LevelManager.LogDayHistory();

            if (Patches.RoundManager.dungeonGenerator.Generator.DungeonFlow == null)
                DebugHelper.LogError("Critical Failure! DungeonGenerator DungeonFlow Is Null!", DebugType.User);
        }

        //Base game has a bug where it stops listening before it gets the Complete call, so this is just a fixed version of the base game function.
        [HarmonyPatch(typeof(RoundManager), "Generator_OnGenerationStatusChanged"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool OnGenerationStatusChanged_Prefix(RoundManager __instance, GenerationStatus status)
        {
            if (status == GenerationStatus.Complete && !__instance.dungeonCompletedGenerating)
            {
                __instance.FinishGeneratingLevel();
                __instance.dungeonGenerator.Generator.OnGenerationStatusChanged -= __instance.Generator_OnGenerationStatusChanged;
                Debug.Log("Dungeon has finished generating on this client after multiple frames");
            }
            return (false);
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc)), HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GenerateNewLevelClientRpcTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions)
                .SearchForward(instructions => instructions.Calls(AccessTools.Method(typeof(RoundManager), nameof(RoundManager.GenerateNewFloor))))
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(InjectHostDungeonFlowSelection))))
                .Advance(-1)
                .SetInstruction(new CodeInstruction(OpCodes.Nop));
            return (codeMatcher.InstructionEnumeration());
        }


        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewFloor)), HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GenerateNewFloorTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions).End()
                .MatchBack(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(RuntimeDungeon), "Generate")))
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(InjectHostDungeonSizeSelection))))
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
        internal static void InjectHostDungeonFlowSelection()
        {
            if (LevelManager.CurrentExtendedLevel != null)
                DungeonLoader.SelectDungeon();
            else
                Patches.RoundManager.GenerateNewFloor();
        }

        [HarmonyPatch(typeof(RoundManager), "SetLockedDoors"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerSetLockedDoors_Prefix()
        {
            RoundManager.keyPrefab = DungeonManager.CurrentExtendedDungeonFlow.OverrideKeyPrefab != null ? DungeonManager.CurrentExtendedDungeonFlow.OverrideKeyPrefab : DungeonLoader.defaultKeyPrefab;
        }

        [HarmonyPatch(typeof(RoundManager), "SpawnOutsideHazards"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerSpawnOutsideHazards_Prefix()
        {
            RoundManager.quicksandPrefab = LevelManager.CurrentExtendedLevel.OverrideQuicksandPrefab;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void RoundManagerFinishGeneratingNewLevelClientRpc_Prefix()
        {
            if (TimeOfDay.sunAnimator == null) return;
            LevelLoader.RefreshFootstepSurfaces();
            LevelLoader.BakeSceneColliderMaterialData(TimeOfDay.sunAnimator.gameObject.scene);
            if (LevelLoader.vanillaWaterShader != null)
                LevelLoader.TryRestoreWaterShaders(TimeOfDay.sunAnimator.gameObject.scene);
            ApplyCamerDistanceOverride();
        }

        internal static void ApplyCamerDistanceOverride()
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

        static List<SpawnableMapObject> temporarySpawnableMapObjectList = new List<SpawnableMapObject>();
        [HarmonyPatch(typeof(RoundManager), "SpawnMapObjects"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void RoundManagerSpawnMapObjects_Prefix()
        {
            List<SpawnableMapObject> spawnableMapObjects = new List<SpawnableMapObject>(LevelManager.CurrentExtendedLevel.SelectableLevel.spawnableMapObjects);
            foreach (SpawnableMapObject newRandomMapObject in DungeonManager.CurrentExtendedDungeonFlow.SpawnableMapObjects)
            {
                spawnableMapObjects.Add(newRandomMapObject);
                temporarySpawnableMapObjectList.Add(newRandomMapObject);
            }
            LevelManager.CurrentExtendedLevel.SelectableLevel.spawnableMapObjects = spawnableMapObjects.ToArray();
        }

        [HarmonyPatch(typeof(RoundManager), "SpawnMapObjects"), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void RoundManagerSpawnMapObjects_Postfix()
        {
            List<SpawnableMapObject> spawnableMapObjects = new List<SpawnableMapObject>(LevelManager.CurrentExtendedLevel.SelectableLevel.spawnableMapObjects);
            foreach (SpawnableMapObject spawnableMapObject in temporarySpawnableMapObjectList)
                spawnableMapObjects.Remove(spawnableMapObject);
            LevelManager.CurrentExtendedLevel.SelectableLevel.spawnableMapObjects = spawnableMapObjects.ToArray();
            temporarySpawnableMapObjectList.Clear();
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

        static FootstepSurface previousFootstepSurface;

        [HarmonyPatch(typeof(PlayerControllerB), "GetCurrentMaterialStandingOn"), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void PlayerControllerBGetCurrentMaterialStandingOn_Postfix(PlayerControllerB __instance)
        {
            if (LevelLoader.TryGetFootstepSurface(__instance.hit.collider, out FootstepSurface footstepSurface))
                __instance.currentFootstepSurfaceIndex = StartOfRound.footstepSurfaces.IndexOf(footstepSurface);
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
            CodeMatch[] matches = [new(OpCodes.Call, AccessTools.Method(typeof(GameNetworkManager), "get_Instance")),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(GameNetworkManager), nameof(GameNetworkManager.gameHasStarted)))];

            return new CodeMatcher(instructions, generator).MatchForward(false, matches)
            .Advance(3)
            .InsertAndAdvance( // Set lever as uninteractable for clients while the game hasn't started yet.
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(StartMatchLever), nameof(StartMatchLever.triggerScript))),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Stfld, AccessTools.Field(typeof(InteractTrigger), nameof(InteractTrigger.interactable))))
            .MatchForward(false, matches)
            .CreateLabel(out Label readyTarget)
            .Insert(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(StartMatchLever), nameof(StartMatchLever.triggerScript))),
                new(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(CheckLever))),
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

        //DunGen Optimization Patches (Credit To LadyRaphtalia, Author Of Scarlet Devil Mansion)
        [HarmonyPatch(typeof(DoorwayPairFinder), "GetDoorwayPairs"), HarmonyPrefix, HarmonyPriority(priority)]
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
        }

        //IL Hook stuff to replace Mold related save data references to a Level's ID to instead the Level's Name. Credit to Hamunii.
        private static readonly HookHelper.DisposableHookCollection monomodHooks = new();
        internal static void InitMonoModHooks()
        {
            monomodHooks.ILHook<GameNetworkManager>(nameof(GameNetworkManager.SaveGameValues), ReplaceSavedMoldLevelIDsWithLevelNames_ILHook);
            monomodHooks.ILHook<GameNetworkManager>(nameof(GameNetworkManager.ResetSavedGameValues), ReplaceSavedMoldLevelIDsWithLevelNames_ILHook);
            monomodHooks.ILHook<StartOfRound>(nameof(StartOfRound.LoadPlanetsMoldSpreadData), ReplaceSavedMoldLevelIDsWithLevelNames_ILHook);
            monomodHooks.ILHook<MoldSpreadManager>(nameof(MoldSpreadManager.Start), ReplaceSavedMoldLevelIDsWithLevelNames_ILHook);
        }

        private static void ReplaceSavedMoldLevelIDsWithLevelNames_ILHook(ILContext il)
        {
            int dbgModificationsAmount = 0;
            string dbgMatchedStr = "";

            ILCursor c = new(il);
            while (
                c.TryGotoNext(MoveType.After,
                    x => x.MatchLdstr(out dbgMatchedStr) && dbgMatchedStr.Contains("Mold"), // The save file key, e.g. "Level{0}Mold"
                    x => true   // game has various ways of referencing StartOfRound, listed here purely for reference:
                        || x.MatchLdloc(out _)                                                  // via local variable
                        || x.MatchLdarg(0)                                                      // via 'this'
                        || x.MatchCall<StartOfRound>("get_" + nameof(StartOfRound.Instance)),   // via StartOfRound.Instance
                    x => x.MatchLdfld<StartOfRound>(nameof(StartOfRound.levels)),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdelemRef(),
                    x => x.MatchLdfld<SelectableLevel>(nameof(SelectableLevel.levelID)),
                    x => x.MatchBox<Int32>()
                )
            )
            {
                c.Index -= 2;
                c.RemoveRange(2);
                c.EmitDelegate<Func<SelectableLevel, object>>(selectableLevel =>
                    { return selectableLevel.name; }
                );
                dbgModificationsAmount++;
            }
            DebugHelper.Log($"Modified {dbgModificationsAmount} save data level IDs to level names", DebugType.Developer);
        }
    }
}