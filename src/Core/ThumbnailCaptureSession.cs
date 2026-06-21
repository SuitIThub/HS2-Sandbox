using System;
using System.Collections;
using System.Collections.Generic;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Reusable on-screen thumbnail capture session (extracted from the Pose Browser so the Anim
    /// Browser can use it too). It drives a queue of items, applies each one to the scene, lets the
    /// user frame a square capture rect, and grabs that screen area to a PNG via callbacks. Manual or
    /// auto-chained. Browser-specific bits (display name, UI scale, auto delay, the screen grab) are
    /// supplied by the concrete subclass.
    /// </summary>
    internal abstract class ThumbnailCaptureSession<T>
    {
        public enum CaptureMode { Manual, Auto }

        public bool IsActive { get; private set; }
        public bool IsCapturing { get; private set; }
        public CaptureMode Mode { get; set; } = CaptureMode.Manual;
        public int CurrentIndex { get; private set; }
        public int TotalCount { get; private set; }
        public T? CurrentItem { get; private set; }

        public Rect CaptureRect { get; set; }

        private List<T>? _queue;
        private Action<T, byte[]>? _onCaptured;
        private Action? _onComplete;
        private Action<T>? _onApplyItem;
        private Action? _onGroupSetup;
        private Action<int>? _onGroupFocusIndex;
        private Action? _onGroupCleanup;
        private bool _groupCaptureMode;
        private MonoBehaviour? _runner;
        private Coroutine? _activeCoroutine;

        public bool IsGroupCaptureMode => _groupCaptureMode;

        // ---- Subclass hooks ----
        protected abstract string GetDisplayName(T item);
        protected abstract float ScalePx(float value);
        protected abstract float AutoCaptureDelaySeconds();

        /// <summary>Grab the given screen-space rect from the camera and return it as a (already sized) texture. Caller destroys it.</summary>
        protected abstract Texture2D CaptureScreenArea(Camera camera, Rect screenRect);

        public void StartCapture(
            MonoBehaviour runner,
            List<T> items,
            Action<T> onApplyItem,
            Action<T, byte[]> onCaptured,
            Action onComplete)
        {
            if (items.Count == 0)
                return;

            _runner = runner;
            _queue = items;
            _onApplyItem = onApplyItem;
            _onCaptured = onCaptured;
            _onComplete = onComplete;
            _onGroupSetup = null;
            _onGroupFocusIndex = null;
            _onGroupCleanup = null;
            _groupCaptureMode = false;
            TotalCount = items.Count;
            CurrentIndex = 0;
            Mode = CaptureMode.Manual;
            IsActive = true;
            IsCapturing = false;

            InitCaptureRect();
            ApplyCurrent();
        }

        /// <summary>Group capture: all items are set up once up front; each step focuses one element via callbacks.</summary>
        public void StartGroupCapture(
            MonoBehaviour runner,
            List<T> items,
            Action onGroupSetup,
            Action<int> onGroupFocusIndex,
            Action onGroupCleanup,
            Action<T, byte[]> onCaptured,
            Action onComplete)
        {
            if (items.Count == 0)
                return;

            _runner = runner;
            _queue = items;
            _onApplyItem = null;
            _onCaptured = onCaptured;
            _onComplete = onComplete;
            _onGroupSetup = onGroupSetup;
            _onGroupFocusIndex = onGroupFocusIndex;
            _onGroupCleanup = onGroupCleanup;
            _groupCaptureMode = true;
            TotalCount = items.Count;
            CurrentIndex = 0;
            Mode = CaptureMode.Manual;
            IsActive = true;
            IsCapturing = false;

            InitCaptureRect();
            _onGroupSetup?.Invoke();
            ApplyCurrent();
        }

        private void InitCaptureRect()
        {
            float side = Screen.height * 0.9f;
            side = Mathf.Min(side, Screen.width - 16f);
            side = Mathf.Min(side, Screen.height - 16f);
            CaptureRect = new Rect(
                (Screen.width - side) / 2f,
                (Screen.height - side) / 2f,
                side,
                side);
        }

        public void Cancel()
        {
            if (_activeCoroutine != null && _runner != null)
                _runner.StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
            if (_groupCaptureMode)
                _onGroupCleanup?.Invoke();
            ResetSession();
        }

        private void ResetSession()
        {
            IsActive = false;
            IsCapturing = false;
            CurrentItem = default;
            _queue = null;
            _onApplyItem = null;
            _onGroupSetup = null;
            _onGroupFocusIndex = null;
            _onGroupCleanup = null;
            _groupCaptureMode = false;
        }

        private void ApplyCurrent()
        {
            if (_queue == null || CurrentIndex >= _queue.Count)
            {
                Finish();
                return;
            }

            CurrentItem = _queue[CurrentIndex];
            if (_groupCaptureMode)
                _onGroupFocusIndex?.Invoke(CurrentIndex);
            else if (CurrentItem != null)
                _onApplyItem?.Invoke(CurrentItem);

            if (Mode == CaptureMode.Auto)
                ScheduleAutoCapture();
        }

        private void ScheduleAutoCapture()
        {
            if (_runner == null)
                return;
            if (_activeCoroutine != null)
                _runner.StopCoroutine(_activeCoroutine);
            _activeCoroutine = _runner.StartCoroutine(AutoCaptureCoroutine());
        }

        private IEnumerator AutoCaptureCoroutine()
        {
            yield return null;
            yield return new WaitForSeconds(Mathf.Clamp(AutoCaptureDelaySeconds(), 0.1f, 30f));
            yield return new WaitForEndOfFrame();
            if (IsActive && Mode == CaptureMode.Auto && !IsCapturing)
                DoCapture();
        }

        public void ConfirmCapture()
        {
            if (!IsActive || IsCapturing)
                return;
            DoCapture();
        }

        /// <summary>Capture the current item, then auto-capture all remaining items in the queue.</summary>
        public void StartAutoCaptureChain()
        {
            if (!IsActive || IsCapturing)
                return;
            Mode = CaptureMode.Auto;
            ScheduleAutoCapture();
        }

        public void SkipCurrent()
        {
            if (!IsActive)
                return;
            CurrentIndex++;
            ApplyCurrent();
        }

        private void DoCapture()
        {
            if (CurrentItem == null)
                return;
            IsCapturing = true;

            try
            {
                Camera cam = Camera.main;
                if (cam == null)
                    return;

                var flippedRect = new Rect(
                    CaptureRect.x,
                    Screen.height - CaptureRect.y - CaptureRect.height,
                    CaptureRect.width,
                    CaptureRect.height);

                Texture2D tex = CaptureScreenArea(cam, flippedRect);
                byte[] pngBytes = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);

                _onCaptured?.Invoke(CurrentItem, pngBytes);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError("ThumbnailCapture: capture failed: " + ex.Message);
            }
            finally
            {
                IsCapturing = false;
            }

            CurrentIndex++;
            ApplyCurrent();
        }

        private void Finish()
        {
            if (_groupCaptureMode)
                _onGroupCleanup?.Invoke();
            ResetSession();
            _onComplete?.Invoke();
        }

        public void DrawOverlay()
        {
            if (!IsActive)
                return;

            Color oldColor = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, CaptureRect.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, CaptureRect.yMax, Screen.width, Screen.height - CaptureRect.yMax), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, CaptureRect.y, CaptureRect.x, CaptureRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.xMax, CaptureRect.y, Screen.width - CaptureRect.xMax, CaptureRect.height), Texture2D.whiteTexture);

            GUI.color = Color.green;
            const float b = 2f;
            GUI.DrawTexture(new Rect(CaptureRect.x - b, CaptureRect.y - b, CaptureRect.width + b * 2, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.x - b, CaptureRect.yMax, CaptureRect.width + b * 2, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.x - b, CaptureRect.y, b, CaptureRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.xMax, CaptureRect.y, b, CaptureRect.height), Texture2D.whiteTexture);

            GUI.color = oldColor;

            const float gap = 6f;
            float btnH = ScalePx(26f);
            float btnGap = ScalePx(4f);
            float labelH = ScalePx(20f);
            float padV = ScalePx(7f);
            float panelH = padV + labelH + btnH + padV;
            float panelW = Mathf.Min(ScalePx(Mode == CaptureMode.Manual ? 420f : 300f), Screen.width - 16f);

            float panelX = CaptureRect.center.x - panelW * 0.5f;
            panelX = Mathf.Clamp(panelX, 8f, Mathf.Max(8f, Screen.width - panelW - 8f));

            float panelY = CaptureRect.yMax + gap;
            if (panelY + panelH > Screen.height - 8f)
            {
                float above = CaptureRect.y - gap - panelH;
                panelY = above >= 8f ? above : Mathf.Max(8f, Screen.height - panelH - 8f);
            }

            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            string prefix = _groupCaptureMode ? "Group thumb" : "Capture";
            string name = CurrentItem != null ? GetDisplayName(CurrentItem) : string.Empty;
            string status = Mode == CaptureMode.Auto
                ? "Auto " + (CurrentIndex + 1) + " / " + TotalCount + ": " + name
                : prefix + " " + (CurrentIndex + 1) + " / " + TotalCount + ": " + name;
            GUILayout.Label(status, GUILayout.Height(labelH));

            GUILayout.BeginHorizontal();
            if (Mode == CaptureMode.Manual)
            {
                if (GUILayout.Button("Capture", GUILayout.Height(btnH), GUILayout.Width(ScalePx(86f))))
                    ConfirmCapture();
                GUILayout.Space(btnGap);
                if (GUILayout.Button("Skip", GUILayout.Height(btnH), GUILayout.Width(ScalePx(72f))))
                    SkipCurrent();
                GUILayout.Space(btnGap);
                bool canAuto = CurrentIndex < TotalCount;
                GUI.enabled = canAuto && !IsCapturing;
                if (GUILayout.Button(
                        new GUIContent("Auto-capture", "Capture this item, then capture all remaining items automatically"),
                        GUILayout.Height(btnH),
                        GUILayout.Width(ScalePx(104f))))
                    StartAutoCaptureChain();
                GUI.enabled = true;
                GUILayout.Space(btnGap);
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(btnH), GUILayout.Width(ScalePx(78f))))
                Cancel();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
            IMGUIUtils.EatInputInRect(panelRect);

            HandleDragResize();
        }

        private bool _isDragging;
        private bool _isResizing;
        private Vector2 _dragOffset;

        private void HandleDragResize()
        {
            if (!IsActive)
                return;
            Event e = Event.current;
            if (e == null)
                return;

            const float handleSize = 16f;
            var resizeHandle = new Rect(CaptureRect.xMax - handleSize, CaptureRect.yMax - handleSize, handleSize, handleSize);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (resizeHandle.Contains(e.mousePosition))
                {
                    _isResizing = true;
                    e.Use();
                }
                else if (CaptureRect.Contains(e.mousePosition))
                {
                    _isDragging = true;
                    _dragOffset = e.mousePosition - new Vector2(CaptureRect.x, CaptureRect.y);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_isResizing)
                {
                    float nw = e.mousePosition.x - CaptureRect.x;
                    float nh = e.mousePosition.y - CaptureRect.y;
                    float side = Mathf.Max(nw, nh);
                    float maxSide = Mathf.Min(Screen.width - CaptureRect.x, Screen.height - CaptureRect.y);
                    side = Mathf.Clamp(side, 64f, maxSide);
                    CaptureRect = new Rect(CaptureRect.x, CaptureRect.y, side, side);
                    e.Use();
                }
                else if (_isDragging)
                {
                    float nx = e.mousePosition.x - _dragOffset.x;
                    float ny = e.mousePosition.y - _dragOffset.y;
                    nx = Mathf.Clamp(nx, 0, Screen.width - CaptureRect.width);
                    ny = Mathf.Clamp(ny, 0, Screen.height - CaptureRect.height);
                    CaptureRect = new Rect(nx, ny, CaptureRect.width, CaptureRect.height);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                _isDragging = false;
                _isResizing = false;
            }
        }
    }
}
