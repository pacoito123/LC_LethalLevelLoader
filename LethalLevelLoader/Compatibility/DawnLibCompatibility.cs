using System;
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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RegisterDawnExtendedLevels()
        {
            foreach (DawnMoonInfo dawnMoonInfo in LethalContent.Moons.Values)
            {
                // Skip any vanilla or non-DawnLib moons.
                if (dawnMoonInfo.Key.IsVanilla() || dawnMoonInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                {
                    continue;
                }

                ExtendedMod dawnExtendedMod = ExtendedMod.Create(ConvertToLLLFormat(dawnMoonInfo.Key.Namespace));
                ExtendedLevel dawnExtendedLevel = ExtendedLevel.Create(dawnMoonInfo.Level);

                dawnExtendedLevel.RouteNode = dawnMoonInfo.RouteNode;
                dawnExtendedLevel.RouteConfirmNode = dawnMoonInfo.ReceiptNode;
                dawnExtendedLevel.RoutePrice = dawnMoonInfo.DawnPurchaseInfo.Cost.Provide();

                PatchedContent.AllLevelSceneNames.AddRange(dawnMoonInfo.Scenes.ConvertAll(static sceneInfo => sceneInfo.SceneName));

                dawnExtendedLevel.ContentType = ContentType.External;
                dawnExtendedLevel.Initialize(string.Empty, generateTerminalAssets: true);
                dawnExtendedLevel.name = dawnExtendedLevel.NumberlessPlanetName + "ExtendedLevel";

                // Copy all moon tags into the ExtendedLevel, in 'LLL format'.
                foreach (NamespacedKey namespacedTag in dawnMoonInfo.AllTags())
                {
                    string tag = ConvertToLLLFormat(namespacedTag.Key);

                    if (dawnExtendedLevel.TryAddTag(tag))
                    {
                        DebugHelper.Log("Added tag: " + tag, DebugType.Developer);
                    }
                }
                // ...

                // Let DawnLib handle moon configuration.
                dawnExtendedLevel.GenerateAutomaticConfigurationOptions = false;
                dawnExtendedLevel.IsRouteRemoved = true;

                PatchedContent.ExtendedLevels.Add(dawnExtendedLevel);
                dawnExtendedMod.RegisterExtendedContent(dawnExtendedLevel);
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

        /* // TODO: Extension method makes soft compat angry?
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static int CopyLLLFormatTags<T>(this DawnBaseInfo<T> dawnInfo, ExtendedContent content) where T : DawnBaseInfo<T>
        {
            int addedTags = 0;

            foreach (NamespacedKey namespacedTag in dawnInfo.AllTags())
            {
                string tag = ConvertToLLLFormat(namespacedTag.Key);

                if (content.TryAddTag(tag))
                {
                    DebugHelper.Log("Added tag: " + tag, DebugType.Developer);
                    addedTags++;
                }
            }

            return addedTags;
        } */

        internal static string ConvertToLLLFormat(string dawnFormatString)
        {
            string LLLFormatString = string.Empty;

            foreach (string word in dawnFormatString.Split('_', StringSplitOptions.RemoveEmptyEntries))
            {
                LLLFormatString += char.ToUpperInvariant(word[0]) + word.Substring(1);
            }

            return LLLFormatString;
        }
    }
}