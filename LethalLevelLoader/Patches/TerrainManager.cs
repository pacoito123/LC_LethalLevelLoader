using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LethalLevelLoader
{
    public static class TerrainManager
    {
        public static Terrain CurrentTerrain { get; internal set; }
        public static float[,,] CurrentTerrainAlphaMaps => (CurrentTerrain != null) ? TerrainAlphaMaps[CurrentTerrain] : null;

        public static Dictionary<Terrain, float[,,]> TerrainAlphaMaps { get; } = [];
        public static Dictionary<TerrainData, ExtendedFootstepSurface[]> TerrainFootstepsDict { get; } = [];

        internal static void BakeTerrainFootsteps()
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                TerrainData terrainData = terrain.terrainData;
                float[,,] alphaMaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
                TerrainAlphaMaps[terrain] = alphaMaps;

                int terrainLayers = alphaMaps.Length / (terrainData.alphamapWidth * terrainData.alphamapHeight);
                if (TerrainFootstepsDict.TryAdd(terrainData, new ExtendedFootstepSurface[terrainLayers]))
                {
                    List<ExtendedFootstepSurface> matchingSurfaces = PatchedContent.ExtendedFootstepSurfaces.FindAll(surface => surface.IsTerrainMatch(terrainData));
                    for (int i = 0; i < terrainLayers; i++)
                        TerrainFootstepsDict[terrainData][i] = matchingSurfaces.Find(surface => surface.IsTerrainMatch(terrainData, i));
                }
            }
        }

        internal static void CleanupTerrainFootsteps(Scene _)
        {
            SceneManager.sceneUnloaded -= CleanupTerrainFootsteps;

            CurrentTerrain = null;
            TerrainAlphaMaps.Clear();
            TerrainFootstepsDict.Clear();

            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.currentTerrainAlphaMaps = null;
                StartOfRound.Instance.gotCurrentTerrainAlphamaps = false;
            }
        }
    }
}