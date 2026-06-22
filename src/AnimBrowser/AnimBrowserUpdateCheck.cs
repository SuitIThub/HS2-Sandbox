using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.Networking;

namespace HS2SandboxPlugin
{
    /// <summary>Checks GitHub for a newer Anim Browser release (versions.json + release asset URL).
    /// Every step is failure-tolerant: any network/parse error simply lands in <see cref="Status.Unavailable"/>
    /// and the UI shows nothing — it never throws into the caller's coroutine.</summary>
    public sealed class AnimBrowserUpdateCheck
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

        private static Regex? _versionRegex;
        private static Regex VersionRegex =>
            _versionRegex ??
            (_versionRegex = VersionsJsonStringRegex(AnimBrowserVersionInfo.VersionsJsonVersionKey));

        private static Regex? _downloadRegex;
        private static Regex DownloadRegex =>
            _downloadRegex ??
            (_downloadRegex = VersionsJsonUrlRegex(AnimBrowserVersionInfo.VersionsJsonDownloadKey));

        public IEnumerator RunCheck()
        {
            State = Status.Checking;
            RemoteVersion = null;
            DownloadUrl = null;

            string? remoteVersion = null;
            string? versionsJsonDownload = null;
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

            RemoteVersion = remoteVersion!.Trim();
            if (AnimBrowserSemver.Compare(RemoteVersion, AnimBrowserVersionInfo.Version) <= 0)
            {
                State = Status.UpToDate;
                yield break;
            }

            string? downloadUrl = versionsJsonDownload;
            if (string.IsNullOrEmpty(downloadUrl))
                yield return FetchNewestReleaseAssetUrl(AnimBrowserVersionInfo.StandaloneDllAssetName, url => downloadUrl = url);

            DownloadUrl = string.IsNullOrEmpty(downloadUrl)
                ? AnimBrowserVersionInfo.LatestReleasePageUrl
                : downloadUrl;
            State = Status.UpdateAvailable;
        }

        private static IEnumerator FetchVersionsJson(Action<string?, string?> onDone)
        {
            UnityWebRequest? req = null;
            try
            {
                req = UnityWebRequest.Get(AnimBrowserVersionInfo.VersionsJsonUrl);
                req.timeout = 12;
            }
            catch
            {
                if (req != null) req.Dispose();
                onDone(null, null);
                yield break;
            }

            using (req)
            {
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

                string? version = null;
                string? download = null;
                try
                {
                    string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                    if (body == null) body = "";

                    var versionMatch = VersionRegex.Match(body);
                    if (versionMatch.Success)
                        version = versionMatch.Groups[1].Value;

                    var downloadMatch = DownloadRegex.Match(body);
                    if (downloadMatch.Success)
                        download = downloadMatch.Groups[1].Value;
                }
                catch
                {
                    version = null;
                    download = null;
                }

                onDone(version, download);
            }
        }

        private static IEnumerator FetchNewestReleaseAssetUrl(string assetFileName, Action<string?> onDone)
        {
            UnityWebRequest? req = null;
            try
            {
                req = UnityWebRequest.Get(AnimBrowserVersionInfo.GitHubReleasesApiUrl);
                req.timeout = 15;
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("User-Agent", AnimBrowserVersionInfo.UpdateCheckUserAgent);
            }
            catch
            {
                if (req != null) req.Dispose();
                onDone(null);
                yield break;
            }

            using (req)
            {
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

                string? url = null;
                try
                {
                    string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                    if (body == null) body = "";
                    url = TryParseAssetDownloadUrl(body, assetFileName);
                }
                catch
                {
                    url = null;
                }

                onDone(url);
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

    internal static class AnimBrowserSemver
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
