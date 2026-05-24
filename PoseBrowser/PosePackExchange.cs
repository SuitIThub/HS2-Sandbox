using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Pose ZIP exchange: original <c>.png</c>/<c>.dat</c> files under <c>poses/</c>, plus <c>manifest.json</c> (schema + kind)
    /// and <c>metadata.json</c> (tags, favorites, paths; v3+ optional <c>groups</c>; v4 adds <c>memberRelativeOffsets</c>; v5 adds
    /// <c>memberBodyHeights</c>). Import parses <c>metadata.json</c> with a small JSON reader — not <see cref="JsonUtility"/> — so
    /// <c>items</c> arrays round-trip with export. Exports use manifest <see cref="ManifestVersion"/> (5); imports accept 2–5.
    /// Flat <c>poses</c> export uses only <c>poses/&lt;filename&gt;</c>
    /// (no subfolders; collisions get <c>-01</c> suffixes). <c>treeBranch</c> keeps structure under <c>poses/&lt;branchRoot&gt;/…</c>.
    /// Legacy blob packs (v1) still import.
    /// </summary>
    public static class PosePackExchange
    {
        public const string ZipExtension = ".zip";

        /// <summary>All pose/binary content lives under this prefix inside the ZIP.</summary>
        public const string PosesDirectoryPrefix = "poses/";

        public const string ManifestEntryName = "manifest.json";
        public const string DefaultMetadataEntryName = "metadata.json";

        public const string SchemaId = "HS2Sandbox.poseZip";
        /// <summary>Manifest <c>version</c> written on export (current format).</summary>
        public const int ManifestVersion = 5;
        /// <summary>Oldest manifest <c>version</c> still accepted on import (v2 packs without <c>groups</c>).</summary>
        public const int MinImportManifestVersion = 2;

        public const string KindPoses = "poses";
        public const string KindTreeBranch = "treeBranch";

        // --- Legacy v1 (opaque files/0000 blobs; still supported for import) ---
        private const string LegacyPoseFormatId = "HS2Sandbox.PosePack";
        private const string LegacyTreeFormatId = "HS2Sandbox.PoseTreePack";
        private const int LegacyManifestVersion = 1;
        private const string LegacyFilesDir = "files/";

        [Serializable]
        public class PoseZipManifestJson
        {
            public string schema = "";
            public int version;
            /// <summary><see cref="KindPoses"/> or <see cref="KindTreeBranch"/>.</summary>
            public string kind = "";
            public string exportedUtc = "";
            /// <summary>Folder name under <c>poses/</c> for <see cref="KindTreeBranch"/>; empty for poses-only.</summary>
            public string branchRoot = "";
            /// <summary>Entry in the ZIP listing per-file metadata (default <see cref="DefaultMetadataEntryName"/>).</summary>
            public string metadata = DefaultMetadataEntryName;
        }

        [Serializable]
        public class PoseZipItemJson
        {
            /// <summary>Path inside the ZIP (forward slashes), e.g. <c>poses/MyPose.png</c> or <c>poses/Branch/sub/a.png</c>.</summary>
            public string file = "";
            public string[] tags = Array.Empty<string>();
            public bool favorite;
            public string lastWriteTimeUtc = "";
            public string creationTimeUtc = "";
        }

        [Serializable]
        public class PoseZipGroupJson
        {
            public string id = "";
            public string name = "";
            public string[] tags = Array.Empty<string>();
            /// <summary>ZIP-internal pose paths (<c>poses/…</c>) belonging to this group.</summary>
            public string[] members = Array.Empty<string>();
            /// <summary>
            /// Optional layout offsets parallel to <see cref="members"/> (v4). Index 0 is anchor [0,0,0];
            /// later entries are world-space offsets from the first member's character position.
            /// </summary>
            public float[][]? memberRelativeOffsets;
            /// <summary>Maker body-height slider per member, parallel to <see cref="members"/> (v5).</summary>
            public float[]? memberBodyHeights;
        }

        public sealed class PosePackReadGroup
        {
            public string Id { get; }
            public string Name { get; }
            public string[] Tags { get; }
            public string[] MemberZipPaths { get; }
            public Vector3[]? MemberRelativeOffsets { get; }
            public float[]? MemberBodyHeights { get; }

            public PosePackReadGroup(
                string id,
                string name,
                string[] tags,
                string[] memberZipPaths,
                Vector3[]? memberRelativeOffsets = null,
                float[]? memberBodyHeights = null)
            {
                Id = id;
                Name = name;
                Tags = tags;
                MemberZipPaths = memberZipPaths;
                MemberRelativeOffsets = memberRelativeOffsets;
                MemberBodyHeights = memberBodyHeights;
            }
        }

        public sealed class PosePackReadEntry
        {
            public string Id { get; }
            public string SuggestedFileName { get; }
            /// <summary>Relative path under the tree root (tree packs only); empty for flat packs.</summary>
            public string RelPath { get; }
            public byte[] FileBytes { get; }
            public string DisplayName { get; }
            public bool IsPng { get; }
            public string[] Tags { get; }
            public bool Favorite { get; }
            public DateTime LastWriteUtc { get; }
            public DateTime CreationUtc { get; }
            /// <summary>ZIP-internal path from metadata (<c>poses/…</c>).</summary>
            public string ZipInternalPath { get; }

            public PosePackReadEntry(
                string id,
                string suggestedFileName,
                string relPath,
                byte[] fileBytes,
                string displayName,
                bool isPng,
                string[] tags,
                bool favorite,
                DateTime lastWriteUtc,
                DateTime creationUtc,
                string zipInternalPath)
            {
                Id = id;
                SuggestedFileName = suggestedFileName;
                RelPath = relPath;
                FileBytes = fileBytes;
                DisplayName = displayName;
                IsPng = isPng;
                Tags = tags;
                Favorite = favorite;
                LastWriteUtc = lastWriteUtc;
                CreationUtc = creationUtc;
                ZipInternalPath = zipInternalPath;
            }
        }

        public sealed class PosePackReadResult
        {
            public bool IsTreePack { get; }
            public string TreeRootFolderName { get; }
            public string ExportedUtcIso { get; }
            public List<PosePackReadEntry> Entries { get; }
            public List<PosePackReadGroup> Groups { get; }

            public PosePackReadResult(
                bool isTreePack,
                string treeRootFolderName,
                string exportedUtcIso,
                List<PosePackReadEntry> entries,
                List<PosePackReadGroup>? groups = null)
            {
                IsTreePack = isTreePack;
                TreeRootFolderName = treeRootFolderName;
                ExportedUtcIso = exportedUtcIso;
                Entries = entries;
                Groups = groups ?? new List<PosePackReadGroup>();
            }
        }

        /// <summary>Maps library-relative paths to flat <c>poses/…</c> ZIP entry paths (same rules as export).</summary>
        public static Dictionary<string, string> MapItemsToTreeZipPaths(
            string poseLibraryRoot,
            string rootFolderNodeFullPath,
            string treeRootFolderName,
            IReadOnlyList<PoseGridItem> items)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string rootNorm = Path.GetFullPath(rootFolderNodeFullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string safeRootName = PoseDataService.SanitizeFileName(treeRootFolderName);
            if (string.IsNullOrEmpty(safeRootName))
                safeRootName = "folder";
            string branchPrefix = PosesDirectoryPrefix + safeRootName.Trim('/').Replace('\\', '/') + "/";
            foreach (var item in items)
            {
                string rel = item.RelativePath(poseLibraryRoot);
                if (string.IsNullOrEmpty(rel)) continue;
                string itemFull = Path.GetFullPath(item.FilePath);
                string? itemDir = Path.GetDirectoryName(itemFull);
                if (string.IsNullOrEmpty(itemDir))
                    itemDir = rootNorm;
                string relFromNode = PoseDataService.GetRelativePath(rootNorm, itemDir);
                string fileName = Path.GetFileName(item.FilePath);
                string relUnderBranch = string.IsNullOrEmpty(relFromNode)
                    ? fileName
                    : relFromNode.Replace('\\', '/') + "/" + fileName;
                string normRel = PoseGroupDatabase.NormalizeMemberPath(rel);
                if (!string.IsNullOrEmpty(normRel))
                    map[normRel] = branchPrefix + relUnderBranch;
            }

            return map;
        }

        public static Dictionary<string, string> MapItemsToFlatZipPaths(
            string poseLibraryRoot,
            IReadOnlyList<PoseGridItem> items)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var usedFlatLeaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string libNorm = Path.GetFullPath(poseLibraryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var item in items)
            {
                string rel = PoseDataService.GetRelativePath(libNorm, item.FilePath);
                if (string.IsNullOrEmpty(rel)) continue;
                string normRel = PoseGroupDatabase.NormalizeMemberPath(rel);
                if (string.IsNullOrEmpty(normRel)) continue;
                string leaf = MakeUniqueFlatZipEntryName(item.FilePath, usedFlatLeaves);
                map[normRel] = PosesDirectoryPrefix + leaf;
            }

            return map;
        }

        public static bool TryExportPosePack(
            string zipPath,
            string poseLibraryRoot,
            IReadOnlyList<PoseGridItem> items,
            IReadOnlyList<PoseZipGroupJson>? groups = null)
        {
            try
            {
                if (items.Count == 0) return false;
                string libNorm = Path.GetFullPath(poseLibraryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string? dir = Path.GetDirectoryName(Path.GetFullPath(zipPath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var metaItems = new PoseZipItemJson[items.Count];
                var parts = new List<(string name, byte[] data)>(items.Count + 2);
                var usedFlatLeaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    string full = Path.GetFullPath(item.FilePath);
                    if (full.Length < libNorm.Length || !full.StartsWith(libNorm, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Exported pose is not under the pose library folder.");
                    if (full.Length > libNorm.Length && full[libNorm.Length] != Path.DirectorySeparatorChar &&
                        full[libNorm.Length] != Path.AltDirectorySeparatorChar)
                        throw new InvalidDataException("Exported pose is not under the pose library folder.");

                    string leaf = MakeUniqueFlatZipEntryName(item.FilePath, usedFlatLeaves);
                    string zipInternal = PosesDirectoryPrefix + leaf;
                    if (!IsSafeZipInternalPath(zipInternal))
                        throw new InvalidDataException($"Unsafe path for ZIP: {zipInternal}");

                    byte[] bytes = File.ReadAllBytes(item.FilePath);
                    parts.Add((zipInternal, bytes));

                    metaItems[i] = new PoseZipItemJson
                    {
                        file = zipInternal,
                        tags = item.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray(),
                        favorite = item.IsFavorite,
                        lastWriteTimeUtc = item.LastWriteTime.ToUniversalTime().ToString("o"),
                        creationTimeUtc = item.CreationTimeUtc.ToString("o")
                    };
                }

                var manifest = new PoseZipManifestJson
                {
                    schema = SchemaId,
                    version = ManifestVersion,
                    kind = KindPoses,
                    exportedUtc = DateTime.UtcNow.ToString("o"),
                    branchRoot = "",
                    metadata = DefaultMetadataEntryName
                };

                parts.Add((ManifestEntryName, Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest))));
                parts.Add((DefaultMetadataEntryName, SerializeMetadataUtf8(metaItems, groups)));

                MinimalStoredZip.Write(zipPath, parts);
                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Export pose ZIP failed: {ex.Message}");
                try
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                }
                catch { /* ignore */ }

                return false;
            }
        }

        public static bool TryExportTreePack(
            string zipPath,
            string poseLibraryRoot,
            string rootFolderNodeFullPath,
            string treeRootFolderName,
            IReadOnlyList<PoseGridItem> items,
            IReadOnlyList<PoseZipGroupJson>? groups = null)
        {
            try
            {
                if (items.Count == 0) return false;
                string rootNorm = Path.GetFullPath(rootFolderNodeFullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string libNorm = Path.GetFullPath(poseLibraryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!rootNorm.StartsWith(libNorm, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Tree export root is not under the pose library.");
                string? dir = Path.GetDirectoryName(Path.GetFullPath(zipPath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string safeRootName = PoseDataService.SanitizeFileName(treeRootFolderName);
                if (string.IsNullOrEmpty(safeRootName))
                    safeRootName = "folder";

                var entryList = new List<PoseZipItemJson>();
                var parts = new List<(string name, byte[] data)>(items.Count + 2);

                string branchPrefix = PosesDirectoryPrefix + safeRootName.Trim('/').Replace('\\', '/') + "/";

                foreach (var item in items)
                {
                    string itemFull = Path.GetFullPath(item.FilePath);
                    string? itemDir = Path.GetDirectoryName(itemFull);
                    if (string.IsNullOrEmpty(itemDir))
                        itemDir = rootNorm;
                    string relFromNode = PoseDataService.GetRelativePath(rootNorm, itemDir);
                    string fileName = Path.GetFileName(item.FilePath);
                    string relUnderBranch = string.IsNullOrEmpty(relFromNode)
                        ? fileName
                        : relFromNode.Replace('\\', '/') + "/" + fileName;

                    string zipInternal = branchPrefix + relUnderBranch;
                    if (!IsSafeZipInternalPath(zipInternal))
                        throw new InvalidDataException($"Unsafe path for ZIP: {zipInternal}");

                    byte[] bytes = File.ReadAllBytes(item.FilePath);
                    parts.Add((zipInternal, bytes));

                    entryList.Add(new PoseZipItemJson
                    {
                        file = zipInternal,
                        tags = item.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray(),
                        favorite = item.IsFavorite,
                        lastWriteTimeUtc = item.LastWriteTime.ToUniversalTime().ToString("o"),
                        creationTimeUtc = item.CreationTimeUtc.ToString("o")
                    });
                }

                var manifest = new PoseZipManifestJson
                {
                    schema = SchemaId,
                    version = ManifestVersion,
                    kind = KindTreeBranch,
                    exportedUtc = DateTime.UtcNow.ToString("o"),
                    branchRoot = safeRootName,
                    metadata = DefaultMetadataEntryName
                };

                var metaArr = entryList.ToArray();

                parts.Add((ManifestEntryName, Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest))));
                parts.Add((DefaultMetadataEntryName, SerializeMetadataUtf8(metaArr, groups)));

                MinimalStoredZip.Write(zipPath, parts);
                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Export tree ZIP failed: {ex.Message}");
                try
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                }
                catch { /* ignore */ }

                return false;
            }
        }

        public static bool TryReadPack(string zipPath, out PosePackReadResult? result, out string? error)
        {
            result = null;
            error = null;
            try
            {
                Dictionary<string, byte[]> dict = MinimalStoredZip.ReadAll(zipPath);
                if (!dict.TryGetValue(ManifestEntryName, out byte[]? manifestBytes) || manifestBytes == null || manifestBytes.Length == 0)
                {
                    error = "Missing manifest.json.";
                    return false;
                }

                string manifestJson = Encoding.UTF8.GetString(manifestBytes);

                var manifest = JsonUtility.FromJson<PoseZipManifestJson>(manifestJson);
                if (manifest != null && string.Equals(manifest.schema, SchemaId, StringComparison.Ordinal))
                {
                    if (!IsSupportedImportManifestVersion(manifest.version))
                    {
                        error = $"Unsupported manifest version {manifest.version} (supported: {MinImportManifestVersion}–{ManifestVersion}).";
                        return false;
                    }

                    return TryReadPackV2(dict, manifest, out result, out error);
                }

                if (TryReadPackLegacy(dict, manifestJson, out result, out error))
                    return true;

                error = error ?? $"Unknown or unsupported pack (need manifest.schema \"{SchemaId}\" version {MinImportManifestVersion} or {ManifestVersion}, or a legacy Studio Sandbox pack).";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsSupportedImportManifestVersion(int version) =>
            version >= MinImportManifestVersion && version <= ManifestVersion;

        private static bool TryReadPackV2(
            Dictionary<string, byte[]> dict,
            PoseZipManifestJson manifest,
            out PosePackReadResult? result,
            out string? error)
        {
            result = null;
            error = null;

            string metaName = string.IsNullOrWhiteSpace(manifest.metadata) ? DefaultMetadataEntryName : manifest.metadata!.Trim().Replace('\\', '/');
            if (metaName.IndexOf("..", StringComparison.Ordinal) >= 0 || metaName.StartsWith("/", StringComparison.Ordinal))
            {
                error = "Invalid metadata path in manifest.";
                return false;
            }

            if (!dict.TryGetValue(metaName, out byte[]? metaBytes) || metaBytes == null || metaBytes.Length == 0)
            {
                error = $"Missing metadata file \"{metaName}\".";
                return false;
            }

            bool isTree = string.Equals(manifest.kind, KindTreeBranch, StringComparison.Ordinal);
            if (!isTree && !string.Equals(manifest.kind, KindPoses, StringComparison.Ordinal))
            {
                error = $"Unknown manifest.kind \"{manifest.kind}\" (expected \"{KindPoses}\" or \"{KindTreeBranch}\").";
                return false;
            }

            string branchRoot = (manifest.branchRoot ?? "").Replace('\\', '/').Trim('/');
            if (isTree && string.IsNullOrEmpty(branchRoot))
            {
                error = "treeBranch pack requires branchRoot in manifest.";
                return false;
            }

            if (!isTree && !string.IsNullOrEmpty(branchRoot))
                branchRoot = "";

            string treePrefix = isTree ? $"{PosesDirectoryPrefix}{branchRoot}/" : "";

            if (!TryParseMetadataUtf8(metaBytes, out PoseZipItemJson[] metaItems, out var readGroups, out string? parseErr) || metaItems.Length == 0)
            {
                error = string.IsNullOrEmpty(parseErr) ? "Metadata has no items." : parseErr;
                return false;
            }

            var list = new List<PosePackReadEntry>(metaItems.Length);
            int idx = 0;
            foreach (var it in metaItems)
            {
                if (it == null || string.IsNullOrWhiteSpace(it.file))
                {
                    error = "Metadata item missing \"file\" path.";
                    return false;
                }

                string fileKey = it.file.Replace('\\', '/').TrimStart('/');
                if (!IsSafeZipInternalPath(fileKey) || !fileKey.StartsWith(PosesDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Illegal or unexpected file path in metadata: {it.file}";
                    return false;
                }

                if (!dict.TryGetValue(fileKey, out byte[]? bytes) || bytes == null)
                {
                    error = $"ZIP missing file listed in metadata: {fileKey}";
                    return false;
                }

                string leaf = fileKey;
                int slash = leaf.LastIndexOf('/');
                string suggestedName = slash >= 0 ? leaf.Substring(slash + 1) : leaf;

                string relPathUnderBranch = "";
                if (isTree)
                {
                    if (!fileKey.StartsWith(treePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"File is not under poses/{branchRoot}/: {fileKey}";
                        return false;
                    }

                    relPathUnderBranch = fileKey.Substring(treePrefix.Length);
                }

                string id = idx.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                bool isPng = Path.GetExtension(suggestedName).Equals(".png", StringComparison.OrdinalIgnoreCase);

                list.Add(new PosePackReadEntry(
                    id,
                    suggestedName,
                    relPathUnderBranch,
                    bytes,
                    Path.GetFileNameWithoutExtension(suggestedName),
                    isPng,
                    it.tags ?? Array.Empty<string>(),
                    it.favorite,
                    ParseIsoOrMin(it.lastWriteTimeUtc),
                    ParseIsoOrMin(it.creationTimeUtc),
                    fileKey));
                idx++;
            }

            var packGroups = readGroups.Select(g => new PosePackReadGroup(
                g.id ?? "",
                g.name ?? "",
                g.tags ?? Array.Empty<string>(),
                g.members ?? Array.Empty<string>(),
                ConvertGroupMemberOffsets(g),
                ConvertGroupMemberBodyHeights(g))).ToList();
            result = new PosePackReadResult(isTree, branchRoot, manifest.exportedUtc ?? "", list, packGroups);
            return true;
        }

        private static bool TryReadPackLegacy(Dictionary<string, byte[]> dict, string manifestJson, out PosePackReadResult? result, out string? error)
        {
            result = null;
            error = null;

            var probePose = JsonUtility.FromJson<LegacyPosePackManifestJson>(manifestJson);
            if (probePose != null && string.Equals(probePose.format, LegacyPoseFormatId, StringComparison.Ordinal))
            {
                if (probePose.version != LegacyManifestVersion || probePose.entries == null)
                {
                    error = "Invalid legacy pose pack manifest.";
                    return false;
                }

                var list = new List<PosePackReadEntry>(probePose.entries.Length);
                foreach (var e in probePose.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    if (!TryGetLegacyFileBytes(dict, e.id, out byte[] bytes))
                    {
                        error = $"Missing data for entry {e.id}.";
                        return false;
                    }

                    bool isPng = e.formatKind == 0;
                    if (!isPng && e.formatKind != 1)
                        isPng = Path.GetExtension(e.fileName).Equals(".png", StringComparison.OrdinalIgnoreCase);

                    list.Add(new PosePackReadEntry(
                        e.id,
                        e.fileName,
                        "",
                        bytes,
                        e.displayName,
                        isPng,
                        e.tags ?? Array.Empty<string>(),
                        e.favorite,
                        ParseIsoOrMin(e.lastWriteTimeUtc),
                        ParseIsoOrMin(e.creationTimeUtc),
                        ""));
                }

                result = new PosePackReadResult(false, "", probePose.exportedUtc ?? "", list);
                return true;
            }

            var probeTree = JsonUtility.FromJson<LegacyTreePackManifestJson>(manifestJson);
            if (probeTree != null && string.Equals(probeTree.format, LegacyTreeFormatId, StringComparison.Ordinal))
            {
                if (probeTree.version != LegacyManifestVersion || probeTree.entries == null)
                {
                    error = "Invalid legacy tree pack manifest.";
                    return false;
                }

                var list = new List<PosePackReadEntry>(probeTree.entries.Length);
                foreach (var e in probeTree.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    if (!TryGetLegacyFileBytes(dict, e.id, out byte[] bytes))
                    {
                        error = $"Missing data for entry {e.id}.";
                        return false;
                    }

                    bool isPng = e.formatKind == 0;
                    if (!isPng && e.formatKind != 1)
                    {
                        string leaf = e.relPath?.Replace('\\', '/') ?? "";
                        int s = leaf.LastIndexOf('/');
                        string nameOnly = s >= 0 ? leaf.Substring(s + 1) : leaf;
                        isPng = Path.GetExtension(nameOnly).Equals(".png", StringComparison.OrdinalIgnoreCase);
                    }

                    string rel = (e.relPath ?? "").Replace('\\', '/');
                    string leafName = rel;
                    int si = rel.LastIndexOf('/');
                    if (si >= 0)
                        leafName = rel.Substring(si + 1);

                    list.Add(new PosePackReadEntry(
                        e.id,
                        leafName,
                        rel,
                        bytes,
                        e.displayName,
                        isPng,
                        e.tags ?? Array.Empty<string>(),
                        e.favorite,
                        ParseIsoOrMin(e.lastWriteTimeUtc),
                        ParseIsoOrMin(e.creationTimeUtc),
                        ""));
                }

                result = new PosePackReadResult(true, probeTree.rootFolderName ?? "folder", probeTree.exportedUtc ?? "", list);
                return true;
            }

            return false;
        }

        private static bool TryGetLegacyFileBytes(Dictionary<string, byte[]> dict, string id, out byte[] bytes)
        {
            if (dict.TryGetValue(LegacyFilesDir + id, out byte[]? b) && b != null)
            {
                bytes = b;
                return true;
            }

            bytes = Array.Empty<byte>();
            return false;
        }

        /// <summary>
        /// Unity <see cref="JsonUtility.ToJson"/> often drops arrays of nested serializable types; metadata must round-trip reliably.
        /// </summary>
        private static byte[] SerializeMetadataUtf8(PoseZipItemJson[] items, IReadOnlyList<PoseZipGroupJson>? groups = null)
        {
            var sb = new StringBuilder(Math.Max(64, items.Length * 160));
            sb.Append("{\"items\":[");
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) sb.Append(',');
                AppendMetadataItemJson(sb, items[i]);
            }

            sb.Append(']');
            if (groups != null && groups.Count > 0)
            {
                sb.Append(",\"groups\":[");
                for (int g = 0; g < groups.Count; g++)
                {
                    if (g > 0) sb.Append(',');
                    AppendMetadataGroupJson(sb, groups[g]);
                }

                sb.Append(']');
            }

            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static void AppendMetadataItemJson(StringBuilder sb, PoseZipItemJson it)
        {
            sb.Append("{\"file\":");
            AppendJsonString(sb, it.file);
            sb.Append(",\"tags\":[");
            string[]? tags = it.tags;
            if (tags != null)
            {
                for (int t = 0; t < tags.Length; t++)
                {
                    if (t > 0) sb.Append(',');
                    AppendJsonString(sb, tags[t]);
                }
            }

            sb.Append("],\"favorite\":");
            sb.Append(it.favorite ? "true" : "false");
            sb.Append(",\"lastWriteTimeUtc\":");
            AppendJsonString(sb, it.lastWriteTimeUtc);
            sb.Append(",\"creationTimeUtc\":");
            AppendJsonString(sb, it.creationTimeUtc);
            sb.Append('}');
        }

        private static void AppendMetadataGroupJson(StringBuilder sb, PoseZipGroupJson g)
        {
            sb.Append("{\"id\":");
            AppendJsonString(sb, g.id);
            sb.Append(",\"name\":");
            AppendJsonString(sb, g.name);
            sb.Append(",\"tags\":[");
            if (g.tags != null)
            {
                for (int t = 0; t < g.tags.Length; t++)
                {
                    if (t > 0) sb.Append(',');
                    AppendJsonString(sb, g.tags[t]);
                }
            }

            sb.Append("],\"members\":[");
            if (g.members != null)
            {
                for (int m = 0; m < g.members.Length; m++)
                {
                    if (m > 0) sb.Append(',');
                    AppendJsonString(sb, g.members[m]);
                }
            }

            sb.Append(']');
            if (g.memberRelativeOffsets != null && g.memberRelativeOffsets.Length > 0)
            {
                sb.Append(",\"memberRelativeOffsets\":[");
                for (int o = 0; o < g.memberRelativeOffsets.Length; o++)
                {
                    if (o > 0) sb.Append(',');
                    AppendMetadataVec3Json(sb, g.memberRelativeOffsets[o]);
                }

                sb.Append(']');
            }

            if (g.memberBodyHeights != null && g.memberBodyHeights.Length > 0)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                sb.Append(",\"memberBodyHeights\":[");
                for (int h = 0; h < g.memberBodyHeights.Length; h++)
                {
                    if (h > 0) sb.Append(',');
                    sb.Append(g.memberBodyHeights[h].ToString("R", inv));
                }

                sb.Append(']');
            }

            sb.Append('}');
        }

        private static void AppendMetadataVec3Json(StringBuilder sb, float[]? xyz)
        {
            sb.Append('[');
            if (xyz != null && xyz.Length >= 3)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                sb.Append(xyz[0].ToString("R", inv));
                sb.Append(',');
                sb.Append(xyz[1].ToString("R", inv));
                sb.Append(',');
                sb.Append(xyz[2].ToString("R", inv));
            }
            else
            {
                sb.Append("0,0,0");
            }

            sb.Append(']');
        }

        private static Vector3[]? ConvertGroupMemberOffsets(PoseZipGroupJson g)
        {
            if (g.memberRelativeOffsets == null || g.memberRelativeOffsets.Length == 0)
                return null;

            var arr = new Vector3[g.memberRelativeOffsets.Length];
            for (int i = 0; i < g.memberRelativeOffsets.Length; i++)
            {
                float[]? xyz = g.memberRelativeOffsets[i];
                if (xyz != null && xyz.Length >= 3)
                    arr[i] = new Vector3(xyz[0], xyz[1], xyz[2]);
            }

            return arr;
        }

        private static float[]? ConvertGroupMemberBodyHeights(PoseZipGroupJson g)
        {
            if (g.memberBodyHeights == null || g.memberBodyHeights.Length == 0)
                return null;
            return (float[])g.memberBodyHeights.Clone();
        }

        private static void AppendJsonString(StringBuilder sb, string? s)
        {
            sb.Append('"');
            if (string.IsNullOrEmpty(s))
            {
                sb.Append('"');
                return;
            }

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        private static string MetadataUtf8BytesToText(byte[] metaBytes)
        {
            int start = 0;
            if (metaBytes.Length >= 3 && metaBytes[0] == 0xEF && metaBytes[1] == 0xBB && metaBytes[2] == 0xBF)
                start = 3;
            return Encoding.UTF8.GetString(metaBytes, start, metaBytes.Length - start);
        }

        /// <summary>
        /// <see cref="JsonUtility.FromJson"/> does not deserialize arrays of nested types reliably; parse the v2 metadata we emit.
        /// </summary>
        private static bool TryParseMetadataUtf8(
            byte[] metaBytes,
            out PoseZipItemJson[] items,
            out PoseZipGroupJson[] groups,
            out string? error)
        {
            items = Array.Empty<PoseZipItemJson>();
            groups = Array.Empty<PoseZipGroupJson>();
            error = null;
            try
            {
                string json = MetadataUtf8BytesToText(metaBytes);
                if (!TryParsePoseZipMetadataJson(json, out items, out groups, out error))
                    return false;
                return items.Length > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParsePoseZipMetadataJson(
            string json,
            out PoseZipItemJson[] items,
            out PoseZipGroupJson[] groups,
            out string? error)
        {
            items = Array.Empty<PoseZipItemJson>();
            groups = Array.Empty<PoseZipGroupJson>();
            error = null;
            int i = 0;
            MetadataJson_SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{')
            {
                error = "Metadata must be a JSON object.";
                return false;
            }

            i++;
            List<PoseZipItemJson>? parsed = null;
            List<PoseZipGroupJson>? parsedGroups = null;
            while (true)
            {
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                    break;
                }

                if (!MetadataJson_TryReadString(json, ref i, out string key, out error))
                {
                    error ??= "Invalid metadata JSON (key).";
                    return false;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':')
                {
                    error = "Invalid metadata JSON (expected ':').";
                    return false;
                }

                i++;
                MetadataJson_SkipWs(json, ref i);
                if (string.Equals(key, "items", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadItemsArray(json, ref i, out parsed, out error))
                        return false;
                }
                else if (string.Equals(key, "groups", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadGroupsArray(json, ref i, out parsedGroups, out error))
                        return false;
                }
                else
                {
                    if (!MetadataJson_TrySkipValue(json, ref i, out error))
                        return false;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    continue;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                    break;
                }

                error = "Invalid metadata JSON (trailing in root object).";
                return false;
            }

            if (parsed == null || parsed.Count == 0)
            {
                error = "Metadata has no items.";
                return false;
            }

            items = parsed.ToArray();
            groups = parsedGroups != null ? parsedGroups.ToArray() : Array.Empty<PoseZipGroupJson>();
            return true;
        }

        private static void MetadataJson_SkipWs(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
        }

        private static bool MetadataJson_TryReadString(string json, ref int i, out string value, out string? error)
        {
            value = "";
            error = null;
            if (i >= json.Length || json[i] != '"')
            {
                error = "Expected string.";
                return false;
            }

            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"')
                {
                    value = sb.ToString();
                    error = null;
                    return true;
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (i >= json.Length)
                {
                    error = "Unterminated string escape.";
                    return false;
                }

                c = json[i++];
                switch (c)
                {
                    case '"':
                        sb.Append('"');
                        break;
                    case '\\':
                        sb.Append('\\');
                        break;
                    case '/':
                        sb.Append('/');
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 > json.Length)
                        {
                            error = "Invalid \\u escape.";
                            return false;
                        }

                        int code = 0;
                        for (int h = 0; h < 4; h++)
                        {
                            int d = HexDigit(json[i++]);
                            if (d < 0)
                            {
                                error = "Invalid \\u hex.";
                                return false;
                            }

                            code = (code << 4) | d;
                        }

                        sb.Append((char)code);
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            error = "Unterminated string.";
            return false;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static bool MetadataJson_TrySkipValue(string json, ref int i, out string? error)
        {
            error = null;
            MetadataJson_SkipWs(json, ref i);
            if (i >= json.Length)
            {
                error = "Unexpected end of metadata JSON.";
                return false;
            }

            char c = json[i];
            if (c == '"')
                return MetadataJson_TryReadString(json, ref i, out _, out error);
            if (c == '{')
            {
                i++;
                while (true)
                {
                    MetadataJson_SkipWs(json, ref i);
                    if (i < json.Length && json[i] == '}')
                    {
                        i++;
                        return true;
                    }

                    if (!MetadataJson_TryReadString(json, ref i, out _, out error))
                        return false;
                    MetadataJson_SkipWs(json, ref i);
                    if (i >= json.Length || json[i] != ':')
                    {
                        error = "Invalid object (expected ':').";
                        return false;
                    }

                    i++;
                    if (!MetadataJson_TrySkipValue(json, ref i, out error))
                        return false;
                    MetadataJson_SkipWs(json, ref i);
                    if (i < json.Length && json[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    MetadataJson_SkipWs(json, ref i);
                    if (i < json.Length && json[i] == '}')
                    {
                        i++;
                        return true;
                    }

                    error = "Invalid object structure.";
                    return false;
                }
            }

            if (c == '[')
            {
                i++;
                while (true)
                {
                    MetadataJson_SkipWs(json, ref i);
                    if (i < json.Length && json[i] == ']')
                    {
                        i++;
                        return true;
                    }

                    if (!MetadataJson_TrySkipValue(json, ref i, out error))
                        return false;
                    MetadataJson_SkipWs(json, ref i);
                    if (i < json.Length && json[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    MetadataJson_SkipWs(json, ref i);
                    if (i < json.Length && json[i] == ']')
                    {
                        i++;
                        return true;
                    }

                    error = "Invalid array structure.";
                    return false;
                }
            }

            if (c == 't' && i + 3 < json.Length && json[i + 1] == 'r' && json[i + 2] == 'u' && json[i + 3] == 'e')
            {
                i += 4;
                return true;
            }

            if (c == 'f' && i + 4 < json.Length &&
                json[i + 1] == 'a' && json[i + 2] == 'l' && json[i + 3] == 's' && json[i + 4] == 'e')
            {
                i += 5;
                return true;
            }

            if (c == 'n' && i + 3 < json.Length &&
                json[i + 1] == 'u' && json[i + 2] == 'l' && json[i + 3] == 'l')
            {
                i += 4;
                return true;
            }

            if (c == '-' || (c >= '0' && c <= '9'))
            {
                i++;
                while (i < json.Length)
                {
                    char x = json[i];
                    if ((x >= '0' && x <= '9') || x == '.' || x == 'e' || x == 'E' || x == '+' || x == '-')
                    {
                        i++;
                        continue;
                    }

                    break;
                }

                return true;
            }

            error = "Unexpected token in metadata JSON.";
            return false;
        }

        private static bool MetadataJson_TryReadItemsArray(string json, ref int i, out List<PoseZipItemJson>? items, out string? error)
        {
            items = null;
            error = null;
            if (i >= json.Length || json[i] != '[')
            {
                error = "Expected '[' for items.";
                return false;
            }

            i++;
            var list = new List<PoseZipItemJson>();
            MetadataJson_SkipWs(json, ref i);
            if (i < json.Length && json[i] == ']')
            {
                i++;
                items = list;
                return true;
            }

            while (true)
            {
                if (!MetadataJson_TryReadItemObject(json, ref i, out var one, out error))
                    return false;
                list.Add(one);
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    MetadataJson_SkipWs(json, ref i);
                    continue;
                }

                if (i < json.Length && json[i] == ']')
                {
                    i++;
                    items = list;
                    return true;
                }

                error = "Invalid items array.";
                return false;
            }
        }

        private static bool MetadataJson_TryReadGroupsArray(string json, ref int i, out List<PoseZipGroupJson>? groups, out string? error)
        {
            groups = null;
            error = null;
            if (i >= json.Length || json[i] != '[')
            {
                error = "Expected '[' for groups.";
                return false;
            }

            i++;
            var list = new List<PoseZipGroupJson>();
            MetadataJson_SkipWs(json, ref i);
            if (i < json.Length && json[i] == ']')
            {
                i++;
                groups = list;
                return true;
            }

            while (true)
            {
                if (!MetadataJson_TryReadGroupObject(json, ref i, out var one, out error))
                    return false;
                list.Add(one);
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    MetadataJson_SkipWs(json, ref i);
                    continue;
                }

                if (i < json.Length && json[i] == ']')
                {
                    i++;
                    groups = list;
                    return true;
                }

                error = "Invalid groups array.";
                return false;
            }
        }

        private static bool MetadataJson_TryReadGroupObject(string json, ref int i, out PoseZipGroupJson group, out string? error)
        {
            group = new PoseZipGroupJson();
            error = null;
            var tagsList = new List<string>();
            var membersList = new List<string>();
            List<float[]>? offsetsList = null;
            List<float>? heightsList = null;
            MetadataJson_SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{')
            {
                error = "Expected group object.";
                return false;
            }

            i++;
            while (true)
            {
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                    group.tags = tagsList.Count > 0 ? tagsList.ToArray() : Array.Empty<string>();
                    group.members = membersList.Count > 0 ? membersList.ToArray() : Array.Empty<string>();
                    group.memberRelativeOffsets = offsetsList != null && offsetsList.Count > 0
                        ? offsetsList.ToArray()
                        : null;
                    group.memberBodyHeights = heightsList != null && heightsList.Count > 0
                        ? heightsList.ToArray()
                        : null;
                    return true;
                }

                if (!MetadataJson_TryReadString(json, ref i, out string key, out error))
                    return false;
                MetadataJson_SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':')
                {
                    error = "Invalid group (expected ':').";
                    return false;
                }

                i++;
                MetadataJson_SkipWs(json, ref i);
                if (string.Equals(key, "id", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadString(json, ref i, out string v, out error))
                        return false;
                    group.id = v;
                }
                else if (string.Equals(key, "name", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadString(json, ref i, out string v, out error))
                        return false;
                    group.name = v;
                }
                else if (string.Equals(key, "tags", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadStringArray(json, ref i, tagsList, out error))
                        return false;
                }
                else if (string.Equals(key, "members", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadStringArray(json, ref i, membersList, out error))
                        return false;
                }
                else if (string.Equals(key, "memberRelativeOffsets", StringComparison.Ordinal))
                {
                    offsetsList ??= new List<float[]>();
                    if (!MetadataJson_TryReadVec3ArrayArray(json, ref i, offsetsList, out error))
                        return false;
                }
                else if (string.Equals(key, "memberBodyHeights", StringComparison.Ordinal))
                {
                    heightsList ??= new List<float>();
                    if (!MetadataJson_TryReadFloatArray(json, ref i, heightsList, out error))
                        return false;
                }
                else
                {
                    if (!MetadataJson_TrySkipValue(json, ref i, out error))
                        return false;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    continue;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                    group.tags = tagsList.Count > 0 ? tagsList.ToArray() : Array.Empty<string>();
                    group.members = membersList.Count > 0 ? membersList.ToArray() : Array.Empty<string>();
                    group.memberRelativeOffsets = offsetsList != null && offsetsList.Count > 0
                        ? offsetsList.ToArray()
                        : null;
                    group.memberBodyHeights = heightsList != null && heightsList.Count > 0
                        ? heightsList.ToArray()
                        : null;
                    return true;
                }

                error = "Invalid group object.";
                return false;
            }
        }

        private static bool MetadataJson_TryReadFloatArray(
            string json,
            ref int i,
            List<float> sink,
            out string? error)
        {
            error = null;
            if (i >= json.Length || json[i] != '[')
            {
                error = "Expected '[' for memberBodyHeights.";
                return false;
            }

            i++;
            while (true)
            {
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ']')
                {
                    i++;
                    return true;
                }

                if (!MetadataJson_TryReadNumber(json, ref i, out float num, out error))
                    return false;
                sink.Add(num);

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    continue;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ']')
                {
                    i++;
                    return true;
                }

                error = "Invalid memberBodyHeights array.";
                return false;
            }
        }

        private static bool MetadataJson_TryReadVec3ArrayArray(
            string json,
            ref int i,
            List<float[]> sink,
            out string? error)
        {
            error = null;
            if (i >= json.Length || json[i] != '[')
            {
                error = "Expected '[' for memberRelativeOffsets.";
                return false;
            }

            i++;
            MetadataJson_SkipWs(json, ref i);
            if (i < json.Length && json[i] == ']')
            {
                i++;
                return true;
            }

            while (true)
            {
                if (!MetadataJson_TryReadVec3Array(json, ref i, out float[] vec, out error))
                    return false;
                sink.Add(vec);
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    MetadataJson_SkipWs(json, ref i);
                    continue;
                }

                if (i < json.Length && json[i] == ']')
                {
                    i++;
                    return true;
                }

                error = "Invalid memberRelativeOffsets array.";
                return false;
            }
        }

        private static bool MetadataJson_TryReadVec3Array(
            string json,
            ref int i,
            out float[] vec,
            out string? error)
        {
            vec = new float[3];
            error = null;
            if (i >= json.Length || json[i] != '[')
            {
                error = "Expected '[' for vec3.";
                return false;
            }

            i++;
            for (int c = 0; c < 3; c++)
            {
                MetadataJson_SkipWs(json, ref i);
                if (!MetadataJson_TryReadNumber(json, ref i, out float n, out error))
                    return false;
                vec[c] = n;
                MetadataJson_SkipWs(json, ref i);
                if (c < 2)
                {
                    if (i >= json.Length || json[i] != ',')
                    {
                        error = "Invalid vec3 array.";
                        return false;
                    }

                    i++;
                }
            }

            MetadataJson_SkipWs(json, ref i);
            if (i >= json.Length || json[i] != ']')
            {
                error = "Expected ']' for vec3.";
                return false;
            }

            i++;
            return true;
        }

        private static bool MetadataJson_TryReadNumber(string json, ref int i, out float value, out string? error)
        {
            value = 0f;
            error = null;
            MetadataJson_SkipWs(json, ref i);
            if (i >= json.Length)
            {
                error = "Expected number.";
                return false;
            }

            int start = i;
            if (json[i] == '-')
                i++;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == 'e' || json[i] == 'E' ||
                                       json[i] == '+' || json[i] == '-'))
            {
                i++;
            }

            if (start == i)
            {
                error = "Expected number.";
                return false;
            }

            string token = json.Substring(start, i - start);
            if (!float.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                error = "Invalid number.";
                return false;
            }

            return true;
        }

        private static bool MetadataJson_TryReadItemObject(string json, ref int i, out PoseZipItemJson item, out string? error)
        {
            item = new PoseZipItemJson();
            error = null;
            var tagsList = new List<string>();
            MetadataJson_SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{')
            {
                error = "Expected item object.";
                return false;
            }

            i++;
            while (true)
            {
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                    item.tags = tagsList.Count > 0 ? tagsList.ToArray() : Array.Empty<string>();
                    return true;
                }

                if (!MetadataJson_TryReadString(json, ref i, out string key, out error))
                    return false;
                MetadataJson_SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':')
                {
                    error = "Invalid item (expected ':').";
                    return false;
                }

                i++;
                MetadataJson_SkipWs(json, ref i);
                if (string.Equals(key, "file", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadString(json, ref i, out string v, out error))
                        return false;
                    item.file = v;
                }
                else if (string.Equals(key, "tags", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadStringArray(json, ref i, tagsList, out error))
                        return false;
                }
                else if (string.Equals(key, "favorite", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadBoolLoose(json, ref i, out bool fav, out error))
                        return false;
                    item.favorite = fav;
                }
                else if (string.Equals(key, "lastWriteTimeUtc", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadString(json, ref i, out string v, out error))
                        return false;
                    item.lastWriteTimeUtc = v;
                }
                else if (string.Equals(key, "creationTimeUtc", StringComparison.Ordinal))
                {
                    if (!MetadataJson_TryReadString(json, ref i, out string v, out error))
                        return false;
                    item.creationTimeUtc = v;
                }
                else
                {
                    if (!MetadataJson_TrySkipValue(json, ref i, out error))
                        return false;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    continue;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}')
                {
                    i++;
                    item.tags = tagsList.Count > 0 ? tagsList.ToArray() : Array.Empty<string>();
                    return true;
                }

                error = "Invalid item object.";
                return false;
            }
        }

        private static bool MetadataJson_TryReadStringArray(string json, ref int i, List<string> sink, out string? error)
        {
            error = null;
            if (i >= json.Length || json[i] != '[')
            {
                error = "Expected '[' for tags.";
                return false;
            }

            i++;
            while (true)
            {
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ']')
                {
                    i++;
                    return true;
                }

                if (!MetadataJson_TryReadString(json, ref i, out string s, out error))
                    return false;
                sink.Add(s);
                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                    continue;
                }

                MetadataJson_SkipWs(json, ref i);
                if (i < json.Length && json[i] == ']')
                {
                    i++;
                    return true;
                }

                error = "Invalid tags array.";
                return false;
            }
        }

        private static bool MetadataJson_TryReadBoolLoose(string json, ref int i, out bool value, out string? error)
        {
            value = false;
            error = null;
            MetadataJson_SkipWs(json, ref i);
            if (i >= json.Length)
            {
                error = "Expected boolean.";
                return false;
            }

            if (json[i] == 't' && i + 3 < json.Length &&
                json[i + 1] == 'r' && json[i + 2] == 'u' && json[i + 3] == 'e')
            {
                i += 4;
                value = true;
                return true;
            }

            if (json[i] == 'f' && i + 4 < json.Length &&
                json[i + 1] == 'a' && json[i + 2] == 'l' && json[i + 3] == 's' && json[i + 4] == 'e')
            {
                i += 5;
                value = false;
                return true;
            }

            if (json[i] == '1' && (i + 1 >= json.Length || json[i + 1] == ',' || json[i + 1] == '}' || json[i + 1] == ']' || char.IsWhiteSpace(json[i + 1])))
            {
                i++;
                value = true;
                return true;
            }

            if (json[i] == '0' && (i + 1 >= json.Length || json[i + 1] == ',' || json[i + 1] == '}' || json[i + 1] == ']' || char.IsWhiteSpace(json[i + 1])))
            {
                i++;
                value = false;
                return true;
            }

            error = "Expected boolean.";
            return false;
        }

        /// <summary>Flat <see cref="KindPoses"/> export: single segment under <c>poses/</c>, matching <see cref="PoseDataService.GetUniqueFilePath"/> collision pattern.</summary>
        private static string MakeUniqueFlatZipEntryName(string filePath, HashSet<string> used)
        {
            string name = PoseDataService.SanitizeFileName(Path.GetFileName(filePath));
            if (string.IsNullOrEmpty(name))
                name = "pose.png";
            if (used.Add(name))
                return name;
            string baseName = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrEmpty(baseName))
                baseName = "pose";
            string ext = Path.GetExtension(name);
            for (int c = 1; ; c++)
            {
                string candidate = string.Concat(baseName, "-", c.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), ext);
                if (used.Add(candidate))
                    return candidate;
            }
        }

        /// <summary>No path traversal, no absolute segments, only forward slashes; must stay under sane relative paths.</summary>
        private static bool IsSafeZipInternalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            path = path.Replace('\\', '/').Trim('/');
            if (path.Length == 0 || path.StartsWith("/", StringComparison.Ordinal) || path.IndexOf("//", StringComparison.Ordinal) >= 0)
                return false;
            foreach (string p in path.Split('/'))
            {
                if (string.IsNullOrEmpty(p) || p == "." || p == "..")
                    return false;
                if (p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return false;
            }

            return true;
        }

        private static DateTime ParseIsoOrMin(string? s)
        {
            if (string.IsNullOrEmpty(s)) return DateTime.MinValue;
            if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return DateTime.MinValue;
        }

        /// <summary>Validates a relative path from the pack; no '..', empty segments, or invalid file name chars.</summary>
        public static bool TryValidateTreeRelativePath(string relPathUnix, out string normalizedRelPath, out string? error)
        {
            normalizedRelPath = "";
            error = null;
            if (string.IsNullOrWhiteSpace(relPathUnix))
            {
                error = "Empty relative path.";
                return false;
            }

            relPathUnix = relPathUnix.Replace('\\', '/').Trim('/');
            var parts = relPathUnix.Split('/');
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (string.IsNullOrEmpty(p) || p == "." || p == "..")
                {
                    error = "Invalid path in pack.";
                    return false;
                }

                if (p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = "Invalid characters in path.";
                    return false;
                }

                if (sb.Length > 0) sb.Append('/');
                sb.Append(p);
            }

            normalizedRelPath = sb.ToString();
            return true;
        }

        [Serializable]
        private class LegacyPosePackManifestJson
        {
            public string format = "";
            public int version;
            public string exportedUtc = "";
            public LegacyPosePackEntryJson[] entries = Array.Empty<LegacyPosePackEntryJson>();
        }

        [Serializable]
        private class LegacyPosePackEntryJson
        {
            public string id = "";
            public string fileName = "";
            public string displayName = "";
            public int formatKind;
            public string[] tags = Array.Empty<string>();
            public bool favorite;
            public string lastWriteTimeUtc = "";
            public string creationTimeUtc = "";
        }

        [Serializable]
        private class LegacyTreePackManifestJson
        {
            public string format = "";
            public int version;
            public string exportedUtc = "";
            public string rootFolderName = "";
            public LegacyTreePackEntryJson[] entries = Array.Empty<LegacyTreePackEntryJson>();
        }

        [Serializable]
        private class LegacyTreePackEntryJson
        {
            public string id = "";
            public string relPath = "";
            public string displayName = "";
            public int formatKind;
            public string[] tags = Array.Empty<string>();
            public bool favorite;
            public string lastWriteTimeUtc = "";
            public string creationTimeUtc = "";
        }
    }
}
