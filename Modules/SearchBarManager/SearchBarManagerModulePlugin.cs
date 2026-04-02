using BepInEx;

namespace HS2SandboxPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SearchBarManagerModulePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hs2.sandbox.searchbarmanager";
        public const string PluginName = "HS2 Sandbox - SearchBarManager";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            SandboxServices.Initialize(Logger, Config);

            SandboxConfig.AdditionalSearchBarParentPaths = Config.Bind(
                "Search Bars",
                "Additional Parent Paths",
                "",
                "Optional extra GameObject paths, one per line. These are added on top of the hard-coded search bar target paths.");

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded");
        }

        private void Start()
        {
            gameObject.AddComponent<MultiPathSearchBarManager>();
        }
    }
}

