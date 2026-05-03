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

        internal static void SwapTerrainAlphaMap(Terrain terrain)
        {
            if (CurrentTerrain == terrain) return;
            CurrentTerrain = terrain;

            if (!TerrainAlphaMaps.TryGetValue(terrain, out float[,,] alphaMaps))
            {
                TerrainData terrainData = terrain.terrainData;
                alphaMaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
                TerrainAlphaMaps[terrain] = alphaMaps;
            }

            StartOfRound.Instance.currentTerrainAlphaMaps = alphaMaps;
            StartOfRound.Instance.gotCurrentTerrainAlphamaps = alphaMaps != null;
        }

        public static int ObtainTerrainLayerAtPoint(Vector3 point, Terrain terrain)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPos = terrain.transform.position;

            Vector3 splatMapCoordinate = Vector3.zero;
            splatMapCoordinate.x = (point.x - terrainPos.x) / terrainData.size.x * terrainData.alphamapWidth;
            splatMapCoordinate.z = (point.z - terrainPos.z) / terrainData.size.z * terrainData.alphamapHeight;

            if (!TerrainAlphaMaps.TryGetValue(terrain, out float[,,] terrainAlphaMaps))
                return -1;

            float largestLayerBlend = 0.0f;
            int terrainLayer = -1;
            int terrainLayers = terrainAlphaMaps.Length / (terrainData.alphamapWidth * terrainData.alphamapHeight);
            for (int i = 0; i < terrainLayers; i++)
            {
                float currentLayerBlend = terrainAlphaMaps[(int)splatMapCoordinate.z, (int)splatMapCoordinate.x, i];
                if (currentLayerBlend == 1.0f)
                {
                    terrainLayer = i;
                    break;
                }
                if (largestLayerBlend < currentLayerBlend)
                {
                    largestLayerBlend = currentLayerBlend;
                    terrainLayer = i;
                }
            }
            return terrainLayer;
        }

        public static bool TryObtainFootstepSurfaceAtPoint(Vector3 point, Terrain terrain, out ExtendedFootstepSurface footstepSurface)
        {
            footstepSurface = null;

            int terrainLayer = ObtainTerrainLayerAtPoint(point, terrain);
            if (terrainLayer == -1)
                return false;

            if (!TerrainFootstepsDict.TryGetValue(terrain.terrainData, out ExtendedFootstepSurface[] extendedFootsteps)
                || terrainLayer >= extendedFootsteps.Length)
            {
                return false;
            }

            footstepSurface = extendedFootsteps[terrainLayer];
            return footstepSurface != null;
        }

        public static bool CanWormEmergeFromPoint(Vector3 point, Terrain terrain)
        {
            return TryObtainFootstepSurfaceAtPoint(point, terrain, out ExtendedFootstepSurface footstepSurface)
                && footstepSurface.AllowEarthLeviathanEmerge;
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