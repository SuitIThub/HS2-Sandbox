using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess(PluginProcessTargets.StudioNeoV2)]
    public class TimelineModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.timeline";
        public const string PluginName = "HS2 Sandbox - Timeline";
        public const string PluginVersion = "1.0.1";

        private static Texture2D _timelineIcon = null!;
        private static ToolbarToggle _timelineToolbarToggle = null!;

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            // Shared config (so SearchBarManager can also use it when needed; harmless here)
            SandboxConfig.AdditionalSearchBarParentPaths ??= Config.Bind(
                "Search Bars",
                "Additional Parent Paths",
                "",
                "Optional extra GameObject paths, one per line. These are added on top of the hard-coded search bar target paths.");

            var gui = gameObject.AddComponent<SandboxGUI>();
            gui.RegisterWindow(SandboxWindowKeys.Timeline, gameObject.AddComponent<ActionTimeline>(), initialVisible: false);

            _timelineIcon = ToolbarIconLoader.LoadPng("timeline-icon.png");
            _timelineToolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _timelineIcon,
                onValueChanged: val => gui.SetTimelineVisible(val));
            _timelineToolbarToggle.Value = gui.IsTimelineVisible;

            gui.WindowVisibilityChanged += (_, visible) =>
            {
                if (_timelineToolbarToggle != null)
                    _timelineToolbarToggle.Value = visible;
            };
        }
    }
}

