using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class SubWindow3 : SubWindow
    {
        protected override void Start()
        {
            base.Start();
            windowID = 2003;
            windowTitle = "Subwindow 3";
            windowRect = new Rect(400, 600, 300, 200);
        }

        protected override void DrawWindowContent(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("This is Subwindow 3", GUILayout.Height(20));
            GUILayout.Label("Functionality will be added here", GUILayout.Height(20));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Close"))
            {
                SetVisible(false);
                // Update parent checkbox state
                var sandboxGUI = FindObjectOfType<SandboxGUI>();
                if (sandboxGUI != null)
                {
                    sandboxGUI.SetSubwindowState("Window3", false);
                }
            }

            GUILayout.EndVertical();

            
            // Make window draggable and prevent mouse passthrough
            GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
            IMGUIUtils.EatInputInRect(windowRect);
        }
    }
}

