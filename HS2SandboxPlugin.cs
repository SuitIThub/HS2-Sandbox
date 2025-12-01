using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class HS2SandboxPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox";
        public const string PluginName = "HS2 Sandbox Plugin";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log = null!;
        internal static HS2SandboxPlugin Instance = null!;

        // Configuration
        public static ConfigEntry<KeyboardShortcut> KeyToggleWindow { get; private set; } = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loaded");

            // Initialize configuration
            Init();
        }

        private void Init()
        {
            KeyToggleWindow = Config.Bind(
                "Keyboard shortcuts", "Toggle Window",
                new KeyboardShortcut(KeyCode.F6),
                new ConfigDescription("Keyboard shortcut to toggle the main sandbox window."));

            Log.LogInfo("Configuration initialized");
        }

        private void Start()
        {
            InitializeSandbox();
        }

        private void InitializeSandbox()
        {
            try
            {
                gameObject.AddComponent<SandboxGUI>();
                Log.LogInfo("Sandbox GUI initialized");
            }
            catch (Exception ex)
            {
                Log.LogError($"Error initializing sandbox: {ex.Message}");
            }
        }
    }
}

