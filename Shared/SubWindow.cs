using UnityEngine;

namespace HS2SandboxPlugin
{
    public abstract class SubWindow : MonoBehaviour
    {
        protected bool isVisible = false;
        protected Rect windowRect;
        protected int windowID;
        protected string windowTitle = string.Empty;

        protected virtual void Start()
        {
            // Initialize window position and size
            windowRect = new Rect(400, 100, 300, 200);
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            OnVisibilityChanged(visible);
        }

        protected virtual void OnVisibilityChanged(bool visible)
        {
            // Override in derived classes if needed
        }

        public virtual void DrawWindow()
        {
            if (isVisible)
            {
                windowRect = GUILayout.Window(windowID, windowRect, DrawWindowContent, windowTitle);
            }
        }

        protected abstract void DrawWindowContent(int windowID);
    }
}

