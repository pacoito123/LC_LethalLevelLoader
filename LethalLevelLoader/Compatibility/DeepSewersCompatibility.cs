using System;
using DunGen;

namespace LethalLevelLoader.Compatibility
{
    internal static class DeepSewersCompatibility
    {
        /// <summary>
        ///     Whether <c>WesleysInteriors</c> is present in the BepInEx Chainloader or not.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                _enabled ??= BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("MW.MagicWesleyInteriors");

                return (bool)_enabled;
            }
        }
        private static bool? _enabled;

        internal static void FixDeepSewersGeneration()
        {
            AssetBundleLoader.AddOnExtendedModLoadedListener(extendedMod =>
            {
                ExtendedDungeonFlow deepSewersExtendedFlow = extendedMod.ExtendedDungeonFlows.Find(extendedDungeonFlow =>
                    string.Equals(extendedDungeonFlow.name, "DeepSewersExtended", StringComparison.Ordinal));
                if (deepSewersExtendedFlow != null)
                {
                    Tile startTile = deepSewersExtendedFlow.AllTiles[0];
                    if (startTile != null)
                    {
                        startTile.OverrideAutomaticTileBounds = false; // Needs to be disabled in order to be able to generate.
                        startTile.RecalculateBounds();
                    }
                }
            }, "Magic Wesley", "Magic WesleysMod");
        }
    }
}