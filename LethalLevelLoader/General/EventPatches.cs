using DunGen;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalLevelLoader
{
    //This class is dedicated to the patches needed to collect data sent to events inside the current ExtendedLevel and ExtendedDungeonFlow.
    //They are separated for organisation purposes and to enforce that all of these patches should only be reading information and sending it off
    //Nothing in this class should modify the game in any way.
    internal static class EventPatches
    {
        internal static DayMode previousDayMode = DayMode.None;
        internal static readonly List<GrabbableObject> scrapSpawnedThisRound = [];
        internal static readonly List<GameObject> spawnedMapObjects = [];

        ////////// Level Patches //////////
        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(StoryLog), nameof(StoryLog.CollectLog))]
        [HarmonyPrefix]
        internal static void StoryLogCollectLog_Prefix(StoryLog __instance)
        {
            if (LevelManager.CurrentExtendedLevel != null)
            {
                LevelManager.CurrentExtendedLevel.LevelEvents.onStoryLogCollected.Invoke(__instance);
                LevelManager.GlobalLevelEvents.onStoryLogCollected.Invoke(__instance);
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnRandomDaytimeEnemy))]
        [HarmonyPostfix]
        internal static void RoundManagerSpawnRandomDaytimeEnemy_Postfix(RoundManager __instance, bool __result)
        {
            if (__result && LevelManager.CurrentExtendedLevel != null)
            {
                EnemyAI spawnedEnemy = (__instance.SpawnedEnemies.Count > 0) ? __instance.SpawnedEnemies[^1] : null;
                if (spawnedEnemy != null)
                {
                    LevelManager.CurrentExtendedLevel.LevelEvents.onDaytimeEnemySpawn.Invoke(spawnedEnemy); // TODO: Send to clients?
                    LevelManager.GlobalLevelEvents.onDaytimeEnemySpawn.Invoke(spawnedEnemy);
                }
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnRandomOutsideEnemy))]
        [HarmonyPostfix]
        internal static void RoundManagerSpawnRandomOutsideEnemy_Postfix(RoundManager __instance, bool __result)
        {
            if (__result && LevelManager.CurrentExtendedLevel != null)
            {
                EnemyAI spawnedEnemy = (__instance.SpawnedEnemies.Count > 0) ? __instance.SpawnedEnemies[^1] : null;
                if (spawnedEnemy != null)
                {
                    LevelManager.CurrentExtendedLevel.LevelEvents.onNighttimeEnemySpawn.Invoke(spawnedEnemy); // TODO: Send to clients?
                    LevelManager.GlobalLevelEvents.onNighttimeEnemySpawn.Invoke(spawnedEnemy);
                }
            }
        }

        ////////// Dungeon Patches //////////
        [HarmonyPriority(Patches.priority + 1)] // +1 Because this needs to run after the Patch in Patches, second patch here for consistency.
        [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.Generate))]
        [HarmonyPrefix]
        internal static void DungeonGeneratorGenerate_Prefix()
        {
            if (DungeonManager.CurrentExtendedDungeonFlow != null)
            {
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onBeforeDungeonGenerate.Invoke(Patches.RoundManager);
                DungeonManager.GlobalDungeonEvents.onBeforeDungeonGenerate.Invoke(Patches.RoundManager);
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SwitchPower))]
        [HarmonyPrefix]
        internal static void RoundManagerSwitchPower_Prefix(bool on)
        {
            if (DungeonManager.CurrentExtendedDungeonFlow != null)
            {
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onPowerSwitchToggle.Invoke(on);
                DungeonManager.GlobalDungeonEvents.onPowerSwitchToggle.Invoke(on);
            }
            if (LevelManager.CurrentExtendedLevel != null)
            {
                LevelManager.CurrentExtendedLevel.LevelEvents.onPowerSwitchToggle.Invoke(on);
                LevelManager.GlobalLevelEvents.onPowerSwitchToggle.Invoke(on);
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnScrapInLevel))]
        [HarmonyPostfix]
        internal static void RoundManagerSpawnScrapInLevel_Postfix()
        {
            if (DungeonManager.CurrentExtendedDungeonFlow != null)
            {
                scrapSpawnedThisRound.Clear();
                scrapSpawnedThisRound.AddRange(UnityEngine.Object.FindObjectsByType<GrabbableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
                scrapSpawnedThisRound.RemoveAll(item => item.isInElevator || item.isInShipRoom || item.scrapPersistedThroughRounds);
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onSpawnedScrapObjects.Invoke(scrapSpawnedThisRound); // TODO: Send to clients?
                DungeonManager.GlobalDungeonEvents.onSpawnedScrapObjects.Invoke(scrapSpawnedThisRound);
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnSyncedProps))]
        [HarmonyPostfix]
        internal static void RoundManagerSpawnSyncedProps_Postfix()
        {
            if (DungeonManager.CurrentExtendedDungeonFlow != null)
            {
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onSpawnedSyncedObjects.Invoke(Patches.RoundManager.spawnedSyncedObjects); // TODO: Send to clients?
                DungeonManager.GlobalDungeonEvents.onSpawnedSyncedObjects.Invoke(Patches.RoundManager.spawnedSyncedObjects);
            }
        }

        private static EnemyVent cachedSelectedVent;
        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnEnemyFromVent))]
        [HarmonyPrefix]
        internal static void RoundManagerSpawnEventFromVent_Prefix(EnemyVent vent)
        {
            if (DungeonManager.CurrentExtendedDungeonFlow != null)
                cachedSelectedVent = vent;
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnEnemyGameObject))]
        [HarmonyPostfix]
        internal static void RoundManagerSpawnEventFromVent_Postfix()
        {
            if (DungeonManager.CurrentExtendedDungeonFlow != null && cachedSelectedVent != null)
            {
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onEnemySpawnedFromVent.Invoke((cachedSelectedVent, Patches.RoundManager.SpawnedEnemies[^1]));
                DungeonManager.GlobalDungeonEvents.onEnemySpawnedFromVent.Invoke((cachedSelectedVent, Patches.RoundManager.SpawnedEnemies[^1]));
                cachedSelectedVent = null;
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnMapObjects))]
        [HarmonyPostfix]
        internal static void RoundManagerSpawnMapObjects_Postfix()
        {
            spawnedMapObjects.Clear();
            if (DungeonManager.CurrentExtendedDungeonFlow != null && LevelLoader.currentLevelScene.isLoaded)
            {
                foreach (GameObject rootObjects in LevelLoader.currentLevelScene.GetRootGameObjects())
                    foreach (IIndoorMapHazard indoorMapHazard in rootObjects.GetComponentsInChildren<IIndoorMapHazard>(includeInactive: false))
                        if (indoorMapHazard is Behaviour indoorMapHazardScript)
                            spawnedMapObjects.Add(indoorMapHazardScript.transform.root.gameObject);
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onSpawnedMapObjects.Invoke(spawnedMapObjects); // TODO: Send to clients?
                DungeonManager.GlobalDungeonEvents.onSpawnedMapObjects.Invoke(spawnedMapObjects);
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnShipLandedMiscEvents))]
        [HarmonyPrefix]
        internal static void StartOfRoundOnShipLandedMiscEvents_Prefix()
        {
            if (LevelManager.CurrentExtendedLevel != null)
            {
                LevelManager.CurrentExtendedLevel.LevelEvents.onShipLand.Invoke();
                LevelManager.GlobalLevelEvents.onShipLand.Invoke();
            }
            if (DungeonManager.CurrentExtendedDungeonFlow != null)
            {
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onShipLand.Invoke();
                DungeonManager.GlobalDungeonEvents.onShipLand.Invoke();
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ShipLeave))]
        [HarmonyPrefix]
        internal static void StartOfRoundShipLeave_Prefix()
        {
            if (LevelManager.CurrentExtendedLevel != null)
            {
                LevelManager.CurrentExtendedLevel.LevelEvents.onShipLeave.Invoke();
                LevelManager.GlobalLevelEvents.onShipLeave.Invoke();
            }
            if (DungeonManager.CurrentExtendedDungeonFlow != null)
            {
                DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onShipLeave.Invoke();
                DungeonManager.GlobalDungeonEvents.onShipLeave.Invoke();
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayerServerRpc))]
        [HarmonyPrefix]
        internal static void EntranceTeleportTeleportPlayerServerRpc_Prefix(EntranceTeleport __instance, int playerObj)
        {
            // Only run on the player calling the ServerRpc.
            if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Send)
            {
                return;
            }

            PlayerControllerB player = Patches.StartOfRound.allPlayerScripts[playerObj];
            if (player == null || !player.IsOwner) return;

            if (DungeonManager.CurrentExtendedDungeonFlow != null)
            {
                if (__instance.isEntranceToBuilding == true)
                {
                    DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onPlayerEnterDungeon.Invoke((__instance, player));
                    DungeonManager.GlobalDungeonEvents.onPlayerEnterDungeon.Invoke((__instance, player));
                }
                else
                {
                    DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onPlayerExitDungeon.Invoke((__instance, player));
                    DungeonManager.GlobalDungeonEvents.onPlayerExitDungeon.Invoke((__instance, player));
                }
            }

            if (LevelManager.CurrentExtendedLevel != null)
            {
                if (__instance.isEntranceToBuilding == true)
                {
                    LevelManager.CurrentExtendedLevel.LevelEvents.onPlayerEnterDungeon.Invoke((__instance, player));
                    LevelManager.GlobalLevelEvents.onPlayerEnterDungeon.Invoke((__instance, player));
                }
                else
                {
                    LevelManager.CurrentExtendedLevel.LevelEvents.onPlayerExitDungeon.Invoke((__instance, player));
                    LevelManager.GlobalLevelEvents.onPlayerExitDungeon.Invoke((__instance, player));
                }
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(LungProp), nameof(LungProp.EquipItem))]
        [HarmonyPrefix]
        internal static void LungPropEquipItem_Prefix(LungProp __instance)
        {
            if (__instance.isLungDocked)
            {
                if (DungeonManager.CurrentExtendedDungeonFlow != null)
                {
                    DungeonManager.CurrentExtendedDungeonFlow.DungeonEvents.onApparatusTaken.Invoke(__instance);
                    DungeonManager.GlobalDungeonEvents.onApparatusTaken.Invoke(__instance);
                }
                if (LevelManager.CurrentExtendedLevel != null)
                {
                    LevelManager.CurrentExtendedLevel.LevelEvents.onApparatusTaken.Invoke(__instance);
                    LevelManager.GlobalLevelEvents.onApparatusTaken.Invoke(__instance);
                }
            }
        }

        [HarmonyPriority(Patches.priority)]
        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.GetDayPhase))]
        [HarmonyPostfix]
        internal static void TimeOfDayGetDayPhase_Postfix(DayMode __result)
        {
            if (previousDayMode is DayMode.None || previousDayMode != __result)
            {
                LevelManager.CurrentExtendedLevel.LevelEvents.onDayModeToggle.Invoke(__result);
                LevelManager.GlobalLevelEvents.onDayModeToggle.Invoke(__result);
            }

            previousDayMode = __result;
        }
    }

    public delegate void ParameterEvent<T>(T param);
    public class ExtendedEvent<T> : ExtendedEvent
    {
        public override int Listeners => base.Listeners + paramListeners.Count;
        private event ParameterEvent<T> onParameterEvent;
        private readonly List<ParameterEvent<T>> paramListeners = new List<ParameterEvent<T>>();

        public void Invoke(T param)
        {
            onParameterEvent?.Invoke(param);
            Invoke();
        }

        public void AddListener(ParameterEvent<T> listener)
        {
            onParameterEvent += listener;
            paramListeners.Add(listener);
        }
        public void RemoveListener(ParameterEvent<T> listener)
        {
            onParameterEvent -= listener;
            paramListeners.Remove(listener);
        }

        public override void ClearListeners()
        {
            base.ClearListeners();
            foreach (ParameterEvent<T> listener in paramListeners)
                onParameterEvent -= listener;
            paramListeners.Clear();
        }
    }

    public class ExtendedEvent
    {
        protected event Action onEvent;
        public bool HasListeners => (Listeners != 0);

        public virtual int Listeners => listeners.Count;
        private readonly List<Action> listeners = new List<Action>();

        public void Invoke()
        {
            try
            {
                onEvent?.Invoke();
            }
            catch (Exception e) // Got a TypeLoadException on something calling this that broke everything...
            {
                DebugHelper.LogWarning(e.Message, DebugType.User);
            }
        }

        public void AddListener(Action listener)
        {
            onEvent += listener;
            listeners.Add(listener);
        }
        public void RemoveListener(Action listener)
        {
            onEvent -= listener;
            listeners.Remove(listener);
        }

        public virtual void ClearListeners()
        {
            foreach (Action listener in listeners)
                onEvent -= listener;
            listeners.Clear();
        }
    }
}
