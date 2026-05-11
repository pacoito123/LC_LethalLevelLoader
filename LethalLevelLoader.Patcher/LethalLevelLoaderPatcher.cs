using HarmonyLib;
using Mono.Cecil;
using System.Collections.Generic;
using System.Diagnostics;

// using static FixPluginTypesSerialization.FixPluginTypesSerializationPatcher;

namespace LethalLevelLoader.Patcher
{
    public class LethalLevelLoaderPatcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = [];

        internal static Harmony Harmony { get; } = new(nameof(LethalLevelLoaderPatcher));

        public static void Initialize()
        {
            Trace.TraceInformation("[LethalLevelLoader.Patcher] Initializing...");
        }

        private static void Finish()
        {
            Trace.TraceInformation("[LethalLevelLoader.Patcher] Patching...");
            Harmony.PatchAll(typeof(CursedHarmonyPatch));
            Trace.TraceInformation("[LethalLevelLoader.Patcher] Done!");
        }

        /* /// <summary>
        ///     Admittedly a bit wonky, but it works.
        /// </summary>
        /// <remarks>This'll need to be removed once <c>LethalLevelLoader</c> updates.</remarks>
        private static void InitializeInternal()
        {
            // Check if the old LethalLevelLoader is found in FixPluginTypesSerialization's list of found plugins.
            int oldLLL = PluginPaths.FindIndex(name => name.Contains("IAmBatby-LethalLevelLoader"));

            if (oldLLL != -1)
            {
                // Remove old LethalLevelLoader from the list(s) of plugins to serialize.
                PluginPaths.RemoveAt(oldLLL);
                PluginNames.RemoveAt(oldLLL);
            }
        } */

        public static void Patch(AssemblyDefinition _) { }
    }
}