using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const float WindowChromeButtonWidthBase = 22f;
        private float WindowChromeButtonWidth => AnimBrowserScale.Px(WindowChromeButtonWidthBase);
        private const float MinimizedChipSizeBase = 28f;
        private float MinimizedChipSize => AnimBrowserScale.Px(MinimizedChipSizeBase);
        private const float MinimizedChipClickDragThreshold = 4f;

        private bool _isMinimized;
        private Rect _minimizedChipRect;
        private Vector2 _minimizeBtnOffsetFromWindow;
        private bool _chipDragging;
        private Vector2 _chipDragOffset;
        private Vector2 _chipMouseDownPos;

        private void DrawWindowChromeButtons(float buttonHeight)
        {
            if (GUILayout.Button(new GUIContent("−", "Minimize Anim Browser"), GUILayout.Width(WindowChromeButtonWidth), AnimBrowserScale.H(buttonHeight)))
            {
                var btnRect = GUILayoutUtility.GetLastRect();
                Vector2 btnScreen = GUIUtility.GUIToScreenPoint(new Vector2(btnRect.x, btnRect.y));
                MinimizeAnimBrowser(btnScreen);
            }

            if (GUILayout.Button(new GUIContent("×", "Close Anim Browser"), GUILayout.Width(WindowChromeButtonWidth), AnimBrowserScale.H(buttonHeight)))
                CloseAnimBrowser();
        }

        private void MinimizeAnimBrowser(Vector2 minimizeButtonScreen)
        {
            CaptureWindowRectForCurrentViewMode();
            _minimizeBtnOffsetFromWindow = minimizeButtonScreen - new Vector2(windowRect.x, windowRect.y);
            _minimizedChipRect = new Rect(minimizeButtonScreen.x, minimizeButtonScreen.y, MinimizedChipSize, MinimizedChipSize);
            _chipDragging = false;
            _isMinimized = true;
        }

        private void RestoreFromMinimize()
        {
            _isMinimized = false;
            RestoreWindowRectForViewMode(_viewMode);

            windowRect.x = _minimizedChipRect.x - _minimizeBtnOffsetFromWindow.x;
            windowRect.y = _minimizedChipRect.y - _minimizeBtnOffsetFromWindow.y;
            windowRect.x = Mathf.Clamp(windowRect.x, 4f, Mathf.Max(4f, Screen.width - windowRect.width - 4f));
            windowRect.y = Mathf.Clamp(windowRect.y, 4f, Mathf.Max(4f, Screen.height - windowRect.height - 4f));
        }

        private void CloseAnimBrowser()
        {
            _isMinimized = false;
            var gui = FindObjectOfType<SandboxGUI>();
            if (gui != null)
                gui.SetAnimBrowserVisible(false);
            else
                SetVisible(false);
        }

        private void DrawMinimizedRestoreChip()
        {
            Event e = Event.current;
            if (_minimizedChipRect.width < 1f)
                _minimizedChipRect = new Rect(_minimizedChipRect.x, _minimizedChipRect.y, MinimizedChipSize, MinimizedChipSize);

            var chip = _minimizedChipRect;
            GUI.Box(chip, new GUIContent("AB", "Restore Anim Browser"));

            if (e.type == EventType.MouseDown && e.button == 0 && chip.Contains(e.mousePosition))
            {
                _chipDragging = true;
                _chipDragOffset = e.mousePosition - new Vector2(chip.x, chip.y);
                _chipMouseDownPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _chipDragging)
            {
                float nx = e.mousePosition.x - _chipDragOffset.x;
                float ny = e.mousePosition.y - _chipDragOffset.y;
                nx = Mathf.Clamp(nx, 0f, Screen.width - chip.width);
                ny = Mathf.Clamp(ny, 0f, Screen.height - chip.height);
                _minimizedChipRect = new Rect(nx, ny, chip.width, chip.height);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _chipDragging)
            {
                bool clicked = (e.mousePosition - _chipMouseDownPos).sqrMagnitude <=
                    MinimizedChipClickDragThreshold * MinimizedChipClickDragThreshold;
                _chipDragging = false;
                if (clicked)
                    RestoreFromMinimize();
                e.Use();
            }
        }
    }
}
