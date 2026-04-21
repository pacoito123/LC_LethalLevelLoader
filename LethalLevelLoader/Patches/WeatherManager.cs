using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Random = System.Random;

namespace LethalLevelLoader
{
    internal class WeatherManager
    {
        public static ExtendedWeatherEffect CurrentExtendedWeatherEffect;

        public static Dictionary<LevelWeatherType, ExtendedWeatherEffect> vanillaExtendedWeatherEffectsDictionary = new Dictionary<LevelWeatherType, ExtendedWeatherEffect>();

        public static void RefreshVanillaWeatherEffects(TimeOfDay timeOfDay)
        {
            foreach (ExtendedWeatherEffect extendedEffect in PatchedContent.VanillaExtendedWeatherEffects)
            {
                LevelWeatherType weatherType = extendedEffect.BaseWeatherType;
                vanillaExtendedWeatherEffectsDictionary[weatherType] = extendedEffect;

                int effectIndex = (int)weatherType;
                if (effectIndex < 0 || effectIndex >= timeOfDay.effects.Length) continue;

                WeatherEffect weatherEffect = timeOfDay.effects[effectIndex];
                if (weatherEffect == null) continue;

                RefreshEffectWorldObject(extendedEffect, weatherEffect.effectObject);
                RefreshEffectGlobalObject(extendedEffect, weatherEffect.effectPermanentObject);
            }
        }

