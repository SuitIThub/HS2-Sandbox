using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PoseBrowserModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.posebrowser";
        public const string PluginName = "HS2 Sandbox - Pose Browser";
        public const string PluginVersion = "2.0.0";

        private static Texture2D _icon = null!;
        private static ToolbarToggle _toolbarToggle = null!;

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            PoseBrowserConfig.Register(Config);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            PoseBrowserWikiRegistration.TryRegister(Logger);

            var gui = gameObject.AddComponent<SandboxGUI>();
            gui.RegisterWindow(SandboxWindowKeys.PoseBrowser, gameObject.AddComponent<PoseBrowserWindow>(), initialVisible: false);

            _icon = ToolbarIconLoader.LoadPng("pose-icon.png");
            _toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _icon,
                onValueChanged: val => gui.SetPoseBrowserVisible(val));
            _toolbarToggle.Value = gui.IsPoseBrowserVisible;

            gui.WindowVisibilityChanged += (key, visible) =>
            {
                if (key == SandboxWindowKeys.PoseBrowser && _toolbarToggle != null)
                    _toolbarToggle.Value = visible;
            };
        }
    }
}
