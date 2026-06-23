using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const string PreviewHoverTooltipPrefix = "\x01ab:";
        private const float PreviewHoverDebounceSeconds = 0.12f;

        private AnimPreviewStage? _previewStage;
        private int _previewHoverIndex = -1;
        private float _previewHoverRowScreenY = -1f;
        // Direct geometric hover capture, written by EmitPreviewHoverSensor during Repaint and consumed
        // at end of frame. Fallback for GUI.tooltip, which on some Unity-IMGUI versions (AI / 2018.2)
        // does not populate for empty-content GUIStyle.none sensors on plain hover (only when an event
        // flows, e.g. a click). rect.Contains is equivalent and reliable on every target.
        private int _previewHoverIndexPending = -1;
        private float _previewHoverRowScreenYPending = -1f;
        private const float ListPreviewPopupSizeBase = 240f;
        private AnimDisplayEntry? _debouncedHoverEntry;
        private string _debouncedHoverKey = string.Empty;
        private string _pendingHoverKey = string.Empty;
        private float _hoverStableSince = float.NegativeInfinity;
        private GUIStyle? _previewHoverSensorStyle;
        private GUIStyle? _previewThumbOverlayStyle;

        private static readonly GUIContent GcPreviewLoading = new GUIContent("…");
        private static readonly GUIContent GcPreviewUnavailable = new GUIContent("—");

        internal void BindPreviewStage(AnimPreviewStage stage)
        {
            _previewStage = stage;
            ApplyPreviewCameraOptionsToStage();
        }

        /// <summary>Pushes the persisted preview-camera settings onto the stage. Safe to call repeatedly;
        /// must run again after options are loaded (binding happens before <see cref="Start"/> loads them).</summary>
        private void ApplyPreviewCameraOptionsToStage()
        {
            if (_previewStage == null)
                return;
            _previewStage.CameraMode = _options.previewCameraMode;
            _previewStage.IterationCameraMode = _options.previewIterationCameraMode;
            _previewStage.CameraRotateSpeed = _options.previewCameraRotateSpeed;
            _previewStage.CameraPitch = _options.previewCameraPitch;
            _previewStage.IterationPanSeconds = _options.previewIterationPanSeconds;
        }

        private GUIStyle PreviewHoverSensorStyle
        {
            get
            {
                if (_previewHoverSensorStyle == null)
                    _previewHoverSensorStyle = new GUIStyle(GUIStyle.none);
                return _previewHoverSensorStyle;
            }
        }

        private GUIStyle PreviewThumbOverlayStyle
        {
            get
            {
                if (_previewThumbOverlayStyle == null)
                {
                    _previewThumbOverlayStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                        fontSize = Mathf.Max(9, GUI.skin.label.fontSize - 1),
                    };
                }
                return _previewThumbOverlayStyle;
            }
        }

        private void TickPreviewSystem()
        {
            if (IsThumbnailCaptureActive)
            {
                if (_previewStage != null && _previewStage.IsActive)
                    _previewStage.ClearTarget();
                ClearPreviewHoverTracking();
                return;
            }

            if (_previewStage == null || !_options.enableHoverPreview)
            {
                if (_previewStage != null && _previewStage.IsActive)
                    _previewStage.ClearTarget();
                return;
            }

            if (!isVisible || _isMinimized)
            {
                _previewStage.ClearTarget();
                ClearPreviewHoverTracking();
                return;
            }

            UpdateDebouncedHover();
        }

        private void UpdateDebouncedHover()
        {
            if (_previewStage == null)
                return;

            if (_previewHoverIndex < 0 || _previewHoverIndex >= _visibleEntries.Count)
            {
                if (_debouncedHoverEntry != null)
                {
                    _debouncedHoverEntry = null;
                    _debouncedHoverKey = string.Empty;
                    _pendingHoverKey = string.Empty;
                    _previewStage.ClearTarget();
                }
                return;
            }

            AnimDisplayEntry entry = _visibleEntries[_previewHoverIndex];
            string key = BuildPreviewHoverKey(entry);
            if (string.IsNullOrEmpty(key))
                return;

            if (!string.Equals(key, _pendingHoverKey, System.StringComparison.Ordinal))
            {
                _pendingHoverKey = key;
                _hoverStableSince = Time.unscaledTime;
            }

            if (!string.Equals(key, _debouncedHoverKey, System.StringComparison.Ordinal) &&
                Time.unscaledTime - _hoverStableSince >= PreviewHoverDebounceSeconds)
            {
                _debouncedHoverKey = key;
                _debouncedHoverEntry = entry;
                _previewStage.SetTarget(_debouncedHoverEntry);
            }
        }

        private void ClearPreviewHoverTracking()
        {
            _previewHoverIndex = -1;
            _debouncedHoverEntry = null;
            _debouncedHoverKey = string.Empty;
            _pendingHoverKey = string.Empty;
            _hoverStableSince = float.NegativeInfinity;
        }

        private static string BuildPreviewHoverTooltip(int visibleIndex) =>
            PreviewHoverTooltipPrefix + visibleIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private void EmitPreviewHoverSensor(Rect rect, int visibleIndex)
        {
            if (!_options.enableHoverPreview || rect.width < 1f || rect.height < 1f)
                return;

            GUI.Label(rect, new GUIContent(string.Empty, BuildPreviewHoverTooltip(visibleIndex)), PreviewHoverSensorStyle);

            // Direct hover capture (Repaint only): independent of both GUI.tooltip AND
            // Event.current.mousePosition. On AI / Unity 2018.2 runtime IMGUI, Event.current.mousePosition
            // is only refreshed when an input event flows (so plain hover never registers) — but
            // Input.mousePosition is live every frame. Convert it into the current GUI-local space
            // (no GUI.matrix is used, so ScreenToGUIPoint only undoes the scroll/group offset) and test
            // the same rect the click handler uses. Y is flipped: Input is bottom-left, GUI is top-left.
            if (Event.current.type == EventType.Repaint)
            {
                float screenMouseYTopLeft = Screen.height - Input.mousePosition.y;
                Vector2 localMouse = GUIUtility.ScreenToGUIPoint(
                    new Vector2(Input.mousePosition.x, screenMouseYTopLeft));
                if (rect.Contains(localMouse))
                {
                    _previewHoverIndexPending = visibleIndex;
                    _previewHoverRowScreenYPending = screenMouseYTopLeft;
                }
            }
        }

        private void TryCapturePreviewHoverFromTooltip()
        {
            _previewHoverIndex = -1;
            if (!_options.enableHoverPreview)
            {
                _previewHoverIndexPending = -1;
                return;
            }

            string tip = GUI.tooltip;
            if (!string.IsNullOrEmpty(tip) && tip.StartsWith(PreviewHoverTooltipPrefix, System.StringComparison.Ordinal))
            {
                string indexText = tip.Substring(PreviewHoverTooltipPrefix.Length);
                if (int.TryParse(indexText, out int index) && index >= 0 && index < _visibleEntries.Count)
                {
                    _previewHoverIndex = index;
                    // Capture the mouse's screen-space Y so the list-view popup can sit at the hovered row.
                    // (mousePosition is window-relative inside the window callback — convert to screen.)
                    _previewHoverRowScreenY = GUIUtility.GUIToScreenPoint(new Vector2(0f, Event.current.mousePosition.y)).y;
                    GUI.tooltip = string.Empty;
                }
            }

            // Fallback: GUI.tooltip didn't resolve (e.g. AI / Unity 2018.2 IMGUI doesn't populate it on
            // plain hover). Use the geometric capture recorded by EmitPreviewHoverSensor this frame.
            if (_previewHoverIndex < 0 && _previewHoverIndexPending >= 0 &&
                _previewHoverIndexPending < _visibleEntries.Count)
            {
                _previewHoverIndex = _previewHoverIndexPending;
                _previewHoverRowScreenY = _previewHoverRowScreenYPending;
            }

            _previewHoverIndexPending = -1;
        }

        /// <summary>
        /// List view has no in-card thumbnail, so the live stick-figure preview is shown in a popup to the
        /// right of the window + docked panes, centred on the hovered row. Grid view keeps drawing the
        /// preview inside the card instead. Reuses the same preview stage and draw path.
        /// </summary>
        private void DrawListPreviewPopup()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            if (_viewMode != AnimBrowserViewMode.List || !_options.enableHoverPreview)
                return;
            if (_previewStage == null || _previewHoverIndex < 0 || _previewHoverIndex >= _visibleEntries.Count)
                return;

            float size = AnimBrowserScale.Px(ListPreviewPopupSizeBase);

            float rightEdge = windowRect.xMax;
            if (TryGetOpenDockedPaneBounds(out _, out float docksMaxX))
                rightEdge = Mathf.Max(rightEdge, docksMaxX);

            float x = rightEdge + DockedPaneGap;
            float y = _previewHoverRowScreenY - size * 0.5f;

            // No room on the right? Fall back to the left edge of the window.
            if (x + size > Screen.width - 4f)
                x = windowRect.x - size - DockedPaneGap;
            if (x < 4f)
                x = 4f;
            y = Mathf.Clamp(y, 4f, Mathf.Max(4f, Screen.height - size - 4f));

            var outer = new Rect(x, y, size, size);
            GUI.Box(outer, GUIContent.none);
            float pad = AnimBrowserScale.Px(3f);
            DrawPreviewInThumbRect(new Rect(outer.x + pad, outer.y + pad, outer.width - pad * 2f, outer.height - pad * 2f));
        }

        private bool IsPreviewHoverIndex(int visibleIndex) =>
            _options.enableHoverPreview && visibleIndex == _previewHoverIndex;

        private void DrawPreviewInThumbRect(Rect thumbRect)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 1f);
            GUI.DrawTexture(thumbRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
            GUI.color = prev;

            if (_previewStage == null)
            {
                GUI.Label(thumbRect, GcPreviewUnavailable, PreviewThumbOverlayStyle);
                return;
            }

            Texture? tex = _previewStage.OutputTexture;
            if (_previewStage.State == AnimPreviewStageState.Ready && tex != null)
            {
                GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit, false);
                return;
            }

            GUIContent msg = _previewStage.State == AnimPreviewStageState.Unavailable
                ? GcPreviewUnavailable
                : GcPreviewLoading;
            GUI.Label(thumbRect, msg, PreviewThumbOverlayStyle);
        }

        private void OnPreviewHidden()
        {
            ClearPreviewHoverTracking();
            _previewStage?.ClearTarget();
        }

        private static string BuildPreviewHoverKey(AnimDisplayEntry entry)
        {
            if (entry.IsGroup && entry.Group != null)
                return "g:" + entry.Group.Id;
            if (entry.Single != null)
                return "s:" + entry.Single.CatalogKey;
            return string.Empty;
        }
    }
}
