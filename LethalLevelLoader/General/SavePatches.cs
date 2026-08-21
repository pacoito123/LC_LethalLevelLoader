using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace LethalLevelLoader
{
    internal static class SavePatches
    {
        public const string saveDataName = "LCSaveFile";
        public const string saveDataExtension = ".moddata";

        [HarmonyPatch(typeof(ES3), nameof(ES3.DeleteFile), [typeof(ES3Settings)]), HarmonyPostfix, HarmonyPriority(Patches.priority)]
        internal static void DeleteFile_Postfix(ES3Settings settings)
        {
            if (settings.location is not ES3.Location.File)
                return;
            string filePath = settings.FullPath;

            // Trim file name from full path.
            string fileName = filePath[(filePath.LastIndexOf(Path.AltDirectorySeparatorChar) + 1)..];

            // Check if file name starts with 'LCSaveFile' and does NOT end with '.moddata'.
            if (!fileName.StartsWith(saveDataName, StringComparison.Ordinal) || fileName.EndsWith(saveDataExtension, StringComparison.Ordinal))
                return;

            string fileDataPath = filePath + saveDataExtension;
            if (File.Exists(fileDataPath))
            {
                try
                {
                    File.Delete(fileDataPath);
                }
                catch (Exception e)
                {
                    DebugHelper.LogError($"Could not delete save data file '{fileName}': {e}", DebugType.User);
                }
            }
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