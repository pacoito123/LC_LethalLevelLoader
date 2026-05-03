using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class FootstepSurfaceManager
    {
        public static readonly Dictionary<string, ExtendedFootstepSurface> surfaceTagExtendedFootstepDict = [];

        internal static void PatchVanillaFootstepSurfaceLists()
        {
            List<FootstepSurface> footstepSurfaces = new(PatchedContent.ExtendedFootstepSurfaces.Count);
            foreach (ExtendedFootstepSurface extendedFootstepSurface in PatchedContent.ExtendedFootstepSurfaces)
            {
                extendedFootstepSurface.SurfaceIndex = footstepSurfaces.Count;
                footstepSurfaces.Add(extendedFootstepSurface.FootstepSurface);
            }
            Patches.StartOfRound.footstepSurfaces = [.. footstepSurfaces];
        }

        internal static void MergeExtendedFootstepSurfaces()
        {
            foreach (ExtendedFootstepSurface vanillaExtendedFootstepSurface in PatchedContent.VanillaExtendedFootstepSurfaces)
                if (!surfaceTagExtendedFootstepDict.TryAdd(vanillaExtendedFootstepSurface.FootstepSurface.surfaceTag, vanillaExtendedFootstepSurface))
                    DebugHelper.LogWarning($"Could not add vanilla tag '{vanillaExtendedFootstepSurface.FootstepSurface.surfaceTag}' to surface tag dictionary.", DebugType.Developer);

            int mergedSurfaces = 0;
            foreach (ExtendedFootstepSurface customExtendedFootstepSurface in PatchedContent.CustomExtendedFootstepSurfaces)
            {
                if ((customExtendedFootstepSurface.UseVanillaTag is not VanillaSurfaceTags.None
                    && surfaceTagExtendedFootstepDict.TryGetValue($"{customExtendedFootstepSurface.UseVanillaTag}", out ExtendedFootstepSurface existingFootstepSurface))
                    || surfaceTagExtendedFootstepDict.TryGetValue(customExtendedFootstepSurface.FootstepSurface.surfaceTag, out existingFootstepSurface))
                {
                    existingFootstepSurface.AssociatedTerrains.AddRange(customExtendedFootstepSurface.AssociatedTerrains);
                    Object.Destroy(customExtendedFootstepSurface); // TODO: Add to a List to destroy later perhaps.
                    mergedSurfaces++;
                    continue;
                }
                if (!surfaceTagExtendedFootstepDict.TryAdd(customExtendedFootstepSurface.FootstepSurface.surfaceTag, customExtendedFootstepSurface))
                    DebugHelper.LogWarning($"Could not add custom tag '{customExtendedFootstepSurface.FootstepSurface.surfaceTag}' to surface tag dictionary.", DebugType.Developer);
            }
            if (mergedSurfaces > 0)
                DebugHelper.Log($"Merged '{mergedSurfaces}' ExtendedFootstepSurface assets!", DebugType.Developer);

            PatchedContent.ExtendedFootstepSurfaces = [.. surfaceTagExtendedFootstepDict.Values];
            foreach (ExtendedFootstepSurface extendedFootstepSurface in PatchedContent.ExtendedFootstepSurfaces)
                extendedFootstepSurface.RefreshAssociatedTerrainNames();
        }

        public static bool TryGetFootstepSurfaceIndex(TerrainData terrainData, int terrainLayer, out int footstepSurfaceIndex, out bool allowSinking)
        {
            footstepSurfaceIndex = -1;
            allowSinking = true;
            if (TerrainManager.TerrainFootstepsDict.TryGetValue(terrainData, out ExtendedFootstepSurface[] extendedFootsteps)
                && terrainLayer < extendedFootsteps.Length)
            {
                ExtendedFootstepSurface extendedFootstepSurface = extendedFootsteps[terrainLayer];
                if (extendedFootstepSurface != null)
                {
                    footstepSurfaceIndex = extendedFootstepSurface.SurfaceIndex;
                    allowSinking = extendedFootstepSurface.AllowSinking;
                }
            }
            return (footstepSurfaceIndex > 0);
        }
    }
}