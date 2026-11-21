using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Voxels.TowerDefense;
using Voxels.TowerDefense.CampaignGeneration;
using Voxels.TowerDefense.ProfileInternals;
using Voxels.TowerDefense.UI;

namespace BadNorth.IslandTravel
{
    [BepInPlugin(PluginId, "Bad North Island Travel", "1.1.0")]
    public sealed class IslandTravelPlugin : BaseUnityPlugin
    {
        private const string PluginId = "local.badnorth.island-travel";
        private static ConfigEntry<bool> freezeFrontier;
        private Harmony harmony;

        private void Awake()
        {
            freezeFrontier = Config.Bind("Travel", "FreezeFrontier", true,
                "Keep the Viking frontier at its current position while commanders rest between turns.");
            harmony = new Harmony(PluginId);
            try
            {
                harmony.PatchAll(typeof(IslandTravelPlugin).Assembly);
            }
            catch
            {
                harmony.UnpatchSelf();
                throw;
            }

            Logger.LogInfo("All campaign islands are open for travel.");
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
        }

        [HarmonyPatch]
        private static class OpenIslandPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.DeclaredMethod(typeof(LevelNode), nameof(LevelNode.IsUnlocked));
                yield return AccessTools.DeclaredMethod(typeof(LevelNode), nameof(LevelNode.IsAvailable));
                yield return AccessTools.DeclaredMethod(typeof(LevelNode), nameof(LevelNode.IsPlayable));
                yield return AccessTools.DeclaredMethod(typeof(LevelNode), nameof(LevelNode.ShouldBeVisible));
            }

            private static bool Prefix(ref bool __result)
            {
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(LevelNode), nameof(LevelNode.IsBehindFrontier))]
        private static class FrontierTravelPatch
        {
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(LevelVisuals), "Voxels.TowerDefense.LevelNode.ILevelSetup.OnLevelSetup")]
        private static class MapVisualsPatch
        {
            private static void Prefix(LevelNode level)
            {
                // Initialize the map states before the visuals subscribe to their changes.
                level.unlocked.SetActive(true);
                level.unlocked.ForceToTarget();
                level.visible.SetActive(true);
                level.visible.ForceToTarget();
            }

            private static void Postfix(LevelVisuals __instance)
            {
                if (!__instance.spriteState.active)
                {
                    // A fresh island has a blank atlas entry until the normal generator builds it.
                    var gameObject = new GameObject("Ungenerated island marker", typeof(RectTransform), typeof(Image));
                    var marker = gameObject.GetComponent<Image>();
                    var rect = marker.rectTransform;
                    rect.SetParent(__instance.mapImage.rectTransform, false);
                    rect.sizeDelta = __instance.mapImage.rectTransform.sizeDelta * 0.24f;
                    rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    marker.color = new Color32(76, 102, 91, 255);
                    marker.raycastTarget = false;
                    __instance.spriteState.Subscribe(hasSprite => gameObject.SetActive(!hasSprite), null);
                }

                // The native proxy follows this state. It can now start the normal on-demand load.
                __instance.cloudState.SetActive(true);
                __instance.cloudState.ForceToTarget();
            }
        }

        [HarmonyPatch(typeof(LevelNodeUIProxy), nameof(LevelNodeUIProxy.HandleClick))]
        private static class CheckpointTravelPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var checkpoint = AccessTools.DeclaredField(typeof(LevelState), nameof(LevelState.checkpointState));
                var readForTravel = AccessTools.DeclaredMethod(typeof(CheckpointTravelPatch), nameof(ReadForTravel));
                bool found = false;

                foreach (var instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, checkpoint))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = readForTravel;
                        found = true;
                    }

                    yield return instruction;
                }

                if (!found) throw new InvalidOperationException("Could not find the campaign map's checkpoint click.");
            }

            private static LevelState.CheckpointState ReadForTravel(LevelState level)
            {
                // The map options still offer the native Restore Checkpoint button.
                return level.checkpointState == LevelState.CheckpointState.Current
                    ? LevelState.CheckpointState.Saved
                    : level.checkpointState;
            }
        }

        [HarmonyPatch(typeof(LevelLeaver), "UpdateLevelStates")]
        private static class RecordVisitedIslandPatch
        {
            private static void Prefix(LevelNode levelNode)
            {
                // Persist ordinary battle progress even when the island was reached out of order.
                levelNode.levelState.unlocked = true;
            }
        }

        [HarmonyPatch(typeof(CampaignSave), nameof(CampaignSave.NextTurn))]
        private static class FreezeFrontierPatch
        {
            private static void Prefix(CampaignSave __instance, out int __state)
            {
                __state = __instance.vikingFrontierPosition;
            }

            private static void Postfix(CampaignSave __instance, int __state)
            {
                if (freezeFrontier.Value) __instance.vikingFrontierPosition = __state;
            }
        }
    }
}
