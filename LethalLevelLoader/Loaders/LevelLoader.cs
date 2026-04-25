using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using LethalLevelLoader.Tools;

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

        internal static FootstepSurface[] defaultFootstepSurfaces;

        internal static Dictionary<Collider, List<Material>> cachedLevelColliderMaterialDictionary = new Dictionary<Collider, List<Material>>();
        internal static Dictionary<string, List<Collider>> cachedLevelMaterialColliderDictionary = new Dictionary<string, List<Collider>>();
        internal static Dictionary<string, FootstepSurface> activeExtendedFootstepSurfaceDictionary = new Dictionary<string, FootstepSurface>();
        internal static LayerMask triggerMask;

        internal static Shader vanillaWaterShader;
        internal static Shader vanillaWavingGrassShader;
        internal static LocalKeyword[] vanillaWaterShaderKeywords;
        internal static LocalKeyword[] vanillaWavingGrassShaderKeywords;

        internal static void RefreshShipAnimatorClips(ExtendedLevel extendedLevel)
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

            overrideController["HangarShipLandB"] = extendedLevel.ShipFlyToMoonClip;
            overrideController["ShipLeave"] = extendedLevel.ShipFlyFromMoonClip;
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

        /* internal static void RefreshFootstepSurfaces()
        {
            List<FootstepSurface> activeFootstepSurfaces = new List<FootstepSurface>(defaultFootstepSurfaces);
            foreach (ExtendedFootstepSurface extendedSurface in LevelManager.CurrentExtendedLevel.ExtendedMod.ExtendedFootstepSurfaces)
            {
                extendedSurface.footstepSurface.surfaceTag = "Untagged";
                activeFootstepSurfaces.Add(extendedSurface.footstepSurface);
            }

            Patches.StartOfRound.footstepSurfaces = activeFootstepSurfaces.ToArray();
        } */

        private static readonly HashSet<Material> uniqueMaterials = [];
        private static readonly List<GameObject> tempRootObjects = [];
        private static readonly List<Renderer> tempRenderers = [];

        internal static void TryRestoreShaders(Scene scene)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
                foreach (DetailPrototype detailPrototype in terrain.terrainData.detailPrototypes)
                    TryRestoreShaders(detailPrototype.prototype);

            scene.GetRootGameObjects(tempRootObjects);
            tempRootObjects.ForEach(TryRestoreShaders);

            uniqueMaterials.Clear();
            tempRootObjects.Clear();
            tempRenderers.Clear();
        }

        private static void TryRestoreShaders(GameObject gameObject)
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

        /* public static bool TryGetFootstepSurface(Collider collider, out FootstepSurface footstepSurface)
        {
            footstepSurface = null;

            if (collider == null)
                return (false);

            if (cachedLevelColliderMaterialDictionary.TryGetValue(collider, out List<Material> materials))
                if (materials != null)
                    foreach (Material material in materials)
                        if (material != null && !string.IsNullOrEmpty(material.name))
                            activeExtendedFootstepSurfaceDictionary.TryGetValue(material.name, out footstepSurface);

            return (footstepSurface != null);
        } */
    }
}