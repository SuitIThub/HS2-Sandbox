using System.Collections.Generic;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class SandboxGUI : MonoBehaviour
    {
        private bool showMainWindow = false;
        private Rect mainWindowRect = new(100, 100, 200, 150);

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

        private void Update()
        {
            // Check for keyboard shortcut to toggle main window
            if (HS2SandboxPlugin.KeyToggleWindow.Value.IsDown())
            {
                showMainWindow = !showMainWindow;
            }
        }

        private void OnGUI()
        {
            // Draw main window
            if (showMainWindow)
            {
                // Use a deterministic UUID-based integer for the window ID
                mainWindowRect = GUILayout.Window("79fcdb1b-5449-4b0e-b6d1-0063e007d96b".GetHashCode(), mainWindowRect, DrawMainWindow, "Sandbox Control Panel");
            }

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

        private void DrawMainWindow(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // Checkbox for Window 1
            bool window1State = GUILayout.Toggle(subwindowStates["Window1"], "CopyScript", GUILayout.Height(20));
            if (window1State != subwindowStates["Window1"])
            {
                subwindowStates["Window1"] = window1State;
                subwindows["Window1"].SetVisible(window1State);
            }

            // Checkbox for Window 2
            bool window2State = GUILayout.Toggle(subwindowStates["Window2"], "Timeline", GUILayout.Height(20));
            if (window2State != subwindowStates["Window2"])
            {
                subwindowStates["Window2"] = window2State;
                subwindows["Window2"].SetVisible(window2State);
            }

            // Checkbox for Window 3
            bool window3State = GUILayout.Toggle(subwindowStates["Window3"], "Window 3", GUILayout.Height(20));
            if (window3State != subwindowStates["Window3"])
            {
                subwindowStates["Window3"] = window3State;
                subwindows["Window3"].SetVisible(window3State);
            }

            GUILayout.Space(3);

            if (GUILayout.Button("Close", GUILayout.Height(22)))
            {
                showMainWindow = false;
            }

            GUILayout.EndVertical();

            
            // Make window draggable and prevent mouse passthrough
            GUI.DragWindow(new Rect(0, 0, mainWindowRect.width, mainWindowRect.height));
            IMGUIUtils.EatInputInRect(mainWindowRect);
        }

        public void SetSubwindowState(string windowName, bool state)
        {
            if (subwindowStates.ContainsKey(windowName))
            {
                subwindowStates[windowName] = state;
            }
        }
    }
}

