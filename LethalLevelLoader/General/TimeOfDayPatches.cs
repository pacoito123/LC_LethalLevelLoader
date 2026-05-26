using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace LethalLevelLoader
{
    /* MIT License

    Copyright (c) 2024 WhiteSpike

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE. */

    /// <summary>
    ///     Largely based from <c>Moon Day Speed Multiplier Patcher</c> by <c>WhiteSpike</c>, licensed under the <c>MIT License</c> (shown above).
    /// </summary>
    /// <see href="https://thunderstore.io/c/lethal-company/p/WhiteSpike/Moon_Day_Speed_Multiplier_Patcher"/>
    /// <seealso href="https://github.com/WhiteSpike/Moon-Day-Speed-Multiplier-Patcher/blob/6f646ce8bc74b8e8bd56a61cbe13df7908bf2734/LICENSE"/>
    internal static class TimeOfDayPatches
    {
        [HarmonyPrepare]
        private static void PrepareTimeOfDayPatches(MethodBase original)
        {
            if (original == null)
                DebugHelper.Log("Patching TimeOfDay speed multipliers!", DebugType.User);
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.MoveGlobalTime)), HarmonyTranspiler, HarmonyPriority(Patches.priority)]
        internal static IEnumerable<CodeInstruction> TimeOfDayMoveGlobalTime_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo globalTimeSpeedMultiplierInfo = typeof(TimeOfDay).GetField(nameof(TimeOfDay.globalTimeSpeedMultiplier), BindingFlags.Instance | BindingFlags.Public);
            FieldInfo daySpeedMultiplierInfo = typeof(SelectableLevel).GetField(nameof(SelectableLevel.DaySpeedMultiplier), BindingFlags.Instance | BindingFlags.Public);
            FieldInfo currentLevelInfo = typeof(TimeOfDay).GetField(nameof(TimeOfDay.currentLevel), BindingFlags.Instance | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions).MatchForward(useEnd: false, new CodeMatch(OpCodes.Ldfld, daySpeedMultiplierInfo));

            if (codeMatcher.IsValid) // Assume method to be already patched if a reference to 'SelectableLevel.DaySpeedMultiplier' is found.
            {
                DebugHelper.LogWarning("TimeOfDay.MoveGlobalTime() already patched to account for day speed multipliers!", DebugType.Developer);
                return instructions;
            }

            codeMatcher.Start().MatchForward(useEnd: true,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, globalTimeSpeedMultiplierInfo), // Match 'globalTimeSpeedMultiplier' field, right after multiplying.
                new(OpCodes.Mul),
                new(OpCodes.Add));

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match 'globalTimeSpeedMultiplier' field.", DebugType.User);
                return instructions;
            }

            return codeMatcher.Insert(
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, currentLevelInfo),
                new(OpCodes.Ldfld, daySpeedMultiplierInfo), // Insert 'SelectableLevel.DaySpeedMultiplier' multiplication operation.
                new(OpCodes.Mul))
            .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.Update))]
        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.CalculatePlanetTime)), HarmonyTranspiler, HarmonyPriority(Patches.priority)]
        internal static IEnumerable<CodeInstruction> TimeOfDayUpdateCalculatePlanetTime_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            FieldInfo daySpeedMultiplierInfo = typeof(SelectableLevel).GetField(nameof(SelectableLevel.DaySpeedMultiplier), BindingFlags.Instance | BindingFlags.Public);
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).MatchForward(useEnd: true,
                new(OpCodes.Ldfld, daySpeedMultiplierInfo), // Match 'SelectableLevel.DaySpeedMultiplier' multiplication or division operation.
                new(ins => ins.opcode == OpCodes.Mul || ins.opcode == OpCodes.Div));

            if (codeMatcher.IsInvalid) // Assume method to be already patched if a reference to 'SelectableLevel.DaySpeedMultiplier' is not found.
            {
                DebugHelper.LogWarning("TimeOfDay.Update() and/or TimeOfDay.CalculatePlanetTime() already patched to account for day speed multipliers!", DebugType.Developer);
                return instructions;
            }

            int endPos = codeMatcher.Pos; // Keep index of the last instruction to be removed.
            codeMatcher.SearchBack(ins => ins.opcode == OpCodes.Add || ins.opcode == OpCodes.Sub); // Match prior addition or subtraction operation.

            if (codeMatcher.IsInvalid)
            {
                DebugHelper.LogError("Could not match operation instruction before 'DaySpeedMultiplier' field.", DebugType.User);
                return instructions;
            }

            return codeMatcher.RemoveInstructionsInRange(codeMatcher.Pos + 1, endPos) // Remove instructions for 'SelectableLevel.DaySpeedMultiplier' operation.
            .InstructionEnumeration();
        }
    }
}