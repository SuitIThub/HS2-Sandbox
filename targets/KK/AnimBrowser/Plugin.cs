using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess(PluginProcessTargets.CharaStudio)]
    public class AnimBrowserModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kk.sandbox.animbrowser";
        public const string PluginName = "KK Sandbox - Anim Browser";
        public const string PluginVersion = AnimBrowserVersionInfo.Version;

        private static Texture2D _icon;
        private static ToolbarToggle _toolbarToggle;

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            AnimBrowserConfig.Register(Config);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            var gui = gameObject.AddComponent<SandboxGUI>();
            var previewStage = gameObject.AddComponent<AnimPreviewStage>();
            var animWindow = gameObject.AddComponent<AnimBrowserWindow>();
            animWindow.BindPreviewStage(previewStage);
            gui.RegisterWindow(SandboxWindowKeys.AnimBrowser, animWindow, initialVisible: false);

            _icon = ToolbarIconLoader.LoadPng("anim-icon.png");
            _toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _icon,
                onValueChanged: val => gui.SetAnimBrowserVisible(val));
            _toolbarToggle.Value = gui.IsAnimBrowserVisible;

            gui.WindowVisibilityChanged += (key, visible) =>
            {
                if (key == SandboxWindowKeys.AnimBrowser && _toolbarToggle != null)
                    _toolbarToggle.Value = visible;
            };
        }
    }
}
