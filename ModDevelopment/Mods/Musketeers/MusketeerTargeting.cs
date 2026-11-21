using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorth.Musketeers
{
    [HarmonyPatch(typeof(LineOfSight), nameof(LineOfSight.target), MethodType.Getter)]
    internal static class MusketeerTargeting
    {
        private static readonly AccessTools.FieldRef<Archery, LineOfSight.Sight> AimedTarget =
            AccessTools.FieldRefAccess<Archery, LineOfSight.Sight>("target");
        private static readonly Dictionary<Agent, int> ShotsInFlight = new Dictionary<Agent, int>();

        private static void Postfix(LineOfSight __instance, float ___radius, ref LineOfSight.Sight __result)
        {
            if (!__result.agent || !MusketeersPlugin.IsMusketeer(__instance.agent)) return;

            var shooter = __instance.GetComponent<Archery>();
            if (!shooter) return;

            __result = SelectTarget(shooter, __instance.enemies, ___radius);
        }

        private static LineOfSight.Sight SelectTarget(Archery shooter, List<LineOfSight.Sight> visible, float radius)
        {
            LineOfSight.Sight best = default;
            int smallestCount = int.MaxValue;
            var coordinator = shooter.archerySquad;
            var teammates = coordinator ? coordinator.archers : null;
            Vector3 origin = shooter.ShootPos;

            foreach (var candidate in visible)
            {
                var enemy = candidate.agent;
                if (!IsAvailable(enemy)) continue;

                int count = InFlightCount(enemy);
                if (count >= smallestCount) continue;
                if (teammates != null) count += CountTeammatesAiming(teammates, shooter, enemy);
                if (count >= smallestCount || !MusketeerCombat.TryGetClearAim(origin, candidate, radius, out _)) continue;
                if (count == 0) return candidate;

                best = candidate;
                smallestCount = count;
            }

            return best;
        }

        internal static bool IsAvailable(Agent enemy) =>
            enemy && enemy.gameObject.activeInHierarchy && enemy.aliveAndGrounded != null && enemy.aliveAndGrounded.active;

        internal static int InFlightCount(Agent enemy) =>
            ShotsInFlight.TryGetValue(enemy, out int count) ? count : 0;

        internal static void ChangeInFlightCount(Agent enemy, int change)
        {
            int count = InFlightCount(enemy) + change;
            if (count > 0)
                ShotsInFlight[enemy] = count;
            else
                ShotsInFlight.Remove(enemy);
        }

        private static int CountTeammatesAiming(List<Archery> teammates, Archery shooter, Agent enemy)
        {
            int count = 0;
            foreach (var teammate in teammates)
            {
                // The native shooting state releases the target when the aim ends, even if the shot was cancelled.
                if (teammate && teammate != shooter && teammate.isActiveAndEnabled
                    && teammate.shooting != null && teammate.shooting.active && AimedTarget(teammate).agent == enemy)
                    count++;
            }
            return count;
        }

        [HarmonyPatch(typeof(LineOfSight), nameof(LineOfSight.SetupLineOfSight))]
        private static class SetMusketeerSearchRange
        {
            private static void Postfix(LineOfSight __instance, ref float ___radius, ref float ___sqRadius)
            {
                if (!MusketeersPlugin.IsMusketeer(__instance.agent)) return;

                // Native squad and agent scans read the squared radius from this instance.
                ___radius = MusketeerCombat.ExtendRange(___radius);
                ___sqRadius = ___radius * ___radius;
            }
        }

        [HarmonyPatch(typeof(LineOfSight), "GetSight")]
        private static class DiscoverDirectTargets
        {
            private static bool Prefix(LineOfSight __instance, Agent targetAgent, float ___radius,
                ref LineOfSight.Sight __result)
            {
                if (!MusketeersPlugin.IsMusketeer(__instance.agent)) return true;

                __result = default;
                if (!IsAvailable(targetAgent)) return false;
                var shooter = __instance.GetComponent<Archery>();
                if (!shooter) return true;

                var candidate = new LineOfSight.Sight
                {
                    agent = targetAgent,
                    mask0 = LayerMaster.arrowLow,
                    mask1 = LayerMaster.arrowHigh
                };
                if (MusketeerCombat.TryGetClearAim(shooter.ShootPos, candidate, ___radius, out _))
                    __result = candidate;
                return false;
            }
        }
    }
}
