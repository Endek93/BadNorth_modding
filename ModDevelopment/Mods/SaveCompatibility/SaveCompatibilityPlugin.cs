using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Voxels.TowerDefense;
using Voxels.TowerDefense.ProfileInternals;

namespace BadNorth.SaveCompatibility
{
    [BepInPlugin(PluginId, "Bad North Save Compatibility", "1.0.0")]
    public sealed class SaveCompatibilityPlugin : BaseUnityPlugin
    {
        private const string PluginId = "local.badnorth.save-compatibility";
        private static ManualLogSource log;
        private static bool savePending;
        private Harmony harmony;

        private void Awake()
        {
            log = Logger;
            harmony = new Harmony(PluginId);
            try
            {
                harmony.PatchAll(typeof(SaveCompatibilityPlugin).Assembly);
            }
            catch
            {
                harmony.UnpatchSelf();
                throw;
            }
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
        }

        private void Update()
        {
            // Deferred so the profile is written after the load that produced it finishes.
            if (!savePending) return;
            savePending = false;
            Profile.SaveUserSave();
            Logger.LogInfo("Save rewritten without the upgrades this installation does not define.");
        }

        [HarmonyPatch(typeof(SerializableHeroUpgrade), "PostLoad")]
        private static class ReportMissingUpgrade
        {
            private static void Postfix(SerializableHeroUpgrade __instance, string ___name)
            {
                if (!__instance.definition)
                    log.LogWarning("Upgrade is unavailable in this game installation: " + ___name);
            }
        }

        [HarmonyPatch(typeof(MetaInventory), "AddToStarting")]
        private static class SkipMissingStartingUpgrade
        {
            private static bool Prefix(HeroUpgradeDefinition def) => def && def.upgradeType;
        }

        [HarmonyPatch(typeof(MetaInventory), "PostLoad")]
        private static class ReportRestoredStartingUpgrades
        {
            private static void Postfix(MetaInventory __instance)
            {
                log.LogInfo("Saved starting choices restored: " + __instance.startingTraits.Count + " traits, "
                    + __instance.startingItems.Count + " items.");
            }
        }

        [HarmonyPatch(typeof(SerializableHeroUpgrade), "PreSave")]
        private static class PreserveMissingUpgradeName
        {
            // The saved name is already populated by deserialization and can resolve if the asset returns.
            private static bool Prefix(SerializableHeroUpgrade __instance) => __instance.definition;
        }

        [HarmonyPatch(typeof(MetaInventory), "PostLoad")]
        private static class DropUnavailableEntries
        {
            // Entries whose asset is not installed would otherwise stay in the save and be reported on
            // every launch; they are inert, so drop them and write the profile back once.
            private static void Postfix(MetaInventory __instance)
            {
                int removed = PruneCatalogue(__instance);
                removed += Prune(__instance.startingTraits, definition => !definition || !definition.upgradeType);
                removed += Prune(__instance.startingItems, definition => !definition || !definition.upgradeType);
                if (removed <= 0) return;

                savePending = true;
                log.LogInfo("Dropped " + removed + " saved entries that this installation does not define.");
            }

            // upgrades is private and its entry type is internal, so walk it through the field.
            private static int PruneCatalogue(MetaInventory inventory)
            {
                var entries = AccessTools.Field(typeof(MetaInventory), "upgrades").GetValue(inventory) as System.Collections.IList;
                if (entries == null) return 0;

                int removed = 0;
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    object entry = entries[i];
                    var upgrade = entry == null
                        ? null
                        : AccessTools.Field(entry.GetType(), "upgrade").GetValue(entry) as SerializableHeroUpgrade;
                    if (upgrade != null && upgrade.definition) continue;

                    entries.RemoveAt(i);
                    removed++;
                }
                return removed;
            }

            private static int Prune<T>(List<T> entries, System.Predicate<T> missing)
            {
                return entries == null ? 0 : entries.RemoveAll(missing);
            }
        }
    }
}
