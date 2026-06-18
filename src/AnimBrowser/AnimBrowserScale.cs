using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class AnimBrowserScale
    {
        internal const float MinFactor = 0.8f;
        internal const float MaxFactor = 2.5f;
        internal const float DefaultFactor = 1f;

        private const int FallbackBaseFontSize = 12;
        private static int _baseFontSize;
        private static bool _baseCaptured;

        public static float Factor
        {
            get
            {
                var entry = AnimBrowserConfig.UiScale;
                if (entry == null)
                    return DefaultFactor;
                return Mathf.Clamp(entry.Value, MinFactor, MaxFactor);
            }
        }

        public static void CaptureBaseFont(GUISkin skin)
        {
            if (_baseCaptured || skin == null)
                return;
            int f = skin.label != null ? skin.label.fontSize : 0;
            _baseFontSize = f > 0 ? f : FallbackBaseFontSize;
            _baseCaptured = true;
        }

        public static int BaseFontSize => _baseCaptured && _baseFontSize > 0 ? _baseFontSize : FallbackBaseFontSize;

        public static float Px(float basePx) => basePx * Factor;
        public static int Font(int baseFontSize) => Mathf.Max(1, Mathf.RoundToInt(baseFontSize * Factor));

        public static GUILayoutOption W(float basePx) => GUILayout.Width(basePx * Factor);
        public static GUILayoutOption H(float basePx) => GUILayout.Height(basePx * Factor);
        public static GUILayoutOption MinW(float basePx) => GUILayout.MinWidth(basePx * Factor);
        public static GUILayoutOption MaxW(float basePx) => GUILayout.MaxWidth(basePx * Factor);
        public static GUILayoutOption MinH(float basePx) => GUILayout.MinHeight(basePx * Factor);
        public static GUILayoutOption MaxH(float basePx) => GUILayout.MaxHeight(basePx * Factor);
    }
}
