using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Dawn;
using Dawn.Internal;
using Dawn.Utils;
using HarmonyLib;

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

        [HarmonyPrepare]
        private static void PrepareDawnLibCompatibility(MethodBase original)
        {
            if (original == null)
                DebugHelper.Log("DawnLib found! Enabling compatibility patches...", DebugType.User);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [HarmonyPatch(typeof(DawnMoonNetworker), "DoMoonSceneLoading", MethodType.Enumerator), HarmonyTranspiler, HarmonyPriority(Patches.priority)]
        internal static IEnumerable<CodeInstruction> DawnMoonNetworker_QueueMoonSceneLoadingClientRpc_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            MethodInfo playerSetBundleStateRpcInfo = typeof(DawnMoonNetworker).GetMethod(nameof(DawnMoonNetworker.PlayerSetBundleStateRpc), BindingFlags.Instance | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).End().MatchBack(useEnd: false,
                new(OpCodes.Ldc_I4_4),
                new(OpCodes.Call, playerSetBundleStateRpcInfo));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match local player 'BundleState.Done' assignment.", DebugType.User);
                return instructions;
            }

            MethodInfo isCurrentMoonLLLInfo = typeof(DawnLibCompatibility).GetMethod(nameof(IsCurrentMoonLLL), BindingFlags.Static | BindingFlags.NonPublic);
            return codeMatcher.SetInstructionAndAdvance(new(OpCodes.Call, isCurrentMoonLLLInfo))
            .InsertAndAdvance(
                new(OpCodes.Brfalse),
                new(OpCodes.Pop),
                new(OpCodes.Pop),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Ret),
                new(OpCodes.Ldc_I4_4))
            .Advance(-1)
            .CreateLabel(out Label nonLLLMoonTarget)
            .Advance(-5)
            .SetOperandAndAdvance(nonLLLMoonTarget)
            .InstructionEnumeration();
        }

        private static bool IsCurrentMoonLLL()
        {
            if (LevelManager.CurrentExtendedLevel != null && LevelManager.CurrentExtendedLevel.ContentType is ContentType.Custom)
            {
                bool loadedStatus = NetworkBundleManager.Instance != null && NetworkBundleManager.Instance.GetLoadStatus(LevelManager.CurrentExtendedLevel);
                RefreshLocalClientBundleState(loadedStatus ? 4 : 2); // Done, Loading

                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RegisterDawnExtendedLevels()
        {
            foreach (DawnMoonInfo dawnMoonInfo in LethalContent.Moons.Values)
            {
                // Skip any vanilla or non-DawnLib moons.
                if (dawnMoonInfo == null || dawnMoonInfo.Level == null || dawnMoonInfo.Key.IsVanilla() || dawnMoonInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                    continue;
                ExtendedLevel dawnExtendedLevel = ExtendedLevel.Create(dawnMoonInfo.Level);
                dawnExtendedLevel.name = dawnExtendedLevel.NumberlessPlanetName + "ExtendedLevel";

                dawnExtendedLevel.RouteNode = dawnMoonInfo.RouteNode;
                dawnExtendedLevel.RouteConfirmNode = dawnMoonInfo.ReceiptNode;
                dawnExtendedLevel.RoutePrice = dawnMoonInfo.DawnPurchaseInfo.Cost.Provide();

                PatchedContent.AllLevelSceneNames.AddRange(dawnMoonInfo.Scenes.ConvertAll(static sceneInfo => sceneInfo.SceneName));

                dawnExtendedLevel.Initialize(generateTerminalAssets: true);

                // Let DawnLib handle moon configuration:
                dawnExtendedLevel.GenerateAutomaticConfigurationOptions = false;
                dawnExtendedLevel.IsRouteRemoved = true;
                dawnExtendedLevel.OverrideDynamicRiskLevelAssignment = true;
                dawnExtendedLevel.UseTerrainFootsteps = true;
                // ...

                RegisterDawnExtendedContent(dawnMoonInfo, dawnMoonInfo.Key.Namespace, dawnExtendedLevel);
                PatchedContent.ExtendedLevels.Add(dawnExtendedLevel);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RegisterDawnExtendedEnemyTypes()
        {
            foreach (DawnEnemyInfo dawnEnemyInfo in LethalContent.Enemies.Values)
            {
                // Skip any vanilla or non-DawnLib EnemyTypes.
                if (dawnEnemyInfo == null || dawnEnemyInfo.EnemyType == null || dawnEnemyInfo.Key.IsVanilla() || dawnEnemyInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                    continue;
                ExtendedEnemyType dawnExtendedEnemyType = ExtendedEnemyType.Create(dawnEnemyInfo.EnemyType);
                dawnExtendedEnemyType.name = ConvertToLLLFormat(dawnEnemyInfo.Key.Key) + "ExtendedEnemyType";

                if (dawnEnemyInfo.BestiaryNode != null)
                {
                    dawnExtendedEnemyType.EnemyID = dawnEnemyInfo.BestiaryNode.creatureFileID;
                    dawnExtendedEnemyType.EnemyInfoNode = dawnEnemyInfo.BestiaryNode;
                    dawnExtendedEnemyType.InfoNodeDescription = dawnEnemyInfo.BestiaryNode.displayText;
                    dawnExtendedEnemyType.InfoNodeVideoClip = dawnEnemyInfo.BestiaryNode.displayVideo;

                    if (dawnExtendedEnemyType.EnemyType.enemyPrefab != null)
                    {
                        ScanNodeProperties[] allEnemyScanNodes = dawnExtendedEnemyType.EnemyType.enemyPrefab.GetComponentsInChildren<ScanNodeProperties>(includeInactive: true);
                        ScanNodeProperties enemyScanNode = Array.Find(allEnemyScanNodes, scanNode => scanNode.creatureScanID == dawnExtendedEnemyType.EnemyID);
                        if (enemyScanNode != null)
                        {
                            dawnExtendedEnemyType.ScanNodeProperties = enemyScanNode;
                            dawnExtendedEnemyType.EnemyDisplayName = enemyScanNode.headerText;
                        }
                    }
                }
                if (string.IsNullOrEmpty(dawnExtendedEnemyType.EnemyDisplayName))
                    dawnExtendedEnemyType.EnemyDisplayName = dawnEnemyInfo.EnemyType.enemyName;
                dawnExtendedEnemyType.Initialize();

                RegisterDawnExtendedContent(dawnEnemyInfo, dawnEnemyInfo.Key.Namespace, dawnExtendedEnemyType);
                PatchedContent.ExtendedEnemyTypes.Add(dawnExtendedEnemyType);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RegisterDawnExtendedUnlockableItems()
        {
            foreach (DawnUnlockableItemInfo dawnUnlockableItemInfo in LethalContent.Unlockables.Values)
            {
                // Skip any vanilla or non-DawnLib UnlockableItems.
                if (dawnUnlockableItemInfo == null || dawnUnlockableItemInfo.UnlockableItem == null || dawnUnlockableItemInfo.Key.IsVanilla() || dawnUnlockableItemInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                    continue;
                ExtendedUnlockableItem dawnExtendedUnlockableItem = ExtendedUnlockableItem.Create(dawnUnlockableItemInfo.UnlockableItem);
                dawnExtendedUnlockableItem.name = ConvertToLLLFormat(dawnUnlockableItemInfo.Key.Key) + "ExtendedUnlockableItem";

                dawnExtendedUnlockableItem.BuyNode = dawnUnlockableItemInfo.RequestNode;
                dawnExtendedUnlockableItem.BuyConfirmNode = dawnUnlockableItemInfo.ConfirmNode;
                dawnExtendedUnlockableItem.BuyInfoNode = dawnUnlockableItemInfo.InfoNode;

                if (dawnUnlockableItemInfo.DawnPurchaseInfo != null && dawnUnlockableItemInfo.DawnPurchaseInfo.Cost != null)
                    dawnExtendedUnlockableItem.ItemCost = dawnUnlockableItemInfo.DawnPurchaseInfo.Cost.Provide();
                // dawnExtendedUnlockableItem.Initialize();

                RegisterDawnExtendedContent(dawnUnlockableItemInfo, dawnUnlockableItemInfo.Key.Namespace, dawnExtendedUnlockableItem);
                PatchedContent.ExtendedUnlockableItems.Add(dawnExtendedUnlockableItem);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void ConvertDawnExtendedDungeonFlows()
        {
            foreach (DawnDungeonInfo dawnDungeonInfo in LethalContent.Dungeons.Values)
            {
                // Skip any vanilla or non-DawnLib Items.
                if (dawnDungeonInfo == null || dawnDungeonInfo.DungeonFlow == null || dawnDungeonInfo.Key.IsVanilla() || dawnDungeonInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                    continue;

                ExtendedDungeonFlow dawnExtendedDungeonFlow = PatchedContent.ExtendedDungeonFlows.Find(extendedDungeonFlow => extendedDungeonFlow.DungeonFlow == dawnDungeonInfo.DungeonFlow);
                if (dawnExtendedDungeonFlow == null)
                    continue;

                dawnExtendedDungeonFlow.DungeonName = dawnDungeonInfo.DungeonFlow.name.Replace("Flow", string.Empty, StringComparison.OrdinalIgnoreCase);
                dawnExtendedDungeonFlow.name = dawnExtendedDungeonFlow.DungeonName.RemoveWhitespace() + "ExtendedDungeonFlow";

                dawnExtendedDungeonFlow.MapTileSize = dawnDungeonInfo.MapTileSize;
                if (dawnDungeonInfo.StingerDetail != null)
                    dawnExtendedDungeonFlow.FirstTimeDungeonAudio = dawnDungeonInfo.StingerDetail.FirstTimeAudio;
                if (dawnDungeonInfo.DungeonClampRange != null)
                {
                    dawnExtendedDungeonFlow.IsDynamicDungeonSizeRestrictionEnabled = true;
                    dawnExtendedDungeonFlow.DynamicDungeonSizeMinMax = new(dawnDungeonInfo.DungeonClampRange.Min, dawnDungeonInfo.DungeonClampRange.Max);
                }
                dawnExtendedDungeonFlow.GenerateAutomaticConfigurationOptions = false;

                if (OriginalContent.DungeonFlows.Remove(dawnExtendedDungeonFlow.DungeonFlow))
                    RegisterDawnExtendedContent(dawnDungeonInfo, dawnDungeonInfo.Key.Namespace, dawnExtendedDungeonFlow);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void ConvertDawnExtendedItems()
        {
            foreach (DawnItemInfo dawnItemInfo in LethalContent.Items.Values)
            {
                // Skip any vanilla or non-DawnLib Items.
                if (dawnItemInfo == null || dawnItemInfo.Item == null || dawnItemInfo.Key.IsVanilla() || dawnItemInfo.HasTag(NamespacedKey.From("dawn_lib", "is_external")))
                    continue;

                ExtendedItem dawnExtendedItem = PatchedContent.ExtendedItems.Find(extendedItem => extendedItem.Item == dawnItemInfo.Item);
                if (dawnExtendedItem == null)
                    continue;
                dawnExtendedItem.name = ConvertToLLLFormat(dawnItemInfo.Key.Key) + "ExtendedItem";

                if (OriginalContent.Items.Remove(dawnExtendedItem.Item))
                    RegisterDawnExtendedContent(dawnItemInfo, dawnItemInfo.Key.Namespace, dawnExtendedItem);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void ConvertDawnExtendedFootstepSurfaces()
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
                dawnExtendedFootstepSurface.name = ConvertToLLLFormat(dawnSurfaceInfo.Key.Key) + "ExtendedFootstepSurface";

                dawnExtendedFootstepSurface.SurfaceIndex = dawnSurfaceInfo.SurfaceIndex;
                dawnExtendedFootstepSurface.AllowEarthLeviathanEmerge = dawnSurfaceInfo.IsNatural;
                dawnExtendedFootstepSurface.AllowSinking = dawnSurfaceInfo.QuicksandCompatible;

                FootstepSurfaceManager.surfaceTagExtendedFootstepDict[$"{dawnSurfaceInfo.Key}"] = dawnExtendedFootstepSurface;

                if (OriginalContent.FootstepSurfaces.Remove(dawnExtendedFootstepSurface.FootstepSurface))
                    RegisterDawnExtendedContent(dawnSurfaceInfo, dawnSurfaceInfo.Key.Namespace, dawnExtendedFootstepSurface);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RegisterDawnExtendedContent(object dawnBaseInfo, string dawnNamespace, ExtendedContent extendedContent)
        {
            if (extendedContent.ContentType is ContentType.Vanilla)
                PatchedContent.VanillaMod.UnregisterExtendedContent(extendedContent);
            extendedContent.ContentType = ContentType.External;
            extendedContent.ContentTags = [ExtendedMod.CustomContentTag];

            CopyContentTags(dawnBaseInfo, extendedContent);

            if (!dawnExtendedModsDict.TryGetValue(dawnNamespace, out ExtendedMod dawnExtendedMod))
            {
                dawnExtendedMod = ExtendedMod.Create(ConvertToLLLFormat(dawnNamespace), dawnNamespace);
                dawnExtendedMod.ModNameAliases.Add(dawnNamespace);
                dawnExtendedMod.ModMergeSetting = ModMergeSetting.MatchingModName;

                dawnExtendedModsDict.Add(dawnNamespace, dawnExtendedMod);
                PatchedContent.ExtendedMods.Add(dawnExtendedMod);
            }
            dawnExtendedMod.RegisterExtendedContent(extendedContent);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void RefreshLocalClientBundleState(int state)
        {
            if (GameNetworkManager.Instance == null || GameNetworkManager.Instance.localPlayerController == null) return;
            PlayerControllerReference player = GameNetworkManager.Instance.localPlayerController;

            if (StartOfRound.Instance == null || !StartOfRound.Instance.inShipPhase) return;
            if (DawnMoonNetworker.Instance != null && Enum.IsDefined(typeof(DawnMoonNetworker.BundleState), state))
                DawnMoonNetworker.Instance.PlayerSetBundleStateRpc(player, (DawnMoonNetworker.BundleState)state);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void CopyContentTags(object dawnBaseInfo, ExtendedContent extendedContent)
        {
            if (dawnBaseInfo is not ITaggable taggable) return;

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