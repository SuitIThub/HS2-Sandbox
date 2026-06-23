using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const int ControlsUndockedWindowId = SandboxImguiWindowIds.AnimBrowser.ControlsUndocked;
        private const float ControlsFloatingMinWidth = 220f;
        private const float ControlsFloatingMinHeight = 180f;
        private const float ControlsFloatingMaxWidth = 1080f;
        private const float ControlsFloatingMaxHeight = 900f;
        private const float ControlsFloatingDefaultWidthBase = 480f;
        private const float ControlsFloatingDefaultHeightBase = 480f;
        private float ControlsFloatingDefaultWidth => AnimBrowserScale.Px(ControlsFloatingDefaultWidthBase);
        private float ControlsFloatingDefaultHeight => AnimBrowserScale.Px(ControlsFloatingDefaultHeightBase);

        private bool _showDockedControls;
        private bool _showUndockedControls;
        private bool _controlsPreferUndocked;
        private Rect _controlsFloatingRect;
        private float _savedControlsFloatingW;
        private float _savedControlsFloatingH;
        private float _savedControlsFloatingX;
        private float _savedControlsFloatingY;
        private bool _controlsFloatingResizing;

        /// <summary>Undocked controls draw even when the main Anim Browser window is closed.</summary>
        public bool HasIndependentUndockedControls => _showUndockedControls;

        public override bool ShouldDrawWhileHidden => HasIndependentUndockedControls;

        private bool IsControlsDockedVisible => _showDockedControls && isVisible && !_isMinimized;
        private bool IsControlsUndockedVisible => _showUndockedControls;
        private bool IsAnyControlsVisible => _showDockedControls || _showUndockedControls;

        private void RestoreControlsPaneStateFromOptions()
        {
            _controlsPreferUndocked = _options.controlsPreferUndocked;
            if (_options.controlsFloatingW > 10f)
            {
                _savedControlsFloatingW = _options.controlsFloatingW;
                _savedControlsFloatingH = _options.controlsFloatingH;
                _savedControlsFloatingX = _options.controlsFloatingX;
                _savedControlsFloatingY = _options.controlsFloatingY;
            }

            RestoreControlsFloatingRectFromSaved();

            if (!_options.showControlsPane)
            {
                _showDockedControls = false;
                _showUndockedControls = false;
                return;
            }

            OpenControlsInPreferredMode();
        }

        private void CaptureControlsFloatingRect()
        {
            _savedControlsFloatingW = _controlsFloatingRect.width;
            _savedControlsFloatingH = _controlsFloatingRect.height;
            _savedControlsFloatingX = _controlsFloatingRect.x;
            _savedControlsFloatingY = _controlsFloatingRect.y;
            _options.controlsFloatingW = _savedControlsFloatingW;
            _options.controlsFloatingH = _savedControlsFloatingH;
            _options.controlsFloatingX = _savedControlsFloatingX;
            _options.controlsFloatingY = _savedControlsFloatingY;
        }

        private void RestoreControlsFloatingRectFromSaved()
        {
            if (_savedControlsFloatingW > 10f && _savedControlsFloatingH > 10f)
            {
                _controlsFloatingRect = new Rect(
                    _savedControlsFloatingX,
                    _savedControlsFloatingY,
                    Mathf.Clamp(_savedControlsFloatingW, ControlsFloatingMinWidth, ControlsFloatingMaxWidth),
                    Mathf.Clamp(_savedControlsFloatingH, ControlsFloatingMinHeight, ControlsFloatingMaxHeight));
            }
            else
            {
                EnsureControlsFloatingRectInitialized();
            }
        }

        private void EnsureControlsFloatingRectInitialized(bool fromDockedRect = false)
        {
            if (_savedControlsFloatingW > 10f && _savedControlsFloatingH > 10f && !fromDockedRect)
            {
                RestoreControlsFloatingRectFromSaved();
                return;
            }

            float w = _savedControlsFloatingW > 10f
                ? Mathf.Clamp(_savedControlsFloatingW, ControlsFloatingMinWidth, ControlsFloatingMaxWidth)
                : ControlsFloatingDefaultWidth;
            float h = _savedControlsFloatingH > 10f
                ? Mathf.Clamp(_savedControlsFloatingH, ControlsFloatingMinHeight, ControlsFloatingMaxHeight)
                : ControlsFloatingDefaultHeight;

            if (fromDockedRect && _controlsWindowRect.width > 1f)
            {
                _controlsFloatingRect = new Rect(_controlsWindowRect.x, _controlsWindowRect.y, w, h);
                return;
            }

            float x = isVisible && !_isMinimized ? windowRect.xMax + DockedPaneGap : _savedControlsFloatingX;
            float y = isVisible && !_isMinimized ? windowRect.y : _savedControlsFloatingY;
            if (x < 1f)
                x = 120f;
            if (y < 1f)
                y = 80f;
            _controlsFloatingRect = new Rect(x, y, w, h);
        }

        private void ToggleControlsFromMainWindow()
        {
            if (IsAnyControlsVisible)
            {
                if (_showUndockedControls)
                    CloseUndockedControls();
                else
                    CloseDockedControls();
                return;
            }

            OpenControlsInPreferredMode();
            _options.showControlsPane = true;
            SavePersistedOptions();
        }

        private void OpenControlsInPreferredMode()
        {
            if (_controlsPreferUndocked)
            {
                _showUndockedControls = true;
                EnsureControlsFloatingRectInitialized();
                return;
            }

            _showDockedControls = true;
        }

        private void ToggleUndockedControlsViaHotkey()
        {
            if (_showUndockedControls)
            {
                CloseUndockedControls();
                return;
            }

            _controlsPreferUndocked = true;
            _showUndockedControls = true;
            _showDockedControls = false;
            _options.showControlsPane = true;
            EnsureControlsFloatingRectInitialized();
            SavePersistedOptions();
        }

        private void UndockControlsPane()
        {
            _controlsPreferUndocked = true;
            _showDockedControls = false;
            EnsureControlsFloatingRectInitialized(fromDockedRect: true);
            _showUndockedControls = true;
            SavePersistedOptions();
        }

        private void DockControlsPane()
        {
            CaptureControlsFloatingRect();
            _controlsPreferUndocked = false;
            _showUndockedControls = false;
            if (!isVisible || _isMinimized)
                return;

            _showDockedControls = true;
            SavePersistedOptions();
        }

        private void CloseUndockedControls()
        {
            CaptureControlsFloatingRect();
            _showUndockedControls = false;
            _options.showControlsPane = IsAnyControlsVisible;
            SavePersistedOptions();
        }

        private void CloseDockedControls()
        {
            _showDockedControls = false;
            _options.showControlsPane = IsAnyControlsVisible;
            SavePersistedOptions();
        }

        private void OnMainAnimBrowserHidden()
        {
            _showDockedControls = false;
            AnimThumbnailService.ClearAll();
            OnPreviewHidden();
        }

        private void HandleControlsHotkeys()
        {
            if (GUIUtility.keyboardControl != 0)
                return;

            AnimBrowserConfig.Register(SandboxServices.Config);

            if (AnimBrowserConfig.HotkeyToggleUndockedControls!.Value.IsDown())
                ToggleUndockedControlsViaHotkey();
        }

        private void DrawUndockedControlsWindow()
        {
            HandleControlsFloatingResize();

            _controlsFloatingRect.width = Mathf.Clamp(_controlsFloatingRect.width, ControlsFloatingMinWidth, ControlsFloatingMaxWidth);
            _controlsFloatingRect.height = Mathf.Clamp(_controlsFloatingRect.height, ControlsFloatingMinHeight, ControlsFloatingMaxHeight);
            _controlsFloatingRect.x = Mathf.Clamp(_controlsFloatingRect.x, 4f, Mathf.Max(4f, Screen.width - _controlsFloatingRect.width - 4f));
            _controlsFloatingRect.y = Mathf.Clamp(_controlsFloatingRect.y, 4f, Mathf.Max(4f, Screen.height - _controlsFloatingRect.height - 4f));

            _controlsFloatingRect = GUI.Window(
                ControlsUndockedWindowId,
                _controlsFloatingRect,
                DrawUndockedControlsWindowContent,
                "Anim Browser · Controls");

            _controlsFloatingRect.x = Mathf.Clamp(_controlsFloatingRect.x, 4f, Mathf.Max(4f, Screen.width - _controlsFloatingRect.width - 4f));
            _controlsFloatingRect.y = Mathf.Clamp(_controlsFloatingRect.y, 4f, Mathf.Max(4f, Screen.height - _controlsFloatingRect.height - 4f));

            if (Event.current.type == EventType.MouseUp && !_controlsFloatingResizing)
            {
                CaptureControlsFloatingRect();
                SavePersistedOptions();
            }

            IMGUIUtils.EatInputInRect(_controlsFloatingRect);
        }

        private void HandleControlsFloatingResize()
        {
            Event? e = Event.current;
            if (e == null)
                return;

            var handleRect = new Rect(
                _controlsFloatingRect.x + _controlsFloatingRect.width - ResizeHandleSize,
                _controlsFloatingRect.y + _controlsFloatingRect.height - ResizeHandleSize,
                ResizeHandleSize,
                ResizeHandleSize);

            if (e.type == EventType.MouseDown && e.button == 0 && handleRect.Contains(e.mousePosition))
            {
                _controlsFloatingResizing = true;
                e.Use();
            }
            else if (_controlsFloatingResizing && e.type == EventType.MouseDrag && e.button == 0)
            {
                _controlsFloatingRect.width = Mathf.Clamp(
                    e.mousePosition.x - _controlsFloatingRect.x,
                    ControlsFloatingMinWidth,
                    ControlsFloatingMaxWidth);
                _controlsFloatingRect.height = Mathf.Clamp(
                    e.mousePosition.y - _controlsFloatingRect.y,
                    ControlsFloatingMinHeight,
                    ControlsFloatingMaxHeight);
                e.Use();
            }
            else if (_controlsFloatingResizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _controlsFloatingResizing = false;
                CaptureControlsFloatingRect();
                SavePersistedOptions();
                e.Use();
            }
        }

        private void DrawUndockedControlsWindowContent(int id)
        {
            float innerW = Mathf.Max(1f, _controlsFloatingRect.width - 16f);
            float innerH = Mathf.Max(1f, _controlsFloatingRect.height - 24f);
            GUILayout.BeginArea(new Rect(8f, 22f, innerW, innerH));
            DrawControlsPaneHeader(showUndockButton: false, showDockButton: true, showCloseButton: true);
            DrawControlsPaneBody();
            GUILayout.EndArea();
            FinishUndockedControlsWindowChrome();
        }

        private void FinishUndockedControlsWindowChrome()
        {
            float w = _controlsFloatingRect.width;
            float h = _controlsFloatingRect.height;
            var resizeHandle = new Rect(w - ResizeHandleSize, h - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            GUI.Box(resizeHandle, new GUIContent("◢", "Resize"));
            GUI.DragWindow(new Rect(0f, 0f, w - ResizeHandleSize, 20f));
        }

        private void DrawControlsPaneHeader(bool showUndockButton, bool showDockButton, bool showCloseButton)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (showUndockButton &&
                GUILayout.Button(
                    new GUIContent("Float", "Undock this panel — move and resize it independently."),
                    AnimBrowserScale.W(44f),
                    AnimBrowserScale.H(22f)))
            {
                UndockControlsPane();
            }

            if (showDockButton &&
                GUILayout.Button(
                    new GUIContent("Dock", "Dock this panel beside the Anim Browser window."),
                    AnimBrowserScale.W(44f),
                    AnimBrowserScale.H(22f)))
            {
                DockControlsPane();
            }

            if (showCloseButton &&
                GUILayout.Button(
                    new GUIContent("×", "Close the controls window."),
                    GUILayout.Width(WindowChromeButtonWidth),
                    AnimBrowserScale.H(22f)))
            {
                CloseUndockedControls();
            }

            GUILayout.EndHorizontal();
        }
    }
}
