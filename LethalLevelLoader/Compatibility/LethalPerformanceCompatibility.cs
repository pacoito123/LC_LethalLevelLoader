using System.Runtime.CompilerServices;
using HarmonyLib;
using LethalPerformance.Patches.ReferenceHolder;
using UnityEngine.SceneManagement;

namespace LethalLevelLoader.Compatibility
{
    internal static class LethalPerformanceCompatibility
    {
        /// <summary>
        ///     Whether <c>LethalPerformance</c> is present in the BepInEx Chainloader or not.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                _enabled ??= BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("LethalPerformance");
                return (bool)_enabled;
            }
        }
        private static bool? _enabled;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [HarmonyPatch(typeof(MoonCachingPatch.Patch_NavMeshSurface), nameof(MoonCachingPatch.Patch_NavMeshSurface.FindDropship)), HarmonyPrefix, HarmonyPriority(Patches.priority)]
        internal static void MoonCachingPatchFindDropship_Prefix(ref Scene scene)
        {
            if (string.Equals(scene.name, "SampleSceneRelay", System.StringComparison.Ordinal)) return;
            LevelLoader.currentLevelScene = scene;

            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel == null || currentLevel.ContentType is ContentType.External || currentLevel.SelectableLevel == null) return;

            LevelLoader.ValidateItemShipContainer();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [HarmonyPatch(typeof(MoonCachingPatch.Patch_NavMeshSurface), nameof(MoonCachingPatch.Patch_NavMeshSurface.FindDungeon)), HarmonyPrefix, HarmonyPriority(Patches.priority)]
        internal static void MoonCachingPatchFindDungeon_Prefix(ref Scene scene)
        {
            if (string.Equals(scene.name, "SampleSceneRelay", System.StringComparison.Ordinal)) return;
            LevelLoader.currentLevelScene = scene;

            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel == null || currentLevel.ContentType is ContentType.External || currentLevel.SelectableLevel == null || currentLevel.SelectableLevel.spawnEnemiesAndScrap == false) return;

            LevelLoader.RestoreRuntimeDungeon();
        }
    }
}