using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using RTM.UISystem;
using UnityEngine;
using UnityEngine.UI;
using Voxels.TowerDefense.UI;
using Object = UnityEngine.Object;

namespace BadNorth.ModMenu
{
    internal sealed class NativeModDialog
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ModalMessage = typeof(ModalOverlay).GetField("message", PrivateInstance);
        private static readonly FieldInfo TogglePrefab = typeof(GeneratedMenu).GetField("multiSelectWidgetPrefab", PrivateInstance);
        private static readonly FieldInfo SettingsInstance = typeof(UserSettingsMenu).GetField("_instance",
            BindingFlags.Static | BindingFlags.NonPublic);

        private readonly ManualLogSource log;
        private ModalOverlay modal;
        private RectTransform body;
        private Text notice;
        private Dictionary<string, bool> saved;
        private Dictionary<string, bool> draft;

        public NativeModDialog(ManualLogSource log) => this.log = log;
        public bool IsOpen => modal && modal.isOpen;

        public bool Open()
        {
            if (IsOpen) return true;
            var settings = SettingsInstance?.GetValue(null) as UserSettingsMenu;
            var prefab = settings ? TogglePrefab?.GetValue(settings) as MultiSelectWidget : null;
            if (!ModMenuBootstrap.CatalogueReady || !prefab || ModalMessage == null)
            {
                ModalOverlay.GetInstance().InitializeNonLocalized("模组", "无法读取本地模组。请查看 BepInEx 日志。", false)
                    .AddOKButton();
                return true;
            }

            try
            {
                saved = PluginSelection.Read();
                draft = new Dictionary<string, bool>(saved, StringComparer.OrdinalIgnoreCase);
                modal = ModalOverlay.GetInstance();
                modal.InitializeNonLocalized("模组", "保存后重启游戏，开关才会生效。", true);
                var message = (Text)ModalMessage.GetValue(modal);
                AddBody(message, prefab);
                modal.AddOKButton(Save).SetNonLocalizedLabel("保存");
                modal.transform.ForceChildLayoutUpdates();
                return true;
            }
            catch (Exception error)
            {
                log.LogError(error);
                Close();
                ModalOverlay.GetInstance().InitializeNonLocalized("模组", "无法打开模组设置。请查看 BepInEx 日志。", false)
                    .AddOKButton();
                return true;
            }
        }

        public void Close()
        {
            if (IsOpen) modal.Dismiss();
        }

