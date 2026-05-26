using System.Collections.Generic;
using DunGen;
using DunGen.Adapters;
using LethalLevelLoader.Tools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace LethalLevelLoader
{
    public static class LevelLoader
    {
        internal static AnimationClip defaultShipFlyToMoonClip;
        internal static AnimationClip defaultShipFlyFromMoonClip;

        // Dust Clouds
        internal static LocalVolumetricFog dustCloudFog;
        internal static Vector3 defaultDustCloudFogVolumeSize;

        // Rainy
        internal static GameObject defaultQuicksandPrefab;

        internal static ParticleSystem rainParticles;
        internal static AudioSource rainyAmbienceSource;
        internal static AudioClip defaultRainyAmbience;

        internal static GameObject rainPrefabOverrideInstance;

        // Stormy
        internal static StormyWeather stormyWeather;

        internal static ParticleSystem defaultStormyLightningStrikeExplosion;
        internal static ParticleSystem defaultStormyStaticElectricityParticle;
        internal static AudioClip[] defaultStormyLightningStrikeSFX;
        internal static AudioClip[] defaultStormyDistantThunderSFX;
        internal static AudioClip defaultStormyStaticElectricitySFX;

        internal static ParticleSystem stormyRainParticles;
        internal static AudioSource stormyRainAmbienceSource;
        internal static AudioClip defaultStormyRainAmbience;

        internal static GameObject stormyRainPrefabOverrideInstance;

        // Foggy
        internal static LocalVolumetricFog foggyFog;
        internal static Vector3 defaultFoggyFogVolumeSize;

        // Flooded
        internal static FloodWeather floodedWeather;
        internal static QuicksandTrigger floodedWaterTrigger;
        internal static MeshRenderer floodedWaterRenderer;
        internal static Material defaultFloodedWaterMaterial;

        internal static AudioSource floodedAmbienceSource;
        internal static AudioClip defaultFloodedAmbience;

        internal static GameObject floodedPrefabOverrideInstance;

        // Eclipsed
        internal static AudioSource eclipsedMusicSource;
        internal static AudioClip defaultEclipsedMusic;

        // TimeOfDay AudioClips
        internal static AudioClip[] timeOfDayCues;
        internal static AudioClip defaultStartOfDayMusic;
        internal static AudioClip defaultMidDayMusic;
        internal static AudioClip defaultLateDayMusic;
        internal static AudioClip defaultNightMusic;

        // Vanilla shaders
        internal static Shader vanillaWaterShader;
        internal static Shader vanillaWavingGrassShader;
        internal static LocalKeyword[] vanillaWaterShaderKeywords;
        internal static LocalKeyword[] vanillaWavingGrassShaderKeywords;

        // Scene stuff
        internal static Scene currentLevelScene;

        internal static void RefreshShipAnimatorClips(ExtendedLevel extendedLevel, int randomSeed)
        {
            DebugHelper.Log("Refreshing Ship Animator Clips!", DebugType.Developer);

            /* Animator shipAnimator = Patches.StartOfRound.shipAnimator;
            if (shipAnimator.runtimeAnimatorController is not AnimatorOverrideController overrideController)
            {
                // Create new AnimatorOverrideController only if not already one.
                overrideController = new AnimatorOverrideController(shipAnimator.runtimeAnimatorController);
                shipAnimator.runtimeAnimatorController = overrideController;
            } */

            Animator shipAnimator = Patches.StartOfRound.shipAnimator;
            AnimatorOverrideController overrideController = new AnimatorOverrideController(shipAnimator.runtimeAnimatorController);
            shipAnimator.runtimeAnimatorController = overrideController;

            System.Random shipAnimatorRandom = new System.Random(randomSeed + 33);
            overrideController["HangarShipLandB"] = GetRandomWeightedClip(extendedLevel.ShipFlyToMoonClips, shipAnimatorRandom);
            overrideController["ShipLeave"] = GetRandomWeightedClip(extendedLevel.ShipFlyFromMoonClips, shipAnimatorRandom);
        }

        private static AnimationClip GetRandomWeightedClip(List<ClipWithRarity> clipSelections, System.Random random = null)
        {
            if (clipSelections.Count == 1)
                return (clipSelections[0].Clip);

            int[] clipWeights = new int[clipSelections.Count];
            for (int i = 0; i < clipWeights.Length; i++)
                clipWeights[i] = clipSelections[i].Rarity;

            int selectedClipIndex = Patches.RoundManager.GetRandomWeightedIndex(clipWeights, random);
            return (clipSelections[selectedClipIndex].Clip);
        }

        internal static void RefreshWeatherEffects(ExtendedLevel extendedLevel)
        {
            DebugHelper.Log("Refreshing Weather Effects!", DebugType.Developer);

            // Dust Clouds
            if (dustCloudFog != null)
                dustCloudFog.parameters.size = extendedLevel.OverrideDustStormVolumeSize;

            // Rainy
            if (rainyAmbienceSource != null)
                rainyAmbienceSource.clip = extendedLevel.OverrideRainAmbience;
            if (extendedLevel.OverrideRainPrefab != null && rainParticles != null)
            {
                rainPrefabOverrideInstance = Object.Instantiate(extendedLevel.OverrideRainPrefab, rainParticles.transform.parent);

                if (rainParticles != null)
                    rainParticles.gameObject.SetActive(false);

                SceneManager.sceneUnloaded += CleanupRainyOverride;
            }

            // Stormy
            if (stormyRainAmbienceSource != null)
                stormyRainAmbienceSource.clip = extendedLevel.OverrideStormyRainAmbience;
            if (stormyWeather != null)
            {
                if (extendedLevel.OverrideStormyLightningStrikeExplosion != null)
                {
                    stormyWeather.explosionEffectParticle = Object.Instantiate(extendedLevel.OverrideStormyLightningStrikeExplosion, stormyWeather.transform);
                    PreventParticleDestroy(stormyWeather.explosionEffectParticle);
                }
                if (extendedLevel.OverrideStormyStaticElectricityParticle != null)
                {
                    stormyWeather.staticElectricityParticle = Object.Instantiate(extendedLevel.OverrideStormyStaticElectricityParticle, stormyWeather.transform);
                    PreventParticleDestroy(stormyWeather.staticElectricityParticle);
                }

                if (extendedLevel.OverrideStormyLightningStrikeSFX != null && extendedLevel.OverrideStormyLightningStrikeSFX.Length > 0)
                    stormyWeather.strikeSFX = extendedLevel.OverrideStormyLightningStrikeSFX;
                if (extendedLevel.OverrideStormyDistantThunderSFX != null && extendedLevel.OverrideStormyDistantThunderSFX.Length > 0)
                    stormyWeather.distantThunderSFX = extendedLevel.OverrideStormyDistantThunderSFX;
                if (extendedLevel.OverrideStormyStaticElectricitySFX != null)
                    stormyWeather.staticElectricityAudio = extendedLevel.OverrideStormyStaticElectricitySFX;

                if (extendedLevel.OverrideStormyRainPrefab != null && stormyRainParticles != null)
                {
                    stormyRainPrefabOverrideInstance = Object.Instantiate(extendedLevel.OverrideStormyRainPrefab, stormyWeather.transform);

                    if (stormyRainParticles != null)
                        stormyRainParticles.gameObject.SetActive(false);
                }

                if (extendedLevel.OverrideStormyLightningStrikeExplosion != null || extendedLevel.OverrideStormyStaticElectricityParticle != null
                    || stormyRainPrefabOverrideInstance != null)
                {
                    SceneManager.sceneUnloaded += CleanupStormyOverride;
                }
            }

            // Foggy
            if (foggyFog != null)
                foggyFog.parameters.size = extendedLevel.OverrideFoggyVolumeSize;

            // Flooded
            if (floodedAmbienceSource != null)
                floodedAmbienceSource.clip = extendedLevel.OverrideFloodedAmbience;
            if (extendedLevel.OverrideFloodedPrefab != null && floodedWeather != null)
            {
                floodedPrefabOverrideInstance = Object.Instantiate(extendedLevel.OverrideFloodedPrefab, floodedWeather.transform);

                if (floodedWaterTrigger != null)
                    floodedWaterTrigger.gameObject.SetActive(false);
                if (floodedWaterRenderer != null)
                    floodedWaterRenderer.gameObject.SetActive(false);

                SceneManager.sceneUnloaded += CleanupFloodedOverride;
            }

            // Eclipsed
            if (eclipsedMusicSource != null)
                eclipsedMusicSource.clip = extendedLevel.OverrideEclipsedMusic;
        }

        private static void CleanupRainyOverride(Scene scene)
        {
            SceneManager.sceneUnloaded -= CleanupRainyOverride;

            if (rainPrefabOverrideInstance != null)
                Object.Destroy(rainPrefabOverrideInstance);

            if (rainParticles != null)
                rainParticles.gameObject.SetActive(true);
        }

        private static void CleanupStormyOverride(Scene scene)
        {
            SceneManager.sceneUnloaded -= CleanupStormyOverride;

            if (stormyWeather.explosionEffectParticle != defaultStormyLightningStrikeExplosion)
            {
                Object.Destroy(stormyWeather.explosionEffectParticle);
                stormyWeather.explosionEffectParticle = defaultStormyLightningStrikeExplosion;
            }

            if (stormyWeather.staticElectricityParticle != defaultStormyStaticElectricityParticle)
            {
                Object.Destroy(stormyWeather.staticElectricityParticle);
                stormyWeather.staticElectricityParticle = defaultStormyStaticElectricityParticle;
            }

            if (stormyRainPrefabOverrideInstance != null)
                Object.Destroy(stormyRainPrefabOverrideInstance);

            if (stormyRainParticles != null)
                stormyRainParticles.gameObject.SetActive(true);
        }

        private static void CleanupFloodedOverride(Scene scene)
        {
            SceneManager.sceneUnloaded -= CleanupFloodedOverride;

            if (floodedPrefabOverrideInstance != null)
                Object.Destroy(floodedPrefabOverrideInstance);

            if (floodedWaterTrigger != null)
                floodedWaterTrigger.gameObject.SetActive(true);
            if (floodedWaterRenderer != null)
                floodedWaterRenderer.gameObject.SetActive(true);
        }

        private static void PreventParticleDestroy(ParticleSystem particle)
        {
            ParticleSystem.MainModule particleMain = particle.main;
            if (particleMain.stopAction is ParticleSystemStopAction.Destroy or ParticleSystemStopAction.Disable)
            {
                if (LevelManager.CurrentExtendedLevel != null)
                    DebugHelper.LogWarning($"Setting particle stop action to None for particle {particle.name} in {LevelManager.CurrentExtendedLevel.name} to prevent errors.", DebugType.Developer);
                particleMain.stopAction = ParticleSystemStopAction.None;
            }
        }

        internal static void ApplyCameraDistanceOverride() // TODO: Overhaul!
        {
            float newDistance = 0;
            if (LevelManager.CurrentExtendedLevel.OverrideCameraMaxDistance > 400f || (DungeonManager.CurrentExtendedDungeonFlow != null && DungeonManager.CurrentExtendedDungeonFlow.OverrideCameraMaxDistance > 400f))
                newDistance = Mathf.Max(LevelManager.CurrentExtendedLevel.OverrideCameraMaxDistance, DungeonManager.CurrentExtendedDungeonFlow.OverrideCameraMaxDistance);
            foreach (KeyValuePair<Camera, float> cameraPair in Patches.playerCameras)
                cameraPair.Key.farClipPlane = Mathf.Max(cameraPair.Value, newDistance);
        }

        internal static void RefreshTimeOfDayMusic(ExtendedLevel extendedLevel)
        {
            if (Patches.TimeOfDay.timeOfDayCues != null && Patches.TimeOfDay.timeOfDayCues.Length == 4)
            {
                Patches.TimeOfDay.timeOfDayCues[0] = extendedLevel.OverrideStartOfDayMusic;
                Patches.TimeOfDay.timeOfDayCues[1] = extendedLevel.OverrideMidDayMusic;
                Patches.TimeOfDay.timeOfDayCues[2] = extendedLevel.OverrideLateDayMusic;
                Patches.TimeOfDay.timeOfDayCues[3] = extendedLevel.OverrideNightMusic;
            }
        }

        private static readonly HashSet<Material> uniqueMaterials = [];
        private static readonly List<Renderer> tempRenderers = [];

        internal static void RestoreShaders()
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
                foreach (DetailPrototype detailPrototype in terrain.terrainData.detailPrototypes)
                    RestoreShaders(detailPrototype.prototype);

            foreach (GameObject rootObject in currentLevelScene.GetRootGameObjects())
                RestoreShaders(rootObject);

            uniqueMaterials.Clear();
            tempRenderers.Clear();
        }

        private static void RestoreShaders(GameObject gameObject)
        {
            if (gameObject == null) return;
            gameObject.GetComponentsInChildren(includeInactive: true, tempRenderers);
            foreach (Renderer renderer in tempRenderers)
                foreach (Material sharedMaterial in renderer.sharedMaterials)
                    if (sharedMaterial != null && sharedMaterial.shader != null && uniqueMaterials.Add(sharedMaterial))
                    {
                        if (vanillaWaterShader != null && vanillaWaterShaderKeywords?.Length > 0)
                            ContentRestorer.TryRestoreShader(sharedMaterial, vanillaWaterShader, vanillaWaterShaderKeywords);
                        if (vanillaWavingGrassShader != null && vanillaWavingGrassShaderKeywords?.Length > 0)
                            ContentRestorer.TryRestoreShader(sharedMaterial, vanillaWavingGrassShader, vanillaWavingGrassShaderKeywords);
                    }
        }

        private static readonly List<SpawnSyncedObject> tempSpawnSyncedObjects = [];
        private static readonly List<RandomScrapSpawn> tempRandomScrapSpawns = [];
        private static readonly List<RandomMapObject> tempRandomMapObjects = [];
        private static readonly List<BridgeTrigger> tempBridgeTriggers = [];

        internal static void RestoreSceneBlankReferences()
        {
            foreach (GameObject rootObject in currentLevelScene.GetRootGameObjects())
                RestoreSceneBlankReferences(rootObject);

            tempSpawnSyncedObjects.Clear();
            tempRandomScrapSpawns.Clear();
            tempRandomMapObjects.Clear();
            tempBridgeTriggers.Clear();
        }

        private static void RestoreSceneBlankReferences(GameObject gameObject)
        {
            if (gameObject == null) return;
            gameObject.GetComponentsInChildren(includeInactive: true, tempSpawnSyncedObjects);
            foreach (SpawnSyncedObject spawnSyncedObject in tempSpawnSyncedObjects)
                ContentRestorer.TryRestoreSpawnSyncedObject(spawnSyncedObject);

            gameObject.GetComponentsInChildren(includeInactive: true, tempRandomScrapSpawns);
            foreach (RandomScrapSpawn randomScrapSpawn in tempRandomScrapSpawns)
                ContentRestorer.TryRestoreRandomScrapSpawn(randomScrapSpawn);

            gameObject.GetComponentsInChildren(includeInactive: true, tempRandomMapObjects);
            foreach (RandomMapObject randomMapObject in tempRandomMapObjects)
                ContentRestorer.RestoreRandomMapObject(randomMapObject);

            gameObject.GetComponentsInChildren(includeInactive: true, tempBridgeTriggers);
            foreach (BridgeTrigger bridgeTrigger in tempBridgeTriggers)
                ContentRestorer.RestoreBridgeTrigger(bridgeTrigger);
        }

        internal static void RestoreRuntimeDungeon()
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
                    if (levelGenerationContainer.GetChild(i).name.Contains("Root", System.StringComparison.InvariantCultureIgnoreCase))
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

                Patches.RoundManager.dungeonGenerator = dungeon;
                dungeon.Generator.DungeonFlow = Patches.RoundManager.dungeonFlowTypes[0].dungeonFlow; // Set Facility as default, before interior selection.
                DebugHelper.Log("RuntimeDungeon created, proceeding as usual!", DebugType.User);
            }
        }
    }
}