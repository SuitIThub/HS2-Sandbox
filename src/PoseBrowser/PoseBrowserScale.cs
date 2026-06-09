using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Single source of truth for the Pose Browser UI scale. The factor is stored in
    /// <see cref="PoseBrowserConfig.UiScale"/> (mirrored in the Options pane). Scaling is applied as a
    /// <em>logical</em> multiplier on font sizes and structural pixel dimensions — the GUI coordinate space
    /// stays in real screen pixels, so all screen-space clamping / dragging / hit-testing keeps working.
    /// </summary>
    internal static class PoseBrowserScale
    {
        internal const float MinFactor = 0.8f;
        internal const float MaxFactor = 2.5f;
        internal const float DefaultFactor = 1f;

        private const int FallbackBaseFontSize = 12;

        private static int _baseFontSize;
        private static bool _baseCaptured;

        /// <summary>Configured UI scale, clamped to the supported range. Falls back to 1 before config registration.</summary>
        public static float Factor
        {
            get
            {
                var entry = PoseBrowserConfig.UiScale;
                if (entry == null)
                    return DefaultFactor;
                return Mathf.Clamp(entry.Value, MinFactor, MaxFactor);
            }
        }

        /// <summary>Records the unscaled label font size of the active skin once, so scaling is relative to the game default.</summary>
        public static void CaptureBaseFont(GUISkin skin)
        {
            if (_baseCaptured || skin == null)
                return;
            int f = skin.label != null ? skin.label.fontSize : 0;
            _baseFontSize = f > 0 ? f : FallbackBaseFontSize;
            _baseCaptured = true;
        }

        public static int BaseFontSize => _baseCaptured && _baseFontSize > 0 ? _baseFontSize : FallbackBaseFontSize;

        /// <summary>Scale a base pixel dimension by the current factor.</summary>
        public static float Px(float basePx) => basePx * Factor;

        /// <summary>Scale a base font size (rounded, never below 1).</summary>
        public static int Font(int baseFontSize) => Mathf.Max(1, Mathf.RoundToInt(baseFontSize * Factor));

        // Scaled GUILayout sizing options. Use these instead of GUILayout.Width/Height/etc. with a *raw design
        // pixel* literal so fixed-size controls (bar buttons, badges, columns) grow with the UI scale and stop
        // clipping their — now larger — text. Pass a value that is NOT already scaled.
        public static GUILayoutOption W(float basePx) => GUILayout.Width(basePx * Factor);
        public static GUILayoutOption H(float basePx) => GUILayout.Height(basePx * Factor);
        public static GUILayoutOption MinW(float basePx) => GUILayout.MinWidth(basePx * Factor);
        public static GUILayoutOption MaxW(float basePx) => GUILayout.MaxWidth(basePx * Factor);
        public static GUILayoutOption MinH(float basePx) => GUILayout.MinHeight(basePx * Factor);
        public static GUILayoutOption MaxH(float basePx) => GUILayout.MaxHeight(basePx * Factor);
    }
}
