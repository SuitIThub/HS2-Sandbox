using System.Collections.Generic;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class SandboxGUI : MonoBehaviour
    {
        // Subwindow states
        private readonly Dictionary<string, bool> _windowStates = [];
        private readonly Dictionary<string, SubWindow> _windows = [];

        public delegate void WindowVisibilityChangedHandler(string key, bool visible);
        public event WindowVisibilityChangedHandler? WindowVisibilityChanged;

        private void Start()
        {
            // Intentionally empty: windows are registered by the plugin entrypoint(s).
        }

        public void RegisterWindow(string key, SubWindow window, bool initialVisible = false)
        {
            if (string.IsNullOrWhiteSpace(key) || window == null)
                return;

            _windows[key] = window;
            _windowStates[key] = initialVisible;
            window.SetVisible(initialVisible);
        }

        private void OnGUI()
        {
            // Draw subwindows based on their states
            foreach (var kvp in _windowStates)
            {
                if (kvp.Value && _windows.ContainsKey(kvp.Key))
                {
                    _windows[kvp.Key].DrawWindow();
                }
            }

            // Draw timeline mouse-position crosses on top when enabled
            if (_windowStates.TryGetValue(SandboxWindowKeys.Timeline, out bool timelineVisible)
                && timelineVisible
                && _windows.TryGetValue(SandboxWindowKeys.Timeline, out var w)
                && w is IOverlayDrawable overlay)
            {
                overlay.DrawOverlay();
            }
        }

        public bool IsCopyScriptVisible => IsWindowVisible(SandboxWindowKeys.CopyScript);

        public bool IsTimelineVisible => IsWindowVisible(SandboxWindowKeys.Timeline);

        public bool IsSonScaleVisible => IsWindowVisible(SandboxWindowKeys.SonScale);

        public bool IsWindowVisible(string key) =>
            _windowStates.TryGetValue(key, out var value) && value;

        public void SetWindowVisible(string key, bool visible)
        {
            if (!_windows.TryGetValue(key, out var window))
                return;

            _windowStates[key] = visible;
            window.SetVisible(visible);
            WindowVisibilityChanged?.Invoke(key, visible);
        }

        public void SetCopyScriptVisible(bool visible) => SetWindowVisible(SandboxWindowKeys.CopyScript, visible);
        public void SetTimelineVisible(bool visible) => SetWindowVisible(SandboxWindowKeys.Timeline, visible);
        public void SetSonScaleVisible(bool visible) => SetWindowVisible(SandboxWindowKeys.SonScale, visible);
    }
}

