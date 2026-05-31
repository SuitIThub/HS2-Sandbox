using System.Collections.Generic;
using System.Linq;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
        private const int StashWindowId = 2029;
        private const int StashUndockedWindowId = 2030;
        private const float StashPaneDefaultWidth = 360f;
        private const float StashFloatingMinWidth = 220f;
        private const float StashFloatingMinHeight = 180f;
        private const float StashFloatingMaxWidth = 720f;
        private const float StashFloatingMaxHeight = 900f;
        private const float StashFloatingDefaultWidth = 360f;
        private const float StashFloatingDefaultHeight = 420f;

        private readonly PoseBrowserStash _poseStash = new PoseBrowserStash();
        private bool _showDockedStash;
        private bool _showUndockedStash;
        private bool _stashPreferUndocked;
        private Rect _stashWindowRect;
        private Rect _stashFloatingRect;
        private float _savedStashFloatingW;
        private float _savedStashFloatingH;
        private float _savedStashFloatingX;
        private float _savedStashFloatingY;
        private bool _stashFloatingResizing;
        private Vector2 _stashScroll;
        private string? _pendingDeleteStashEntryId;
        private bool _showClearStashConfirm;

        /// <summary>Undocked stash draws even when the main Pose Browser window is closed.</summary>
        public bool HasIndependentUndockedStash => _showUndockedStash;

        public override bool ShouldDrawWhileHidden => HasIndependentUndockedStash;

        private bool IsStashDockedVisible => _showDockedStash && isVisible;
        private bool IsStashUndockedVisible => _showUndockedStash;
        private bool IsAnyStashVisible => _showDockedStash || _showUndockedStash;

        private void InitPoseStash()
        {
            _poseStash.LoadFromDisk();
            _stashWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, StashPaneDefaultWidth, windowRect.height);
            EnsureStashFloatingRectInitialized();
        }

        private void SavePoseStash() => _poseStash.SaveToDiskIfDirty();

        private void CaptureStashFloatingRect()
        {
            _savedStashFloatingW = _stashFloatingRect.width;
            _savedStashFloatingH = _stashFloatingRect.height;
            _savedStashFloatingX = _stashFloatingRect.x;
            _savedStashFloatingY = _stashFloatingRect.y;
        }

        private void RestoreStashFloatingRectFromSaved()
        {
            if (_savedStashFloatingW > 10f && _savedStashFloatingH > 10f)
            {
                _stashFloatingRect = new Rect(
                    _savedStashFloatingX,
                    _savedStashFloatingY,
                    Mathf.Clamp(_savedStashFloatingW, StashFloatingMinWidth, StashFloatingMaxWidth),
                    Mathf.Clamp(_savedStashFloatingH, StashFloatingMinHeight, StashFloatingMaxHeight));
            }
            else
            {
                EnsureStashFloatingRectInitialized();
            }
        }

        private void EnsureStashFloatingRectInitialized(bool fromDockedRect = false)
        {
            if (_savedStashFloatingW > 10f && _savedStashFloatingH > 10f && !fromDockedRect)
            {
                RestoreStashFloatingRectFromSaved();
                return;
            }

            float w = _savedStashFloatingW > 10f
                ? Mathf.Clamp(_savedStashFloatingW, StashFloatingMinWidth, StashFloatingMaxWidth)
                : StashFloatingDefaultWidth;
            float h = _savedStashFloatingH > 10f
                ? Mathf.Clamp(_savedStashFloatingH, StashFloatingMinHeight, StashFloatingMaxHeight)
                : StashFloatingDefaultHeight;

            if (fromDockedRect && _stashWindowRect.width > 1f)
            {
                _stashFloatingRect = new Rect(_stashWindowRect.x, _stashWindowRect.y, w, h);
                return;
            }

            float x = isVisible ? windowRect.xMax + DockedPaneGap : _savedStashFloatingX;
            float y = isVisible ? windowRect.y : _savedStashFloatingY;
            if (x < 4f)
                x = 40f;
            if (y < 4f)
                y = 40f;
            _stashFloatingRect = new Rect(x, y, w, h);
        }

        private void DrawStashPaneToggleButton(float height = 24f, float width = 64f)
        {
            if (GUILayout.Button(
                    IsAnyStashVisible ? "Stash ▶" : "Stash",
                    GUILayout.Width(width),
                    GUILayout.Height(height)))
                ToggleStashFromMainWindow();
        }

        private void DrawStashTopBarButton() => DrawStashPaneToggleButton();

        private void ToggleStashFromMainWindow()
        {
            if (IsAnyStashVisible)
            {
                if (_showUndockedStash)
                    CloseUndockedStash();
                else
                    CloseDockedStash();
                return;
            }

            OpenStashInPreferredMode();
        }

        private void OpenStashInPreferredMode()
        {
            if (_stashPreferUndocked)
            {
                _showUndockedStash = true;
                EnsureStashFloatingRectInitialized();
                return;
            }

            _showDockedStash = true;
        }

        private void ToggleUndockedStashViaHotkey()
        {
            if (_showUndockedStash)
            {
                CloseUndockedStash();
                return;
            }

            _stashPreferUndocked = true;
            _showUndockedStash = true;
            _showDockedStash = false;
            EnsureStashFloatingRectInitialized();
        }

        private void UndockStashPane()
        {
            _stashPreferUndocked = true;
            _showDockedStash = false;
            EnsureStashFloatingRectInitialized(fromDockedRect: true);
            _showUndockedStash = true;
            SavePersistedOptions();
        }

        private void DockStashPane()
        {
            CaptureStashFloatingRect();
            _stashPreferUndocked = false;
            _showUndockedStash = false;
            if (!isVisible)
                return;

            _showDockedStash = true;
            SavePersistedOptions();
        }

        private void CloseUndockedStash()
        {
            CaptureStashFloatingRect();
            _showUndockedStash = false;
            SavePersistedOptions();
        }

        private void CloseDockedStash() => _showDockedStash = false;

        private void OnMainPoseBrowserHidden()
        {
            _showDockedStash = false;
        }

        private void PerformStashSelectedCharacter()
        {
            var selected = _dataService.GetSelectedCharacters().ToList();
            if (selected.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Select exactly one character in Studio to stash a pose.");
                return;
            }

            if (selected.Count > 1)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Select only one character to stash a pose.");
                return;
            }

            if (!_poseStash.TryStashFromCharacter(selected[0], out _))
            {
                SandboxServices.Log.LogWarning("PoseBrowser: Could not stash pose from the selected character.");
                return;
            }

            _poseStash.SaveToDiskIfDirty();
            SandboxServices.Log.LogMessage($"PoseBrowser: Stashed pose from {PoseDataService.GetOCICharDisplayName(selected[0])}.");
        }

        private void ApplyStashEntry(PoseBrowserStashEntry entry)
        {
            var selected = _dataService.GetSelectedCharacters().ToList();
            if (selected.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Select one or more characters in Studio to apply a stashed pose.");
                return;
            }

            int applied = _poseStash.ApplyEntryToCharacters(entry, selected);
            if (applied == 0)
            {
                SandboxServices.Log.LogWarning("PoseBrowser: Could not apply stashed pose.");
                return;
            }

            if (_poseStash.AutoDeleteAfterApply)
            {
                _poseStash.RemoveEntry(entry.Id);
                if (_pendingDeleteStashEntryId == entry.Id)
                    _pendingDeleteStashEntryId = null;
            }

            _poseStash.SaveToDiskIfDirty();
        }

        private void ConfirmDeleteStashEntry(string entryId)
        {
            _poseStash.RemoveEntry(entryId);
            _pendingDeleteStashEntryId = null;
            _poseStash.SaveToDiskIfDirty();
        }

        private void ConfirmClearEntireStash()
        {
            _poseStash.ClearAll();
            _showClearStashConfirm = false;
            _pendingDeleteStashEntryId = null;
            _poseStash.SaveToDiskIfDirty();
        }

        private void DrawUndockedStashWindow()
        {
            HandleStashFloatingResize();

            _stashFloatingRect.width = Mathf.Clamp(_stashFloatingRect.width, StashFloatingMinWidth, StashFloatingMaxWidth);
            _stashFloatingRect.height = Mathf.Clamp(_stashFloatingRect.height, StashFloatingMinHeight, StashFloatingMaxHeight);
            _stashFloatingRect.x = Mathf.Clamp(_stashFloatingRect.x, 4f, Mathf.Max(4f, Screen.width - _stashFloatingRect.width - 4f));
            _stashFloatingRect.y = Mathf.Clamp(_stashFloatingRect.y, 4f, Mathf.Max(4f, Screen.height - _stashFloatingRect.height - 4f));

            _stashFloatingRect = GUI.Window(
                StashUndockedWindowId,
                _stashFloatingRect,
                DrawUndockedStashWindowContent,
                "Pose Browser · Stash");

            _stashFloatingRect.x = Mathf.Clamp(_stashFloatingRect.x, 4f, Mathf.Max(4f, Screen.width - _stashFloatingRect.width - 4f));
            _stashFloatingRect.y = Mathf.Clamp(_stashFloatingRect.y, 4f, Mathf.Max(4f, Screen.height - _stashFloatingRect.height - 4f));

            if (Event.current.type == EventType.MouseUp && !_stashFloatingResizing)
            {
                CaptureStashFloatingRect();
                SavePersistedOptions();
            }

            IMGUIUtils.EatInputInRect(_stashFloatingRect);
        }

        private void HandleStashFloatingResize()
        {
            Event? e = Event.current;
            if (e == null)
                return;

            var handleRect = new Rect(
                _stashFloatingRect.x + _stashFloatingRect.width - ResizeHandleSize,
                _stashFloatingRect.y + _stashFloatingRect.height - ResizeHandleSize,
                ResizeHandleSize,
                ResizeHandleSize);

            if (e.type == EventType.MouseDown && e.button == 0 && handleRect.Contains(e.mousePosition))
            {
                _stashFloatingResizing = true;
                e.Use();
            }
            else if (_stashFloatingResizing && e.type == EventType.MouseDrag && e.button == 0)
            {
                _stashFloatingRect.width = Mathf.Clamp(
                    e.mousePosition.x - _stashFloatingRect.x,
                    StashFloatingMinWidth,
                    StashFloatingMaxWidth);
                _stashFloatingRect.height = Mathf.Clamp(
                    e.mousePosition.y - _stashFloatingRect.y,
                    StashFloatingMinHeight,
                    StashFloatingMaxHeight);
                e.Use();
            }
            else if (_stashFloatingResizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _stashFloatingResizing = false;
                CaptureStashFloatingRect();
                SavePersistedOptions();
                e.Use();
            }
        }

        private void DrawStashWindowContent(int id)
        {
            DrawStashWindowHeader(showUndockButton: true, showDockButton: false, showCloseButton: false);
            DrawStashWindowBody(undocked: false);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawUndockedStashWindowContent(int id)
        {
            float innerW = Mathf.Max(1f, _stashFloatingRect.width - 16f);
            float innerH = Mathf.Max(1f, _stashFloatingRect.height - 24f);
            GUILayout.BeginArea(new Rect(8f, 22f, innerW, innerH));
            DrawStashWindowHeader(showUndockButton: false, showDockButton: true, showCloseButton: true);
            DrawStashWindowBody(undocked: true);
            GUILayout.EndArea();
            FinishUndockedStashWindowChrome();
        }

        private void FinishUndockedStashWindowChrome()
        {
            float w = _stashFloatingRect.width;
            float h = _stashFloatingRect.height;
            var resizeHandle = new Rect(w - ResizeHandleSize, h - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            GUI.Box(resizeHandle, new GUIContent("◢", "Resize"));
            GUI.DragWindow(new Rect(0f, 0f, w - ResizeHandleSize, 20f));
        }

        private void DrawStashWindowHeader(bool showUndockButton, bool showDockButton, bool showCloseButton)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (showUndockButton &&
                GUILayout.Button(
                    new GUIContent("Float", "Undock this panel — move and resize it independently."),
                    GUILayout.Width(44f),
                    GUILayout.Height(22f)))
            {
                UndockStashPane();
            }

            if (showDockButton &&
                GUILayout.Button(
                    new GUIContent("Dock", "Dock this panel beside the Pose Browser window."),
                    GUILayout.Width(44f),
                    GUILayout.Height(22f)))
            {
                DockStashPane();
            }

            if (showCloseButton &&
                GUILayout.Button(
                    new GUIContent("×", "Close the stash window."),
                    GUILayout.Width(WindowChromeButtonWidth),
                    GUILayout.Height(22f)))
            {
                CloseUndockedStash();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawStashWindowBody(bool undocked)
        {
            GUILayout.Label(
                "Stash the FK/IK pose from one selected character, then apply it to any Studio selection.",
                GUI.skin.label);

            if (GUILayout.Button("Stash selected character", GUILayout.Height(28f)))
                PerformStashSelectedCharacter();

            GUILayout.Space(6f);
            bool autoDelete = _poseStash.AutoDeleteAfterApply;
            bool newAutoDelete = GUILayout.Toggle(
                autoDelete,
                new GUIContent(
                    "Auto-delete after apply",
                    "Remove a stashed pose from the list after applying it."));
            if (newAutoDelete != autoDelete)
            {
                _poseStash.AutoDeleteAfterApply = newAutoDelete;
                _poseStash.SaveToDiskIfDirty();
            }

            GUILayout.Space(8f);
            if (undocked)
                DrawStashEntryList(GetUndockedStashScrollHeight(), scrollToPendingDelete: true);
            else
                DrawStashEntryList();

            GUILayout.Space(8f);
            DrawClearEntireStashControls();

            if (!undocked)
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("Close panel", GUILayout.Height(26f)))
                    CloseDockedStash();
            }
        }

        private float GetUndockedStashScrollHeight()
        {
            float innerH = Mathf.Max(1f, _stashFloatingRect.height - 24f);
            const float headerH = 22f;
            const float introH = 36f;
            const float stashBtnH = 28f;
            const float toggleBlockH = 28f;
            const float spacing = 22f;
            float footerH = _showClearStashConfirm ? 72f : 34f;
            float scrollH = innerH - headerH - introH - stashBtnH - toggleBlockH - spacing - footerH;
            return Mathf.Max(48f, scrollH);
        }

        private void DrawStashEntryList(float? fixedScrollHeight = null, bool scrollToPendingDelete = false)
        {
            var entries = _poseStash.GetEntriesNewestFirst().ToList();
            if (fixedScrollHeight.HasValue)
                _stashScroll = GUILayout.BeginScrollView(_stashScroll, GUILayout.Height(fixedScrollHeight.Value));
            else
                _stashScroll = GUILayout.BeginScrollView(_stashScroll, GUILayout.ExpandHeight(true));

            if (entries.Count == 0)
            {
                GUILayout.Label("No stashed poses yet.");
            }
            else
            {
                foreach (var entry in entries)
                    DrawStashEntryRow(entry);
            }

            if (scrollToPendingDelete)
                MaybeScrollStashToPendingDelete(entries, fixedScrollHeight ?? 0f);

            GUILayout.EndScrollView();
        }

        private void MaybeScrollStashToPendingDelete(IReadOnlyList<PoseBrowserStashEntry> entries, float viewHeight)
        {
            if (string.IsNullOrEmpty(_pendingDeleteStashEntryId) || viewHeight <= 0f)
                return;
            if (Event.current.type != EventType.Repaint)
                return;

            int index = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, _pendingDeleteStashEntryId, System.StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return;

            const float rowHeight = 44f;
            float rowTop = index * rowHeight;
            float rowBottom = rowTop + rowHeight;
            if (rowBottom > _stashScroll.y + viewHeight)
                _stashScroll.y = rowBottom - viewHeight + 4f;
            if (rowTop < _stashScroll.y)
                _stashScroll.y = rowTop;
        }

        private void DrawStashEntryRow(PoseBrowserStashEntry entry)
        {
            bool pendingDelete = string.Equals(_pendingDeleteStashEntryId, entry.Id, System.StringComparison.Ordinal);

            GUILayout.BeginVertical(GetStashEntryBoxStyle());
            GUILayout.BeginHorizontal();

            if (pendingDelete)
            {
                GUILayout.Label(
                    new GUIContent(entry.ListLabel, "Confirm delete for this stashed pose."),
                    GetStashEntryButtonStyle(),
                    GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Yes", GUILayout.Width(32f), GUILayout.Height(22f)))
                    ConfirmDeleteStashEntry(entry.Id);
                if (GUILayout.Button("No", GUILayout.Width(32f), GUILayout.Height(22f)))
                    _pendingDeleteStashEntryId = null;
            }
            else
            {
                if (GUILayout.Button(
                        new GUIContent(entry.ListLabel, "Apply this stashed pose to Studio-selected character(s)."),
                        GetStashEntryButtonStyle(),
                        GUILayout.ExpandWidth(true)))
                {
                    ApplyStashEntry(entry);
                }

                if (GUILayout.Button(
                             new GUIContent("x", "Delete this stashed pose (confirmation required)."),
                             GUILayout.Width(22f),
                             GUILayout.Height(22f)))
                {
                    _pendingDeleteStashEntryId = entry.Id;
                    _showClearStashConfirm = false;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawClearEntireStashControls()
        {
            if (_showClearStashConfirm)
            {
                GUILayout.Label("Delete all stashed poses? This cannot be undone.", GUI.skin.label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm delete", GUILayout.Height(26f), GUILayout.MinWidth(120f)))
                    ConfirmClearEntireStash();
                if (GUILayout.Button("Cancel", GUILayout.Height(26f), GUILayout.MinWidth(80f)))
                    _showClearStashConfirm = false;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUI.enabled = _poseStash.Entries.Count > 0;
                if (GUILayout.Button("Clear entire stash", GUILayout.Height(26f)))
                {
                    _showClearStashConfirm = true;
                    _pendingDeleteStashEntryId = null;
                }

                GUI.enabled = true;
            }
        }

        private GUIStyle? _stashEntryBoxStyle;
        private GUIStyle? _stashEntryButtonStyle;

        private GUIStyle GetStashEntryBoxStyle()
        {
            if (_stashEntryBoxStyle == null)
            {
                _stashEntryBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(8, 8, 6, 6),
                    margin = new RectOffset(0, 0, 4, 4),
                    wordWrap = true
                };
                _stashEntryBoxStyle.normal.background = MakeTex(8, 8, new Color(0.11f, 0.11f, 0.13f, 1f));
                _stashEntryBoxStyle.border = GUI.skin.box.border;
            }

            return _stashEntryBoxStyle;
        }

        private GUIStyle GetStashEntryButtonStyle()
        {
            if (_stashEntryButtonStyle != null)
                return _stashEntryButtonStyle;

            _stashEntryButtonStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 2, 2),
                stretchWidth = true
            };
            _stashEntryButtonStyle.normal.textColor = new Color(0.92f, 0.92f, 0.94f, 1f);
            _stashEntryButtonStyle.hover.textColor = new Color(0.75f, 0.88f, 1f, 1f);
            _stashEntryButtonStyle.active.textColor = new Color(0.65f, 0.82f, 1f, 1f);
            _stashEntryButtonStyle.hover.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.06f));
            _stashEntryButtonStyle.active.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
            return _stashEntryButtonStyle;
        }
    }
}
