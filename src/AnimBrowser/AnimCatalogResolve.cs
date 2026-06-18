using System;
using System.Collections;
using Manager;
using Studio;

namespace HS2SandboxPlugin
{
    internal readonly struct AnimAnimationMetadata
    {
        public readonly string DisplayName;
        public readonly string CatalogPath;
        public readonly string CatalogKey;
        public readonly string ClipName;
        public readonly string BundlePath;
        public readonly string FileName;
        public readonly string Manifest;
        public readonly string SourceLabel;
        public readonly string AssetPathLine;

        public AnimAnimationMetadata(
            string displayName,
            string catalogPath,
            string catalogKey,
            string clipName,
            string bundlePath,
            string fileName,
            string manifest,
            string sourceLabel,
            string assetPathLine)
        {
            DisplayName = displayName;
            CatalogPath = catalogPath;
            CatalogKey = catalogKey;
            ClipName = clipName;
            BundlePath = bundlePath;
            FileName = fileName;
            Manifest = manifest;
            SourceLabel = sourceLabel;
            AssetPathLine = assetPathLine;
        }

        public bool IsValid => !string.IsNullOrEmpty(DisplayName) || !string.IsNullOrEmpty(CatalogKey);
    }

    internal static class AnimCatalogResolve
    {
        public static bool TryGetLoadInfo(int group, int category, int no, out Info.AnimeLoadInfo? loadInfo)
        {
            loadInfo = null;
            try
            {
                var info = Singleton<Info>.Instance;
                if (info?.dicAnimeLoadInfo == null)
                    return false;
                if (!info.dicAnimeLoadInfo.TryGetValue(group, out var byCategory) ||
                    !byCategory.TryGetValue(category, out var byNo) ||
                    !byNo.TryGetValue(no, out loadInfo))
                    return false;
                return loadInfo != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsMainGameLoadInfo(Info.AnimeLoadInfo? loadInfo)
        {
            if (loadInfo == null)
                return false;

            string bundlePath = NormalizePath(loadInfo.bundlePath ?? "");
            string manifest = (loadInfo.manifest ?? "").Trim();
            return IsGameAbdataPath(bundlePath) || IsGameManifest(manifest);
        }

        public static bool IsMainGameAnimation(int group, int category, int no) =>
            TryGetLoadInfo(group, category, no, out Info.AnimeLoadInfo? loadInfo) && IsMainGameLoadInfo(loadInfo);

        /// <summary>
        /// True when the group/category appears in Studio's animation category tree
        /// (<see cref="Info.dicAGroupCategory"/>). Entries only present in
        /// <see cref="Info.dicAnimeLoadInfo"/> (e.g. H-scene lists) are not listed.
        /// </summary>
        public static bool IsStudioListedCategory(int group, int category)
        {
            try
            {
                var info = Singleton<Info>.Instance;
                if (info?.dicAGroupCategory == null)
                    return false;
                if (!info.dicAGroupCategory.TryGetValue(group, out Info.GroupInfo? groupInfo) ||
                    groupInfo?.dicCategory == null)
                {
                    return false;
                }

                if (groupInfo.dicCategory.ContainsKey(category))
                    return true;

                if (groupInfo.dicCategory is IDictionary idict)
                    return idict.Contains(category);

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static AnimAnimationMetadata BuildMetadata(AnimCatalogRef reference, AnimCatalogService catalog)
        {
            string catalogPath = catalog.GetCatalogPath(reference.Group, reference.Category);
            string catalogKey = reference.Key;
            string displayName = "Animation " + catalogKey;

            if (!TryGetLoadInfo(reference.Group, reference.Category, reference.No, out Info.AnimeLoadInfo? loadInfo) ||
                loadInfo == null)
            {
                return new AnimAnimationMetadata(
                    displayName,
                    catalogPath,
                    catalogKey,
                    "",
                    "",
                    "",
                    "",
                    "Unknown",
                    "");
            }

            if (!string.IsNullOrEmpty(loadInfo.name))
                displayName = StudioAutoTranslation.Resolve(loadInfo.name);

            string bundlePath = loadInfo.bundlePath ?? "";
            string fileName = loadInfo.fileName ?? "";
            string manifest = loadInfo.manifest ?? "";
            string clip = loadInfo.clip ?? "";
            string sourceLabel = ResolveSourceLabel(bundlePath, manifest);
            string assetPathLine = BuildAssetPathLine(bundlePath, fileName);

            return new AnimAnimationMetadata(
                displayName,
                catalogPath,
                catalogKey,
                clip,
                bundlePath,
                fileName,
                manifest,
                sourceLabel,
                assetPathLine);
        }

        private static string ResolveSourceLabel(string bundlePath, string manifest)
        {
            string normalizedBundle = NormalizePath(bundlePath);
            string manifestTrim = (manifest ?? "").Trim();

            if (IsGameAbdataPath(normalizedBundle) || IsGameManifest(manifestTrim))
                return "Game — abdata";

            if (!string.IsNullOrEmpty(manifestTrim))
                return "Sideloader — " + manifestTrim;

            if (!string.IsNullOrEmpty(normalizedBundle))
                return "Asset bundle";

            return "Unknown";
        }

        private static bool IsGameAbdataPath(string normalizedBundle) =>
            !string.IsNullOrEmpty(normalizedBundle) &&
            normalizedBundle.IndexOf("abdata", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsGameManifest(string manifest) =>
            manifest.Equals("studio", StringComparison.OrdinalIgnoreCase) ||
            manifest.Equals("abdata", StringComparison.OrdinalIgnoreCase) ||
            manifest.Equals("chara", StringComparison.OrdinalIgnoreCase);

        private static string BuildAssetPathLine(string bundlePath, string fileName)
        {
            if (string.IsNullOrEmpty(bundlePath) && string.IsNullOrEmpty(fileName))
                return "";

            if (string.IsNullOrEmpty(fileName))
                return NormalizePath(bundlePath);

            if (string.IsNullOrEmpty(bundlePath))
                return fileName;

            return NormalizePath(bundlePath) + " / " + fileName;
        }

        private static string NormalizePath(string path) =>
            (path ?? "").Replace('\\', '/').Trim();
    }
}
