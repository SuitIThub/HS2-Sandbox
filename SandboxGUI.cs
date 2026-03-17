using System.Collections.Generic;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class SandboxGUI : MonoBehaviour
    {
        // Subwindow states
        private readonly Dictionary<string, bool> subwindowStates = [];
        private readonly Dictionary<string, SubWindow> subwindows = [];

        private void Start()
        {
            // Initialize subwindow states
            subwindowStates["Window1"] = false;
            subwindowStates["Window2"] = false;
            subwindowStates["Window3"] = false;

            // Create subwindow instances
            subwindows["Window1"] = gameObject.AddComponent<CopyScript>();
            subwindows["Window2"] = gameObject.AddComponent<ActionTimeline>();
            subwindows["Window3"] = gameObject.AddComponent<SubWindow3>();
        }

        private void OnGUI()
        {
            // Draw subwindows based on their states
            foreach (var kvp in subwindowStates)
            {
                if (kvp.Value && subwindows.ContainsKey(kvp.Key))
                {
                    subwindows[kvp.Key].DrawWindow();
                }
            }

            // Draw timeline mouse-position crosses on top when enabled
            if (subwindowStates.TryGetValue("Window2", out bool timelineVisible) && timelineVisible && subwindows["Window2"] is ActionTimeline timeline)
                timeline.DrawCrossesOverlay();
        }

        public bool IsCopyScriptVisible =>
            subwindowStates.TryGetValue("Window1", out var value) && value;

        public bool IsTimelineVisible =>
            subwindowStates.TryGetValue("Window2", out var value) && value;

        public void SetSubwindowState(string windowName, bool state)
        {
            if (subwindowStates.ContainsKey(windowName))
            {
                subwindowStates[windowName] = state;
            }
        }

        public void SetCopyScriptVisible(bool state)
        {
            if (!subwindows.TryGetValue("Window1", out var window))
                return;

            subwindowStates["Window1"] = state;
            window.SetVisible(state);
        }

        public void SetTimelineVisible(bool state)
        {
            if (!subwindows.TryGetValue("Window2", out var window))
                return;

            subwindowStates["Window2"] = state;
            window.SetVisible(state);
        }
    }
}

