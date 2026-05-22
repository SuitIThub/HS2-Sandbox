using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess(PluginProcessTargets.StudioNeoV2)]
    public class NotebookModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.notebook";
        public const string PluginName = "HS2 Sandbox - Notebook";
        public const string PluginVersion = "1.0.1";

        private static Texture2D _icon = null!;
        private static ToolbarToggle _toolbarToggle = null!;

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            SandboxConfig.AdditionalSearchBarParentPaths ??= Config.Bind(
                "Search Bars",
                "Additional Parent Paths",
                "",
                "Optional extra GameObject paths, one per line. These are added on top of the hard-coded search bar target paths.");

            var gui = gameObject.AddComponent<SandboxGUI>();
            gui.RegisterWindow(SandboxWindowKeys.Notebook, gameObject.AddComponent<NotebookWindow>(), initialVisible: false);

            _icon = ToolbarIconLoader.LoadPng("notes-icon.png");
            _toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _icon,
                onValueChanged: val => gui.SetNotebookVisible(val));
            _toolbarToggle.Value = gui.IsNotebookVisible;

            gui.WindowVisibilityChanged += (key, visible) =>
            {
                if (key == SandboxWindowKeys.Notebook && _toolbarToggle != null)
                    _toolbarToggle.Value = visible;
            };
        }
    }
}
