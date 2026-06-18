using BepInEx.Configuration;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class AnimBrowserConfig
    {
        public const int OptionsJsonVersion = 4;

        /// <summary>World-space radius for merging characters into one controls box (same animation group).</summary>
        public const float ControlsProximityRadius = 3.5f;

        /// <summary>Speed values at or below this break Studio character playback.</summary>
        public const float MinPlaybackSpeed = 0.01f;

        public static ConfigEntry<float>? UiScale;
        public static ConfigEntry<float>? CardColumnWidth;
        public static ConfigEntry<bool>? AutoTranslate;
        public static ConfigEntry<KeyboardShortcut>? HotkeyToggleUndockedControls;

        private const string KeyboardSection = "Anim Browser · Keyboard shortcuts";

        public static void Register(ConfigFile cfg)
        {
            if (CardColumnWidth != null)
                return;

            UiScale = cfg.Bind(
                "Anim Browser",
                "UI scale",
                AnimBrowserScale.DefaultFactor,
                new ConfigDescription(
                    "Scales the Anim Browser UI.",
                    new AcceptableValueRange<float>(AnimBrowserScale.MinFactor, AnimBrowserScale.MaxFactor)));

            CardColumnWidth = cfg.Bind(
                "Anim Browser",
                "Card column width (px)",
                120f,
                new ConfigDescription(
                    "Minimum width of each animation card column in the grid.",
                    new AcceptableValueRange<float>(80f, 320f)));

            AutoTranslate = cfg.Bind(
                "Anim Browser",
                "Auto translate names",
                true,
                "Translate animation and category names through XUnity Auto Translator when installed.");

            const string windowHk =
                "Uses BepInEx KeyboardShortcut (main key + optional modifiers in Configuration Manager). " +
                "Leave unassigned (None) to disable.";

            HotkeyToggleUndockedControls = cfg.Bind(
                KeyboardSection,
                "Toggle undocked controls",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(
                    "Open or close the floating animation controls window (undocked). " + windowHk));
        }
    }
}
