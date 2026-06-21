using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess(PluginProcessTargets.StudioNeoV2)]
    public class AnimBrowserModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.animbrowser";
        public const string PluginName = "HS2 Sandbox - Anim Browser";
        public const string PluginVersion = AnimBrowserVersionInfo.Version;

        private static Texture2D _icon = null!;
        private static ToolbarToggle _toolbarToggle = null!;

        private SandboxGUI? _gui;

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            AnimBrowserConfig.Register(Config);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            _gui = gameObject.AddComponent<SandboxGUI>();
            var previewStage = gameObject.AddComponent<AnimPreviewStage>();
            var animWindow = gameObject.AddComponent<AnimBrowserWindow>();
            animWindow.BindPreviewStage(previewStage);
            _gui.RegisterWindow(SandboxWindowKeys.AnimBrowser, animWindow, initialVisible: false);

            _icon = ToolbarIconLoader.LoadPng("anim-icon.png");
            _toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _icon,
                onValueChanged: val => _gui.SetAnimBrowserVisible(val));
            _toolbarToggle.Value = _gui.IsAnimBrowserVisible;

            _gui.WindowVisibilityChanged += (key, visible) =>
            {
                if (key == SandboxWindowKeys.AnimBrowser && _toolbarToggle != null)
                    _toolbarToggle.Value = visible;
            };

            AnimBrowserWikiRegistration.TryRegister(Logger);
        }
    }
}
