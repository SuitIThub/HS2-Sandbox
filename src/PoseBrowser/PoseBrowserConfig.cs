using BepInEx.Configuration;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>BepInEx ConfigurationManager entries for Pose Browser (mirrored in the in-game Options panel).</summary>
    internal static class PoseBrowserConfig
    {
        public const int OptionsJsonVersion = 12;

        /// <summary>Config section used for <see cref="KeyboardShortcut"/> entries (ConfigurationManager key picker).</summary>
        public const string KeyboardSection = "Pose Browser · Keyboard shortcuts";

        public static ConfigEntry<float>? CardColumnWidth;
        public static ConfigEntry<int>? ItemsPerPage;
        public static ConfigEntry<float>? AutoCaptureDelaySeconds;
        public static ConfigEntry<KeyboardShortcut>? HotkeyNextPose;
        public static ConfigEntry<KeyboardShortcut>? HotkeyPrevPose;
        public static ConfigEntry<KeyboardShortcut>? HotkeyNextBrowse;
        public static ConfigEntry<KeyboardShortcut>? HotkeyPrevBrowse;
        public static ConfigEntry<KeyboardShortcut>? HotkeyToggleVisible;
        public static ConfigEntry<KeyboardShortcut>? HotkeyToggleMinimize;
        public static ConfigEntry<int>? HistoryMaxEntries;
        public static ConfigEntry<bool>? FreezeAnimationSpeedOnApply;
        public static ConfigEntry<KeyboardShortcut>? HotkeyUndo;
        public static ConfigEntry<KeyboardShortcut>? HotkeyRedo;

        public static void Register(ConfigFile cfg)
        {
            if (CardColumnWidth != null)
                return;

            CardColumnWidth = cfg.Bind(
                "Pose Browser",
                "Card column width (px)",
                140f,
                new ConfigDescription(
                    "Minimum width of each pose card column in the grid (full layout). Editable in Pose Browser → Options.",
                    new AcceptableValueRange<float>(80f, 400f)));

            ItemsPerPage = cfg.Bind(
                "Pose Browser",
                "Items per page (grid)",
                0,
                new ConfigDescription(
                    "Maximum poses per page in the thumbnail grid; 0 shows all. Editable in Pose Browser → Options.",
                    new AcceptableValueRange<int>(0, 5000)));

            AutoCaptureDelaySeconds = cfg.Bind(
                "Pose Browser",
                "Auto-capture delay (seconds)",
                2f,
                new ConfigDescription(
                    "Pause after applying each pose before taking a thumbnail during Auto-capture. Editable in Pose Browser → Options.",
                    new AcceptableValueRange<float>(0.5f, 30f)));

            HistoryMaxEntries = cfg.Bind(
                "Pose Browser",
                "History entries per character",
                PoseBrowserHistory.DefaultMaxEntriesPerCharacter,
                new ConfigDescription(
                    "Maximum pose history snapshots kept per character; oldest entries are removed. Editable in Pose Browser → Options.",
                    new AcceptableValueRange<int>(10, 5000)));

            FreezeAnimationSpeedOnApply = cfg.Bind(
                "Pose Browser",
                "Freeze animation speed on apply",
                false,
                new ConfigDescription(
                    "When enabled, sets each affected character's animation speed to 0 after applying a pose from the browser or restoring a history entry. Editable in Pose Browser → Options."));

            const string hk =
                "Active while Pose Browser is open. Uses BepInEx KeyboardShortcut (main key + optional modifiers in Configuration Manager). " +
                "Leave unassigned (None) to disable. Browse keys match Folder ◀ / ▶ (root, folders depth-first, all poses, favorites).";

            HotkeyNextPose = cfg.Bind(
                KeyboardSection,
                "Next pose",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(hk));

            HotkeyPrevPose = cfg.Bind(
                KeyboardSection,
                "Previous pose",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(hk));

            HotkeyNextBrowse = cfg.Bind(
                KeyboardSection,
                "Next browse target (folder step)",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(hk));

            HotkeyPrevBrowse = cfg.Bind(
                KeyboardSection,
                "Previous browse target (folder step)",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(hk));

            const string windowHk =
                "Uses BepInEx KeyboardShortcut (main key + optional modifiers in Configuration Manager). " +
                "Leave unassigned (None) to disable.";

            HotkeyToggleVisible = cfg.Bind(
                KeyboardSection,
                "Toggle Pose Browser window",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(
                    "Open or close the Pose Browser window (toolbar toggle stays in sync). " + windowHk));

            HotkeyToggleMinimize = cfg.Bind(
                KeyboardSection,
                "Toggle minimize (PB chip)",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(
                    "When the window is open: minimize to the draggable PB chip or restore from it. " + windowHk));

            HotkeyUndo = cfg.Bind(
                KeyboardSection,
                "Undo pose change",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(
                    "Undo the last pose history step for Studio-selected characters. " + hk));

            HotkeyRedo = cfg.Bind(
                KeyboardSection,
                "Redo pose change",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(
                    "Redo pose history for Studio-selected characters. " + hk));
        }
    }
}
