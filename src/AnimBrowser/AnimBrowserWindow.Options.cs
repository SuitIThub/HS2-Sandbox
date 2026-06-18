using System;
using BepInEx.Configuration;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const float OptionsPaneDefaultWidthBase = 420f;
        private float OptionsPaneDefaultWidth => AnimBrowserScale.Px(OptionsPaneDefaultWidthBase);
        private const float HotkeyBindingColumnWidthBase = 128f;
        private float HotkeyBindingColumnWidth => AnimBrowserScale.Px(HotkeyBindingColumnWidthBase);

        private bool _showOptionsPane;
        private Rect _optionsWindowRect;
        private Vector2 _optionsScroll;
        private bool _pendingDissolveAllConfirm;
        private string _optionsCardSizeLabel = string.Empty;
        private int _optionsCardSizeLabelRounded = -1;
        private string _optionsUiScaleLabel = string.Empty;
        private float _optionsUiScaleLabelValue = float.NaN;

        private GUIStyle? _optionsWrapStyle;
        private GUIStyle? _hotkeySectionBoxStyle;
        private GUIStyle? _hotkeyHeaderStyle;
        private GUIStyle? _hotkeyRowBoxStyle;
        private GUIStyle? _hotkeyActionStyle;
        private GUIStyle? _hotkeyBindingBadgeStyle;
        private GUIStyle? _hotkeyUnassignedBadgeStyle;

        private static readonly GUIContent GcOptionsOn = new GUIContent("Options ▶", "Hide options pane");
        private static readonly GUIContent GcOptionsOff = new GUIContent("Options", "Show options pane");
        private static readonly GUIContent GcUiScale = new GUIContent(
            "UI scale",
            "Enlarges the whole Anim Browser (text, buttons, panels, cards). Helps on 4K / high-DPI.");
        private static readonly GUIContent GcGridSize = new GUIContent("Card size", "Minimum width of animation cards in the grid view.");
        private static readonly GUIContent GcHideNonStudioCatalog = new GUIContent(
            "Hide non-Studio animation lists",
            "Remove animation groups/categories that are not registered in Studio's category tree "
            + "(dicAGroupCategory). These often appear as \"Group 101\" / \"Category 2018\" and come from "
            + "H-scene-only lists loaded via dicAnimeLoadInfo. Named abdata Studio animations are kept. On by default.");
        private static readonly GUIContent GcDissolveAll = new GUIContent(
            "Dissolve all groups…",
            "Remove every display group and tree merge (debug reset).");
        private static readonly GUIContent GcNoGroupingHint = new GUIContent("No groups or tree merges to remove.");
        private static readonly GUIContent GcEmptyHint = new GUIContent(" ");
        private static readonly GUIContent GcControlsProximity = new GUIContent(
            "Group controls by proximity",
            "When enabled, characters sharing the same animation group are only merged into one controls box if they are within "
            + AnimBrowserConfig.ControlsProximityRadius.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
            + " world units of each other.");

        private GUIStyle GetOptionsWrapStyle()
        {
            if (_optionsWrapStyle == null)
                _optionsWrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            return _optionsWrapStyle;
        }

        private bool DrawOptionsToggle(bool value, GUIContent content)
        {
            GUILayout.BeginHorizontal();
            bool v = GUILayout.Toggle(value, GUIContent.none, AnimBrowserScale.W(18f));
            GUILayout.Label(content, GetOptionsWrapStyle(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return v;
        }

        private void DrawOptionsWindowContent(int paneId)
        {
            var wrap = GetOptionsWrapStyle();

            _optionsScroll = GUILayout.BeginScrollView(_optionsScroll, false, true, GUILayout.ExpandHeight(true));

            AnimBrowserConfig.Register(SandboxServices.Config);

            GUILayout.Label("UI", GUI.skin.label);
            GUILayout.Space(4f);
            GUILayout.Label(
                "Enlarges the whole Anim Browser (text, buttons, panels, cards). Same setting as BepInEx → Anim Browser → UI scale.",
                wrap);
            float uiScale = AnimBrowserScale.Factor;
            float newUiScale = GUILayout.HorizontalSlider(uiScale, AnimBrowserScale.MinFactor, AnimBrowserScale.MaxFactor);
            newUiScale = Mathf.Round(newUiScale / 0.05f) * 0.05f;
            if (Mathf.Abs(newUiScale - uiScale) > 0.001f)
            {
                AnimBrowserConfig.UiScale!.Value = Mathf.Clamp(
                    newUiScale,
                    AnimBrowserScale.MinFactor,
                    AnimBrowserScale.MaxFactor);
            }

            if (!Mathf.Approximately(_optionsUiScaleLabelValue, AnimBrowserScale.Factor))
            {
                _optionsUiScaleLabelValue = AnimBrowserScale.Factor;
                _optionsUiScaleLabel = _optionsUiScaleLabelValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "× UI scale";
            }
            GUILayout.Label(_optionsUiScaleLabel, wrap);

            GUILayout.Space(12f);
            GUILayout.Label("Display", GUI.skin.label);
            GUILayout.Space(4f);

            GUILayout.Label(GcGridSize, wrap);
            float newCard = GUILayout.HorizontalSlider(_cardCellSize, MinCardSize, MaxCardSize);
            if (Mathf.Abs(newCard - _cardCellSize) > 0.001f)
            {
                _cardCellSize = newCard;
                _options.cardCellSize = _cardCellSize;
                _optionsCardSizeLabelRounded = -1;
                InvalidateGridLayoutCache();
                SavePersistedOptions();
            }

            int roundedCard = Mathf.RoundToInt(_cardCellSize);
            if (roundedCard != _optionsCardSizeLabelRounded)
            {
                _optionsCardSizeLabelRounded = roundedCard;
                _optionsCardSizeLabel = roundedCard + " px column";
            }
            GUILayout.Label(_optionsCardSizeLabel, wrap);

            bool newHideNonStudio = DrawOptionsToggle(_hideNonStudioCatalogAnimations, GcHideNonStudioCatalog);
            if (newHideNonStudio != _hideNonStudioCatalogAnimations)
            {
                _hideNonStudioCatalogAnimations = newHideNonStudio;
                _options.hideNonStudioCatalogAnimations = _hideNonStudioCatalogAnimations;
                _displayCatalog.SetHideNonStudioCatalogAnimations(_hideNonStudioCatalogAnimations);
                InvalidateAnimBrowserViewCaches();
                SavePersistedOptions();
            }

            GUILayout.Space(12f);
            GUILayout.Label("Controls", GUI.skin.label);
            GUILayout.Space(4f);

            bool newProximity = DrawOptionsToggle(_controlsGroupByProximity, GcControlsProximity);
            if (newProximity != _controlsGroupByProximity)
            {
                _controlsGroupByProximity = newProximity;
                _options.controlsGroupByProximity = _controlsGroupByProximity;
                _controlsSelectionFingerprint = -1;
                SavePersistedOptions();
            }

            GUILayout.Space(12f);
            DrawHotkeyOptionsSection(wrap);

            GUILayout.Space(12f);
            GUILayout.Label("Debug", GUI.skin.label);
            GUILayout.Space(4f);

            int groups = _groupStore.DisplayGroupCount;
            int merges = _groupStore.TreeMergeCount;
            bool hasGrouping = groups > 0 || merges > 0;

            if (_pendingDissolveAllConfirm)
            {
                GUILayout.Label(
                    "Remove " + groups + " display group(s) and " + merges + " tree merge(s)?",
                    wrap);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm", AnimBrowserScale.H(26f), GUILayout.ExpandWidth(true)))
                {
                    _groupStore.ClearAllGrouping();
                    _selectedGroupIds.Clear();
                    _pendingDissolveAllConfirm = false;
                }
                if (GUILayout.Button("Cancel", AnimBrowserScale.H(26f), GUILayout.ExpandWidth(true)))
                    _pendingDissolveAllConfirm = false;
                GUILayout.EndHorizontal();
            }
            else
            {
                bool prevEnabled = GUI.enabled;
                GUI.enabled = hasGrouping;
                if (GUILayout.Button(GcDissolveAll, AnimBrowserScale.H(28f), GUILayout.ExpandWidth(true)))
                    _pendingDissolveAllConfirm = true;
                GUI.enabled = prevEnabled;
                GUILayout.Label(hasGrouping ? GcEmptyHint : GcNoGroupingHint, wrap);
            }

            GUILayout.EndScrollView();
        }

        private void InitHotkeyOptionStyles()
        {
            if (_hotkeySectionBoxStyle != null)
                return;

            _hotkeySectionBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };
            _hotkeySectionBoxStyle.normal.background = MakeTex(8, 8, new Color(0.09f, 0.09f, 0.11f, 1f));
            _hotkeySectionBoxStyle.border = GUI.skin.box.border;

            _hotkeyHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.72f, 0.76f, 0.82f, 1f) }
            };

            _hotkeyRowBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 5, 5),
                margin = new RectOffset(0, 0, 3, 3)
            };
            _hotkeyRowBoxStyle.normal.background = MakeTex(8, 8, new Color(0.12f, 0.12f, 0.15f, 1f));
            _hotkeyRowBoxStyle.border = GUI.skin.box.border;

            _hotkeyActionStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.93f, 0.96f, 1f) }
            };

            _hotkeyBindingBadgeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 4, 4),
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            _hotkeyBindingBadgeStyle.normal.background = MakeTex(6, 6, new Color(0.18f, 0.24f, 0.32f, 1f));
            _hotkeyBindingBadgeStyle.normal.textColor = new Color(0.82f, 0.9f, 1f, 1f);
            _hotkeyBindingBadgeStyle.border = GUI.skin.box.border;

            _hotkeyUnassignedBadgeStyle = new GUIStyle(_hotkeyBindingBadgeStyle)
            {
                fontStyle = FontStyle.Italic
            };
            _hotkeyUnassignedBadgeStyle.normal.background = MakeTex(6, 6, new Color(0.14f, 0.14f, 0.16f, 1f));
            _hotkeyUnassignedBadgeStyle.normal.textColor = new Color(0.55f, 0.58f, 0.62f, 1f);
        }

        private void DrawHotkeyOptionsSection(GUIStyle introStyle)
        {
            GUILayout.Label("Keyboard shortcuts", introStyle);
            GUILayout.Label(
                "Read-only overview. Assign keys in BepInEx → Configuration Manager → Anim Browser · Keyboard shortcuts.",
                introStyle);
            GUILayout.Space(6f);

            InitHotkeyOptionStyles();

            GUILayout.BeginVertical(_hotkeySectionBoxStyle!);
            DrawHotkeyColumnHeader();
            DrawHotkeyReadonlyRow("Toggle undocked controls", AnimBrowserConfig.HotkeyToggleUndockedControls);
            GUILayout.EndVertical();
        }

        private void DrawHotkeyColumnHeader()
        {
            GUILayout.BeginHorizontal(AnimBrowserScale.H(20f));
            GUILayout.Label("Action", _hotkeyHeaderStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("Key", _hotkeyHeaderStyle, GUILayout.Width(HotkeyBindingColumnWidth));
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        private void DrawHotkeyReadonlyRow(string label, ConfigEntry<KeyboardShortcut>? entry)
        {
            if (entry == null)
                return;

            InitHotkeyOptionStyles();
            KeyboardShortcut shortcut = entry.Value;
            bool unassigned = IsHotkeyUnassigned(shortcut);
            string bindingText = FormatHotkeyBindingText(shortcut);
            var badgeStyle = unassigned ? _hotkeyUnassignedBadgeStyle! : _hotkeyBindingBadgeStyle!;

            GUILayout.BeginVertical(_hotkeyRowBoxStyle!);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                new GUIContent(label, entry.Description?.Description ?? ""),
                _hotkeyActionStyle,
                GUILayout.ExpandWidth(true),
                AnimBrowserScale.MinH(26f));
            GUILayout.Label(bindingText, badgeStyle, GUILayout.Width(HotkeyBindingColumnWidth), AnimBrowserScale.MinH(26f));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static bool IsHotkeyUnassigned(KeyboardShortcut shortcut) =>
            shortcut.MainKey == KeyCode.None;

        private static string FormatHotkeyBindingText(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return "Not assigned";

            string text = shortcut.ToString();
            if (StringEx.IsNullOrWhiteSpace(text) ||
                string.Equals(text, "Not set", StringComparison.OrdinalIgnoreCase))
                return "Not assigned";

            return text;
        }
    }
}