        private void AddBody(Text message, MultiSelectWidget prefab)
        {
            var layout = message.GetComponent<LayoutElement>();
            bool added = !layout;
            if (added) layout = message.gameObject.AddComponent<LayoutElement>();
            float previousWidth = layout.preferredWidth;
            float previousHeight = layout.preferredHeight;
            int previousPriority = layout.layoutPriority;
            TextAnchor previousAlignment = message.alignment;
            var mods = ModMenuBootstrap.Plugins.ToArray();
            float rowHeight = Mathf.Max(64f, LayoutUtility.GetPreferredHeight((RectTransform)prefab.transform));
            const float rowSpacing = 8f;
            const float messageSpace = 54f;
            const float noticeSpace = 34f;
            layout.preferredWidth = 864f;
            layout.preferredHeight = messageSpace + noticeSpace
                + Mathf.Clamp(mods.Length * (rowHeight + rowSpacing) + rowSpacing, 96f, 440f);
            layout.layoutPriority = 10;
            message.alignment = TextAnchor.UpperCenter;

            modal.SetCloseAction(() =>
            {
                if (body)
                {
                    body.gameObject.SetActive(false);
                    Object.Destroy(body.gameObject);
                }
                if (added) Object.Destroy(layout);
                else
                {
                    layout.preferredWidth = previousWidth;
                    layout.preferredHeight = previousHeight;
                    layout.layoutPriority = previousPriority;
                }
                message.alignment = previousAlignment;
                body = null;
                notice = null;
                modal = null;
            });

            body = AddRect("Local Mod Settings", message.rectTransform);
            Stretch(body, Vector2.zero, Vector2.zero);
            var viewport = AddRect("Installed Mod List", body);
            Stretch(viewport, new Vector2(0, noticeSpace), new Vector2(0, -messageSpace));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = AddRect("Mod Rows", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var rows = content.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.spacing = rowSpacing;
            rows.padding = new RectOffset(4, 4, 4, 4);
            rows.childControlWidth = true;
            rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            notice = AddLabel(string.Empty, message, body);
            notice.rectTransform.anchorMin = Vector2.zero;
            notice.rectTransform.anchorMax = Vector2.right;
            notice.rectTransform.offsetMin = Vector2.zero;
            notice.rectTransform.offsetMax = new Vector2(0, 30);
            foreach (LocalMod mod in mods)
                AddRow(mod, prefab, content, rowHeight, scroll);

            RefreshNotice();
        }

        private void AddRow(LocalMod mod, MultiSelectWidget prefab, RectTransform parent, float minHeight, ScrollRect scroll)
        {
            var row = Object.Instantiate(prefab, parent, false);
            row.SetOnOff().Initialize("UI/COMMON/SETTINGS", () => NextEnabled(draft, mod) ? 1 : 0,
                value =>
                {
                    draft[mod.Key] = value != 0;
                    RefreshNotice();
                    return true;
                });

            bool loaded = Chainloader.PluginInfos.Values.Any(plugin =>
                string.Equals(plugin.Location, mod.ActivePath, StringComparison.OrdinalIgnoreCase));
            string status = mod.ConflictingFiles ? "文件冲突" : mod.ChangeFailed ? "切换失败" : loaded ? "已加载" : "未加载";
            row.SetNonLocalizedLabel(mod.Name + "（" + status + "）");
            var layout = row.GetComponent<LayoutElement>();
            if (!layout) layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = Mathf.Max(layout.minHeight, minHeight);
            layout.preferredHeight = Mathf.Max(layout.preferredHeight, minHeight);
            if (mod.IsMenu || mod.ConflictingFiles)
            {
                foreach (UIInteractable control in row.GetComponentsInChildren<UIInteractable>(true))
                    control.disabled = true;
            }
            row.ForceUpdate();
            var navigable = row.GetComponentInChildren<UINavigable>(true);
            if (navigable)
                navigable.onFocusChanged += focused =>
                {
                    if (focused) RevealRow(scroll, (RectTransform)row.transform);
                };
        }

        private void RefreshNotice()
        {
            bool saveCompatibilityOff = ModMenuBootstrap.Plugins.Any(
                mod => mod.IsSaveCompatibility && !NextEnabled(draft, mod));
            notice.text = saveCompatibilityOff ? "关闭存档兼容补丁可能让旧档再次无法读取。" : string.Empty;
        }

        private bool Save()
        {
            if (!ModMenuBootstrap.Plugins.Any(mod => NextEnabled(saved, mod) != NextEnabled(draft, mod))) return true;
            try
            {
                PluginSelection.Save(draft);
                log.LogInfo("Saved local mod selection for the next launch.");
                return true;
            }
            catch (Exception error)
            {
                notice.text = "保存失败。请查看 BepInEx 日志。";
                log.LogError(error);
                return false;
            }
        }

        private static bool NextEnabled(Dictionary<string, bool> selection, LocalMod mod) =>
            selection.TryGetValue(mod.Key, out bool enabled) ? enabled : mod.IsEnabled;

        private static RectTransform AddRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static Text AddLabel(string value, Text style, Transform parent)
        {
            var label = AddRect("Text", parent).gameObject.AddComponent<Text>();
            label.font = style.font;
            label.fontSize = style.fontSize;
            label.color = style.color;
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = value;
            return label;
        }

        private static void RevealRow(ScrollRect scroll, RectTransform row)
        {
            float travel = scroll.content.rect.height - scroll.viewport.rect.height;
            if (travel <= 0) return;

            float offset = (1 - scroll.verticalNormalizedPosition) * travel;
            float rowTop = -row.anchoredPosition.y - row.rect.height * (1 - row.pivot.y);
            float minOffset = rowTop + row.rect.height - scroll.viewport.rect.height;
            scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(Mathf.Clamp(offset, minOffset, rowTop) / travel);
        }
    }
}
