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
        public const string Version = "1.0.1";
        public const string StandaloneDllAssetName = "KKSSandbox.AnimBrowser.dll";
        public const string VersionsJsonVersionKey = "animBrowserKks";
#elif KK
        public const string Version = "1.0.1";
        public const string StandaloneDllAssetName = "KKSandbox.AnimBrowser.dll";
        public const string VersionsJsonVersionKey = "animBrowserKk";
#else
        public const string Version = "1.0.1";
        public const string StandaloneDllAssetName = "HS2Sandbox.AnimBrowser.dll";
        public const string VersionsJsonVersionKey = "animBrowser";
#endif
    }
}
