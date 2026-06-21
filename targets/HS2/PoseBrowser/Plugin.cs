using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess(PluginProcessTargets.StudioNeoV2)]
    public class PoseBrowserModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.posebrowser";
        public const string PluginName = "HS2 Sandbox - Pose Browser";
        public const string PluginVersion = PoseBrowserVersionInfo.Version;

        private static Texture2D _icon = null!;
        private static ToolbarToggle _toolbarToggle = null!;

        private SandboxGUI? _gui;

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            PoseBrowserConfig.Register(Config);
            HeelzControlService.Initialize(Config);
            PePoseCompatService.Initialize(Config);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            PePoseCompatService.EnsureDetected();
            PoseBrowserWikiRegistration.TryRegister(Logger);

            _gui = gameObject.AddComponent<SandboxGUI>();
            var pbWindow = gameObject.AddComponent<PoseBrowserWindow>();
            _gui.RegisterWindow(SandboxWindowKeys.PoseBrowser, pbWindow, initialVisible: false);

            // Heelz Control window — wired up to PoseBrowser's tag list
            var heelzWindow = gameObject.AddComponent<HeelzControlWindow>();
            heelzWindow.GetAllTagNames = () => pbWindow.GetAllLibraryTagNames();
            _gui.RegisterWindow(SandboxWindowKeys.HeelzControl, heelzWindow, initialVisible: false);

            _icon = ToolbarIconLoader.LoadPng("pose-icon.png");
            _toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _icon,
                onValueChanged: val => _gui.SetPoseBrowserVisible(val));
            _toolbarToggle.Value = _gui.IsPoseBrowserVisible;

            _gui.WindowVisibilityChanged += (key, visible) =>
            {
                if (key == SandboxWindowKeys.PoseBrowser && _toolbarToggle != null)
                    _toolbarToggle.Value = visible;
            };
        }

        private void Update()
        {
            if (_gui == null) return;

            var hk = HeelzControlService.HotkeyToggleHeelzControl;
            if (hk != null && hk.Value.MainKey != KeyCode.None && hk.Value.IsDown())
                _gui.SetHeelzControlVisible(!_gui.IsHeelzControlVisible);
        }
    }
}
