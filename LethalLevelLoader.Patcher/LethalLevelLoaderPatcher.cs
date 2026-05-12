using HarmonyLib;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;

// using static FixPluginTypesSerialization.FixPluginTypesSerializationPatcher;

namespace LethalLevelLoader.Patcher
{
    public class LethalLevelLoaderPatcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = [];

        internal static Harmony Harmony { get; } = new("imabatby.lethallevelloader.patcher");

        public static event Action onChainloaderFinish;

        private static void Initialize()
        {
            Trace.TraceInformation("[LethalLevelLoader.Patcher] Initializing...");
        }

        private static void Finish()
        {
            Trace.TraceInformation("[LethalLevelLoader.Patcher] Patching...");
            Harmony.PatchAll(typeof(ChainloaderEventPatch));
            Harmony.PatchAll(typeof(CursedHarmonyPatch));
            Trace.TraceInformation("[LethalLevelLoader.Patcher] Done!");
        }

        private static void ChainloaderFinish()
        {
            try
            {
                onChainloaderFinish?.Invoke();
                onChainloaderFinish = null;
            }
            catch (Exception exception)
            {
                Trace.TraceError("[LethalLevelLoader.Patcher] Error during Chainloader event invokation: " + exception);
            }
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