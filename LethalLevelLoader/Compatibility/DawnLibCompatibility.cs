using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dawn;
using Dawn.Internal;
using Dawn.Utils;

namespace LethalLevelLoader.Compatibility
{
    internal static class DawnLibCompatibility
    {
        /// <summary>
        ///     Whether <c>DawnLib</c> is present in the BepInEx Chainloader or not.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                _enabled ??= BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.github.teamxiaolan.dawnlib");

                return (bool)_enabled;
            }
        }
        private static bool? _enabled;

        private static readonly Dictionary<string, ExtendedMod> dawnExtendedModsDict = [];

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RegisterDawnExtendedLevels()
        {
            foreach (DawnMoonInfo dawnMoonInfo in LethalContent.Moons.Values)
            {
                // Skip any vanilla or non-DawnLib moons.
                if (dawnMoonInfo == null || dawnMoonInfo.Key.IsVanilla() || dawnMoonInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                    continue;
                ExtendedLevel dawnExtendedLevel = ExtendedLevel.Create(dawnMoonInfo.Level);

                dawnExtendedLevel.RouteNode = dawnMoonInfo.RouteNode;
                dawnExtendedLevel.RouteConfirmNode = dawnMoonInfo.ReceiptNode;
                dawnExtendedLevel.RoutePrice = dawnMoonInfo.DawnPurchaseInfo.Cost.Provide();

                PatchedContent.AllLevelSceneNames.AddRange(dawnMoonInfo.Scenes.ConvertAll(static sceneInfo => sceneInfo.SceneName));

                dawnExtendedLevel.ContentType = ContentType.External;
                dawnExtendedLevel.Initialize(generateTerminalAssets: true);
                dawnExtendedLevel.name = dawnExtendedLevel.NumberlessPlanetName + "ExtendedLevel";

                CopyContentTags(dawnMoonInfo, dawnExtendedLevel);

                // Let DawnLib handle moon configuration.
                dawnExtendedLevel.GenerateAutomaticConfigurationOptions = false;
                dawnExtendedLevel.IsRouteRemoved = true;

                if (!dawnExtendedModsDict.TryGetValue(dawnMoonInfo.Key.Namespace, out ExtendedMod dawnExtendedMod))
                {
                    dawnExtendedMod = ExtendedMod.Create(ConvertToLLLFormat(dawnMoonInfo.Key.Namespace));
                    dawnExtendedModsDict.Add(dawnMoonInfo.Key.Namespace, dawnExtendedMod);
                    PatchedContent.ExtendedMods.Add(dawnExtendedMod);
                }
                dawnExtendedMod.RegisterExtendedContent(dawnExtendedLevel);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void ConvertDawnExtendedFootstepSurfaces() // TODO: This but for every ExtendedContent type.
        {
            foreach (DawnSurfaceInfo dawnSurfaceInfo in LethalContent.Surfaces.Values)
            {
                // Skip any vanilla or non-DawnLib FootstepSurfaces.
                if (dawnSurfaceInfo == null || dawnSurfaceInfo.Key.IsVanilla() || dawnSurfaceInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                    continue;
                if (dawnSurfaceInfo.SurfaceIndex < 0 || dawnSurfaceInfo.SurfaceIndex >= PatchedContent.ExtendedFootstepSurfaces.Count)
                    continue;

                ExtendedFootstepSurface dawnExtendedFootstepSurface = PatchedContent.ExtendedFootstepSurfaces[dawnSurfaceInfo.SurfaceIndex];
                if (dawnExtendedFootstepSurface == null)
                    continue;

                if (dawnExtendedFootstepSurface.ContentType is ContentType.Vanilla)
                {
                    OriginalContent.FootstepSurfaces.Remove(dawnExtendedFootstepSurface.FootstepSurface);
                    PatchedContent.VanillaMod.UnregisterExtendedContent(dawnExtendedFootstepSurface);
                }
                dawnExtendedFootstepSurface.ContentType = ContentType.External;

                string dawnKey = ConvertToLLLFormat(dawnSurfaceInfo.Key.Key);
                dawnExtendedFootstepSurface.name = dawnKey + "ExtendedFootstepSurface";
                dawnExtendedFootstepSurface.ContentTags[0] = ExtendedMod.CustomContentTag;

                FootstepSurfaceManager.surfaceTagExtendedFootstepDict[$"{dawnSurfaceInfo.Key}"] = dawnExtendedFootstepSurface;
                CopyContentTags(dawnSurfaceInfo, dawnExtendedFootstepSurface);

                dawnExtendedFootstepSurface.SurfaceIndex = dawnSurfaceInfo.SurfaceIndex;
                dawnExtendedFootstepSurface.AllowEarthLeviathanEmerge = dawnSurfaceInfo.IsNatural;
                dawnExtendedFootstepSurface.AllowSinking = dawnSurfaceInfo.QuicksandCompatible;

                if (!dawnExtendedModsDict.TryGetValue(dawnSurfaceInfo.Key.Namespace, out ExtendedMod dawnExtendedMod))
                {
                    dawnExtendedMod = ExtendedMod.Create(ConvertToLLLFormat(dawnSurfaceInfo.Key.Namespace));
                    dawnExtendedModsDict.Add(dawnSurfaceInfo.Key.Namespace, dawnExtendedMod);
                    PatchedContent.ExtendedMods.Add(dawnExtendedMod);
                }
                dawnExtendedMod.RegisterExtendedContent(dawnExtendedFootstepSurface);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RefreshLocalClientBundleState(int state)
        {
            if (GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null) return;
            PlayerControllerReference player = GameNetworkManager.Instance.localPlayerController;

            if (DawnMoonNetworker.Instance != null && Enum.IsDefined(typeof(DawnMoonNetworker.BundleState), state))
                DawnMoonNetworker.Instance.PlayerSetBundleStateRpc(player, (DawnMoonNetworker.BundleState)state);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void CopyContentTags(ITaggable taggable, ExtendedContent extendedContent)
        {
            foreach (NamespacedKey namespacedTag in taggable.AllTags())
            {
                string tag = ConvertToLLLFormat(namespacedTag.Key);

                if (extendedContent.TryAddTag(tag))
                    DebugHelper.Log($"Added tag: {tag} to {extendedContent.name}", DebugType.Developer);
            }
        }

        internal static string ConvertToLLLFormat(string dawnFormatString)
        {
            string LLLFormatString = string.Empty;

            foreach (string word in dawnFormatString.Split('_', StringSplitOptions.RemoveEmptyEntries))
                LLLFormatString += char.ToUpperInvariant(word[0]) + word[1..];

            return (LLLFormatString);
        }
    }
}