        private static void RefreshEffectWorldObject(ExtendedWeatherEffect extendedEffect, GameObject worldObject)
        {
            if (worldObject == null) return;

            switch (extendedEffect.BaseWeatherType)
            {
                case LevelWeatherType.DustClouds:
                    if (worldObject.TryGetComponent(out LocalVolumetricFog dustFog))
                    {
                        LevelLoader.dustCloudFog = dustFog;
                        if (LevelLoader.defaultDustCloudFogVolumeSize == Vector3.zero)
                            LevelLoader.defaultDustCloudFogVolumeSize = dustFog.parameters.size;
                    }
                    break;
                case LevelWeatherType.Rainy:
                    if (LevelLoader.defaultQuicksandPrefab == null)
                        LevelLoader.defaultQuicksandPrefab = Patches.RoundManager.quicksandPrefab;

                    LevelLoader.rainyAmbienceSource = worldObject.GetComponentInChildren<AudioSource>(includeInactive: true);
                    if (LevelLoader.defaultRainyAmbience == null && LevelLoader.rainyAmbienceSource != null)
                        LevelLoader.defaultRainyAmbience = LevelLoader.rainyAmbienceSource.clip;

                    foreach (ParticleSystem particle in worldObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
                        if (particle.transform.parent == worldObject.transform)
                        {
                            LevelLoader.rainParticles = particle;
                            break;
                        }
                    break;
                case LevelWeatherType.Stormy:
                    LevelLoader.stormyRainAmbienceSource = worldObject.GetComponentInChildren<AudioSource>(includeInactive: true);
                    if (LevelLoader.defaultStormyRainAmbience == null && LevelLoader.stormyRainAmbienceSource != null)
                        LevelLoader.defaultStormyRainAmbience = LevelLoader.stormyRainAmbienceSource.clip;

                    foreach (ParticleSystem particle in worldObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
                        if (particle.transform.parent == worldObject.transform)
                        {
                            LevelLoader.stormyRainParticles = particle;
                            break;
                        }
                    break;
                case LevelWeatherType.Eclipsed:
                    LevelLoader.eclipsedMusicSource = worldObject.GetComponentInChildren<AudioSource>(includeInactive: true);
                    if (LevelLoader.defaultEclipsedMusic == null && LevelLoader.eclipsedMusicSource != null)
                        LevelLoader.defaultEclipsedMusic = LevelLoader.eclipsedMusicSource.clip;
                    break;
                default:
                    break;
            }

            extendedEffect.WorldObject = worldObject;
        }

        private static void RefreshEffectGlobalObject(ExtendedWeatherEffect extendedEffect, GameObject globalObject)
        {
            if (globalObject == null) return;

            switch (extendedEffect.BaseWeatherType)
            {
                case LevelWeatherType.Stormy:
                    if (globalObject.TryGetComponent(out StormyWeather stormyWeather))
                    {
                        LevelLoader.stormyWeather = stormyWeather;
                        if (stormyWeather.explosionEffectParticle != null)
                            LevelLoader.defaultStormyLightningStrikeExplosion = stormyWeather.explosionEffectParticle;
                        if (stormyWeather.staticElectricityParticle != null)
                            LevelLoader.defaultStormyStaticElectricityParticle = stormyWeather.staticElectricityParticle;

                        if (LevelLoader.defaultStormyLightningStrikeSFX == null && stormyWeather.strikeSFX?.Length > 0)
                            LevelLoader.defaultStormyLightningStrikeSFX = [.. stormyWeather.strikeSFX];
                        if (LevelLoader.defaultStormyDistantThunderSFX == null && stormyWeather.distantThunderSFX?.Length > 0)
                            LevelLoader.defaultStormyDistantThunderSFX = [.. stormyWeather.distantThunderSFX];
                        if (LevelLoader.defaultStormyStaticElectricitySFX == null && stormyWeather.staticElectricityAudio != null)
                            LevelLoader.defaultStormyStaticElectricitySFX = stormyWeather.staticElectricityAudio;
                    }
                    break;
                case LevelWeatherType.Foggy:
                    if (globalObject.TryGetComponent(out LocalVolumetricFog foggyFog))
                    {
                        LevelLoader.foggyFog = foggyFog;
                        if (LevelLoader.defaultFoggyFogVolumeSize == Vector3.zero)
                            LevelLoader.defaultFoggyFogVolumeSize = foggyFog.parameters.size;
                    }
                    break;
                case LevelWeatherType.Flooded:
                    if (globalObject.TryGetComponent(out FloodWeather floodedWeather))
                    {
                        LevelLoader.floodedWeather = floodedWeather;
                        LevelLoader.floodedAmbienceSource = floodedWeather.waterAudio;
                        if (LevelLoader.defaultFloodedAmbience == null && LevelLoader.floodedAmbienceSource != null)
                            LevelLoader.defaultFloodedAmbience = LevelLoader.floodedAmbienceSource.clip;

                        LevelLoader.floodedWaterTrigger = floodedWeather.GetComponentInChildren<QuicksandTrigger>(includeInactive: true);
                        foreach (MeshRenderer renderer in floodedWeather.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
                        {
                            if (renderer.gameObject == LevelLoader.floodedWaterTrigger.gameObject) continue;
                            LevelLoader.floodedWaterRenderer = renderer;

                            if (LevelLoader.defaultFloodedWaterMaterial != null) break;
                            foreach (Material material in renderer.sharedMaterials)
                            {
                                if (material != null && material.shader != null && string.Equals(material.shader.name, "Shader Graphs/WaterShaderHDRP", StringComparison.Ordinal))
                                {
                                    LevelLoader.defaultFloodedWaterMaterial = material;
                                    LevelLoader.vanillaWaterShader = material.shader;
                                    LevelLoader.vanillaWaterShaderKeywords = material.enabledKeywords;
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    break;
                default:
                    break;
            }

            extendedEffect.GlobalObject = globalObject;
        }

        public static void PopulateExtendedLevelEnabledExtendedWeatherEffects()
        {
            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
            {
                if (extendedLevel.SelectableLevel.randomWeathers != null)
                    foreach (RandomWeatherWithVariables randomWeatherWithVariables in extendedLevel.SelectableLevel.randomWeathers)
                        if (vanillaExtendedWeatherEffectsDictionary.TryGetValue(randomWeatherWithVariables.weatherType, out ExtendedWeatherEffect extendedWeatherEffect))
                            extendedLevel.EnabledExtendedWeatherEffects.Add(extendedWeatherEffect);

                foreach (ExtendedWeatherEffect customExtendedWeatherEffect in PatchedContent.CustomExtendedWeatherEffects)
                    extendedLevel.EnabledExtendedWeatherEffects.Add(customExtendedWeatherEffect);

                if (extendedLevel.ContentType is ContentType.Vanilla)
                {
                    // Set default weather overrides:
                    extendedLevel.OverrideDustStormVolumeSize = LevelLoader.defaultDustCloudFogVolumeSize;

                    extendedLevel.OverrideQuicksandPrefab = LevelLoader.defaultQuicksandPrefab;
                    extendedLevel.OverrideRainAmbience = LevelLoader.defaultRainyAmbience;

                    extendedLevel.OverrideStormyLightningStrikeSFX = [.. LevelLoader.defaultStormyLightningStrikeSFX];
                    extendedLevel.OverrideStormyDistantThunderSFX = [.. LevelLoader.defaultStormyDistantThunderSFX];
                    extendedLevel.OverrideStormyStaticElectricitySFX = LevelLoader.defaultStormyStaticElectricitySFX;
                    extendedLevel.OverrideStormyRainAmbience = LevelLoader.defaultStormyRainAmbience;

                    extendedLevel.OverrideFoggyVolumeSize = LevelLoader.defaultFoggyFogVolumeSize;

                    extendedLevel.OverrideFloodedAmbience = LevelLoader.defaultFloodedAmbience;

                    extendedLevel.OverrideEclipsedMusic = LevelLoader.defaultEclipsedMusic;
                    // ...

                    continue;
                }

                // Set default weather overrides (if left blank):
                if (extendedLevel.OverrideDustStormVolumeSize == Vector3.zero)
                    extendedLevel.OverrideDustStormVolumeSize = LevelLoader.defaultDustCloudFogVolumeSize;

                if (extendedLevel.OverrideQuicksandPrefab == null)
                    extendedLevel.OverrideQuicksandPrefab = LevelLoader.defaultQuicksandPrefab;
                if (extendedLevel.OverrideRainAmbience == null)
                    extendedLevel.OverrideRainAmbience = LevelLoader.defaultRainyAmbience;

                if (extendedLevel.OverrideStormyLightningStrikeSFX == null || extendedLevel.OverrideStormyLightningStrikeSFX.Length == 0)
                    extendedLevel.OverrideStormyLightningStrikeSFX = [.. LevelLoader.defaultStormyLightningStrikeSFX];
                if (extendedLevel.OverrideStormyDistantThunderSFX == null || extendedLevel.OverrideStormyDistantThunderSFX.Length == 0)
                    extendedLevel.OverrideStormyDistantThunderSFX = [.. LevelLoader.defaultStormyDistantThunderSFX];
                if (extendedLevel.OverrideStormyStaticElectricitySFX == null)
                    extendedLevel.OverrideStormyStaticElectricitySFX = LevelLoader.defaultStormyStaticElectricitySFX;
                if (extendedLevel.OverrideStormyRainAmbience == null)
                    extendedLevel.OverrideStormyRainAmbience = LevelLoader.defaultStormyRainAmbience;

                if (extendedLevel.OverrideFoggyVolumeSize == Vector3.zero)
                    extendedLevel.OverrideFoggyVolumeSize = LevelLoader.defaultFoggyFogVolumeSize;

                if (extendedLevel.OverrideFloodedAmbience == null)
                    extendedLevel.OverrideFloodedAmbience = LevelLoader.defaultFloodedAmbience;

                if (extendedLevel.OverrideEclipsedMusic == null)
                    extendedLevel.OverrideEclipsedMusic = LevelLoader.defaultEclipsedMusic;
                // ...

                // Replace references to any materials named 'Water_mat_04' with the one actually used by the game.
                if (extendedLevel.OverrideFloodedPrefab != null && LevelLoader.vanillaWaterShader != null)
                {
                    foreach (MeshRenderer renderer in extendedLevel.OverrideFloodedPrefab.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
                    {
                        Material[] materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                            if (materials[i].name == LevelLoader.defaultFloodedWaterMaterial.name)
                                materials[i] = LevelLoader.defaultFloodedWaterMaterial;
                        renderer.sharedMaterials = materials;
                    }
                }
                // ...
            }
        }

        public static void SetExtendedLevelsWeather(int connectedPlayersOnServer)
        {
            StartOfRound startOfRound = Patches.StartOfRound;
            List<ExtendedLevel> extendedLevels = new List<ExtendedLevel>(PatchedContent.ExtendedLevels);

            foreach (ExtendedLevel extendedLevel in extendedLevels)
            {
                if (extendedLevel.SelectableLevel.overrideWeather == false)
                {
                    extendedLevel.CurrentExtendedWeatherEffect = null;
                    extendedLevel.SelectableLevel.currentWeather = LevelWeatherType.None;
                }
                else
                {
                    extendedLevel.SelectableLevel.currentWeather = extendedLevel.SelectableLevel.overrideWeatherType;
                }
            }

            Random random = new Random(startOfRound.randomMapSeed + 31);
            float daySurvivalStreakMultiplier = 1f;
            if (connectedPlayersOnServer + 1 > 1 && startOfRound.daysPlayersSurvivedInARow > 2 && startOfRound.daysPlayersSurvivedInARow % 3 == 0)
                daySurvivalStreakMultiplier = (float)random.Next(15, 25) / 10f;

            int randomWeatherEffectToggleAttempts = Mathf.Clamp((int)(Mathf.Clamp(startOfRound.planetsWeatherRandomCurve.Evaluate((float)random.NextDouble()) * daySurvivalStreakMultiplier, 0f, 1f) * (float)PatchedContent.ExtendedLevels.Count), 0, PatchedContent.ExtendedLevels.Count);

            for (int j = 0; j < randomWeatherEffectToggleAttempts; j++)
            {
                ExtendedLevel extendedLevel = extendedLevels[random.Next(0, extendedLevels.Count)];
                if (extendedLevel.SelectableLevel.randomWeathers != null && extendedLevel.SelectableLevel.randomWeathers.Length != 0)
                    extendedLevel.SelectableLevel.currentWeather = extendedLevel.SelectableLevel.randomWeathers[random.Next(0, extendedLevel.SelectableLevel.randomWeathers.Length)].weatherType;
                extendedLevels.Remove(extendedLevel);

            }
        }

        public static void SetExtendedLevelsExtendedWeatherEffect(int connectedPlayersOnServer)
        {
            StartOfRound startOfRound = Patches.StartOfRound;
            List<ExtendedLevel> extendedLevels = new List<ExtendedLevel>(PatchedContent.ExtendedLevels);

            foreach (ExtendedLevel extendedLevel in extendedLevels)
            {
                extendedLevel.CurrentExtendedWeatherEffect = null;
                if (extendedLevel.SelectableLevel.overrideWeather != false)
                    if (vanillaExtendedWeatherEffectsDictionary.TryGetValue(extendedLevel.SelectableLevel.overrideWeatherType, out ExtendedWeatherEffect extendedWeatherEffect))
                        extendedLevel.CurrentExtendedWeatherEffect = extendedWeatherEffect;
            }

            Random random = new Random(startOfRound.randomMapSeed + 31);
            float daySurvivalStreakMultiplier = 1f;
            if (connectedPlayersOnServer + 1 > 1 && startOfRound.daysPlayersSurvivedInARow > 2 && startOfRound.daysPlayersSurvivedInARow % 3 == 0)
                daySurvivalStreakMultiplier = (float)random.Next(15, 25) / 10f;

            int randomWeatherEffectToggleAttempts = Mathf.Clamp((int)(Mathf.Clamp(startOfRound.planetsWeatherRandomCurve.Evaluate((float)random.NextDouble()) * daySurvivalStreakMultiplier, 0f, 1f) * (float)PatchedContent.ExtendedLevels.Count), 0, PatchedContent.ExtendedLevels.Count);

            for (int j = 0; j < randomWeatherEffectToggleAttempts; j++)
            {
                ExtendedLevel extendedLevel = extendedLevels[random.Next(0, extendedLevels.Count)];
                extendedLevel.CurrentExtendedWeatherEffect = extendedLevel.EnabledExtendedWeatherEffects[random.Next(0, extendedLevel.EnabledExtendedWeatherEffects.Count)];
                extendedLevels.Remove(extendedLevel);
            }

            foreach (ExtendedLevel extendedLevel in PatchedContent.ExtendedLevels)
            {
                if (extendedLevel.CurrentExtendedWeatherEffect == null)
                    extendedLevel.SelectableLevel.currentWeather = LevelWeatherType.None;
                else if (extendedLevel.CurrentExtendedWeatherEffect.contentType == ContentType.Vanilla)
                    extendedLevel.SelectableLevel.currentWeather = extendedLevel.CurrentExtendedWeatherEffect.BaseWeatherType;
            }
        }

        public static ExtendedWeatherEffect GetVanillaExtendedWeatherEffect(LevelWeatherType levelWeatherType)
        {
            foreach (ExtendedWeatherEffect extendedWeatherEffect in PatchedContent.ExtendedWeatherEffects)
                if (extendedWeatherEffect.contentType == ContentType.Vanilla)
                    if (extendedWeatherEffect.BaseWeatherType == levelWeatherType)
                        return (extendedWeatherEffect);

            return (null);
        }
    }
}
