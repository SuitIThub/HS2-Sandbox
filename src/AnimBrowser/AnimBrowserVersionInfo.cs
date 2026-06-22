namespace HS2SandboxPlugin
{
    /// <summary>
    /// Anim Browser module version (<c>versions.json</c> keys are updated by CI after each release).
    /// CI reads all <c>Version = "…"</c> lines in this file by index (see <c>.github/plugins.manifest.json</c>):
    /// 0 = KKS, 1 = KK, 2 = HS2 (#else).
    /// </summary>
    public static class AnimBrowserVersionInfo
    {
#if KKS
        public const string Version = "1.1.0";
        public const string StandaloneDllAssetName = "KKSSandbox.AnimBrowser.dll";
        public const string VersionsJsonVersionKey = "animBrowserKks";
        public const string VersionsJsonDownloadKey = "animBrowserKksDownload";
        public const string UpdateCheckUserAgent = "KKSSandbox-AnimBrowser-UpdateCheck";
#elif KK
        public const string Version = "1.1.0";
        public const string StandaloneDllAssetName = "KKSandbox.AnimBrowser.dll";
        public const string VersionsJsonVersionKey = "animBrowserKk";
        public const string VersionsJsonDownloadKey = "animBrowserKkDownload";
        public const string UpdateCheckUserAgent = "KKSandbox-AnimBrowser-UpdateCheck";
#else
        public const string Version = "1.1.0";
        public const string StandaloneDllAssetName = "HS2Sandbox.AnimBrowser.dll";
        public const string VersionsJsonVersionKey = "animBrowser";
        public const string VersionsJsonDownloadKey = "animBrowserDownload";
        public const string UpdateCheckUserAgent = "HS2Sandbox-AnimBrowser-UpdateCheck";
#endif

        public const string GitHubRepo = "SuitIThub/HS2-Sandbox";
        public const string VersionsJsonUrl =
            "https://raw.githubusercontent.com/SuitIThub/HS2-Sandbox/main/versions.json";
        public const string GitHubReleasesApiUrl =
            "https://api.github.com/repos/SuitIThub/HS2-Sandbox/releases?per_page=25";
        public const string LatestReleasePageUrl = "https://github.com/SuitIThub/HS2-Sandbox/releases/latest";
    }
}
