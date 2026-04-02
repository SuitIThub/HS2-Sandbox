using BepInEx.Configuration;
using BepInEx.Logging;

namespace HS2SandboxPlugin
{
    internal static class SandboxServices
    {
        public static ManualLogSource Log { get; private set; } = null!;
        public static ConfigFile Config { get; private set; } = null!;

        public static void Initialize(ManualLogSource log, ConfigFile config)
        {
            Log = log;
            Config = config;
        }
    }
}

