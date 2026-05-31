using System;
using System.Collections.Generic;
using Studio;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Resolves Studio item catalog indices from <see cref="Info.dicItemLoadInfo"/>.
    /// Matches <c>AdvancedItemSearch</c>: <c>AddItem(group, category, localSlot)</c> where
    /// <c>localSlot</c> is the inner dictionary key, not a session-local runtime id.
    /// </summary>
    internal static class PoseItemCatalogResolve
    {
        public static bool TryGetCatalogPaths(
            int group,
            int category,
            int localSlot,
            out string bundlePath,
            out string fileName,
            out string manifest)
        {
            bundlePath = string.Empty;
            fileName = string.Empty;
            manifest = string.Empty;

            try
            {
                var info = Singleton<Info>.Instance;
                if (!info.dicItemLoadInfo.TryGetValue(group, out var byCategory) ||
                    !byCategory.TryGetValue(category, out var bySlot) ||
                    !bySlot.TryGetValue(localSlot, out Info.ItemLoadInfo? loadInfo) ||
                    loadInfo == null)
                    return false;

                bundlePath = loadInfo.bundlePath ?? string.Empty;
                fileName = loadInfo.fileName ?? string.Empty;
                manifest = loadInfo.manifest ?? string.Empty;
                return !string.IsNullOrEmpty(bundlePath) || !string.IsNullOrEmpty(fileName);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryFindCatalogSlot(
            string? bundlePath,
            string? fileName,
            string? manifest,
            out int group,
            out int category,
            out int localSlot)
        {
            group = category = localSlot = 0;
            if (string.IsNullOrEmpty(bundlePath) && string.IsNullOrEmpty(fileName))
                return false;

            try
            {
                var catalog = Singleton<Info>.Instance.dicItemLoadInfo;
                foreach (var groupEntry in catalog)
                {
                    foreach (var categoryEntry in groupEntry.Value)
                    {
                        foreach (var slotEntry in categoryEntry.Value)
                        {
                            Info.ItemLoadInfo loadInfo = slotEntry.Value;
                            if (!PathsMatch(bundlePath, fileName, manifest, loadInfo))
                                continue;

                            group = groupEntry.Key;
                            category = categoryEntry.Key;
                            localSlot = slotEntry.Key;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public static bool TryResolveSpawnIndices(
            int savedGroup,
            int savedCategory,
            int savedSlot,
            string? bundlePath,
            string? fileName,
            string? manifest,
            out int group,
            out int category,
            out int localSlot)
        {
            if (TryFindCatalogSlot(bundlePath, fileName, manifest, out group, out category, out localSlot))
                return true;

            group = savedGroup;
            category = savedCategory;
            localSlot = savedSlot;

            if (savedGroup == 0 && savedCategory == 0 && savedSlot == 0)
                return false;

            return TryGetCatalogPaths(savedGroup, savedCategory, savedSlot, out _, out _, out _);
        }

        public static void FillCatalogPathsFromWorkspace(OCIItem item, PoseAssociatedItemRecord record)
        {
            if (item?.itemInfo == null)
                return;

            int g = item.itemInfo.group;
            int c = item.itemInfo.category;
            int slot = item.itemInfo.no;

            record.ItemGroup = g;
            record.ItemCategory = c;
            record.ItemNo = slot;

            if (TryGetCatalogPaths(g, c, slot, out string bundle, out string file, out string manifest))
            {
                record.BundlePath = bundle;
                record.AssetName = file;
                record.Manifest = manifest;
            }
        }

        private static bool PathsMatch(
            string? bundlePath,
            string? fileName,
            string? manifest,
            Info.ItemLoadInfo loadInfo)
        {
            if (!string.IsNullOrEmpty(bundlePath) &&
                !string.Equals(NormalizePath(bundlePath), NormalizePath(loadInfo.bundlePath), StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(fileName) &&
                !string.Equals(fileName, loadInfo.fileName, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(manifest) &&
                !string.Equals(manifest, loadInfo.manifest, StringComparison.OrdinalIgnoreCase))
                return false;

            return !string.IsNullOrEmpty(bundlePath) || !string.IsNullOrEmpty(fileName);
        }

        private static string NormalizePath(string path) =>
            path.Replace('\\', '/').Trim();
    }
}
