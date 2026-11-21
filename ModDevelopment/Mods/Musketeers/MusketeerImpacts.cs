using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorth.Musketeers
{
    internal static class MusketeerImpacts
    {
        [HarmonyPatch(typeof(Arrow), nameof(Arrow.Bounce))]
        private static class StopAtFirstContact
        {
            private static bool Prefix(Arrow __instance, Projectile ___projectile, LayerMask ___mask0,
                LayerMask ___mask1, int ___hitLayer)
            {
                if (!MusketeerCombat.IsRound(__instance)) return true;

                // Native arrows also bounce after a nonlethal hit. That path already played the hit effect.
                Vector3 velocity = ___projectile.velocity;
                int mask = velocity.y > 0f ? (int)___mask0 : (int)___mask1;
                if (Physics.Raycast(___projectile.pos, velocity, out var hit, velocity.magnitude * Time.fixedDeltaTime, mask)
                    && hit.collider.gameObject.layer != ___hitLayer)
                    StopOnCover(__instance, hit.point, hit.normal);
                else
                    __instance.ReturnToPool();
                return false;
            }
        }

        [HarmonyPatch(typeof(Arrow), "Stick")]
        private static class SkipEmbeddedArrow
        {
            private static bool Prefix(Arrow __instance, RaycastHit hit)
            {
                if (!MusketeerCombat.IsRound(__instance)) return true;
                StopOnCover(__instance, hit.point, hit.normal);
                return false;
            }
        }

        [HarmonyPatch(typeof(Arrow), nameof(Arrow.Smash))]
        private static class ReplaceArrowFragments
        {
            private static bool Prefix(Arrow __instance, Vector3 pos, Vector3 dir)
            {
                if (!MusketeerCombat.IsRound(__instance)) return true;
                StopOnCover(__instance, pos, dir);
                return false;
            }
        }

        private static void StopOnCover(Arrow arrow, Vector3 point, Vector3 normal)
        {
            try
            {
                var gameplay = Singleton<IslandGameplayManager>.instance;
                if (!gameplay || !gameplay.island) return;
                var dust = ScriptableObjectSingleton<PrefabManager>.instance.dustEffect;
                if (dust)
                    dust.PlayAt(point, Quaternion.LookRotation(normal), Vector3.one * 0.45f);
                IslandGameplayManager.RequestCombatAudio(arrow.smashSound, arrow.gameObject);
            }
            finally
            {
                arrow.ReturnToPool();
            }
        }
    }
}
