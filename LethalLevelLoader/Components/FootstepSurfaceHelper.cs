using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    [DisallowMultipleComponent]
    public class FootstepSurfaceHelper : MonoBehaviour
    {
        public TerrainData AssignedTerrainData { get; private set; }

        [field: Header("ExtendedFootstepSurface Helper")]
        [field: Tooltip("List of Terrain layers and their assigned footstep surface.")]
        [field: SerializeField] public List<TerrainLayerWithSurface> AssignedLayerSurfaces { get; private set; } = new List<TerrainLayerWithSurface>(8);

        public void Reset()
        {
            if (!TryGetComponent(out Terrain terrain)) return;
            AssignedTerrainData = terrain.terrainData;
            if (AssignedTerrainData == null) return;

            for (int i = 0; i < AssignedTerrainData.terrainLayers?.Length; i++)
            {
                TerrainLayer layer = AssignedTerrainData.terrainLayers[i];
                if (layer != null)
                {
                    TerrainLayerWithSurface layerWithSurface = new()
                    {
                        surfaceTag = layer.name,
                        useVanillaTag = VanillaSurfaceTags.None,
                        terrainLayer = layer,
                    };
                    AssignedLayerSurfaces.Add(layerWithSurface);
                }
            }
        }

        public void Awake()
        {
            if (!TryGetComponent(out Terrain terrain))
            {
                DebugHelper.LogWarning($"Terrain not found for FootstepSurfaceHelper component in: {name}", DebugType.User);
                return;
            }

            AssignedTerrainData = terrain.terrainData;
            if (AssignedTerrainData == null)
            {
                DebugHelper.LogWarning($"TerrainData is null or missing for Terrain in: {name}", DebugType.User);
                return;
            }

            ExtendedLevel extendedLevel = LevelManager.CurrentExtendedLevel;
            if (extendedLevel == null || extendedLevel.ContentType is ContentType.External) return;

            if (!extendedLevel.UseTerrainFootsteps) // Forcibly enable 'ExtendedLevel.UseTerrainFootsteps', if this component exists.
            {
                extendedLevel.UseTerrainFootsteps = true;
                DebugHelper.LogWarning($"Enabled 'UseTerrainFootsteps' for ExtendedLevel {extendedLevel.name} due to the presence of a FootstepSurfaceHelper!", DebugType.Developer);
            }

            ExtendedFootstepSurface[] footstepSurfaces = new ExtendedFootstepSurface[8];
            for (int i = 0; i < AssignedTerrainData.terrainLayers?.Length; i++)
            {
                TerrainLayer terrainLayer = AssignedTerrainData.terrainLayers[i];
                if (terrainLayer == null)
                {
                    DebugHelper.LogWarning($"Layer at index '{i}' is null or missing for Terrain: {AssignedTerrainData.name}", DebugType.User);
                    continue;
                }

                int layerWithSurfaceIndex = AssignedLayerSurfaces.FindIndex(surface => surface.terrainLayer == terrainLayer);
                if (layerWithSurfaceIndex == -1)
                {
                    DebugHelper.LogDebug($"No surface specified for Terrain layer '{terrainLayer.name}' at index '{i}', GameObject tag footsteps will be used for it instead.", DebugType.Developer);
                    continue;
                }

                TerrainLayerWithSurface layerWithSurface = AssignedLayerSurfaces[layerWithSurfaceIndex];
                if (layerWithSurface.useVanillaTag is not VanillaSurfaceTags.None)
                {
                    int vanillaTagIndex = (int)layerWithSurface.useVanillaTag;
                    if (vanillaTagIndex < PatchedContent.ExtendedFootstepSurfaces.Count)
                        footstepSurfaces[i] = PatchedContent.ExtendedFootstepSurfaces[vanillaTagIndex];
                    else
                        DebugHelper.LogError($"Could not get vanilla FootstepSurface '{layerWithSurface.useVanillaTag}' as ExtendedFootstepSurface list is empty.", DebugType.User);
                    continue;
                }

                if (string.IsNullOrEmpty(layerWithSurface.surfaceTag))
                {
                    DebugHelper.LogWarning($"Custom surface tag for Terrain layer '{terrainLayer.name}' is missing or empty in: {name}", DebugType.User);
                    continue;
                }

                string identifier = (extendedLevel.ExtendedMod.ModMergeSetting) switch
                {
                    ModMergeSetting.MatchingAuthorName => extendedLevel.ExtendedMod.AuthorName,
                    ModMergeSetting.MatchingModName => extendedLevel.ExtendedMod.ModName,
                    _ => extendedLevel.UniqueIdentificationName
                };
                string fullSurfaceTag = identifier + '/' + layerWithSurface.surfaceTag;

                ExtendedFootstepSurface matchedSurface = PatchedContent.ExtendedFootstepSurfaces.Find(surface =>
                    string.Equals(surface.FootstepSurface.surfaceTag, fullSurfaceTag, StringComparison.Ordinal));
                if (matchedSurface == null)
                {
                    DebugHelper.LogWarning($"Could not find ExtendedFootstepSurface with matching tag '{layerWithSurface.surfaceTag}' for layer '{terrainLayer.name}' in: {name}", DebugType.User);
                    continue;
                }
                footstepSurfaces[i] = matchedSurface;
            }
            TerrainManager.TerrainFootstepsDict[AssignedTerrainData] = footstepSurfaces;
        }
    }

    [Serializable]
    public struct TerrainLayerWithSurface
    {
        [Tooltip("Surface tag of the ExtendedFootstepSurface to use for this Terrain layer.")]
        public string surfaceTag;
        [Tooltip("Use a vanilla tag for this Terrain layer instead. NOTE: For custom footsteps, leave as 'None' and use the field above!")]
        public VanillaSurfaceTags useVanillaTag;
        [Tooltip("Terrain layer to change the footsteps of.")]
        [Space(10)] public TerrainLayer terrainLayer;
    }
}