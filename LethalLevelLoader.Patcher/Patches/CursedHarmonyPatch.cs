using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using HarmonyLib;
using HarmonyLib.Internal.Patching;
using Mono.Cecil;
using MonoMod.Utils;

namespace LethalLevelLoader.Patcher
{
    internal static class CursedHarmonyPatch
    {
        private static void ReadInstruction_Prefix(Mono.Cecil.Cil.Instruction ins)
        {
            if (ins.Operand is DynamicMethod dynamicMethod) // DynamicMethod is received when a MonoMod Transpiler creates an ILLabel for a method.
            {
                ModuleDefinition moduleDefinition = ModuleDefinition.ReadModule(typeof(object).Module.FullyQualifiedName);
                ins.Operand = new DynamicMethodReference(moduleDefinition, dynamicMethod); // Replace DynamicMethod with a DynamicMethodReference, which CAN be cast.
                Trace.TraceInformation($"[LethalLevelLoader.Patcher] DynamicMethod '{dynamicMethod.Name}' replaced!");
            }
        }

        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void ChainloaderInitialize_Postfix()
        {
            string harmonyBackend = Environment.GetEnvironmentVariable("MONOMOD_DMD_TYPE"); // Only needs to run if set to either 'auto' (blank) or 'dynamicmethod'.
            if (!string.IsNullOrEmpty(harmonyBackend) && !string.Equals(harmonyBackend, "dynamicmethod", StringComparison.Ordinal)) return;

            Type cursedType = null; // 'ILManipulator+<>c__DisplayClass14_0'
            MethodInfo cursedMethod = null; // '<ReadBody>g__ReadInstruction|0'

            Assembly harmonyAssembly = Assembly.GetAssembly(typeof(ILManipulator)); // Obtain Harmony assembly.
            Type[] types = harmonyAssembly.GetTypes();
            for (int i = types.Length - 1; i >= 0; i--) // Iterate backwards since target method is near the end.
            {
                cursedType = types[i];
                MethodInfo[] methods = cursedType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
                cursedMethod = Array.Find(methods, method => method.Name.Contains("ReadInstruction", StringComparison.Ordinal));

                if (cursedMethod != null)
                {
                    Trace.TraceInformation($"[LethalLevelLoader.Patcher] Found Method '{cursedType.FullName}/{cursedMethod.Name}'!");
                    break;
                }
            }
            if (cursedMethod == null) return;

            HarmonyMethod prefix = new(typeof(CursedHarmonyPatch), nameof(ReadInstruction_Prefix));
            LethalLevelLoaderPatcher.Harmony.Patch(cursedMethod, prefix);
        }
    }
}