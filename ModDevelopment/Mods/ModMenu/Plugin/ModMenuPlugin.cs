using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using RTM.UISystem;
using UnityEngine;
using Voxels.TowerDefense.UI;

namespace BadNorth.ModMenu
{
    [BepInPlugin(ModMenuBootstrap.MenuId, "Bad North Mod Menu", "1.1.1")]
    public sealed class ModMenuPlugin : BaseUnityPlugin
    {
        private ConfigEntry<KeyboardShortcut> shortcut;
        private MainMenu installedIn;
        private NativeModDialog dialog;

        private void Awake()
        {
            shortcut = Config.Bind("Menu", "Shortcut", new KeyboardShortcut(KeyCode.F10),
                "Open or close the local mod menu from the main menu.");
            dialog = new NativeModDialog(Logger);
        }

        private void Update()
        {
            UIManager ui = UIManager.instance;
            if (!ui) return;

            if (ui.activeMenu is MainMenu main && main && installedIn != main)
                InstallButton(main);

            if (!shortcut.Value.IsDown()) return;
            if (dialog.IsOpen) dialog.Close();
            else if (ui.activeMenu is MainMenu) dialog.Open();
        }

        private void InstallButton(MainMenu main)
        {
            installedIn = main;
            var settings = main.GetComponentsInChildren<ButtonWidget>(true)
                .FirstOrDefault(button => button.localize && button.localize.Term == "UI/COMMON/SETTINGS");
            ButtonWidget mods = main.AddButton("UI/COMMON/SETTINGS", dialog.Open);
            mods.gameObject.name = "Local Mods";
            mods.SetNonLocalizedLabel("模组").SetSuccessAudio("UI/Menu/Settings").SetHoverAudio(FabricID.uiHover);
            if (settings && mods.transform.parent == settings.transform.parent)
                mods.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);

            main.transform.ForceChildLayoutUpdates();
            Logger.LogInfo("Added Local Mods to the main menu.");
        }
    }
}
