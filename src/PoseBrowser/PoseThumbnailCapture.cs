using System;
using System.Collections;
using System.Collections.Generic;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PoseThumbnailCapture
    {
        public enum CaptureMode { Manual, Auto }

        public bool IsActive { get; private set; }
        public bool IsCapturing { get; private set; }
        public CaptureMode Mode { get; set; } = CaptureMode.Manual;
        public int CurrentIndex { get; private set; }
        public int TotalCount { get; private set; }
        public PoseGridItem? CurrentItem { get; private set; }

        public Rect CaptureRect { get; set; }

        private List<PoseGridItem>? _queue;
        private Action<PoseGridItem, byte[]>? _onCaptured;
        private Action? _onComplete;
        private Action<PoseGridItem>? _onApplyPose;
        private Action? _onGroupSetup;
        private Action<int>? _onGroupFocusIndex;
        private Action? _onGroupCleanup;
        private bool _groupCaptureMode;
        private MonoBehaviour? _runner;
        private Coroutine? _activeCoroutine;

        public bool IsGroupCaptureMode => _groupCaptureMode;

        public void StartCapture(
            MonoBehaviour runner,
            List<PoseGridItem> items,
            Action<PoseGridItem> onApplyPose,
            Action<PoseGridItem, byte[]> onCaptured,
            Action onComplete)
        {
            if (items.Count == 0) return;

            _runner = runner;
            _queue = items;
            _onApplyPose = onApplyPose;
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
            ApplyCurrentPose();
        }

        /// <summary>
        /// Group thumbnail capture: all poses are applied once up front; each step toggles monocolor on non-focus characters.
        /// </summary>
        public void StartGroupCapture(
            MonoBehaviour runner,
            List<PoseGridItem> items,
            Action onGroupSetup,
            Action<int> onGroupFocusIndex,
            Action onGroupCleanup,
            Action<PoseGridItem, byte[]> onCaptured,
            Action onComplete)
        {
            if (items.Count == 0) return;

            _runner = runner;
            _queue = items;
            _onApplyPose = null;
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
            ApplyCurrentPose();
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
            CurrentItem = null;
            _queue = null;
            _onApplyPose = null;
            _onGroupSetup = null;
            _onGroupFocusIndex = null;
            _onGroupCleanup = null;
            _groupCaptureMode = false;
        }

        private void ApplyCurrentPose()
        {
            if (_queue == null || CurrentIndex >= _queue.Count)
            {
                Finish();
                return;
            }

            CurrentItem = _queue[CurrentIndex];
            if (_groupCaptureMode)
                _onGroupFocusIndex?.Invoke(CurrentIndex);
            else
                _onApplyPose?.Invoke(CurrentItem);

            if (Mode == CaptureMode.Auto)
                ScheduleAutoCapture();
        }

        private static float ResolveAutoCaptureDelaySeconds()
        {
            PoseBrowserConfig.Register(SandboxServices.Config);
            var entry = PoseBrowserConfig.AutoCaptureDelaySeconds;
            if (entry == null)
                return 2f;
            return Mathf.Clamp(entry.Value, 0.5f, 30f);
        }

        private void ScheduleAutoCapture()
        {
            if (_runner == null) return;
            if (_activeCoroutine != null)
                _runner.StopCoroutine(_activeCoroutine);
            _activeCoroutine = _runner.StartCoroutine(AutoCaptureCoroutine());
        }

        private IEnumerator AutoCaptureCoroutine()
        {
            yield return null;
            yield return new WaitForSeconds(ResolveAutoCaptureDelaySeconds());
            yield return new WaitForEndOfFrame();
            if (IsActive && Mode == CaptureMode.Auto && !IsCapturing)
                DoCapture();
        }

        public void ConfirmCapture()
        {
            if (!IsActive || IsCapturing) return;
            DoCapture();
        }

        /// <summary>Capture the current pose, then auto-capture all remaining poses in the queue.</summary>
        public void StartAutoCaptureChain()
        {
            if (!IsActive || IsCapturing) return;
            Mode = CaptureMode.Auto;
            ScheduleAutoCapture();
        }

        public void SkipCurrent()
        {
            if (!IsActive) return;
            CurrentIndex++;
            ApplyCurrentPose();
        }

        private void DoCapture()
        {
            if (CurrentItem == null) return;
            IsCapturing = true;

            try
            {
                var cam = Camera.main;
                if (cam == null) return;

                Rect flippedRect = new Rect(
                    CaptureRect.x,
                    Screen.height - CaptureRect.y - CaptureRect.height,
                    CaptureRect.width,
                    CaptureRect.height);

                var tex = PoseDataService.CaptureScreenArea(cam, flippedRect);
                byte[] pngBytes = ImageConversion.EncodeToPNG(tex);
                UnityEngine.Object.Destroy(tex);

                _onCaptured?.Invoke(CurrentItem, pngBytes);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Capture failed: {ex.Message}");
            }
            finally
            {
                IsCapturing = false;
            }

            CurrentIndex++;
            ApplyCurrentPose();
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
            if (!IsActive) return;

            var oldColor = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, CaptureRect.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, CaptureRect.yMax, Screen.width, Screen.height - CaptureRect.yMax), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, CaptureRect.y, CaptureRect.x, CaptureRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.xMax, CaptureRect.y, Screen.width - CaptureRect.xMax, CaptureRect.height), Texture2D.whiteTexture);

            GUI.color = Color.green;
            float b = 2f;
            GUI.DrawTexture(new Rect(CaptureRect.x - b, CaptureRect.y - b, CaptureRect.width + b * 2, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.x - b, CaptureRect.yMax, CaptureRect.width + b * 2, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.x - b, CaptureRect.y, b, CaptureRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CaptureRect.xMax, CaptureRect.y, b, CaptureRect.height), Texture2D.whiteTexture);

            GUI.color = oldColor;

            const float gap = 6f;
            const float btnH = 26f;
            const float btnGap = 4f;
            float labelH = 20f;
            float padV = 7f;
            float panelH = padV + labelH + btnH + padV;
            float panelW = Mathf.Min(Mode == CaptureMode.Manual ? 420f : 300f, Screen.width - 16f);

            float panelX = CaptureRect.center.x - panelW * 0.5f;
            panelX = Mathf.Clamp(panelX, 8f, Mathf.Max(8f, Screen.width - panelW - 8f));

            float panelY = CaptureRect.yMax + gap;
            if (panelY + panelH > Screen.height - 8f)
            {
                float above = CaptureRect.y - gap - panelH;
                if (above >= 8f)
                    panelY = above;
                else
                    panelY = Mathf.Max(8f, Screen.height - panelH - 8f);
            }

            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            string prefix = _groupCaptureMode ? "Group thumb" : "Capture";
            string status = Mode == CaptureMode.Auto
                ? $"Auto {CurrentIndex + 1} / {TotalCount}: {CurrentItem?.DisplayName ?? ""}"
                : $"{prefix} {CurrentIndex + 1} / {TotalCount}: {CurrentItem?.DisplayName ?? ""}";
            GUILayout.Label(status, GUILayout.Height(labelH));

            GUILayout.BeginHorizontal();
            if (Mode == CaptureMode.Manual)
            {
                if (GUILayout.Button("Capture", GUILayout.Height(btnH), GUILayout.Width(86f)))
                    ConfirmCapture();
                GUILayout.Space(btnGap);
                if (GUILayout.Button("Skip", GUILayout.Height(btnH), GUILayout.Width(72f)))
                    SkipCurrent();
                GUILayout.Space(btnGap);
                bool canAuto = CurrentIndex < TotalCount;
                GUI.enabled = canAuto && !IsCapturing;
                if (GUILayout.Button(
                        new GUIContent("Auto-capture", "Capture this pose, then capture all remaining poses automatically"),
                        GUILayout.Height(btnH),
                        GUILayout.Width(104f)))
                    StartAutoCaptureChain();
                GUI.enabled = true;
                GUILayout.Space(btnGap);
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(btnH), GUILayout.Width(78f)))
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
            if (!IsActive) return;
            Event e = Event.current;
            if (e == null) return;

            float handleSize = 16f;
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
