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
        private const int MaxAttackers = 2;
        private static readonly AccessTools.FieldRef<Archery, LineOfSight.Sight> AimedTarget =
            AccessTools.FieldRefAccess<Archery, LineOfSight.Sight>("target");
        private static readonly AccessTools.FieldRef<LineOfSight, float> ScoutRadius =
            AccessTools.FieldRefAccess<LineOfSight, float>("radius");
        private static readonly HashSet<Archery> ActiveAims = new HashSet<Archery>();
        private static readonly Dictionary<Agent, int> ShotsInFlight = new Dictionary<Agent, int>();

        private static void Postfix(LineOfSight __instance, float ___radius, ref LineOfSight.Sight __result)
        {
            if (!__result.agent || !MusketeersPlugin.IsMusketeer(__instance.agent)) return;

            var shooter = __instance.GetComponent<Archery>();
            if (!shooter) return;

            __result = SelectTarget(shooter, __instance.enemies, ___radius);
        }

        internal static LineOfSight.Sight SelectTarget(Archery shooter, List<LineOfSight.Sight> visible, float radius,
            LineOfSight.Sight current = default)
        {
            int smallestCount = MaxAttackers;
            LineOfSight.Sight best = default;
            if (current.agent)
            {
                int count = CountOtherAttacks(shooter, current.agent);
                if (count == 0) return current;
                if (count < smallestCount)
                {
                    best = current;
                    smallestCount = count;
                }
            }
            Vector3 origin = shooter.ShootPos;

            foreach (var candidate in visible)
            {
                var enemy = candidate.agent;
                if (!IsAvailable(enemy)) continue;

                int count = CountOtherAttacks(shooter, enemy);
                if (count >= smallestCount || !MusketeerCombat.TryGetClearAim(origin, candidate, radius, out _)) continue;
                if (count == 0) return candidate;

                best = candidate;
                smallestCount = count;
            }

            return best;
        }

        internal static bool IsAvailable(Agent enemy) =>
            enemy && enemy.gameObject.activeInHierarchy && enemy.aliveAndGrounded != null && enemy.aliveAndGrounded.active;

        internal static bool HasSpottedEnemyInRange(LineOfSight scout, Vector3 origin)
        {
            if (!scout || scout.enemies == null || scout.enemies.Count == 0) return false;

            // Use the scanner's visible candidates before applying the attack-slot limit.
            float radius = ScoutRadius(scout);
            foreach (var candidate in scout.enemies)
            {
                var enemy = candidate.agent;
                if (IsAvailable(enemy) && (MusketeerCombat.AimPoint(enemy) - origin).sqrMagnitude <= radius * radius)
                    return true;
            }
            return false;
        }

        internal static bool HasAttackSlot(Archery shooter, Agent enemy) => CountOtherAttacks(shooter, enemy) < MaxAttackers;

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

        private static int CountOtherAttacks(Archery shooter, Agent enemy)
        {
            int count = InFlightCount(enemy);
            if (count >= MaxAttackers) return count;

            foreach (var other in ActiveAims)
            {
                if (!other || other == shooter || !other.isActiveAndEnabled || AimedTarget(other).agent != enemy
                    || !IsAvailable(other.agent)) continue;
                count++;
                if (count == MaxAttackers) break;
            }
            return count;
        }

        [HarmonyPatch(typeof(Archery), nameof(Archery.MaybeStartShooting))]
        private static class TrackCommittedAim
        {
            private static void Postfix(Archery __instance, bool __result)
            {
                if (!__result || !MusketeersPlugin.IsMusketeer(__instance.agent)) return;
                var shooting = __instance.shooting;
                if (!shooting.active || !ActiveAims.Add(__instance)) return;

                shooting.OnDeactivate += ReleaseAim;

                void ReleaseAim()
                {
                    ActiveAims.Remove(__instance);
                    shooting.OnDeactivate -= ReleaseAim;
                }
            }
        }

        [HarmonyPatch(typeof(Archery), "OnDestroy")]
        private static class ForgetDestroyedShooter
        {
            private static void Prefix(Archery __instance) => ActiveAims.Remove(__instance);
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
