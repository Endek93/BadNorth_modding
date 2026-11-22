using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using I2.Loc;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.UI.UpgradeScreen;

namespace BadNorth.Musketeers
{
    [BepInPlugin(PluginId, "Bad North Musketeers", "0.7.16")]
    public sealed class MusketeersPlugin : BaseUnityPlugin
    {
        private const string PluginId = "local.badnorth.musketeers";
        private const string ArcherId = "Hero_Class_Archers";
        private const string MusketeerId = "BadNorth_Class_Musketeers";
        private const string NameTerm = "BADNORTH_MUSKETEERS/NAME";
        private const string DescriptionTerm = "BADNORTH_MUSKETEERS/DESCRIPTION";

        private static HeroClassUpgradeDefinition archers;
        private static HeroClassUpgradeDefinition musketeers;
        private static int finalTier;

        private void Awake()
        {
            MusketeerShotAudio.Logger = Logger;
            archers = ResourceList<HeroUpgradeDefinition>.Get(ArcherId) as HeroClassUpgradeDefinition;
            if (!archers || archers.numLevels < 3)
            {
                Logger.LogError("Could not find the three-tier Archer upgrade.");
                return;
            }
            finalTier = archers.numLevels - 1;
            if (ResourceList<HeroUpgradeDefinition>.dictionary.ContainsKey(MusketeerId))
            {
                Logger.LogError("The Musketeers upgrade ID is already registered.");
                return;
            }

            var labelField = AccessTools.Field(typeof(HeroClassUpgradeDefinition), "squadLevelTerms");
            var classLabels = (string[])((string[])labelField.GetValue(archers)).Clone();
            classLabels[Math.Min(finalTier + 1, classLabels.Length - 1)] = NameTerm;

            var definition = Instantiate(archers);
            definition.name = MusketeerId;
            definition.OnEnable();
            definition.nameTerm = NameTerm;
            definition.shortDescription = DescriptionTerm;
            var icon = LoadUpgradeIcon(archers.GetSprite().pixelsPerUnit);
            if (icon) definition.infoSprite = icon;
            definition.levels = (HeroUpgradeDefinition.Level[])archers.levels.Clone();
            definition.levels[finalTier].description = DescriptionTerm;
            definition.levels[finalTier].prepurchaseDescription = DescriptionTerm;
            labelField.SetValue(definition, classLabels);

            var harmony = new Harmony(PluginId);
            try
            {
                harmony.PatchAll(typeof(MusketeersPlugin).Assembly);
                ResourceList<HeroUpgradeDefinition>.dictionary.Add(MusketeerId, definition);
                musketeers = definition;
            }
            catch
            {
                harmony.UnpatchSelf();
                Destroy(definition);
                throw;
            }
            Logger.LogInfo("Registered Musketeers as an alternate final Archer upgrade. Base price: "
                + musketeers.GetLevelCost(finalTier));
            MusketeerAppearance.Initialize(Logger);
        }

        private Sprite LoadUpgradeIcon(float pixelsPerUnit)
        {
            using (var stream = typeof(MusketeersPlugin).Assembly.GetManifestResourceStream(
                "BadNorth.Musketeers.MusketeerUpgradeIcon.png"))
            {
                if (stream == null)
                {
                    Logger.LogWarning("The Musketeer upgrade icon resource is missing.");
                    return null;
                }

                using (var reader = new BinaryReader(stream))
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = "Musketeer upgrade icon",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    if (!ImageConversion.LoadImage(texture, reader.ReadBytes((int)stream.Length), true))
                    {
                        Destroy(texture);
                        Logger.LogWarning("The Musketeer upgrade icon could not be decoded.");
                        return null;
                    }

                    var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        Vector2.one * 0.5f, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = texture.name;
                    return sprite;
                }
            }
        }

        private static bool CanBecomeMusketeers(HeroDefinition hero, int level)
        {
            return level == finalTier && hero != null && hero.alive && hero.recruited
                && hero.classUpgrade != null && hero.classUpgrade.definition == archers
                && hero.classUpgrade.level == finalTier - 1;
        }

        internal static bool IsMusketeer(Agent agent)
        {
            return musketeers && agent && agent.squad is EnglishSquad squad && squad
                && squad.hero?.classUpgrade?.definition == musketeers;
        }

        [HarmonyPatch(typeof(HeroUpgradeDefinition), nameof(HeroUpgradeDefinition.AvailableFor),
            typeof(HeroDefinition), typeof(int))]
        private static class BranchEligibility
        {
            private static bool Prefix(HeroUpgradeDefinition __instance, HeroDefinition hero, int atLevel, ref bool __result)
            {
                if (__instance != musketeers) return true;
                __result = CanBecomeMusketeers(hero, atLevel);
                return false;
            }
        }

        [HarmonyPatch(typeof(HeroDefinition), nameof(HeroDefinition.TryPurchase),
            new[] { typeof(HeroUpgradeDefinition), typeof(int), typeof(int) },
            new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref })]
        private static class ValidatePurchase
        {
            private static bool Prefix(HeroDefinition __instance, HeroUpgradeDefinition upgradeDef, int level, ref bool __result)
            {
                if (upgradeDef != musketeers || CanBecomeMusketeers(__instance, level)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(HeroDefinition), "AddUpgrade",
            new[] { typeof(SerializableHeroUpgrade), typeof(HeroUpgradeDefinition), typeof(int) },
            new[] { ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
        private static class ReplaceClassSlot
        {
            private static void Prefix(ref SerializableHeroUpgrade slot, HeroUpgradeDefinition upgradeDef)
            {
                // The ordinary class purchase advances the existing definition's level.
                if (upgradeDef == musketeers)
                    slot = new SerializableHeroUpgrade(upgradeDef);
            }
        }

        [HarmonyPatch(typeof(SuperUpgradeMenu), "MaybeInitialize")]
        private static class OfferInUpgradeMenu
        {
            private static void Prefix(bool ___initialized, out bool __state) => __state = !___initialized;

            private static void Postfix(AgentState ___up, bool __state)
            {
                if (!__state || ___up == null || !musketeers) return;
                var showUpgrades = ___up.OnActivate;
                ___up.OnActivate = () => WithTemporaryUpgrade(showUpgrades);
            }

            private static void WithTemporaryUpgrade(Action showUpgrades)
            {
                var upgrades = ResourceList<HeroUpgradeDefinition>.list;
                if (upgrades.Contains(musketeers))
                {
                    showUpgrades();
                    return;
                }

                int index = upgrades.IndexOf(archers);
                upgrades.Insert(index < 0 ? upgrades.Count : index + 1, musketeers);
                try
                {
                    showUpgrades();
                }
                finally
                {
                    // Keep this out of the ordinary class list used by random commander generation.
                    upgrades.Remove(musketeers);
                }
            }
        }

        [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.TryGetTranslation))]
        private static class TranslateBranch
        {
            private static bool Prefix(string Term, string overrideLanguage, ref string Translation, ref bool __result)
            {
                if (Term != NameTerm && Term != DescriptionTerm) return true;

                string language = string.IsNullOrEmpty(overrideLanguage) ? LocalizationManager.CurrentLanguageCode : overrideLanguage;
                bool chinese = language != null && (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                    || language.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase));
                if (Term == NameTerm)
                    Translation = chinese ? "火枪手" : "Musketeers";
                else
                    Translation = chinese ? "精准的直线射击，伤害和击退更强，可以打破正面举盾格挡。"
                        : "Accurate direct fire with increased damage and knockback that breaks frontal shield blocks.";
                __result = true;
                return false;
            }
        }
    }
}
