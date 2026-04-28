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

        internal static void CleanupTerrainFootsteps(Scene _)
        {
            SceneManager.sceneUnloaded -= CleanupTerrainFootsteps;

            CurrentTerrain = null;
            TerrainAlphaMaps.Clear();

            if (StartOfRound.Instance != null)
            {
                StartOfRound.Instance.currentTerrainAlphaMaps = null;
                StartOfRound.Instance.gotCurrentTerrainAlphamaps = false;
            }
        }
    }
}