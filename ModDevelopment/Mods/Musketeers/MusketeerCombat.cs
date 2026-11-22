using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorth.Musketeers
{
    internal static class MusketeerCombat
    {
        private const float Speed = 32f;
        private const float FlightTime = 0.45f;
        private const float RangeMultiplier = 1.15f;
        private const float MaxRange = Speed * FlightTime;
        private const float ShieldDamageMultiplier = 0.75f;
        internal const float ReloadSeconds = 5f;
        private static readonly FieldInfo ArrowSettings = AccessTools.Field(typeof(Shootable), "projectileSettings");

        // The settings instance also identifies a Musketeer shot on an Arrow taken from the shared pool.
        internal static readonly ProjectileSettings Shot = new ProjectileSettings
        {
            minSpeed = Speed,
            maxSpeed = Speed,
            drag = 0f,
            gravity = 0f
        };

        internal static Vector3 AimVelocity(Vector3 origin, Vector3 target, Vector3 targetVelocity)
            => LedOffset(origin, target, targetVelocity).normalized * Speed;

        private static Vector3 LedOffset(Vector3 origin, Vector3 target, Vector3 targetVelocity)
        {
            Vector3 offset = target - origin;
            float leadTime = Mathf.Min(FlightTime, offset.magnitude / Speed);
            return offset + targetVelocity * leadTime;
        }

        internal static Vector3 AimPoint(Agent enemy)
        {
            SphereCollider body = enemy.col;
            return body && body.enabled ? body.bounds.center : enemy.chestPos;
        }

        internal static LayerMask CoverMask(LayerMask near, LayerMask far)
        {
            return (int)near | (int)far | (int)LayerMaster.longshipModulesMask;
        }

        internal static float ExtendRange(float nativeRange) => Mathf.Min(nativeRange * RangeMultiplier, MaxRange);

        internal static bool TryGetClearAim(Vector3 origin, LineOfSight.Sight sight, float range, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            var enemy = sight.agent;
            if (!MusketeerTargeting.IsAvailable(enemy)) return false;

            Vector3 offset = LedOffset(origin, AimPoint(enemy), enemy.navPos.velocity);
            float distance = offset.magnitude;
            if (distance < 0.001f || distance > Mathf.Min(range, MaxRange)) return false;

            int mask = (int)CoverMask(sight.mask0, sight.mask1) | (int)LayerMaster.viBodies.mask;
            if (Physics.Raycast(origin, offset, out var hit, distance, mask)
                && hit.collider.GetComponent<Agent>() != enemy) return false;

            velocity = offset * (Speed / distance);
            return true;
        }

        internal static AttackSettings Boost(AttackSettings original)
        {
            original.damage *= 2f;
            original.knockback *= 2.4f;
            // Keep the native launch impulse, so additional knockback does not ragdoll every target.
            return original;
        }

        internal static bool IsRound(Arrow arrow) => arrow && ReferenceEquals(ArrowSettings.GetValue(arrow), Shot);

        [HarmonyPatch(typeof(IslandGameplayManager), nameof(IslandGameplayManager.RequestCombatAudio),
            typeof(string), typeof(GameObject))]
        private static class SuppressBowDraw
        {
            private static bool Prefix(string eventName, GameObject gameObject)
            {
                return eventName != "Sfx/English/Archer/Draw" || !gameObject
                    || !MusketeersPlugin.IsMusketeer(gameObject.GetComponent<Agent>());
            }
        }

        [HarmonyPatch(typeof(Archery), nameof(Archery.Setup))]
        private static class HideArrowWhileAiming
        {
            private static readonly AccessTools.FieldRef<Archery, Shootable> HeldShot =
                AccessTools.FieldRefAccess<Archery, Shootable>("shootable");

            private static void Postfix(Archery __instance, Transform ___bowAimer)
            {
                MusketeerAppearance.Attach(__instance, ___bowAimer);
                __instance.shooting.OnActivate += () =>
                {
                    if (MusketeersPlugin.IsMusketeer(__instance.agent) && HeldShot(__instance) is Arrow arrow && arrow)
                        MusketeerRound.For(arrow).HideArrow();
                };
                __instance.shooting.OnDeactivate += () =>
                {
                    if (HeldShot(__instance) is Arrow arrow && arrow)
                        arrow.GetComponent<MusketeerRound>()?.RestoreAppearance();
                };
            }
        }

        [HarmonyPatch(typeof(Archery), nameof(Archery.Shoot))]
        private static class TurnRegularFireIntoDirectFire
        {
            private static readonly MethodInfo NativeShotSound = AccessTools.Method(typeof(IslandGameplayManager),
                nameof(IslandGameplayManager.RequestCombatAudio), new[] { typeof(string), typeof(GameObject) });
            private static readonly MethodInfo FilteredShotSound = AccessTools.Method(typeof(TurnRegularFireIntoDirectFire),
                nameof(RequestShotSound));

            private static bool Prefix(Archery __instance, ref Vector3 shootDir, ref ProjectileSettings projectileSettings,
                TrajectoryUtility ___trajectoryCalculator, LineOfSight.Sight ___target, Shootable ___shootable,
                float ___coolDownTime)
            {
                if (!MusketeersPlugin.IsMusketeer(__instance.agent) || !___trajectoryCalculator
                    || !ReferenceEquals(projectileSettings, ___trajectoryCalculator.settings)) return true;

                if (Time.time < ___coolDownTime) return false;

                var enemy = ___target.agent;
                if (!MusketeerTargeting.IsAvailable(enemy) || !MusketeerTargeting.HasAttackSlot(__instance, enemy)) return false;

                var soldier = __instance.agent;
                Vector3 origin = ___shootable ? ___shootable.transform.position : __instance.ShootPos;
                float range = ExtendRange(___trajectoryCalculator.maxValidRadius);
                var appearance = soldier.GetComponent<MusketeerAppearance>();
                if (appearance && MusketeerAppearance.TryGetMuzzle(soldier, out var muzzle))
                {
                    // Keep the collision start behind nearby bodies and cover when the barrel extends past them.
                    Vector3 toTarget = AimPoint(enemy) - origin;
                    int solid = (int)CoverMask(___target.mask0, ___target.mask1) | (int)LayerMaster.viBodies.mask;
                    if (Vector3.Dot(muzzle - origin, toTarget.normalized) + 0.16f < toTarget.magnitude
                        && !Physics.Linecast(origin, muzzle, solid))
                        origin = muzzle;
                }
                if (!TryGetClearAim(origin, ___target, range, out var aimed)) return false;

                if (appearance) appearance.PrepareOrigin(origin);
                shootDir = aimed;
                projectileSettings = Shot;
                return true;
            }

            private static void Postfix(Archery __instance, Shootable __result, ProjectileSettings projectileSettings,
                LineOfSight.Sight ___target, ref float ___coolDownTime)
            {
                if (!__result || !ReferenceEquals(projectileSettings, Shot)) return;

                ___coolDownTime = Time.time + ReloadSeconds;
                var appearance = __instance.agent.GetComponent<MusketeerAppearance>();
                if (appearance) appearance.BeginReload(___coolDownTime);
                if (__result is Arrow arrow)
                {
                    var round = arrow.GetComponent<MusketeerRound>();
                    if (round) round.ReserveTarget(___target.agent);
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    if (instruction.Calls(NativeShotSound))
                    {
                        // Forward the shot settings along with the original sound and sender arguments.
                        yield return new CodeInstruction(OpCodes.Ldarg_2).MoveLabelsFrom(instruction);
                        instruction.operand = FilteredShotSound;
                    }
                    yield return instruction;
                }
            }

            private static void RequestShotSound(string eventName, GameObject sender, ProjectileSettings settings)
            {
                if (!ReferenceEquals(settings, Shot))
                    IslandGameplayManager.RequestCombatAudio(eventName, sender);
            }
        }

        [HarmonyPatch(typeof(Archery), nameof(Archery.AimAt))]
        private static class AimAlongShotPath
        {
            private static bool Prefix(Archery __instance, TrajectoryUtility trajectory, Vector3 targetPos,
                Vector3 targetVelocity, TrajectoryUtility ___trajectoryCalculator, LineOfSight ___lineOfSight,
                ref LineOfSight.Sight ___target, ref bool __result)
            {
                if (!MusketeersPlugin.IsMusketeer(__instance.agent) || trajectory != ___trajectoryCalculator)
                    return true;

                var enemy = ___target.agent;
                if (enemy && ___lineOfSight && __instance.shooting?.active == true)
                {
                    ___target = MusketeerTargeting.SelectTarget(__instance, ___lineOfSight.enemies,
                        ExtendRange(trajectory.maxValidRadius), ___target);
                    enemy = ___target.agent;
                    if (!enemy)
                    {
                        __result = false;
                        return false;
                    }
                    targetVelocity = enemy.navPos.velocity;
                }
                __instance.aimDirTarget = AimVelocity(__instance.ShootPos, enemy ? AimPoint(enemy) : targetPos,
                    targetVelocity);
                __result = __instance.aimDirTarget != Vector3.zero;
                return false;
            }
        }

        [HarmonyPatch(typeof(Arrow), nameof(Arrow.Shoot))]
        private static class PrepareRound
        {
            private static void Prefix(Arrow __instance, Agent shooter, ProjectileSettings projectileSettings,
                ref AttackSettings attackSettings, ref LayerMask mask0, ref LayerMask mask1)
            {
                if (!ReferenceEquals(projectileSettings, Shot)) return;

                var appearance = shooter.GetComponent<MusketeerAppearance>();
                if (appearance && appearance.TakeOrigin(out var origin))
                    __instance.transform.position = origin;
                attackSettings = Boost(attackSettings);
                // Straight shots use one cover mask for the full flight, including longships in either direction.
                LayerMask cover = CoverMask(mask0, mask1);
                mask0 = cover;
                mask1 = cover;
            }

            private static void Postfix(Arrow __instance, Agent shooter, Vector3 velocity, ProjectileSettings projectileSettings)
            {
                if (ReferenceEquals(projectileSettings, Shot))
                {
                    MusketeerRound.For(__instance).BeginFlight(__instance, FlightTime);
                    MusketeerShotEffects.Play(shooter, __instance.transform.position, velocity,
                        __instance.spriteRenderer ? __instance.spriteRenderer.gameObject.layer : __instance.gameObject.layer);
                }
                else
                    __instance.GetComponent<MusketeerRound>()?.RestoreAppearance();
            }
        }

        [HarmonyPatch(typeof(Shield), nameof(Shield.ModifyAttack))]
        private static class BreakFrontalGuard
        {
            private static bool Prefix(Shield __instance, ref Attack attack, AgentState ___shielding, AgentState ___rangeShield)
            {
                if (___shielding == null || !___shielding.active || !__instance.shield
                    || !(attack.monoAttacker is Arrow arrow) || !IsRound(arrow)) return true;

                float approach = Vector3.Dot(__instance.shield.forward, -attack.direction.normalized);
                bool raisedForArrows = ___rangeShield != null && ___rangeShield.active;
                if (approach <= (raisedForArrows ? 0f : 0.5f)) return true;

                attack.damage *= ShieldDamageMultiplier;
                attack.soundSuffix = "ShieldSmash";
                if (__instance.shieldSmash) attack.effect = __instance.shieldSmash;
                return false;
            }
        }
    }
}
