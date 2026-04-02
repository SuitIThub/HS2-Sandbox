using BepInEx;
using KKAPI.Studio.UI;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CopyScriptModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.copyscript";
        public const string PluginName = "HS2 Sandbox - CopyScript";
        public const string PluginVersion = "1.0.0";

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

            LoadIcon();
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

        private static void LoadIcon()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var pluginDir = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrEmpty(pluginDir))
                return;

            var copyIconPath = Path.Combine(pluginDir, "copy-icon.png");
            _copyIcon = LoadPng(copyIconPath);
        }

        private static Texture2D LoadPng(string filePath)
        {
            if (!File.Exists(filePath))
                return new Texture2D(32, 32);

            try
            {
                var data = File.ReadAllBytes(filePath);
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(data);
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch (Exception)
            {
                return new Texture2D(32, 32);
            }
        }
    }
}

