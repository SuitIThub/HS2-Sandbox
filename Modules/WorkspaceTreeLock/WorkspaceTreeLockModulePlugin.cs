using BepInEx;
using HS2SandboxPlugin.WorkspaceTreeLock;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class WorkspaceTreeLockModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.workspacetreelock";
        public const string PluginName = "HS2 Sandbox - Workspace tree lock";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            WorkspaceTreeLockBootstrap.Install(gameObject);
        }
    }
}
