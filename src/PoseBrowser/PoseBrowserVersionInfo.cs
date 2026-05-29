namespace HS2SandboxPlugin
{
    /// <summary>Pose Browser module version (<c>versions.json</c> poseBrowser is updated by CI after each release).</summary>
    public static class PoseBrowserVersionInfo
    {
#if KKS
        public const string Version = "1.0.0";
        public const string StandaloneDllAssetName = "KKSSandbox.PoseBrowser.dll";
#else
        public const string Version = "4.3.2";
        public const string StandaloneDllAssetName = "HS2Sandbox.PoseBrowser.dll";
#endif

        public const string GitHubRepo = "SuitIThub/HS2-Sandbox";
        public const string VersionsJsonUrl =
            "https://raw.githubusercontent.com/SuitIThub/HS2-Sandbox/main/versions.json";
        public const string GitHubReleasesApiUrl =
            "https://api.github.com/repos/SuitIThub/HS2-Sandbox/releases?per_page=25";
        public const string AllInOneDllAssetName = "HS2SandboxPlugin.dll";
        public const string LatestReleasePageUrl = "https://github.com/SuitIThub/HS2-Sandbox/releases/latest";
    }
}
