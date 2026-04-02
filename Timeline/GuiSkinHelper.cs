using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>IMGUI helpers when <see cref="GUI.skin"/> is not ready (e.g. early frames).</summary>
    internal static class GuiSkinHelper
    {
        public static GUIStyle SafeLabelStyle()
        {
            try
            {
                if (GUI.skin != null && GUI.skin.label != null)
                    return GUI.skin.label;
            }
            catch { /* ignored */ }
            return new GUIStyle();
        }
    }
}
