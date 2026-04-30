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
            int mergedSurfaces = 0;

            foreach (ExtendedFootstepSurface vanillaExtendedFootstepSurface in PatchedContent.VanillaExtendedFootstepSurfaces)
                if (!surfaceTagExtendedFootstepDict.TryAdd(vanillaExtendedFootstepSurface.FootstepSurface.surfaceTag, vanillaExtendedFootstepSurface))
                    DebugHelper.LogWarning("", DebugType.Developer);

            foreach (ExtendedFootstepSurface customExtendedFootstepSurface in PatchedContent.CustomExtendedFootstepSurfaces)
            {
                string vanillaName = customExtendedFootstepSurface.FootstepSurface.surfaceTag.Split('/')[^1];
                if (surfaceTagExtendedFootstepDict.TryGetValue(vanillaName, out ExtendedFootstepSurface existingFootstepSurface)
                    || surfaceTagExtendedFootstepDict.TryGetValue(customExtendedFootstepSurface.FootstepSurface.surfaceTag, out existingFootstepSurface))
                {
                    existingFootstepSurface.AssociatedTerrains.AddRange(customExtendedFootstepSurface.AssociatedTerrains);
                    Object.Destroy(customExtendedFootstepSurface); // TODO: Add to a List to destroy later perhaps.
                    mergedSurfaces++;
                    continue;
                }
                if (!surfaceTagExtendedFootstepDict.TryAdd(customExtendedFootstepSurface.FootstepSurface.surfaceTag, customExtendedFootstepSurface))
                    DebugHelper.LogWarning("", DebugType.Developer);
            }

            PatchedContent.ExtendedFootstepSurfaces = [.. surfaceTagExtendedFootstepDict.Values];
            PatchedContent.ExtendedFootstepSurfaces.ForEach(static surface => surface.RefreshAssociatedTerrainNames());
        }

        public static bool TryGetFootstepSurfaceIndex(TerrainData terrainData, int terrainLayer, out int footstepSurfaceIndex)
        {
            footstepSurfaceIndex = -1;
            if (TerrainManager.TerrainFootstepsDict.TryGetValue(terrainData, out ExtendedFootstepSurface[] extendedFootsteps))
                if (terrainLayer < extendedFootsteps.Length)
                    footstepSurfaceIndex = extendedFootsteps[terrainLayer].SurfaceIndex;
            return (footstepSurfaceIndex > 0);
        }
    }
}