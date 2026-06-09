using System.Runtime.CompilerServices;
using HarmonyLib;
using LethalPerformance.Patches.ReferenceHolder;

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
        [HarmonyPatch(typeof(MoonCachingPatch.Patch_NavMeshSurface), nameof(MoonCachingPatch.Patch_NavMeshSurface.FindDungeon)), HarmonyPostfix, HarmonyPriority(Patches.priority)]
        internal static void MoonCachingPatchFindDungeon_Postfix(ref bool __result)
        {
            if (__result) return;

            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel == null || currentLevel.ContentType is ContentType.External || currentLevel.SelectableLevel == null || currentLevel.SelectableLevel.spawnEnemiesAndScrap == false) return;

            LevelLoader.RestoreRuntimeDungeon();
            if (Patches.RoundManager != null)
                MoonCachingPatch.s_RuntimeDungeon.SetInstance(Patches.RoundManager.dungeonGenerator);
            __result = (MoonCachingPatch.s_RuntimeDungeon.Instance != null);
        }
    }
}