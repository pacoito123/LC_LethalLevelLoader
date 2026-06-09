using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LethalLib.Modules;

namespace LethalLevelLoader.Compatibility
{
    internal static class LethalLibCompatibility
    {
        /// <summary>
        ///     Whether <c>LethalLib</c> is present in the BepInEx Chainloader or not.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                _enabled ??= BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("evaisa.lethallib");

                return (bool)_enabled;
            }
        }
        private static bool? _enabled;

        [HarmonyPrepare]
        private static void PrepareLethalLibCompatibility(MethodBase original)
        {
            if (original == null)
                DebugHelper.Log("LethalLib found! Enabling compatibility patches...", DebugType.User);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [HarmonyPatch(typeof(Dungeon), nameof(Dungeon.RoundManager_GenerateNewFloor)), HarmonyTranspiler, HarmonyPriority(Patches.priority)]
        internal static IEnumerable<CodeInstruction> Dungeon_GenerateNewFloor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions).MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Callvirt)); // Match 'orig(self)' call.

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogWarning("Could not match 'orig(self)' call in 'Dungeon.RoundManager_GenerateNewFloor' hook.", DebugType.User);
                return instructions;
            }

            List<CodeInstruction> origInvoke = codeMatcher.InstructionsInRange(codeMatcher.Pos, codeMatcher.Pos + 2); // Obtain invoke instructions.
            origInvoke.Add(new(OpCodes.Ret)); // Add return instruction.

            return codeMatcher.Start()
            .Insert(origInvoke) // Insert early return at the start.
            .InstructionEnumeration();
        }
    }
}