using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace LethalLevelLoader
{
    internal static class SavePatches
    {
        [HarmonyPatch(typeof(DeleteFileButton), nameof(DeleteFileButton.DeleteFile)), HarmonyPostfix, HarmonyPriority(Patches.priority)]
        internal static void DeleteFile_Postfix(int ___fileToDelete)
        {
            string saveName = $"LCSaveFile{___fileToDelete}.moddata";
            if (ES3.FileExists(saveName))
                ES3.DeleteFile(saveName);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.AutoSaveShipData))]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveGameValues)), HarmonyPostfix, HarmonyPriority(Patches.priority)]
        internal static void GameNetworkManagerSaveGameValues_Postfix(GameNetworkManager __instance)
        {
            // Vanilla checks
            if (!__instance.isHostingGame || !Patches.StartOfRound.inShipPhase || Patches.StartOfRound.isChallengeFile)
                return;
            SaveManager.SaveGameValues();
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetSavedGameValues)), HarmonyPostfix, HarmonyPriority(Patches.priority)]
        internal static void ResetSavedGameValues_Postfix()
        {
            SaveManager.currentSaveFile?.Reset();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadShipGrabbableItems)), HarmonyPrefix, HarmonyPriority(Patches.priority)]
        internal static void StartOfRoundLoadShipGrabbableItems_Prefix()
        {
            SaveManager.LoadShipGrabbableItems();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadPlanetsMoldSpreadData))]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetSavedGameValues))]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveGameValues))]
        [HarmonyPatch(typeof(MoldSpreadManager), nameof(MoldSpreadManager.Start)), HarmonyTranspiler, HarmonyPriority(Patches.priority)]
        internal static IEnumerable<CodeInstruction> MoldSaveData_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo gameObjectGetter = typeof(GameObject).GetProperty(nameof(GameObject.gameObject), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            MethodInfo objectNameGetter = typeof(UnityEngine.Object).GetProperty(nameof(UnityEngine.Object.name), BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            FieldInfo levelIDInfo = typeof(SelectableLevel).GetField(nameof(SelectableLevel.levelID), BindingFlags.Instance | BindingFlags.Public);

            return new CodeMatcher(instructions).MatchForward(useEnd: true,
                new CodeMatch(OpCodes.Ldstr))
            .Repeat(matcher =>
                {
                    string saveKey = $"{matcher.Operand}";
                    if (saveKey.StartsWith("Level{0}", StringComparison.Ordinal))
                    {
                        matcher.SearchForward(ci => ci.Is(OpCodes.Ldfld, levelIDInfo)) // Skip to `SelectableLevel.levelID`.
                            .SetAndAdvance(OpCodes.Callvirt, gameObjectGetter)
                            .SetAndAdvance(OpCodes.Callvirt, objectNameGetter);
                        return;
                    }
                    matcher.Advance(1);
                })
            .InstructionEnumeration();
        }
    }
}