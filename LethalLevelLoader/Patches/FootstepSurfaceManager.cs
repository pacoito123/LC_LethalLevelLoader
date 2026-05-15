using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class FootstepSurfaceManager
    {
        public static readonly Dictionary<string, ExtendedFootstepSurface> surfaceTagExtendedFootstepDict = [];

        internal static void PatchVanillaFootstepSurfaceLists()
        {
            if (Plugin.IsSetupComplete == false)
                MergeExtendedFootstepSurfaces();

            List<FootstepSurface> footstepSurfaces = new(PatchedContent.ExtendedFootstepSurfaces.Count);
            foreach (ExtendedFootstepSurface extendedFootstepSurface in PatchedContent.ExtendedFootstepSurfaces)
            {
                extendedFootstepSurface.SurfaceIndex = footstepSurfaces.Count;
                footstepSurfaces.Add(extendedFootstepSurface.FootstepSurface);
            }
            Patches.StartOfRound.footstepSurfaces = [.. footstepSurfaces];
        }

        private static void MergeExtendedFootstepSurfaces()
        {
            int mergedSurfaces = 0;
            foreach (ExtendedFootstepSurface customExtendedFootstepSurface in PatchedContent.ExtendedFootstepSurfaces)
            {
                if (surfaceTagExtendedFootstepDict.TryGetValue(customExtendedFootstepSurface.FootstepSurface.surfaceTag, out ExtendedFootstepSurface _))
                {
                    Object.Destroy(customExtendedFootstepSurface); // TODO: Add to a List to destroy later perhaps.
                    mergedSurfaces++;
                    continue;
                }
                if (!surfaceTagExtendedFootstepDict.TryAdd(customExtendedFootstepSurface.FootstepSurface.surfaceTag, customExtendedFootstepSurface))
                    DebugHelper.LogWarning($"Could not add custom tag '{customExtendedFootstepSurface.FootstepSurface.surfaceTag}' to surface tag dictionary.", DebugType.Developer);
            }
            if (mergedSurfaces > 0)
                DebugHelper.Log($"Merged '{mergedSurfaces}' ExtendedFootstepSurface assets!", DebugType.Developer);
        }

        public static bool TryGetAndSetFootstepSurfaceIndex(Terrain terrain, int terrainLayer, PlayerControllerB player)
        {
            if (TerrainManager.TerrainFootstepsDict.TryGetValue(terrain.terrainData, out ExtendedFootstepSurface[] extendedFootsteps) && terrainLayer >= 0 && terrainLayer < extendedFootsteps.Length)
            {
                ExtendedFootstepSurface extendedFootstepSurface = extendedFootsteps[terrainLayer];
                if (extendedFootstepSurface != null)
                {
                    player.currentFootstepSurfaceIndex = extendedFootstepSurface.SurfaceIndex;
                    player.standingOnTerrain = extendedFootstepSurface.AllowSinking;
                    return true;
                }
            }
            return false;
        }

        public static bool TryGetAndSetFootstepSurfaceIndex(Terrain terrain, int terrainLayer, MaskedPlayerEnemy masked)
        {
            if (TerrainManager.TerrainFootstepsDict.TryGetValue(terrain.terrainData, out ExtendedFootstepSurface[] extendedFootsteps) && terrainLayer >= 0 && terrainLayer < extendedFootsteps.Length)
            {
                ExtendedFootstepSurface extendedFootstepSurface = extendedFootsteps[terrainLayer];
                if (extendedFootstepSurface != null && extendedFootstepSurface.AllowMaskedFootsteps)
                {
                    masked.currentFootstepSurfaceIndex = extendedFootstepSurface.SurfaceIndex;
                    return true;
                }
            }
            return false;
        }

        internal static void SwitchToUntaggedIndex(ref int currentFootstepSurfaceIndex)
        {
            if (currentFootstepSurfaceIndex < 0 || currentFootstepSurfaceIndex >= PatchedContent.ExtendedFootstepSurfaces.Count) return;
            if (PatchedContent.ExtendedFootstepSurfaces[currentFootstepSurfaceIndex].ContentType is ContentType.Custom)
                currentFootstepSurfaceIndex = (int)VanillaSurfaceTags.Untagged; // Swap tag to Untagged to avoid (harmless) error message when comparing tag.
        }
    }
}