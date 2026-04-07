using BepInEx;
using KKAPI.Studio.UI;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.animal42069.studiobetterpenetration", BepInDependency.DependencyFlags.SoftDependency)]
    public class SonScaleModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.sonscale";
        public const string PluginName = "HS2 Sandbox - Son scale";
        public const string PluginVersion = "1.0.0";

        private static Texture2D _icon = null!;
        private static ToolbarToggle _toolbarToggle = null!;

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            SonScaleBpIntegration.Log = Logger;
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            SonScaleBpIntegration.TryInstall();
            SandboxConfig.AdditionalSearchBarParentPaths ??= Config.Bind(
                "Search Bars",
                "Additional Parent Paths",
                "",
                "Optional extra GameObject paths, one per line. These are added on top of the hard-coded search bar target paths.");

            gameObject.AddComponent<SonScaleApplier>();
            gameObject.AddComponent<SonScaleManipulateUi>();

            var gui = gameObject.AddComponent<SandboxGUI>();
            gui.RegisterWindow(SandboxWindowKeys.SonScale, gameObject.AddComponent<SonScaleWindow>(), initialVisible: false);

            _icon = ToolbarIconLoader.LoadPng("sonscale-icon.png");
            _toolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                _icon,
                onValueChanged: val => gui.SetSonScaleVisible(val));
            _toolbarToggle.Value = gui.IsSonScaleVisible;

            gui.WindowVisibilityChanged += (key, visible) =>
            {
                if (key == SandboxWindowKeys.SonScale && _toolbarToggle != null)
                    _toolbarToggle.Value = visible;
            };
        }
    }
}
