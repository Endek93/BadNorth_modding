using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Voxels.TowerDefense;
using Voxels.TowerDefense.ProfileInternals;

namespace BadNorth.Unlocks
{
    [BepInPlugin(PluginId, "Bad North Unlocks", "1.0.0")]
    public sealed class UnlocksPlugin : BaseUnityPlugin
    {
        private const string PluginId = "local.badnorth.unlocks";
        private Harmony harmony;

        private void Awake()
        {
            harmony = new Harmony(PluginId);
            try
            {
                harmony.PatchAll(typeof(UnlocksPlugin).Assembly);
            }
            catch
            {
                harmony.UnpatchSelf();
                throw;
            }
            Logger.LogInfo("Collection and starting loadout unlocks enabled.");
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
        }

        private static bool IsCatalogueEntry(HeroUpgradeDefinition definition) =>
            definition && definition.upgradeType && definition.levels != null && definition.numLevels > 0;

        private static bool IsItem(HeroUpgradeDefinition definition) =>
            definition.typeEnum == HeroUpgradeTypeEnum.Item || definition.typeEnum == HeroUpgradeTypeEnum.Consumable;

        [HarmonyPatch(typeof(MetaInventory), nameof(MetaInventory.Get),
            new[] { typeof(HeroUpgradeDefinition), typeof(int), typeof(bool), typeof(bool) },
            new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out })]
        private static class CollectionPatch
        {
            private static void Postfix(HeroUpgradeDefinition upgrade, ref int level, ref bool isStarting, ref bool __result)
            {
                if (!IsCatalogueEntry(upgrade)) return;

                __result = true;
                level = upgrade.numLevels - 1;
                isStarting = IsItem(upgrade) || upgrade.typeEnum == HeroUpgradeTypeEnum.Trait;
            }
        }

        [HarmonyPatch]
        private static class StartingLoadoutPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                var selector = AccessTools.TypeByName("Voxels.TowerDefense.UI.NewGameHeroSelector");
                var list = typeof(List<HeroUpgradeDefinition>);
                yield return AccessTools.DeclaredMethod(selector, "MaybeSetupHeroes", new[] { typeof(int), list, list });
                yield return AccessTools.DeclaredMethod(selector, "OnParentMenuOpened", new[] { list, list, typeof(bool) });
            }

            private static void Prefix(ref List<HeroUpgradeDefinition> traits, ref List<HeroUpgradeDefinition> items)
            {
                // Supply copies to the selector so the profile and campaign generation retain their original lists.
                var allTraits = new List<HeroUpgradeDefinition>();
                var allItems = new List<HeroUpgradeDefinition>();
                foreach (HeroUpgradeDefinition definition in ResourceList<HeroUpgradeDefinition>.list)
                {
                    if (!IsCatalogueEntry(definition)) continue;

                    if (definition.typeEnum == HeroUpgradeTypeEnum.Trait)
                        allTraits.Add(definition);
                    else if (IsItem(definition))
                        allItems.Add(definition);
                }

                traits = allTraits;
                items = allItems;
            }
        }
    }
}
