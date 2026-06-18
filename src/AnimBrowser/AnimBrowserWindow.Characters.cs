using System;
using System.Collections.Generic;
using System.Linq;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const float CharacterPaneDefaultWidthBase = 300f;
        private float CharacterPaneDefaultWidth => AnimBrowserScale.Px(CharacterPaneDefaultWidthBase);

        private readonly StudioCharacterPriorityList _characterConfig = new StudioCharacterPriorityList();
        private bool _showCharacterConfigPane;
        private Rect _characterConfigWindowRect;
        private Vector2 _characterConfigScroll;
        private int _selectedSlotIndex = -1;
        private GUIStyle? _characterHintStyle;

        private void DrawCharacterConfigWindowContent(int id)
        {
            StudioCharacterConfigPaneUi.DrawBody(
                ref _characterConfigScroll,
                ref _selectedSlotIndex,
                _characterConfig,
                GetCachedStudioSelectedCharacters(),
                new StudioCharacterConfigPaneUi.Layout
                {
                    W = AnimBrowserScale.W,
                    H = AnimBrowserScale.H,
                    DragHeaderHeight = AnimBrowserScale.Px(20f)
                },
                "Priority list for multi-character animation apply. Top = highest priority.",
                () =>
                {
                    _showCharacterConfigPane = false;
                    _options.showCharacterConfigPane = false;
                    SavePersistedOptions();
                });
        }

        private void DrawTopBarCharacterSection(float controlHeight)
        {
            InitCharacterHintStyle();
            var names = GetCachedStudioCharacterDisplayNames();

            bool charPaneOpen = _showCharacterConfigPane;
            float charBtnWidth = charPaneOpen ? 76f : 52f;
            if (GUILayout.Button(
                    charPaneOpen ? "Chars ▶" : "Chars",
                    AnimBrowserScale.W(charBtnWidth),
                    AnimBrowserScale.H(controlHeight)))
            {
                _showCharacterConfigPane = !charPaneOpen;
                _options.showCharacterConfigPane = _showCharacterConfigPane;
                SavePersistedOptions();
                if (_showCharacterConfigPane)
                    _characterConfig.ReloadFromDisk();
            }

            GUILayoutOption[] labelOpts = { GUILayout.ExpandWidth(true), GUILayout.MinWidth(0f) };
            if (names.Count == 0)
            {
                GUILayout.Label(
                    new GUIContent(
                        "Character: none",
                        "Select one or more characters in Studio."),
                    _characterHintStyle!,
                    labelOpts);
            }
            else if (names.Count == 1)
            {
                GUILayout.Label(new GUIContent("Character: " + names[0], names[0]), _characterHintStyle!, labelOpts);
            }
            else
            {
                GUILayout.Label(
                    new GUIContent("Character: " + names.Count + " selected", string.Join("\n", names.ToArray())),
                    _characterHintStyle!,
                    labelOpts);
            }
        }

        private void InitCharacterHintStyle()
        {
            if (_characterHintStyle != null)
                return;
            _characterHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, GUI.skin.label.fontSize - 1),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.62f, 0.66f, 0.7f) }
            };
        }

        private void SyncAllDockedPaneRects()
        {
            float x = windowRect.xMax + DockedPaneGap;
            if (IsControlsDockedVisible)
                x = PlaceDockedPane(ref _controlsWindowRect, x, ControlsPaneDefaultWidth);
            if (_showOptionsPane)
                x = PlaceDockedPane(ref _optionsWindowRect, x, OptionsPaneDefaultWidth);
            if (_showHelpPane)
                x = PlaceDockedPane(ref _helpWindowRect, x, HelpPaneDefaultWidth);
            if (_showCharacterConfigPane)
                x = PlaceDockedPane(ref _characterConfigWindowRect, x, CharacterPaneDefaultWidth);
            if (_showReviewPane)
                PlaceDockedPane(ref _reviewWindowRect, x, ReviewPaneDefaultWidth);

            ShiftOpenDockedPanesLeftToFitScreen();
        }

        private float PlaceDockedPane(ref Rect pane, float x, float defaultWidth)
        {
            float w = pane.width > 1f ? pane.width : defaultWidth;
            pane = new Rect(x, windowRect.y, w, windowRect.height);
            return x + w + DockedPaneGap;
        }

        private void ShiftOpenDockedPanesLeftToFitScreen()
        {
            const float margin = 4f;
            if (!TryGetOpenDockedPaneBounds(out _, out float maxX))
                return;

            float overflow = maxX - (Screen.width - margin);
            if (overflow <= 0f)
                return;

            if (IsControlsDockedVisible)
                ShiftPaneX(ref _controlsWindowRect, -overflow);
            if (_showOptionsPane)
                ShiftPaneX(ref _optionsWindowRect, -overflow);
            if (_showHelpPane)
                ShiftPaneX(ref _helpWindowRect, -overflow);
            if (_showCharacterConfigPane)
                ShiftPaneX(ref _characterConfigWindowRect, -overflow);
            if (_showReviewPane)
                ShiftPaneX(ref _reviewWindowRect, -overflow);
        }

        private bool TryGetOpenDockedPaneBounds(out float minX, out float maxX)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            bool any = false;

            if (IsControlsDockedVisible)
            {
                any = true;
                minX = Mathf.Min(minX, _controlsWindowRect.x);
                maxX = Mathf.Max(maxX, _controlsWindowRect.xMax);
            }

            if (_showOptionsPane)
            {
                any = true;
                minX = Mathf.Min(minX, _optionsWindowRect.x);
                maxX = Mathf.Max(maxX, _optionsWindowRect.xMax);
            }

            if (_showHelpPane)
            {
                any = true;
                minX = Mathf.Min(minX, _helpWindowRect.x);
                maxX = Mathf.Max(maxX, _helpWindowRect.xMax);
            }

            if (_showCharacterConfigPane)
            {
                any = true;
                minX = Mathf.Min(minX, _characterConfigWindowRect.x);
                maxX = Mathf.Max(maxX, _characterConfigWindowRect.xMax);
            }

            if (_showReviewPane)
            {
                any = true;
                minX = Mathf.Min(minX, _reviewWindowRect.x);
                maxX = Mathf.Max(maxX, _reviewWindowRect.xMax);
            }

            return any;
        }

        private static void ShiftPaneX(ref Rect pane, float dx)
        {
            pane = new Rect(pane.x + dx, pane.y, pane.width, pane.height);
        }

        private void DrawAllDockedPanes()
        {
            if (IsControlsDockedVisible)
            {
                DrawDockedPaneWindow(
                    SandboxImguiWindowIds.AnimBrowser.Controls,
                    ref _controlsWindowRect,
                    DrawControlsWindowContent,
                    "Anim Browser · Controls",
                    ControlsPaneDefaultWidth);
            }

            if (_showOptionsPane)
            {
                DrawDockedPaneWindow(
                    SandboxImguiWindowIds.AnimBrowser.Options,
                    ref _optionsWindowRect,
                    DrawOptionsWindowContent,
                    "Anim Browser · Options",
                    OptionsPaneDefaultWidth);
            }

            if (_showHelpPane)
            {
                DrawDockedPaneWindow(
                    SandboxImguiWindowIds.AnimBrowser.Help,
                    ref _helpWindowRect,
                    DrawHelpWindowContent,
                    "Anim Browser · Help",
                    HelpPaneDefaultWidth);
            }

            if (_showCharacterConfigPane)
            {
                DrawDockedPaneWindow(
                    SandboxImguiWindowIds.AnimBrowser.Characters,
                    ref _characterConfigWindowRect,
                    DrawCharacterConfigWindowContent,
                    "Anim Browser · Characters",
                    CharacterPaneDefaultWidth);
            }

            if (_showReviewPane)
            {
                DrawDockedPaneWindow(
                    SandboxImguiWindowIds.AnimBrowser.GroupReview,
                    ref _reviewWindowRect,
                    DrawGroupReviewWindowContent,
                    "Anim Browser · Group Review",
                    ReviewPaneDefaultWidth);
            }
        }

        private IList<OCIChar> GetApplyTargetCharacters()
        {
            RefreshStudioSelectionCacheIfDue(force: true);
            var selected = GetCachedStudioSelectedCharacters();
            if (selected.Count == 0)
                return selected;

            _characterConfig.ReloadFromDisk();
            return StudioCharacterPriorityResolver.ResolveForApply(_characterConfig, selected);
        }
    }
}
