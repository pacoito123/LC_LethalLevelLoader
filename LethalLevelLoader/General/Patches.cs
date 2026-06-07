using DunGen;
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LethalLevelLoader
{
    internal static class Patches
    {
        internal const int priority = 200;

        internal static string delayedSceneLoadingName = string.Empty;

        internal static List<string> allSceneNamesCalledToLoad = new List<string>();

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
        [HarmonyPatch(typeof(PreInitSceneScript), nameof(PreInitSceneScript.Awake))]
        [HarmonyPrefix]
        internal static void PreInitSceneScriptAwake_Prefix(PreInitSceneScript __instance)
        {
            if (Plugin.IsSetupComplete == false && __instance.TryGetComponent(out AudioSource audioSource))
                OriginalContent.AudioMixers.Add(audioSource.outputAudioMixerGroup.audioMixer);
        }

        [HarmonyPriority(priority)]
        [HarmonyPatch(typeof(SceneManager), nameof(SceneManager.LoadScene), [typeof(string)])]
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

        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void GameNetworkManagerStart_Prefix(GameNetworkManager __instance)
        {
            if (LethalBundleManager.HasFinalisedFoundContent == false)
                LethalBundleManager.FinialiseFoundContent();
            if (Plugin.IsSetupComplete == false)
            {
                NetworkManager networkManager = __instance.GetComponent<NetworkManager>();
                LethalLevelLoaderNetworkManager.networkManager = networkManager;
                NetworkBundleManager.networkManager = networkManager;
                foreach (NetworkPrefab networkPrefab in NetworkBundleManager.networkManager.NetworkConfig.Prefabs.m_Prefabs)
                    if (networkPrefab.Prefab.TryGetComponent(out AudioSource audioSource))
                    {
                        OriginalContent.AudioMixers.Add(audioSource.outputAudioMixerGroup.audioMixer);
                        break;
                    }

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

                AssetBundleLoader.NetworkRegisterCustomContent(networkManager);
                LethalLevelLoaderNetworkManager.RegisterPrefabs(networkManager);
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveGameValues)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void GameNetworkManagerSaveGameValues_Postfix(GameNetworkManager __instance)
        {
            // Vanilla checks
            if (!__instance.isHostingGame || !StartOfRound.inShipPhase || StartOfRound.isChallengeFile)
                return;
            SaveManager.SaveGameValues();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void StartOfRoundAwake_Prefix(StartOfRound __instance)
        {
            Plugin.OnBeforeSetupInvoke();
            //Reference Setup
            StartOfRound = __instance;
            RoundManager = UnityEngine.Object.FindAnyObjectByType<RoundManager>(FindObjectsInactive.Exclude);
            Terminal = UnityEngine.Object.FindAnyObjectByType<Terminal>(FindObjectsInactive.Exclude);
            TimeOfDay = UnityEngine.Object.FindAnyObjectByType<TimeOfDay>(FindObjectsInactive.Exclude);

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
                ContentExtractor.ObtainSpecialContentReferences();

                OnAfterVanillaContentCollected.Invoke();
            }

            //Startup LethalLevelLoader's Network Manager Instance
            if (LethalLevelLoaderNetworkManager.networkManager.IsServer || LethalLevelLoaderNetworkManager.networkManager.IsHost)
            {
                UnityEngine.Object.Instantiate(LethalLevelLoaderNetworkManager.networkingManagerPrefab).GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
                UnityEngine.Object.Instantiate(NetworkBundleManager.networkingManagerPrefab).GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
            }

            DebugStopwatch.StartStopWatch("Fix AudioSource Settings");
            //Disable Spatialization In All AudioSources To Fix Log Spam Bug.
            foreach (AudioSource audioSource in Resources.FindObjectsOfTypeAll<AudioSource>())
                audioSource.spatialize = false;

            if (Plugin.IsSetupComplete == false)
            {
                //Terminal Specific Reference Setup
                TerminalManager.CacheTerminalReferences();

                DebugStopwatch.StartStopWatch("Scrape Vanilla Level Assets");

                //Vanilla Level Asset Reference Setup
                LevelManager.ObtainShipAnimatorClips(StartOfRound);
                LevelManager.ObtainTimeOfDayClips(TimeOfDay);
                LevelManager.ObtainGrassShaderReference();
                DungeonLoader.defaultKeyPrefab = RoundManager.keyPrefab;

                DebugStopwatch.StartStopWatch("Create Vanilla ExtendedContent");

                //Create & Initialize ExtendedContent Objects For Vanilla Content.
                AssetBundleLoader.CreateVanillaExtendedDungeonFlows();
                AssetBundleLoader.CreateVanillaExtendedLevels(StartOfRound);
                AssetBundleLoader.CreateVanillaExtendedItems();
                AssetBundleLoader.CreateVanillaExtendedEnemyTypes();
                AssetBundleLoader.CreateVanillaExtendedBuyableVehicles();
                AssetBundleLoader.CreateVanillaExtendedUnlockableItems();
                AssetBundleLoader.CreateVanillaExtendedFootstepSurfaces();

                DebugStopwatch.StartStopWatch("Initialize Custom ExtendedContent");

                //Initialize ExtendedContent Objects For Custom Content.
                AssetBundleLoader.InitializeBundles();

                if (DawnLibCompatibility.Enabled)
                    DawnLibCompatibility.RegisterDawnExtendedLevels(); // Create ExtendedLevel for DawnLib moons.

                PatchedContent.PopulateContentDictionaries();

                string debugString = "LethalLevelLoader Loaded The Following ExtendedLevels:" + '\n';
                for (int i = 0; i < PatchedContent.ExtendedLevels.Count; i++)
                {
                    ExtendedLevel extendedLevel = PatchedContent.ExtendedLevels[i];
                    if (extendedLevel != null && extendedLevel.SelectableLevel != null)
                    {
                        extendedLevel.SetLevelID(i);
                        debugString += $"{i + 1}. {extendedLevel.SelectableLevel.PlanetName} ({extendedLevel.ContentType})" + '\n';
                    }
                }

                debugString += "LethalLevelLoader Loaded The Following ExtendedDungeonFlows:" + '\n';
                for (int i = 0; i < PatchedContent.ExtendedDungeonFlows.Count; i++)
                {
                    ExtendedDungeonFlow extendedDungeonFlow = PatchedContent.ExtendedDungeonFlows[i];
                    if (extendedDungeonFlow != null && extendedDungeonFlow.DungeonFlow != null)
                        debugString += $"{i + 1}. {extendedDungeonFlow.DungeonName} ({extendedDungeonFlow.DungeonFlow.name}) ({extendedDungeonFlow.ContentType})" + '\n';
                }
                DebugHelper.Log(debugString, DebugType.User);

                //Restore Custom Content References To Vanilla Content
                DebugStopwatch.StartStopWatch("Restore Level Content");
                foreach (ExtendedLevel customLevel in PatchedContent.CustomExtendedLevels)
                    ContentRestorer.RestoreVanillaLevelAssetReferences(customLevel);

                DebugStopwatch.StartStopWatch("Restore Dungeon Content");
                foreach (ExtendedDungeonFlow customDungeonFlow in PatchedContent.CustomExtendedDungeonFlows)
                    ContentRestorer.RestoreVanillaDungeonAssetReferences(customDungeonFlow);

                DebugStopwatch.StartStopWatch("Restore Additional Content");
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
                // ItemManager.GetExtendedItemPriceData();
                // ItemManager.GetExtendedItemWeightData();
            }

            DebugStopwatch.StartStopWatch("Bind Configs");
            //Bind User Configuration Information.
            ConfigLoader.BindConfigs();

            DebugStopwatch.StartStopWatch("ExtendedLevel Injection");

            //Patch The Base game References To SelectableLevel's To Include Enabled Custom SelectableLevels.
            LevelManager.PatchVanillaLevelLists();

            DebugStopwatch.StartStopWatch("ExtendedDungeonFlow Injection");

            //Patch The Base game References To DungeonFlows's To Include Enabled Custom DungeonFlows.
            DungeonManager.PatchVanillaDungeonLists();

            DebugStopwatch.StartStopWatch("ExtendedItem Injection");

            //Patch The Base game References To Buyable Item's To Include Enabled Custom Buyable Items.
            ItemManager.PatchVanillaBuyableItemsLists();

            //Dynamically Inject Custom Item's Into SelectableLevel's Based On Level & Dungeon MatchingProperties.
            ItemManager.RefreshDynamicItemRarityOnAllExtendedLevels();

            DebugStopwatch.StartStopWatch(newStopWatchText: "ExtendedEnemyType Injection");

            if (Plugin.IsSetupComplete == false)
                EnemyManager.UpdateEnemyIDs();

            foreach (ExtendedEnemyType extendedEnemyType in PatchedContent.CustomExtendedEnemyTypes)
                TerminalManager.CreateEnemyTypeTerminalData(extendedEnemyType);

            if (Plugin.IsSetupComplete == false)
            {
                EnemyManager.AddCustomEnemyTypesToTestAllEnemiesLevel();
                EnemyManager.PopulateEnemySizeLists();
            }

            //Dynamically Inject Custom EnemyType's Into SelectableLevel's Based On Level & Dungeon MatchingProperties.
            EnemyManager.RefreshDynamicEnemyTypeRarityOnAllExtendedLevels();

            DebugStopwatch.StartStopWatch("ExtendedBuyableVehicle Injection");

            if (Plugin.IsSetupComplete == false)
                VehiclesManager.SetBuyableVehicleIDs();

            VehiclesManager.PatchVanillaVehiclesLists();

            if (Plugin.IsSetupComplete == false)
            {
                VehiclesManager.SetBuyableVehicleIDs();

                foreach (ExtendedBuyableVehicle customExtendedBuyableVehicle in PatchedContent.CustomExtendedBuyableVehicles)
                    TerminalManager.CreateBuyableVehicleTerminalData(customExtendedBuyableVehicle);
            }

            DebugStopwatch.StartStopWatch("ExtendedUnlockableItem Injection");

            if (Plugin.IsSetupComplete == false)
            {
                UnlockableItemManager.PatchVanillaUnlockableItemLists();
                UnlockableItemManager.SetUnlockableItemIDs();

                foreach (ExtendedUnlockableItem customExtendedUnlockableItem in PatchedContent.CustomExtendedUnlockableItems)
                    TerminalManager.CreateUnlockableItemTerminalData(customExtendedUnlockableItem);
            }

            DebugStopwatch.StartStopWatch("ExtendedFootstepSurface Injection");

            FootstepSurfaceManager.PatchVanillaFootstepSurfaceLists();

            DebugStopwatch.StartStopWatch("ExtendedStoryLog Injection");

            //Create Terminal Data For Custom StoryLog's And Patch Base game References To StoryLog's To Include Custom StoryLogs.
            TerminalManager.CreateTerminalDataForAllExtendedStoryLogs();

            DebugStopwatch.StartStopWatch("Create ExtendedLevelGroups & Filter Assets");

            //Populate SelectableLevel Data To Be Used In Overhaul Of The Terminal Moons Catalogue.
            TerminalManager.CreateExtendedLevelGroups();

            if (Plugin.IsSetupComplete == false)
            {
                //Populate SelectableLevel Data To Be Used In Overhaul Of The Terminal Moons Catalogue.
                TerminalManager.CreateMoonsFilterTerminalAssets();

                foreach (CompatibleNoun routeNode in TerminalManager.routeKeyword.compatibleNouns)
                    TerminalManager.AddTerminalNodeEventListener(routeNode.result, TerminalManager.OnBeforeRouteNodeLoaded, TerminalManager.LoadNodeActionType.Before);

                TerminalManager.AddTerminalNodeEventListener(TerminalManager.moonsKeyword.specialKeywordResult, TerminalManager.RefreshMoonsCataloguePage, TerminalManager.LoadNodeActionType.After);
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

        private static bool hasInitiallyChangedLevel;
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChangeLevel)), HarmonyPrefix, HarmonyPriority(priority)]
        public static void StartOfRoundChangeLevel_Prefix(ref int levelID)
        {
            if (IsServer == false) return;

            //Because Level ID's can change between modpack adjustments and such, we save the name of the level instead and find and load that up instead of the saved ID the base game uses.
            if (hasInitiallyChangedLevel == false && !string.IsNullOrEmpty(SaveManager.currentSaveFile.CurrentLevelName))
                foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
                    if (extendedLevel.SelectableLevel.name == SaveManager.currentSaveFile.CurrentLevelName)
                    {
                        DebugHelper.Log("Loading Previously Saved SelectableLevel: " + extendedLevel.SelectableLevel.PlanetName, DebugType.User);
                        levelID = Array.FindIndex(StartOfRound.levels, level => level == extendedLevel.SelectableLevel);
                        hasInitiallyChangedLevel = true;
                        return;
                    }

            //If we can't find the previous current level, that probably means the game is going to try and use an ID bigger than the current array, or reference the wrong level, so we reset it back to experimentation here.
            if (hasInitiallyChangedLevel == false && !string.IsNullOrEmpty(SaveManager.currentSaveFile.CurrentLevelName) && !SaveManager.currentSaveFile.CurrentLevelName.Contains("Experimentation") && (levelID >= StartOfRound.levels.Length || levelID > OriginalContent.SelectableLevels.Count))
                levelID = 0;

            hasInitiallyChangedLevel = true;
        }


        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChangeLevel)), HarmonyPostfix, HarmonyPriority(priority)]
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

        [HarmonyPatch(typeof(Terminal), "RunTerminalEvents"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool TerminalRunTerminalEvents_Prefix(Terminal __instance, TerminalNode node)
        {
            return (TerminalManager.OnBeforeLoadNewNode(ref node));
        }

        [HarmonyPatch(typeof(Terminal), "LoadNewNode"), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool TerminalLoadNewNode_Prefix(Terminal __instance, ref TerminalNode node)
        {
            TerminalManager.moonsInCataloguePage = 0;
            TerminalManager.linesInCataloguePage = 0;
            return (TerminalManager.OnBeforeLoadNewNode(ref node));
        }

        [HarmonyPatch(typeof(Terminal), "LoadNewNode"), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void TerminalLoadNewNode_Postfix(Terminal __instance, ref TerminalNode node)
        {
            TerminalManager.OnLoadNewNode(ref node);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ScrollMouse_performed)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> TerminalScrollMouse_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            FieldInfo terminalScrollVerticalInfo = typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.terminalScrollVertical), BindingFlags.Instance | BindingFlags.Public);
            MethodInfo scrollbarValueGetter = typeof(Scrollbar).GetProperty(nameof(Scrollbar.value), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo scrollbarValueSetter = typeof(Scrollbar).GetProperty(nameof(Scrollbar.value), BindingFlags.Instance | BindingFlags.Public).GetSetMethod();
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, terminalScrollVerticalInfo),
                new(OpCodes.Dup),
                new(OpCodes.Callvirt, scrollbarValueGetter),
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldc_R4, (float)3),
                new(OpCodes.Div),
                new(OpCodes.Add),
                new(OpCodes.Callvirt, scrollbarValueSetter));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match 1/3 Terminal scroll amount.", DebugType.User);
                return instructions;
            }

            MethodInfo tryAdaptTerminalScrollingInfo = typeof(Patches).GetMethod(nameof(TryAdaptTerminalScrolling), BindingFlags.Static | BindingFlags.NonPublic);
            return codeMatcher.InsertAndAdvance(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, terminalScrollVerticalInfo),
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, tryAdaptTerminalScrollingInfo),
                new(OpCodes.Brfalse_S), // vanillaScrolling
                new(OpCodes.Ret))
            .CreateLabel(out Label vanillaScrolling)
            .Advance(-2)
            .SetOperandAndAdvance(vanillaScrolling)
            .InstructionEnumeration();
        }

        private static bool TryAdaptTerminalScrolling(Scrollbar scrollbar, float scrollDirection)
        {
            if (TerminalManager.moonsInCataloguePage == 0) return false;
            if (TerminalManager.linesInCataloguePage == 0)
                TerminalManager.linesInCataloguePage = Terminal.currentText.Split('\n', StringSplitOptions.None).Length;

            scrollbar.value += scrollDirection * (TerminalManager.linesToScroll / TerminalManager.linesInCataloguePage);
            return true;
        }

        [HarmonyPatch(typeof(SceneManager), nameof(SceneManager.Internal_SceneLoaded)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void OnSceneLoaded(ref Scene scene, LoadSceneMode mode)
        {
            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel == null || currentLevel.IsLevelLoaded == false) return;
            LevelLoader.currentLevelScene = scene;

            if (currentLevel.ContentType is not ContentType.External)
            {
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                    ContentRestorer.RestoreAudioAssetReferencesInParent(rootObject);
                LevelLoader.RestoreSceneBlankReferences();

                LevelLoader.RefreshWeatherEffects(currentLevel);
                LevelLoader.RefreshTimeOfDayMusic(currentLevel);
                if (currentLevel.UseTerrainFootsteps)
                {
                    TerrainManager.BakeTerrainFootsteps();
                    SceneManager.sceneUnloaded += TerrainManager.CleanupTerrainFootsteps;
                }
                LevelLoader.ApplyCameraDistanceOverride(player: GameNetworkManager.Instance.localPlayerController, outside: true);
            }

            EventPatches.previousDayMode = DayMode.None;
            currentLevel.LevelEvents.onLevelLoaded.Invoke();
            LevelManager.GlobalLevelEvents.onLevelLoaded.Invoke();
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void GenerateNewLevelClientRpc_Prefix(RoundManager __instance, int randomSeed)
        {
            // Don't run on the server.
            if (__instance.__rpc_exec_stage is not NetworkBehaviour.__RpcExecStage.Execute) return;

            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel != null && currentLevel.IsLevelLoaded && currentLevel.ContentType is not ContentType.External)
            {
                LevelLoader.RefreshShipAnimatorClips(currentLevel, randomSeed);
                if (currentLevel.SelectableLevel != null && currentLevel.SelectableLevel.spawnEnemiesAndScrap)
                    LevelLoader.RestoreRuntimeDungeon();
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

            int[] sceneSelections = new int[extendedLevel.SceneSelections.Count];
            for (int i = 0; i < sceneSelections.Length; i++)
                sceneSelections[i] = extendedLevel.SceneSelections[i]?.Rarity ?? -1;
            int selectedSceneIndex = RoundManager.GetRandomWeightedIndex(sceneSelections, levelRandom);
            extendedLevel.SelectableLevel.sceneName = extendedLevel.SceneSelections[selectedSceneIndex].Name;
            DebugHelper.Log("Selected SceneName: " + extendedLevel.SelectableLevel.sceneName + " For ExtendedLevel: " + extendedLevel.NumberlessPlanetName, DebugType.Developer);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SceneManager_OnLoadComplete1)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void StartOfRoundOnLoadComplete_Prefix(string sceneName)
        {
            ExtendedLevel extendedLevel = LevelManager.CurrentExtendedLevel;
            if (extendedLevel == null || string.Equals(extendedLevel.SelectableLevel.sceneName, sceneName, StringComparison.Ordinal)) return;

            int sceneSelectionIndex = extendedLevel.SceneSelections.FindIndex(scene => string.Equals(scene.Name, sceneName, StringComparison.Ordinal));
            if (sceneSelectionIndex != -1) // Check if a valid scene loaded.
                extendedLevel.SelectableLevel.sceneName = sceneName; // Update current level's scene name, so the round can end properly.
            else if (!string.Equals(sceneName, "SampleSceneRelay", StringComparison.Ordinal))
                DebugHelper.LogFatal($"Critical Failure! Scene '{sceneName}' has no selection entry for ExtendedLevel {extendedLevel.NumberlessPlanetName}!", DebugType.User);
        }

        [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.Generate)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void DungeonGeneratorGenerate_Prefix()
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
            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel != null && currentLevel.IsLevelLoaded && currentLevel.SelectableLevel != null && currentLevel.SelectableLevel.spawnEnemiesAndScrap)
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
        internal static void RoundManagerFinishGeneratingNewLevelClientRpc_Postfix(bool __state)
        {
            // Don't run on the server.
            if (__state) return;

            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel != null && currentLevel.IsLevelLoaded && currentLevel.ContentType is not ContentType.External)
                LevelLoader.RestoreShaders();
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
            MethodInfo inequalityInfo = typeof(UnityEngine.Object).GetMethod("op_Inequality", BindingFlags.Static | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions).MatchForward(useEnd: false,
                new(OpCodes.Callvirt, terrainGetComponentInfo),
                new(OpCodes.Ldnull),
                new(OpCodes.Call, inequalityInfo)); // Match GetComponent<Terrain>() null comparison.

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match Terrain null comparison.", DebugType.User);
                return instructions;
            }

            Type genericType = Type.MakeGenericMethodParameter(0).MakeByRefType();
            MethodInfo terrainTryGetComponentInfo = typeof(Component).GetMethod(nameof(Component.TryGetComponent), 1, [genericType]).MakeGenericMethod(typeof(Terrain));
            MethodInfo activeTerrainGetter = typeof(Terrain).GetProperty(nameof(Terrain.activeTerrain), BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            codeMatcher.RemoveInstructions(3) // Remove GetComponent<Terrain>() null comparison.
            .InsertAndAdvance(
                new(OpCodes.Ldloca_S, (sbyte)0),
                new(OpCodes.Callvirt, terrainTryGetComponentInfo)) // Insert call to TryGetComponent<Terrain>() and set local variable to obtained value.
            .MatchForward(useEnd: true,
                new(OpCodes.Call, activeTerrainGetter), // Match Terrain.activeTerrain local variable assignment.
                new CodeMatch(OpCodes.Stloc_0));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match active Terrain local variable assignment.", DebugType.User);
                return instructions;
            }

            MethodInfo terrainDataGetter = typeof(Terrain).GetProperty(nameof(Terrain.terrainData), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            codeMatcher.SetOpcodeAndAdvance(OpCodes.Pop) // Not removing Terrain.activeTerrain call before this in case any other Transpiler expects it to still be there.
            .MatchForward(useEnd: true,
                new(OpCodes.Ldloc_0),
                new(OpCodes.Callvirt, terrainDataGetter), // Match TerrainData local variable assignment.
                new(OpCodes.Stloc_1));

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match TerrainData local variable assignment.", DebugType.User);
                return instructions;
            }

            MethodInfo swapTerrainAlphaMapInfo = typeof(TerrainManager).GetMethod(nameof(TerrainManager.SwapTerrainAlphaMap), BindingFlags.Static | BindingFlags.NonPublic);
            return codeMatcher.Insert(
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, swapTerrainAlphaMapInfo)) // Insert call to 'TerrainManager.SwapTerrainAlphaMap()' before alphamaps are obtained.
            .InstructionEnumeration();
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

            FieldInfo currentFootstepSurfaceIndexInfo = typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.currentFootstepSurfaceIndex), BindingFlags.Instance | BindingFlags.Public);
            MethodInfo tryGetAndSetFootstepSurfaceIndexInfo = typeof(FootstepSurfaceManager).GetMethod(nameof(FootstepSurfaceManager.TryGetAndSetFootstepSurfaceIndex), [typeof(Terrain), typeof(int), typeof(PlayerControllerB)]);
            codeMatcher.CreateLabel(out Label vanillaFootstepsTarget)
            .InsertAndAdvance(
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldloc_3),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, tryGetAndSetFootstepSurfaceIndexInfo), // Insert call to 'FootstepSurfaceManager.TryGetFootstepSurfaceIndex()' and jump to vanilla footstep target if false.
                new(OpCodes.Brfalse, vanillaFootstepsTarget),
                new(OpCodes.Ret))
            .MatchForward(useEnd: true,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, currentFootstepSurfaceIndexInfo),
                new(OpCodes.Ldc_I4_S, (sbyte)12), // Match immediately after Gunkfish slime footstep check.
                new(OpCodes.Beq));

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match Gunkfish slime footstep check when replacing footsteps.", DebugType.User);
                return instructions;
            }

            MethodInfo switchToUntaggedIndexInfo = typeof(FootstepSurfaceManager).GetMethod(nameof(FootstepSurfaceManager.SwitchToUntaggedIndex), BindingFlags.Static | BindingFlags.NonPublic);
            return codeMatcher.InsertAndAdvance(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldflda, currentFootstepSurfaceIndexInfo),
                new(OpCodes.Call, switchToUntaggedIndexInfo)) // Insert call to 'FootstepSurfaceManager.SwitchToUntaggedIndex()'.
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
                DebugHelper.LogError("Could not match Gunkfish slime footstep check when restoring footsteps.", DebugType.User);
                return instructions;
            }

            codeMatcher.CreateLabel(out Label terrainFootstepOverrideTarget)
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

        [HarmonyPatch(typeof(SandWormAI), nameof(SandWormAI.StartEmergeAnimation)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> SandWormAIStartEmergeAnimation_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            MethodInfo raycastHitColliderGetter = typeof(RaycastHit).GetProperty(nameof(RaycastHit.collider), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo gameObjectGetter = typeof(Component).GetProperty(nameof(Component.gameObject), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo activeTerrainGetter = typeof(Terrain).GetProperty(nameof(Terrain.activeTerrain), BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            MethodInfo equalityInfo = typeof(UnityEngine.Object).GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).MatchForward(useEnd: false,
                new(OpCodes.Call, raycastHitColliderGetter),
                new(OpCodes.Callvirt, gameObjectGetter),
                new(OpCodes.Call, activeTerrainGetter), // Match Terrain.activeTerrain GameObject comparison.
                new(OpCodes.Callvirt, gameObjectGetter),
                new(OpCodes.Call, equalityInfo));

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match active Terrain equality check.", DebugType.User);
                return instructions;
            }
            CodeInstruction raycastHitLocalInstruction = codeMatcher.InstructionAt(-2); // Obtain instruction for getting RaycastHit local variable.
            LocalBuilder terrainLocal = generator.DeclareLocal(typeof(Terrain)); // Create local variable for Terrain obtained from the Raycast.

            Type genericType = Type.MakeGenericMethodParameter(0).MakeByRefType();
            MethodInfo terrainTryGetComponentInfo = typeof(Component).GetMethod(nameof(Component.TryGetComponent), 1, [genericType]).MakeGenericMethod(typeof(Terrain));
            codeMatcher.RemoveInstructions(4) // Remove Terrain.activeTerrain GameObject comparison instructions.
            .InsertAndAdvance(
                new(OpCodes.Ldloca_S, terrainLocal),
                new(OpCodes.Callvirt, terrainTryGetComponentInfo)) // Insert call to TryGetComponent<Terrain>() and set local variable to obtained value.
            .MatchForward(useEnd: false,
                new(OpCodes.Ldc_I4_1), // Match local variable being set to true.
                new(OpCodes.Stloc_2));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match local variable true assignment.", DebugType.User);
                return instructions;
            }

            MethodInfo raycastHitPointGetter = typeof(RaycastHit).GetProperty(nameof(RaycastHit.point), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo canWormEmergeFromPointInfo = typeof(TerrainManager).GetMethod(nameof(TerrainManager.CanWormEmergeFromPoint), BindingFlags.Static | BindingFlags.Public);
            return codeMatcher.SetInstructionAndAdvance(raycastHitLocalInstruction)
            .Insert(
                new(OpCodes.Call, raycastHitPointGetter),
                new(OpCodes.Ldloc_S, terrainLocal),
                new(OpCodes.Call, canWormEmergeFromPointInfo)) // Insert call to 'TerrainManager.CanWormEmergeFromPoint()'.
            .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.GetMaterialStandingOn)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> MaskedPlayerEnemyGetMaterialStandingOn_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            FieldInfo enemyRayHitInfo = typeof(MaskedPlayerEnemy).GetField(nameof(MaskedPlayerEnemy.enemyRayHit), BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo raycastHitColliderGetter = typeof(RaycastHit).GetProperty(nameof(RaycastHit.collider), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo startOfRoundGetter = typeof(StartOfRound).GetProperty(nameof(StartOfRound.Instance), BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            FieldInfo footstepSurfacesInfo = typeof(StartOfRound).GetField(nameof(StartOfRound.footstepSurfaces), BindingFlags.Instance | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldflda, enemyRayHitInfo),
                new(OpCodes.Call, raycastHitColliderGetter), // Match 'enemyRayHit' collider getter.
                new(OpCodes.Call, startOfRoundGetter),
                new(OpCodes.Ldfld, footstepSurfacesInfo));

            if (codeMatcher.Advance(3).IsInvalid)
            {
                DebugHelper.LogError("Could not match RaycastHit collider getter.", DebugType.User);
                return instructions;
            }
            LocalBuilder terrainLocal = generator.DeclareLocal(typeof(Terrain)); // Create local variable for the Terrain obtained from the Raycast.
            LocalBuilder terrainLayerLocal = generator.DeclareLocal(typeof(int)); // Create local variable for storing the Terrain layer.

            Type genericType = Type.MakeGenericMethodParameter(0).MakeByRefType();
            MethodInfo terrainTryGetComponentInfo = typeof(Component).GetMethod(nameof(Component.TryGetComponent), 1, [genericType]).MakeGenericMethod(typeof(Terrain));
            MethodInfo raycastHitPointGetter = typeof(RaycastHit).GetProperty(nameof(RaycastHit.point), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo tryObtainTerrainLayerAtPointInfo = typeof(TerrainManager).GetMethod(nameof(TerrainManager.TryObtainTerrainLayerAtPoint), BindingFlags.Static | BindingFlags.Public);
            MethodInfo tryGetAndSetFootstepSurfaceIndexInfo = typeof(FootstepSurfaceManager).GetMethod(nameof(FootstepSurfaceManager.TryGetAndSetFootstepSurfaceIndex), [typeof(Terrain), typeof(int), typeof(MaskedPlayerEnemy)]);
            return codeMatcher.Insert(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldflda, enemyRayHitInfo),
                new(OpCodes.Call, raycastHitColliderGetter))
            .CreateLabel(out Label vanillaFootstepTarget)
            .InsertAndAdvance(
                new(OpCodes.Ldloca_S, terrainLocal),
                new(OpCodes.Callvirt, terrainTryGetComponentInfo), // Insert TryGetComponent<Terrain>() call.
                new(OpCodes.Brfalse_S, vanillaFootstepTarget), // Return to vanilla behaviour if no Terrain is obtained.
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldflda, enemyRayHitInfo),
                new(OpCodes.Call, raycastHitPointGetter),
                new(OpCodes.Ldloc_S, terrainLocal),
                new(OpCodes.Ldloca_S, terrainLayerLocal),
                new(OpCodes.Call, tryObtainTerrainLayerAtPointInfo), // Insert TryObtainTerrainLayerAtPoint() call.
                new(OpCodes.Brfalse_S, vanillaFootstepTarget), // Return to vanilla behaviour if layer could not be obtained.
                new(OpCodes.Ldloc_S, terrainLocal),
                new(OpCodes.Ldloc_S, terrainLayerLocal),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, tryGetAndSetFootstepSurfaceIndexInfo), // Insert TryGetAndSetFootstepSurfaceIndex() call.
                new(OpCodes.Brfalse_S, vanillaFootstepTarget), // Return to vanilla behaviour if footstep surface could not be obtained.
                new(OpCodes.Ret))
            .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.PlayCreakSFX)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> EntranceTeleportPlayCreakSFX_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo startOfRoundGetter = typeof(StartOfRound).GetProperty(nameof(StartOfRound.Instance), BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            FieldInfo creakOpenDoorMetalInfo = typeof(StartOfRound).GetField(nameof(StartOfRound.creakOpenDoorMetal), BindingFlags.Instance | BindingFlags.Public);
            CodeMatch[] matches = [new(OpCodes.Call, startOfRoundGetter),
                new(OpCodes.Ldfld, creakOpenDoorMetalInfo), // Match metal open array local variable assignment.
                new(OpCodes.Stloc_0)];
            CodeMatcher codeMatcher = new CodeMatcher(instructions).MatchForward(useEnd: true, matches);

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match first creakOpenDoorMetal local variable assignment.", DebugType.User);
                return instructions;
            }

            CodeInstruction nextInstruction = codeMatcher.Instruction; // Save instruction immediately after match.
            MethodInfo swapOpenDoorSFXInfo = typeof(Patches).GetMethod(nameof(SwapOpenDoorSFX), BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo isEntranceToBuildingInfo = typeof(EntranceTeleport).GetField(nameof(EntranceTeleport.isEntranceToBuilding), BindingFlags.Instance | BindingFlags.Public);
            codeMatcher.SetInstructionAndAdvance(new(OpCodes.Ldloca_S, (sbyte)0)) // Replace instruction to preserve label(s).
            .InsertAndAdvance(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, isEntranceToBuildingInfo),
                new(OpCodes.Call, swapOpenDoorSFXInfo), // Insert call to 'SwapOpenDoorSFX()'.
                nextInstruction) // Insert previously saved instruction.
            .MatchForward(useEnd: true, matches);

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match second creakOpenDoorMetal local variable assignment.", DebugType.User);
                return instructions;
            }

            nextInstruction = codeMatcher.Instruction; // Save instruction immediately after match.
            FieldInfo exitScriptInfo = typeof(EntranceTeleport).GetField(nameof(EntranceTeleport.exitScript), BindingFlags.Instance | BindingFlags.Public);
            return codeMatcher.SetInstructionAndAdvance(new(OpCodes.Ldloca_S, (sbyte)0)) // Replace instruction to preserve label(s).
            .Insert(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, exitScriptInfo),
                new(OpCodes.Ldfld, isEntranceToBuildingInfo),
                new(OpCodes.Call, swapOpenDoorSFXInfo), // Insert call to 'SwapOpenDoorSFX()'.
                nextInstruction) // Insert previously saved instruction.
            .InstructionEnumeration();
        }

        private static void SwapOpenDoorSFX(ref AudioClip[] openDoorClips, bool isEntranceToBuilding)
        {
            if (isEntranceToBuilding)
            {
                ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
                if (currentLevel == null || currentLevel.ContentType is ContentType.External) return;
                if (currentLevel.OverrideCreakOpenDoorSFX?.Length > 0)
                    openDoorClips = currentLevel.OverrideCreakOpenDoorSFX;
            }
            else
            {
                ExtendedDungeonFlow currentDungeonFlow = DungeonManager.CurrentExtendedDungeonFlow;
                if (currentDungeonFlow == null || currentDungeonFlow.ContentType is ContentType.External) return;
                if (currentDungeonFlow.OverrideCreakOpenDoorSFX?.Length > 0)
                    openDoorClips = currentDungeonFlow.OverrideCreakOpenDoorSFX;
            }
        }

        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.PlayAudioAtTeleportPositions)), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> EntranceTeleportPlayAudioAtTeleportPositions_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo startOfRoundGetter = typeof(StartOfRound).GetProperty(nameof(StartOfRound.Instance), BindingFlags.Static | BindingFlags.Public).GetGetMethod();
            FieldInfo shutDoorMetalInfo = typeof(StartOfRound).GetField(nameof(StartOfRound.shutDoorMetal), BindingFlags.Instance | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions).MatchForward(useEnd: true,
                new(OpCodes.Call, startOfRoundGetter),
                new(OpCodes.Ldfld, shutDoorMetalInfo), // Match metal shut array local variable assignment.
                new(OpCodes.Stloc_0));

            if (codeMatcher.Advance(1).IsInvalid)
            {
                DebugHelper.LogError("Could not match shutDoorMetal local variable assignment.", DebugType.User);
                return instructions;
            }

            CodeInstruction nextInstruction = codeMatcher.Instruction; // Save instruction immediately after match.
            MethodInfo swapShutDoorSFXInfo = typeof(Patches).GetMethod(nameof(SwapShutDoorSFX), BindingFlags.Static | BindingFlags.NonPublic);
            return codeMatcher.SetInstructionAndAdvance(new(OpCodes.Ldloca_S, (sbyte)0)) // Replace instruction to preserve label(s).
            .Insert(
                new(OpCodes.Ldloca_S, (sbyte)1),
                new(OpCodes.Call, swapShutDoorSFXInfo), // Insert call to 'SwapShutDoorSFX()'.
                nextInstruction) // Insert previously saved instruction.
            .InstructionEnumeration();
        }

        private static void SwapShutDoorSFX(ref AudioClip[] shutDoorClipsInside, ref AudioClip[] shutDoorClipsOutside)
        {
            ExtendedDungeonFlow currentDungeonFlow = DungeonManager.CurrentExtendedDungeonFlow;
            if (currentDungeonFlow != null && currentDungeonFlow.ContentType is not ContentType.External && currentDungeonFlow.OverrideCreakShutDoorSFX?.Length > 0)
                shutDoorClipsInside = currentDungeonFlow.OverrideCreakShutDoorSFX;

            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel != null && currentLevel.ContentType is not ContentType.External && currentLevel.OverrideCreakShutDoorSFX?.Length > 0)
                shutDoorClipsOutside = currentLevel.OverrideCreakShutDoorSFX;
        }

        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayerServerRpc)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static void EntranceTeleportTeleportPlayerServerRpc_Prefix(EntranceTeleport __instance)
        {
            if (__instance.__rpc_exec_stage is NetworkBehaviour.__RpcExecStage.Send) // Only run on the player calling the ServerRpc.
                LevelLoader.ApplyCameraDistanceOverride(GameNetworkManager.Instance.localPlayerController, outside: !__instance.isEntranceToBuilding);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.TeleportPlayer)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void PlayerControllerBTeleportPlayer_Postfix(PlayerControllerB __instance)
        {
            if (__instance.IsOwner)
                LevelLoader.ApplyCameraDistanceOverride(__instance, outside: !__instance.isInsideFactory);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ShipHasLeft))]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChangePlanet)), HarmonyPostfix, HarmonyPriority(priority)]
        internal static void StartOfRoundChangePlanet_Postfix()
        {
            if (GameNetworkManager.Instance != null)
                LevelLoader.ApplyCameraDistanceOverride(GameNetworkManager.Instance.localPlayerController, outside: true, inOrbit: true);
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

        //DunGen Optimization Patches (Credit To LadyRaphtalia, Author Of Scarlet Devil Mansion)
        [HarmonyPatch(typeof(DoorwayPairFinder), nameof(DoorwayPairFinder.GetDoorwayPairs)), HarmonyPrefix, HarmonyPriority(priority)]
        internal static bool GetDoorwayPairsPatch(DoorwayPairFinder __instance, ref int? maxCount, ref Queue<DoorwayPair> __result)
        {
            __instance.tileOrder = __instance.CalculateOrderedListOfTiles();
            IEnumerable<DoorwayPair> doorwayPairs = (__instance.PreviousTile == null) ?
              __instance.GetPotentialDoorwayPairsForFirstTile() :
              __instance.GetPotentialDoorwayPairsForNonFirstTile();

            int num = doorwayPairs.Count();
            if (maxCount != null)
                num = Mathf.Min(num, maxCount.Value);
            __result = new Queue<DoorwayPair>(num);

            foreach (DoorwayPair item in OrderDoorwayPairs(doorwayPairs, num))
                __result.Enqueue(item);

            return false;
        }

        private struct DoorwayPairComparer : IComparer<DoorwayPair>
        {
            public readonly int Compare(DoorwayPair a, DoorwayPair b)
            {
                int tileWeight = b.TileWeight.CompareTo(a.TileWeight);
                if (tileWeight == 0) return b.DoorwayWeight.CompareTo(a.DoorwayWeight);
                return tileWeight;
            }
        }

        private static IEnumerable<DoorwayPair> OrderDoorwayPairs(IEnumerable<DoorwayPair> list, int num)
        {
            return list.OrderBy(static doorwayPair => doorwayPair, new DoorwayPairComparer()).Take(num);
        }

        /* [HarmonyPatch(typeof(DungeonGenerator), MethodType.Constructor), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> DungeonGenerator_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions).MatchForward(useEnd: true,
                new(OpCodes.Ldnull),
                new(OpCodes.Ldc_I4_0))
            .SetOpcodeAndAdvance(OpCodes.Ldc_I4_8) // Could be neat to have Tiles able to specify their initial capacity for object pooling.
            .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(TileInstanceSource), MethodType.Constructor), HarmonyTranspiler, HarmonyPriority(priority)]
        internal static IEnumerable<CodeInstruction> TileInstanceSource_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions).MatchForward(useEnd: true,
                new(OpCodes.Ldnull),
                new(OpCodes.Ldnull),
                new(OpCodes.Ldc_I4_0))
            .SetOpcodeAndAdvance(OpCodes.Ldc_I4_8)
            .InstructionEnumeration();
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
                    if (saveKey.StartsWith("Level{0}", StringComparison.Ordinal))
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