using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess(PluginProcessTargets.StudioNeoV2)]
    public class CopyScriptModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.copyscript";
        public const string PluginName = "HS2 Sandbox - CopyScript";
        public const string PluginVersion = "1.0.1";

        private static Texture2D _copyIcon = null!;
        private static ToolbarToggle _copyToolbarToggle = null!;

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
            gui.RegisterWindow(SandboxWindowKeys.CopyScript, gameObject.AddComponent<CopyScript>(), initialVisible: false);

            _copyIcon = ToolbarIconLoader.LoadPng("copy-icon.png");
            _copyToolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _copyIcon,
                onValueChanged: val => gui.SetCopyScriptVisible(val));
            _copyToolbarToggle.Value = gui.IsCopyScriptVisible;

            gui.WindowVisibilityChanged += (_, visible) =>
            {
                if (_copyToolbarToggle != null)
                    _copyToolbarToggle.Value = visible;
            };
        }
    }
}

