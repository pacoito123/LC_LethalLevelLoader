using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ContentType { Vanilla, Custom, Any, External } //Any & All included for built in checks, External for content registered by others.

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "ExtendedLevel", menuName = "Lethal Level Loader/Extended Content/ExtendedLevel", order = 20)]
    public class ExtendedLevel : ExtendedContent
    {
        [field: Header("General Settings")]
        [field: SerializeField] public SelectableLevel SelectableLevel { get; set; }
        [Space(5)][SerializeField][Min(0)] private int routePrice = 0;

        [field: Header("Extended Feature Settings")]
        [field: SerializeField] public bool OverrideDynamicRiskLevelAssignment { get; set; } = false;
        [field: Tooltip("Enable to use Terrain layers for Player footstep sound effects instead of GameObject tags, when applicable. "
            + "Disabled by default for older moons to keep their intended (original) footstep sounds.")]
        [field: SerializeField] public bool UseTerrainFootsteps { get; set; }

        [field: Space(5)]

        [field: SerializeField] public bool IsRouteHidden { get; set; } = false;
        [field: SerializeField] public bool IsRouteLocked { get; set; } = false;
        public bool IsRouteRemoved { get; set; } = false;
        [field: SerializeField] public string LockedRouteNodeText { get; set; } = string.Empty;

        [field: Space(5)]

        [field: SerializeField] public List<ClipWithRarity> ShipFlyToMoonClips { get; set; } = new List<ClipWithRarity>();
        [field: SerializeField] public List<ClipWithRarity> ShipFlyFromMoonClips { get; set; } = new List<ClipWithRarity>();

        [field: Space(5)]

        [field: SerializeField] public List<StringWithRarity> SceneSelections { get; set; } = new List<StringWithRarity>();

        [field: Space(5)]
        [field: Tooltip("Overrides vanilla camera Far Plane Clip Distance, The highest value between current Level and Interior will be used.")]
        [field: Range(0f, 10000f)]
        [field: SerializeField] public float OverrideCameraMaxDistance = 400;

        [field: Space(5)]
        [field: Header("Weather Effect Override Settings")]

        [field: SerializeField] public Vector3 OverrideDustStormVolumeSize { get; set; } = Vector3.zero;

        [field: Space(5)]
        [field: SerializeField] public GameObject OverrideQuicksandPrefab { get; set; }
        [field: SerializeField] public GameObject OverrideRainPrefab { get; set; }
        [field: SerializeField] public AudioClip OverrideRainAmbience { get; set; }

        [field: Space(5)]
        [field: SerializeField] public ParticleSystem OverrideStormyLightningStrikeExplosion { get; set; }
        [field: SerializeField] public ParticleSystem OverrideStormyStaticElectricityParticle { get; set; }
        [field: SerializeField] public AudioClip[] OverrideStormyLightningStrikeSFX { get; set; }
        [field: SerializeField] public AudioClip[] OverrideStormyDistantThunderSFX { get; set; }
        [field: SerializeField] public AudioClip OverrideStormyStaticElectricitySFX { get; set; }

        [field: Space(5)]
        [field: SerializeField] public GameObject OverrideStormyRainPrefab { get; set; }
        [field: SerializeField] public AudioClip OverrideStormyRainAmbience { get; set; }

        [field: Space(5)]
        [field: SerializeField] public Vector3 OverrideFoggyVolumeSize { get; set; } = Vector3.zero;

        [field: Space(5)]
        [field: SerializeField] public GameObject OverrideFloodedPrefab { get; set; }
        [field: SerializeField] public AudioClip OverrideFloodedAmbience { get; set; }

        [field: Space(5)]
        [field: SerializeField] public AudioClip OverrideEclipsedMusic { get; set; }

        [field: Space(5)]
        [field: Header("Time Of Day Music Override Settings")]

        [field: SerializeField] public AudioClip OverrideStartOfDayMusic { get; set; }
        [field: SerializeField] public AudioClip OverrideMidDayMusic { get; set; }
        [field: SerializeField] public AudioClip OverrideLateDayMusic { get; set; }
        [field: SerializeField] public AudioClip OverrideNightMusic { get; set; }

        [field: Space(5)]
        [field: Header("Entrance Teleport SFX Override Settings")]
        [field: SerializeField] public AudioClip[] OverrideCreakOpenDoorSFX { get; set; }
        [field: SerializeField] public AudioClip[] OverrideCreakShutDoorSFX { get; set; }

        [field: Space(5)]
        [field: Header("Terminal Route Override Settings")]

        [field: SerializeField] public string OverrideRouteNoun { get; set; } = string.Empty;
        [field: SerializeField][field: TextArea(2, 20)] public string OverrideInfoNodeDescription { get; set; } = string.Empty;
        [field: SerializeField][field: TextArea(2, 20)] public string OverrideRouteNodeDescription { get; set; } = string.Empty;
        [field: SerializeField][field: TextArea(2, 20)] public string OverrideRouteConfirmNodeDescription { get; set; } = string.Empty;

        [field: Space(10)]
        [field: Header("Misc. Settings")]
        [field: Space(5)]
        [field: SerializeField] public bool GenerateAutomaticConfigurationOptions { get; set; } = true;

        [Space(25)]
        [Header("Obsolete (Legacy Fields, Will Be Removed In The Future)")]
        [Obsolete] public SelectableLevel selectableLevel;
        [Obsolete][Space(5)] public string contentSourceName = string.Empty; //Levels from AssetBundles will have this as their Assembly Name.
        [Obsolete][Space(5)] public List<string> levelTags = new List<string>();
        [Obsolete][field: SerializeField] public AnimationClip ShipFlyToMoonClip { get; set; }
        [Obsolete][field: SerializeField] public AnimationClip ShipFlyFromMoonClip { get; set; }

        //Runtime Stuff
        public int RoutePrice
        {
            get
            {
                if (RouteNode != null)
                {
                    routePrice = RouteNode.itemCost;
                    RouteConfirmNode.itemCost = routePrice;
                    return (RouteNode.itemCost);
                }
                else
                {
                    DebugHelper.LogWarning("routeNode Is Missing! Using internal value!", DebugType.Developer);
                    return (routePrice);
                }
            }
            set
            {
                if (RouteNode != null && RouteConfirmNode != null)
                {
                    RouteNode.itemCost = value;
                    RouteConfirmNode.itemCost = value;
                }
                else
                    DebugHelper.LogWarning("routeNode Is Missing! Only setting internal value!", DebugType.Developer);
                routePrice = value;
            }
        }

        public string TerminalNoun => string.IsNullOrEmpty(OverrideRouteNoun) ? NumberlessPlanetName.StripSpecialCharacters().RemoveWhitespace().ToLowerInvariant() : OverrideRouteNoun.StripSpecialCharacters().RemoveWhitespace().ToLowerInvariant();

        public string NumberlessPlanetName => GetNumberlessPlanetName(SelectableLevel);
        public int CalculatedDifficultyRating => LevelManager.CalculateExtendedLevelDifficultyRating(this);
        public bool IsCurrentLevel => LevelManager.CurrentExtendedLevel == this;
        public bool IsLevelLoaded => SceneManager.GetSceneByName(SelectableLevel.sceneName).isLoaded;

        [HideInInspector] public LevelEvents LevelEvents { get; internal set; } = new LevelEvents();

        public TerminalNode RouteNode { get; internal set; }
        public TerminalNode RouteConfirmNode { get; internal set; }
        public TerminalNode InfoNode { get; internal set; }
        public TerminalNode SimulateNode { get; internal set; }

        //Dunno about these yet
        public List<ExtendedWeatherEffect> EnabledExtendedWeatherEffects { get; set; } = new List<ExtendedWeatherEffect>();
        public ExtendedWeatherEffect CurrentExtendedWeatherEffect { get; set; }

        internal static ExtendedLevel Create(SelectableLevel newSelectableLevel)
        {
            ExtendedLevel newExtendedLevel = ScriptableObject.CreateInstance<ExtendedLevel>();
            newExtendedLevel.SelectableLevel = newSelectableLevel;

            return (newExtendedLevel);
        }

        internal void Initialize(string newContentSourceName, bool generateTerminalAssets)
        {
            bool mainSceneRegistered = false;

            foreach (StringWithRarity sceneSelection in SceneSelections)
                if (sceneSelection.Name == SelectableLevel.sceneName)
                    mainSceneRegistered = true;

            if (mainSceneRegistered == false)
            {
                StringWithRarity newSceneSelection = new StringWithRarity(SelectableLevel.sceneName, 300);
                SceneSelections.Add(newSceneSelection);
            }

            foreach (StringWithRarity sceneSelection in new List<StringWithRarity>(SceneSelections))
                if (!PatchedContent.AllLevelSceneNames.Contains(sceneSelection.Name))
                {
                    DebugHelper.LogWarning("Removing SceneSelection From: " + SelectableLevel.PlanetName + " As SceneName: " + sceneSelection.Name + " Is Not Loaded!", DebugType.Developer);
                    SceneSelections.Remove(sceneSelection);
                }

            if (ShipFlyToMoonClips.Count == 0)
                ShipFlyToMoonClips.Add(new ClipWithRarity(LevelLoader.defaultShipFlyToMoonClip, 300));
            if (ShipFlyFromMoonClips.Count == 0)
                ShipFlyFromMoonClips.Add(new ClipWithRarity(LevelLoader.defaultShipFlyFromMoonClip, 300));

            if (OverrideStartOfDayMusic == null)
                OverrideStartOfDayMusic = LevelLoader.defaultStartOfDayMusic;
            if (OverrideMidDayMusic == null)
                OverrideMidDayMusic = LevelLoader.defaultMidDayMusic;
            if (OverrideLateDayMusic == null)
                OverrideLateDayMusic = LevelLoader.defaultLateDayMusic;
            if (OverrideNightMusic == null)
                OverrideNightMusic = LevelLoader.defaultNightMusic;

            if (ContentType is ContentType.Custom or ContentType.External)
            {
                name = NumberlessPlanetName.StripSpecialCharacters() + "ExtendedLevel";
                SelectableLevel.name = NumberlessPlanetName.StripSpecialCharacters() + "Level";
                if (generateTerminalAssets == true) //Needs to be after levelID setting above.
                {
                    //DebugHelper.Log("Generating Terminal Assets For: " + NumberlessPlanetName);
                    TerminalManager.CreateLevelTerminalData(this, routePrice);
                }
            }

            SelectableLevel.spawnableMapObjects ??= [];
            SelectableLevel.indoorMapHazards ??= [];

            if (ContentType == ContentType.Vanilla)
                GetVanillaInfoNode();
            SetExtendedDungeonFlowMatches();
        }

        internal override (bool result, string log) TryValidateContent()
        {
            int removedScenes = SceneSelections.RemoveAll(sceneSelection => sceneSelection == null || string.IsNullOrEmpty(sceneSelection.Name));
            if (removedScenes > 0)
                DebugHelper.LogWarning($"Removed '{removedScenes}' missing or empty scene selections in ExtendedLevel: {name}", DebugType.User);

            int removedFlyToMoonClips = ShipFlyToMoonClips.RemoveAll(clipSelection => clipSelection.Clip == null || clipSelection.Rarity == 0);
            if (removedFlyToMoonClips > 0)
                DebugHelper.LogWarning($"Removed '{removedFlyToMoonClips}' missing, empty, or zero-rarity ShipFlyToMoon clip overrides in ExtendedLevel: {name}", DebugType.User);

            int removedFlyFromMoonClips = ShipFlyFromMoonClips.RemoveAll(clipSelection => clipSelection.Clip == null || clipSelection.Rarity == 0);
            if (removedFlyFromMoonClips > 0)
                DebugHelper.LogWarning($"Removed '{removedFlyFromMoonClips}' missing, empty, or zero-rarity ShipFlyFromMoon clip overrides in ExtendedLevel: {name}", DebugType.User);

            if (SelectableLevel == null)
                return ((false, "SelectableLevel Was Null"));
            else if (string.IsNullOrEmpty(SelectableLevel.sceneName))
                return ((false, "SelectableLevel SceneName Was Null Or Empty"));
            else if (SelectableLevel.planetPrefab == null)
                return ((false, "SelectableLevel PlanetPrefab Was Null"));
            else if (!SelectableLevel.planetPrefab.TryGetComponent(out Animator planetPrefabAnimator))
                return ((false, "SelectableLevel PlanetPrefab Animator Was Null"));
            else if (planetPrefabAnimator.runtimeAnimatorController == null)
                return ((false, "SelectableLevel PlanetPrefab Animator AnimatorController Was Null"));
            else
                return (base.TryValidateContent());
        }

        internal override void ConvertObsoleteValues()
        {
            if (levelTags.Count > 0 && ContentTags.Count == 0)
            {
                DebugHelper.LogWarning("ExtendedLevel.levelTags Is Obsolete and will be removed in following releases, Please use .ContentTags instead.", DebugType.Developer);
                foreach (ContentTag convertedContentTag in ContentTagManager.CreateNewContentTags(levelTags))
                    ContentTags.Add(convertedContentTag);
            }
            levelTags.Clear();

            if (SelectableLevel == null && selectableLevel != null)
            {
                DebugHelper.LogWarning("ExtendedLevel.selectableLevel Is Obsolete and will be removed in following releases, Please use .SelectableLevel instead.", DebugType.Developer);
                SelectableLevel = selectableLevel;
            }

            if (!string.IsNullOrEmpty(contentSourceName))
                DebugHelper.LogWarning("ExtendedLevel.contentSourceName is Obsolete and will be removed in following releases, Please use ExtendedMod.AuthorName instead.", DebugType.Developer);

            if (ShipFlyToMoonClip != null)
            {
                DebugHelper.LogWarning("ExtendedLevel.ShipFlyToMoonClip Is Obsolete and will be removed in following releases, Please use ExtendedLevel.ShipFlyToMoonClips instead.", DebugType.Developer);
                if (ShipFlyToMoonClips.Count == 0)
                    ShipFlyToMoonClips.Add(new(ShipFlyToMoonClip, 300));
            }

            if (ShipFlyFromMoonClip != null)
            {
                DebugHelper.LogWarning("ExtendedLevel.ShipFlyFromMoonClip Is Obsolete and will be removed in following releases, Please use ExtendedLevel.ShipFlyFromMoonClips instead.", DebugType.Developer);
                if (ShipFlyFromMoonClips.Count == 0)
                    ShipFlyFromMoonClips.Add(new(ShipFlyFromMoonClip, 300));
            }
        }

        internal static string GetNumberlessPlanetName(SelectableLevel selectableLevel)
        {
            if (selectableLevel != null)
                return new string(selectableLevel.PlanetName.SkipWhile(c => !char.IsLetter(c)).ToArray());
            else
                return string.Empty;
        }

        internal void SetLevelID(int levelId)
        {
            if (ContentType is ContentType.Custom)
            {
                SelectableLevel.levelID = levelId;
                if (RouteNode != null)
                    RouteNode.displayPlanetInfo = levelId;
                if (RouteConfirmNode != null)
                    RouteConfirmNode.buyRerouteToMoon = levelId;
            }
        }

        internal void SetExtendedDungeonFlowMatches()
        {
            List<IntWithRarity> dungeonFlowTypes = [.. SelectableLevel.dungeonFlowTypes];
            for (int i = 0; i < SelectableLevel.dungeonFlowTypes?.Length; i++)
            {
                IntWithRarity dungeonWithRarity = SelectableLevel.dungeonFlowTypes[i];
                if (dungeonWithRarity != null)
                {
                    ExtendedDungeonFlow extendedDungeonFlow = PatchedContent.ExtendedDungeonFlows.Find(extendedDungeonFlow => extendedDungeonFlow.DungeonID == dungeonWithRarity.id);
                    if (extendedDungeonFlow != null)
                    {
                        extendedDungeonFlow.LevelMatchingProperties.planetNames.Add(new(NumberlessPlanetName, dungeonWithRarity.id));
                        continue;
                    }
                }
                Debug.LogWarning($"Invalid DungeonFlow entry at index '{i}' for SelectableLevel: {SelectableLevel.name}");
                dungeonFlowTypes.RemoveAt(i--);
            }

            if (SelectableLevel.name.Equals("MarchLevel", StringComparison.Ordinal))
            {
                ExtendedDungeonFlow marchDungeonFlow = PatchedContent.ExtendedDungeonFlows.Find(extendedDungeonFlow =>
                    string.Equals(extendedDungeonFlow.DungeonFlow.name, "Level1Flow3Exits", StringComparison.Ordinal));
                if (marchDungeonFlow != null)
                    marchDungeonFlow.LevelMatchingProperties.planetNames.Add(new(NumberlessPlanetName, 300));
            }
        }

        internal void GetVanillaInfoNode()
        {
            foreach (CompatibleNoun infoNoun in TerminalManager.routeInfoKeyword.compatibleNouns)
                if (infoNoun.noun.word == NumberlessPlanetName.ToLower())
                {
                    InfoNode = infoNoun.result;
                    break;
                }
        }

        public void ForceSetRoutePrice(int newValue)
        {
            if (Plugin.Instance != null)
                Debug.LogWarning("ForceSetRoutePrice Should Only Be Used In Editor! Consider Using RoutePrice Property To Sync TerminalNode's With New Value.");
            routePrice = newValue;
        }
    }

    [Serializable]
    public class LevelEvents
    {
        public ExtendedEvent onLevelLoaded = new ExtendedEvent();
        public ExtendedEvent onShipLand = new ExtendedEvent();
        public ExtendedEvent onShipLeave = new ExtendedEvent();
        public ExtendedEvent<EnemyAI> onDaytimeEnemySpawn = new ExtendedEvent<EnemyAI>();
        public ExtendedEvent<EnemyAI> onNighttimeEnemySpawn = new ExtendedEvent<EnemyAI>();
        public ExtendedEvent<StoryLog> onStoryLogCollected = new ExtendedEvent<StoryLog>();
        public ExtendedEvent<LungProp> onApparatusTaken = new ExtendedEvent<LungProp>();
        public ExtendedEvent<(EntranceTeleport, PlayerControllerB)> onPlayerEnterDungeon = new ExtendedEvent<(EntranceTeleport, PlayerControllerB)>();
        public ExtendedEvent<(EntranceTeleport, PlayerControllerB)> onPlayerExitDungeon = new ExtendedEvent<(EntranceTeleport, PlayerControllerB)>();
        public ExtendedEvent<bool> onPowerSwitchToggle = new ExtendedEvent<bool>();
        public ExtendedEvent<DayMode> onDayModeToggle = new ExtendedEvent<DayMode>();
    }
}
