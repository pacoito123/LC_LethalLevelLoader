using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace LethalLevelLoader.Patcher
{
    [HarmonyPatch]
    internal static class ChainloaderEventPatch
    {
        [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void ChainloaderInitialize_Postfix()
        {
            MethodInfo chainloaderStartInfo = typeof(Chainloader).GetMethod(nameof(Chainloader.Start), BindingFlags.Static | BindingFlags.Public);
            MethodInfo onChainloaderFinishInfo = typeof(LethalLevelLoaderPatcher).GetMethod("ChainloaderFinish", BindingFlags.Static | BindingFlags.NonPublic);
            LethalLevelLoaderPatcher.Harmony.Patch(chainloaderStartInfo, postfix: new(onChainloaderFinishInfo));
        }
    }
}