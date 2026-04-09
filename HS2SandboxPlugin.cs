using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI.Studio.UI;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.animal42069.studiobetterpenetration", BepInDependency.DependencyFlags.SoftDependency)]
    public class HS2SandboxPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox";
        public const string PluginName = "HS2 Sandbox Plugin";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log = null!;
        internal static HS2SandboxPlugin Instance = null!;

        private static Texture2D _copyIcon = null!;
        private static Texture2D _timelineIcon = null!;
        private static Texture2D _sonScaleIcon = null!;

        public static ToolbarToggle _copyToolbarToggle = null!;
        public static ToolbarToggle _timelineToolbarToggle = null!;
        public static ToolbarToggle _sonScaleToolbarToggle = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            SandboxServices.Initialize(Logger, Config);
            initializeDefaultConfigs();
            Log.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void initializeDefaultConfigs() {
            SandboxConfig.AdditionalSearchBarParentPaths = Config.Bind(
                "Search Bars",
                "Additional Parent Paths",
                "",
                "Optional extra GameObject paths, one per line. These are added on top of the hard-coded search bar target paths.");
        }

        private void Start()
        {
            InitializeSandbox();
            InitializeSearchBars();
            InitializeToolbarButtons();
        }

        private void InitializeSandbox()
        {
            try
            {
                SonScaleBpIntegration.Log = Log;
                SonScaleBpIntegration.TryInstall();
                Log.LogWarning("Son scale: timeline integration bootstrap starting.");
                SonScaleTimelineIntegration.TryInstall(Log);
                var timelineBootstrap = gameObject.AddComponent<SonScaleTimelineBootstrap>();
                timelineBootstrap.Init(Log);
                gameObject.AddComponent<SonScaleApplier>();
                gameObject.AddComponent<SonScaleManipulateUi>();
                var gui = gameObject.AddComponent<SandboxGUI>();
                gui.RegisterWindow(SandboxWindowKeys.CopyScript, gameObject.AddComponent<CopyScript>(), initialVisible: false);
                gui.RegisterWindow(SandboxWindowKeys.Timeline, gameObject.AddComponent<ActionTimeline>(), initialVisible: false);
                gui.RegisterWindow(SandboxWindowKeys.SonScale, gameObject.AddComponent<SonScaleWindow>(), initialVisible: false);
                Log.LogInfo("Sandbox GUI initialized");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error initializing sandbox: {ex.Message}");
            }
        }

        private void InitializeSearchBars()
        {
            try
            {
                gameObject.AddComponent<MultiPathSearchBarManager>();
                Log.LogInfo("Search bar manager initialized");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error initializing search bar manager: {ex.Message}");
            }
        }

        private void InitializeToolbarButtons()
        {
            try
            {
                var gui = GetComponent<SandboxGUI>();
                if (gui == null)
                {
                    Log.LogWarning("SandboxGUI not found while initializing toolbar buttons");
                    return;
                }

                LoadIcons();

                _copyToolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                    _copyIcon,
                    onValueChanged: val =>
                    {
                        gui.SetCopyScriptVisible(val);
                    });

                _copyToolbarToggle.Value = gui.IsCopyScriptVisible;

                _timelineToolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                    _timelineIcon,
                    onValueChanged: val =>
                    {
                        gui.SetTimelineVisible(val);
                    });

                _timelineToolbarToggle.Value = gui.IsTimelineVisible;

                _sonScaleToolbarToggle = CustomToolbarButtons.AddLeftToolbarToggle(
                    _sonScaleIcon,
                    onValueChanged: val =>
                    {
                        gui.SetSonScaleVisible(val);
                    });

                _sonScaleToolbarToggle.Value = gui.IsSonScaleVisible;

                // Keep toolbar toggles in sync when windows close themselves.
                gui.WindowVisibilityChanged += (key, visible) =>
                {
                    if (key == SandboxWindowKeys.CopyScript && _copyToolbarToggle != null)
                        _copyToolbarToggle.Value = visible;
                    if (key == SandboxWindowKeys.Timeline && _timelineToolbarToggle != null)
                        _timelineToolbarToggle.Value = visible;
                    if (key == SandboxWindowKeys.SonScale && _sonScaleToolbarToggle != null)
                        _sonScaleToolbarToggle.Value = visible;
                };
            }
            catch (Exception ex)
            {
                Log.LogError($"Error initializing toolbar buttons: {ex}");
            }
        }

        private static void LoadIcons()
        {
            try
            {
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var pluginDir = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrEmpty(pluginDir))
                    return;

                var copyIconPath = Path.Combine(pluginDir, "copy-icon.png");
                var timelineIconPath = Path.Combine(pluginDir, "timeline-icon.png");

                _copyIcon = LoadPng(copyIconPath);
                _timelineIcon = LoadPng(timelineIconPath);
                _sonScaleIcon = ToolbarIconLoader.LoadPng("sonscale-icon.png");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error loading toolbar icons: {ex}");
            }
        }

        private static Texture2D LoadPng(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Log.LogWarning($"Icon file not found: {filePath}");
                return new Texture2D(32, 32);
            }

            try
            {
                var data = File.ReadAllBytes(filePath);
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(data);
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to load icon '{filePath}': {ex}");
                return new Texture2D(32, 32);
            }
        }
    }
}

