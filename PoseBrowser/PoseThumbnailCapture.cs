using System;
using System.Collections;
using System.Collections.Generic;
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
        private MonoBehaviour? _runner;
        private Coroutine? _activeCoroutine;

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
            TotalCount = items.Count;
            CurrentIndex = 0;
            IsActive = true;
            IsCapturing = false;

            float side = Screen.height * 0.9f;
            side = Mathf.Min(side, Screen.width - 16f);
            side = Mathf.Min(side, Screen.height - 16f);
            CaptureRect = new Rect(
                (Screen.width - side) / 2f,
                (Screen.height - side) / 2f,
                side,
                side);

            ApplyCurrentPose();
        }

        public void Cancel()
        {
            if (_activeCoroutine != null && _runner != null)
                _runner.StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
            IsActive = false;
            IsCapturing = false;
            CurrentItem = null;
            _queue = null;
        }

        private void ApplyCurrentPose()
        {
            if (_queue == null || CurrentIndex >= _queue.Count)
            {
                Finish();
                return;
            }

            CurrentItem = _queue[CurrentIndex];
            _onApplyPose?.Invoke(CurrentItem);

            if (Mode == CaptureMode.Auto && _runner != null)
                _activeCoroutine = _runner.StartCoroutine(AutoCaptureCoroutine());
        }

        private IEnumerator AutoCaptureCoroutine()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            DoCapture();
        }

        public void ConfirmCapture()
        {
            if (!IsActive || IsCapturing) return;
            DoCapture();
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
            IsActive = false;
            IsCapturing = false;
            CurrentItem = null;
            _queue = null;
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
            float panelW = Mathf.Min(300f, Screen.width - 16f);

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

            GUILayout.BeginArea(new Rect(panelX, panelY, panelW, panelH), GUI.skin.box);
            GUILayout.Label($"Capture {CurrentIndex + 1} / {TotalCount}: {CurrentItem?.DisplayName ?? ""}", GUILayout.Height(labelH));

            GUILayout.BeginHorizontal();
            if (Mode == CaptureMode.Manual)
            {
                if (GUILayout.Button("Capture", GUILayout.Height(btnH), GUILayout.Width(86f)))
                    ConfirmCapture();
                GUILayout.Space(btnGap);
                if (GUILayout.Button("Skip", GUILayout.Height(btnH), GUILayout.Width(72f)))
                    SkipCurrent();
                GUILayout.Space(btnGap);
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(btnH), GUILayout.Width(78f)))
                Cancel();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

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
