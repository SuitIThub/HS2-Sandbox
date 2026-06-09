using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess(PluginProcessTargets.CharaStudio)]
    public class PoseBrowserModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kk.sandbox.posebrowser";
        public const string PluginName = "KK Sandbox - Pose Browser";
        public const string PluginVersion = PoseBrowserVersionInfo.Version;

        private static Texture2D _icon;
        private static ToolbarToggle _toolbarToggle;

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
