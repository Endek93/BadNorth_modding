using System;
using BepInEx;
using BepInEx.Configuration;
using RTM.UISystem;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.ProfileInternals;
using Voxels.TowerDefense.UI;
using Voxels.TowerDefense.UI.UpgradeScreen;

namespace BadNorth.CampaignGold
{
    [BepInPlugin("local.badnorth.campaign-gold", "Bad North Campaign Gold", "1.0.0")]
    public sealed class CampaignGoldPlugin : BaseUnityPlugin
    {
        private const int MaximumBalance = 999999;
        private ConfigEntry<KeyboardShortcut> addShortcut;
        private ConfigEntry<KeyboardShortcut> setShortcut;
        private ConfigEntry<int> addAmount;
        private ConfigEntry<int> targetBalance;

        private void Awake()
        {
            addShortcut = Config.Bind("Gold", "AddShortcut", new KeyboardShortcut(KeyCode.F6),
                "Adds AddAmount on the campaign map.");
            setShortcut = Config.Bind("Gold", "SetShortcut", new KeyboardShortcut(KeyCode.F7),
                "Sets the balance to TargetBalance on the campaign map.");
            addAmount = Config.Bind("Gold", "AddAmount", 100,
                "Amount added per press. A negative amount removes gold.");
            targetBalance = Config.Bind("Gold", "TargetBalance", 999,
                "Balance to set. The resulting balance is limited to 0 through " + MaximumBalance + ".");
        }

        private void Update()
        {
            bool replace = setShortcut.Value.IsDown();
            if (!replace && !addShortcut.Value.IsDown()) return;

            UIManager ui = UIManager.instance;
            if (!ui || !(ui.activeMenu is CampaignMapUI map) || map.blockingInput || Profile.Loading) return;

            CampaignSave save = Profile.campaign;
            SuperUpgradeMenu upgrades = SuperUpgradeMenu.instance;
            if (save == null || save.gameOver || Profile.activeCampaignMeta == null || !upgrades) return;

            int previous = save.coinBank;
            if (!ChangeBalance(save, replace ? targetBalance.Value : addAmount.Value, replace)) return;

            upgrades.UpdateCoinBank();
            Profile.SaveCampaign();
            Logger.LogInfo("Campaign gold changed from " + previous + " to " + save.coinBank + ".");
        }

        private static bool ChangeBalance(CampaignSave save, int amount, bool replace)
        {
            long requested = replace ? amount : (long)save.coinBank + amount;
            int newBalance = (int)Math.Min(MaximumBalance, Math.Max(0L, requested));
            if (newBalance == save.coinBank) return false;

            save.coinBank = newBalance;
            return true;
        }
    }
}
