using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace HS2SandboxPlugin
{
    /// <summary>Checks GitHub for a newer Pose Browser release (versions.json + release asset URL).</summary>
    public sealed class PoseBrowserUpdateCheck
    {
        public enum Status
        {
            Idle,
            Checking,
            UpdateAvailable,
            UpToDate,
            Unavailable
        }

        public Status State { get; private set; } = Status.Idle;
        public string? RemoteVersion { get; private set; }
        public string? DownloadUrl { get; private set; }

        private static Regex _poseBrowserVersionRegex;
        private static Regex PoseBrowserVersionRegex =>
            _poseBrowserVersionRegex ??
            (_poseBrowserVersionRegex = VersionsJsonStringRegex(PoseBrowserVersionInfo.VersionsJsonVersionKey));

        private static Regex _poseBrowserDownloadRegex;
        private static Regex PoseBrowserDownloadRegex =>
            _poseBrowserDownloadRegex ??
            (_poseBrowserDownloadRegex = VersionsJsonUrlRegex(PoseBrowserVersionInfo.VersionsJsonDownloadKey));

        private static Regex _allInOneDownloadRegex;
        private static Regex AllInOneDownloadRegex =>
            _allInOneDownloadRegex ??
            (_allInOneDownloadRegex = RegexEx.Create(@"""allInOneDownload""\s*:\s*""(https://[^""]+)"""));

        public IEnumerator RunCheck()
        {
            State = Status.Checking;
            RemoteVersion = null;
            DownloadUrl = null;

            string remoteVersion = null;
            string versionsJsonDownload = null;
            yield return FetchVersionsJson((version, downloadUrl) =>
            {
                remoteVersion = version;
                versionsJsonDownload = downloadUrl;
            });

            if (StringEx.IsNullOrWhiteSpace(remoteVersion))
            {
                State = Status.Unavailable;
                yield break;
            }

            RemoteVersion = remoteVersion.Trim();
            if (PoseBrowserSemver.Compare(RemoteVersion, PoseBrowserVersionInfo.Version) <= 0)
            {
                State = Status.UpToDate;
                yield break;
            }

            string downloadUrl = versionsJsonDownload;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                string assetName = UsesStandalonePoseBrowserDll()
                    ? PoseBrowserVersionInfo.StandaloneDllAssetName
                    : PoseBrowserVersionInfo.AllInOneDllAssetName;

                yield return FetchNewestReleaseAssetUrl(assetName, url => downloadUrl = url);
            }

            DownloadUrl = string.IsNullOrEmpty(downloadUrl)
                ? PoseBrowserVersionInfo.LatestReleasePageUrl
                : downloadUrl;
            State = Status.UpdateAvailable;
        }

        private static bool UsesStandalonePoseBrowserDll()
        {
            string expectedName = System.IO.Path.GetFileNameWithoutExtension(
                PoseBrowserVersionInfo.StandaloneDllAssetName);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == expectedName)
                    return true;
            }

            return false;
        }

        private static IEnumerator FetchVersionsJson(Action<string, string> onDone)
        {
            using (var req = UnityWebRequest.Get(PoseBrowserVersionInfo.VersionsJsonUrl))
            {
                req.timeout = 12;
#if KK
                yield return req.Send();
                if (!string.IsNullOrEmpty(req.error))
#else
                yield return req.SendWebRequest();
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onDone(null, null);
                    yield break;
                }

                string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (body == null) body = "";
                string version = null;
                var versionMatch = PoseBrowserVersionRegex.Match(body);
                if (versionMatch.Success)
                    version = versionMatch.Groups[1].Value;

                string download = null;
                var downloadMatch = UsesStandalonePoseBrowserDll()
                    ? PoseBrowserDownloadRegex.Match(body)
                    : AllInOneDownloadRegex.Match(body);
                if (downloadMatch.Success)
                    download = downloadMatch.Groups[1].Value;

                onDone(version, download);
            }
        }

        private static IEnumerator FetchNewestReleaseAssetUrl(string assetFileName, Action<string> onDone)
        {
            using (var req = UnityWebRequest.Get(PoseBrowserVersionInfo.GitHubReleasesApiUrl))
            {
                req.timeout = 15;
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("User-Agent", PoseBrowserVersionInfo.UpdateCheckUserAgent);
#if KK
                yield return req.Send();
                if (!string.IsNullOrEmpty(req.error))
#else
                yield return req.SendWebRequest();
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onDone(null);
                    yield break;
                }

                string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (body == null) body = "";
                onDone(TryParseAssetDownloadUrl(body, assetFileName));
            }
        }

        /// <summary>
        /// GitHub asset JSON places <c>browser_download_url</c> after a nested <c>uploader</c> object,
        /// so naive [^}]* regexes stop too early.
        /// </summary>
        internal static string? TryParseAssetDownloadUrl(string releasesJson, string assetFileName)
        {
            if (string.IsNullOrEmpty(releasesJson) || string.IsNullOrEmpty(assetFileName))
                return null;

            string pattern =
                "\"name\"\\s*:\\s*\"" + Regex.Escape(assetFileName) +
                "\"[\\s\\S]{0,8000}?\"browser_download_url\"\\s*:\\s*\"(?<url>https://[^\"]+)\"";

            var match = RegexEx.Create(pattern).Match(releasesJson);

            return match.Success ? match.Groups["url"].Value : null;
        }

        private static Regex VersionsJsonStringRegex(string key) =>
            RegexEx.Create("\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]+)\"");

        private static Regex VersionsJsonUrlRegex(string key) =>
            RegexEx.Create("\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(https://[^\"]+)\"");
    }

    internal static class PoseBrowserSemver
    {
        public static int Compare(string a, string b)
        {
            int am, ai, ap, bm, bi, bp;
            Parse(a, out am, out ai, out ap);
            Parse(b, out bm, out bi, out bp);
            if (am != bm) return am.CompareTo(bm);
            if (ai != bi) return ai.CompareTo(bi);
            return ap.CompareTo(bp);
        }

        private static void Parse(string version, out int major, out int minor, out int patch)
        {
            major = minor = patch = 0;
            if (StringEx.IsNullOrWhiteSpace(version))
                return;

            var parts = version.Trim().Split('.');
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            if (parts.Length > 2) int.TryParse(parts[2], out patch);
        }
    }
}

