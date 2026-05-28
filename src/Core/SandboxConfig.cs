using BepInEx.Configuration;

namespace HS2SandboxPlugin
{
    internal static class SandboxConfig
    {
        public static ConfigEntry<string>? AdditionalSearchBarParentPaths { get; set; }
    }
}

