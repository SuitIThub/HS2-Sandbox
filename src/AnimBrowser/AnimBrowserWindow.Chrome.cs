using KKAPI.Utilities;
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
        private Rect _cachedMinimizeButtonRootRect;

        private void DrawWindowChromeButtons(float buttonHeight)
        {
            if (GUILayout.Button(new GUIContent("−", "Minimize Anim Browser"), GUILayout.Width(WindowChromeButtonWidth), AnimBrowserScale.H(buttonHeight)))
                MinimizeAnimBrowser(_cachedMinimizeButtonRootRect);

            if (Event.current.type == EventType.Repaint)
                _cachedMinimizeButtonRootRect = GUIClip.Unclip(GUILayoutUtility.GetLastRect());

            if (GUILayout.Button(new GUIContent("×", "Close Anim Browser"), GUILayout.Width(WindowChromeButtonWidth), AnimBrowserScale.H(buttonHeight)))
                CloseAnimBrowser();
        }

        private void MinimizeAnimBrowser(Rect minimizeButtonRoot)
        {
            CaptureWindowRectForCurrentViewMode();
            _minimizeBtnOffsetFromWindow = minimizeButtonRoot.min - new Vector2(windowRect.x, windowRect.y);
            _minimizedChipRect = new Rect(minimizeButtonRoot.x, minimizeButtonRoot.y, MinimizedChipSize, MinimizedChipSize);
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

            Rect chip = _minimizedChipRect;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Rect hitChip = chip;
            const float hitPad = 2f;
            hitChip.x -= hitPad;
            hitChip.y -= hitPad;
            hitChip.width += hitPad * 2f;
            hitChip.height += hitPad * 2f;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !hitChip.Contains(e.mousePosition))
                        break;
                    GUIUtility.hotControl = controlId;
                    _chipDragging = true;
                    _chipDragOffset = e.mousePosition - chip.min;
                    _chipMouseDownPos = e.mousePosition;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId || !_chipDragging)
                        break;
                    float nx = e.mousePosition.x - _chipDragOffset.x;
                    float ny = e.mousePosition.y - _chipDragOffset.y;
                    nx = Mathf.Clamp(nx, 0f, Screen.width - chip.width);
                    ny = Mathf.Clamp(ny, 0f, Screen.height - chip.height);
                    _minimizedChipRect = new Rect(nx, ny, chip.width, chip.height);
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId || e.button != 0)
                        break;
                    bool clicked = (e.mousePosition - _chipMouseDownPos).sqrMagnitude <=
                        MinimizedChipClickDragThreshold * MinimizedChipClickDragThreshold;
                    GUIUtility.hotControl = 0;
                    _chipDragging = false;
                    if (clicked)
                        RestoreFromMinimize();
                    e.Use();
                    break;
            }

            GUI.Box(chip, new GUIContent("AB", "Restore Anim Browser"));

            if (GUIUtility.hotControl == controlId || hitChip.Contains(e.mousePosition))
                IMGUIUtils.EatInputInRect(chip);
        }
    }
}
