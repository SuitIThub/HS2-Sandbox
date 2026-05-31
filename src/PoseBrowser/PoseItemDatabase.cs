using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Pose-associated workspace items keyed by library-relative pose path (extends pose metadata, not pose binaries).</summary>
    public sealed class PoseItemDatabase
    {
        private const int FileVersion = 5;
        private const string TsvHeader = "HS2SANDBOX_POSE_ITEMS\t5";
        private const int ColumnCountV1 = 20;
        private const int ColumnCountV2 = 24;
        private const int ColumnCountV3 = 27;
        private const int ColumnCountV4 = 28;
        private const int ColumnCount = 36;

        private readonly string _poseRoot;
        private readonly string _storagePath;
        private readonly Dictionary<string, List<PoseAssociatedItemRecord>> _byPosePath =
            new Dictionary<string, List<PoseAssociatedItemRecord>>(StringComparer.OrdinalIgnoreCase);

        public PoseItemDatabase(string poseRootPath)
        {
            _poseRoot = Path.GetFullPath(poseRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dir = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox");
            _storagePath = Path.Combine(dir, "pose_items.tsv");
            LoadFromDisk();
        }

        public void ForceSave() => SaveToDisk();

        public IReadOnlyList<PoseAssociatedItemRecord> GetItems(PoseGridItem pose)
        {
            string key = GetKey(pose);
            if (string.IsNullOrEmpty(key)) return Array.Empty<PoseAssociatedItemRecord>();
            return _byPosePath.TryGetValue(key, out var list)
                ? list
                : Array.Empty<PoseAssociatedItemRecord>();
        }

        public void SetItems(PoseGridItem pose, IReadOnlyList<PoseAssociatedItemRecord> records)
        {
            string key = GetKey(pose);
            if (string.IsNullOrEmpty(key)) return;

            if (records == null || records.Count == 0)
            {
                _byPosePath.Remove(key);
            }
            else
            {
                _byPosePath[key] = records.Select(CloneRecord).ToList();
            }

            SaveToDisk();
        }

        public void AddItems(PoseGridItem pose, IEnumerable<PoseAssociatedItemRecord> toAdd)
        {
            string key = GetKey(pose);
            if (string.IsNullOrEmpty(key)) return;

            if (!_byPosePath.TryGetValue(key, out var list))
            {
                list = new List<PoseAssociatedItemRecord>();
                _byPosePath[key] = list;
            }

            foreach (var r in toAdd)
                list.Add(CloneRecord(r));

            SaveToDisk();
        }

        public void RemoveItemAt(PoseGridItem pose, int index)
        {
            string key = GetKey(pose);
            if (string.IsNullOrEmpty(key) || !_byPosePath.TryGetValue(key, out var list))
                return;
            if (index < 0 || index >= list.Count)
                return;

            list.RemoveAt(index);
            if (list.Count == 0)
                _byPosePath.Remove(key);
            SaveToDisk();
        }

        public bool TrySetItemDisplayNameAt(PoseGridItem pose, int index, string displayName)
        {
            string trimmed = displayName?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed))
                return false;

            string key = GetKey(pose);
            if (string.IsNullOrEmpty(key) || !_byPosePath.TryGetValue(key, out var list))
                return false;
            if (index < 0 || index >= list.Count)
                return false;

            list[index].DisplayName = trimmed;
            SaveToDisk();
            return true;
        }

        public void OnItemPathChanged(string oldRelPath, PoseGridItem item)
        {
            string oldK = NormalizeStorageKey(oldRelPath);
            if (!_byPosePath.TryGetValue(oldK, out var list))
                return;

            _byPosePath.Remove(oldK);
            _byPosePath[GetKey(item)] = list;
            SaveToDisk();
        }

        public void RemovePoseKey(string relPath)
        {
            string key = NormalizeStorageKey(relPath);
            if (_byPosePath.Remove(key))
                SaveToDisk();
        }

        public void CopyItemsFromTo(PoseGridItem source, PoseGridItem dest)
        {
            string srcKey = GetKey(source);
            if (string.IsNullOrEmpty(srcKey) || !_byPosePath.TryGetValue(srcKey, out var list))
                return;

            string destKey = GetKey(dest);
            if (string.IsNullOrEmpty(destKey)) return;

            _byPosePath[destKey] = list.Select(CloneRecord).ToList();
            SaveToDisk();
        }

        private static PoseAssociatedItemRecord CloneRecord(PoseAssociatedItemRecord r) => new PoseAssociatedItemRecord
        {
            ItemGroup = r.ItemGroup,
            ItemCategory = r.ItemCategory,
            ItemNo = r.ItemNo,
            ItemKind = r.ItemKind,
            ItemKinds = r.ItemKinds?.ToArray() ?? Array.Empty<int>(),
            ItemInfoBlob = r.ItemInfoBlob != null ? (byte[])r.ItemInfoBlob.Clone() : null,
            ItemInfoVersion = r.ItemInfoVersion,
            BundlePath = r.BundlePath,
            AssetName = r.AssetName,
            Manifest = r.Manifest,
            DisplayName = r.DisplayName,
            LocalPosition = r.LocalPosition,
            LocalRotation = r.LocalRotation,
            ItemScale = r.ItemScale,
            ParentObjectName = string.IsNullOrWhiteSpace(r.ParentObjectName) ? null : r.ParentObjectName.Trim(),
            ParentTreePath = string.IsNullOrWhiteSpace(r.ParentTreePath) ? null : r.ParentTreePath.Trim(),
            SavedAnchorBodyHeight = r.SavedAnchorBodyHeight,
            SavedAnchorObjectScale = r.SavedAnchorObjectScale,
            HasAttachChangeAmount = r.HasAttachChangeAmount,
            AttachChangePosition = r.AttachChangePosition,
            AttachChangeRotation = r.AttachChangeRotation
        };

        private void SaveToDisk()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tempPath = _storagePath + ".tmp";
                using (var sw = new StreamWriter(tempPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    sw.WriteLine(TsvHeader);
                    foreach (var kvp in _byPosePath.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        foreach (var r in kvp.Value)
                            WriteRecordLine(sw, kvp.Key, r);
                    }
                }

                if (File.Exists(_storagePath))
                    File.Replace(tempPath, _storagePath, null);
                else
                    File.Move(tempPath, _storagePath);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to save pose_items.tsv: {ex.Message}");
            }
        }

        private static void WriteRecordLine(TextWriter sw, string poseKey, PoseAssociatedItemRecord r)
        {
            var inv = CultureInfo.InvariantCulture;
            string parent = r.ParentObjectName ?? "";
            sw.Write(poseKey);
            sw.Write('\t');
            sw.Write(r.ItemGroup.ToString(inv));
            sw.Write('\t');
            sw.Write(r.ItemCategory.ToString(inv));
            sw.Write('\t');
            sw.Write(r.ItemNo.ToString(inv));
            sw.Write('\t');
            sw.Write(EscapeCell(r.DisplayName));
            sw.Write('\t');
            sw.Write(r.LocalPosition.x.ToString(inv));
            sw.Write('\t');
            sw.Write(r.LocalPosition.y.ToString(inv));
            sw.Write('\t');
            sw.Write(r.LocalPosition.z.ToString(inv));
            sw.Write('\t');
            sw.Write(r.LocalRotation.x.ToString(inv));
            sw.Write('\t');
            sw.Write(r.LocalRotation.y.ToString(inv));
            sw.Write('\t');
            sw.Write(r.LocalRotation.z.ToString(inv));
            sw.Write('\t');
            sw.Write(r.LocalRotation.w.ToString(inv));
            sw.Write('\t');
            sw.Write(r.ItemScale.x.ToString(inv));
            sw.Write('\t');
            sw.Write(r.ItemScale.y.ToString(inv));
            sw.Write('\t');
            sw.Write(r.ItemScale.z.ToString(inv));
            sw.Write('\t');
            sw.Write(EscapeCell(parent));
            sw.Write('\t');
            sw.Write(r.SavedAnchorBodyHeight.ToString(inv));
            sw.Write('\t');
            sw.Write(r.SavedAnchorObjectScale.x.ToString(inv));
            sw.Write('\t');
            sw.Write(r.SavedAnchorObjectScale.y.ToString(inv));
            sw.Write('\t');
            sw.Write(r.SavedAnchorObjectScale.z.ToString(inv));
            sw.Write('\t');
            sw.Write(r.ItemKind.ToString(inv));
            sw.Write('\t');
            sw.Write(EscapeCell(PoseItemInfoSnapshot.FormatKinds(r.ItemKinds)));
            sw.Write('\t');
            sw.Write(r.ItemInfoBlob != null && r.ItemInfoBlob.Length > 0
                ? Convert.ToBase64String(r.ItemInfoBlob)
                : "");
            sw.Write('\t');
            sw.Write(EscapeCell(r.ItemInfoVersion ?? ""));
            sw.Write('\t');
            sw.Write(EscapeCell(r.BundlePath));
            sw.Write('\t');
            sw.Write(EscapeCell(r.AssetName));
            sw.Write('\t');
            sw.Write(EscapeCell(r.Manifest));
            sw.Write('\t');
            sw.Write(EscapeCell(r.ParentTreePath ?? ""));
            sw.Write('\t');
            sw.Write(r.HasAttachChangeAmount ? "1" : "0");
            sw.Write('\t');
            sw.Write(r.AttachChangePosition.x.ToString(inv));
            sw.Write('\t');
            sw.Write(r.AttachChangePosition.y.ToString(inv));
            sw.Write('\t');
            sw.Write(r.AttachChangePosition.z.ToString(inv));
            sw.Write('\t');
            sw.Write(r.AttachChangeRotation.x.ToString(inv));
            sw.Write('\t');
            sw.Write(r.AttachChangeRotation.y.ToString(inv));
            sw.Write('\t');
            sw.Write(r.AttachChangeRotation.z.ToString(inv));
            sw.Write('\t');
            sw.Write(r.AttachChangeRotation.w.ToString(inv));
            sw.WriteLine();
        }

        private static string EscapeCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private void LoadFromDisk()
        {
            if (!File.Exists(_storagePath))
                return;

            try
            {
                TryLoadTsv(_storagePath, out int count);
                if (count > 0)
                    SandboxServices.Log.LogInfo($"PoseBrowser: Loaded {count} pose-item association(s) from pose_items.tsv");
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to load pose_items.tsv: {ex.Message}");
            }
        }

        private bool TryLoadTsv(string path, out int imported)
        {
            imported = 0;
            var temp = new Dictionary<string, List<PoseAssociatedItemRecord>>(StringComparer.OrdinalIgnoreCase);
            var inv = CultureInfo.InvariantCulture;

            foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
            {
                string line = rawLine.TrimStart('\uFEFF').TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("HS2SANDBOX_POSE_ITEMS", StringComparison.Ordinal)) continue;

                string[] cols = line.Split('\t');
                if (cols.Length < ColumnCountV1) continue;

                string poseKey = NormalizeStorageKey(cols[0]);
                if (string.IsNullOrEmpty(poseKey)) continue;

                if (!int.TryParse(cols[1], NumberStyles.Integer, inv, out int group) ||
                    !int.TryParse(cols[2], NumberStyles.Integer, inv, out int category) ||
                    !int.TryParse(cols[3], NumberStyles.Integer, inv, out int itemNo))
                    continue;

                var record = new PoseAssociatedItemRecord
                {
                    ItemGroup = group,
                    ItemCategory = category,
                    ItemNo = itemNo,
                    DisplayName = cols[4],
                    LocalPosition = ParseVec3(cols, 5, inv),
                    LocalRotation = ParseQuat(cols, 8, inv),
                    ItemScale = ParseVec3(cols, 12, inv),
                    ParentObjectName = string.IsNullOrWhiteSpace(cols[15]) ? null : cols[15].Trim(),
                    SavedAnchorBodyHeight = ParseFloat(cols, 16, inv),
                    SavedAnchorObjectScale = ParseVec3(cols, 17, inv)
                };

                if (cols.Length >= ColumnCountV2)
                {
                    int.TryParse(cols[20], NumberStyles.Integer, inv, out int kind);
                    record.ItemKind = kind;
                    record.ItemKinds = PoseItemInfoSnapshot.ParseKinds(cols[21]);
                    record.ItemInfoBlob = TryParseBlob(cols[22]);
                    record.ItemInfoVersion = string.IsNullOrWhiteSpace(cols[23]) ? null : cols[23].Trim();
                }

                if (cols.Length >= ColumnCountV3)
                {
                    record.BundlePath = cols[24];
                    record.AssetName = cols[25];
                    record.Manifest = cols[26];
                }

                if (cols.Length >= ColumnCountV4)
                    record.ParentTreePath = string.IsNullOrWhiteSpace(cols[27]) ? null : cols[27].Trim();

                if (cols.Length >= ColumnCount)
                {
                    record.HasAttachChangeAmount = cols[28] == "1";
                    record.AttachChangePosition = ParseVec3(cols, 29, inv);
                    record.AttachChangeRotation = ParseQuat(cols, 32, inv);
                }

                if (cols.Length < ColumnCountV4 && (record.ItemGroup != 0 || record.ItemCategory != 0 || record.ItemNo != 0))
                {
                    PoseItemCatalogResolve.TryGetCatalogPaths(
                        record.ItemGroup,
                        record.ItemCategory,
                        record.ItemNo,
                        out string bundle,
                        out string file,
                        out string manifest);
                    record.BundlePath = bundle;
                    record.AssetName = file;
                    record.Manifest = manifest;
                }

                if (!temp.TryGetValue(poseKey, out var list))
                {
                    list = new List<PoseAssociatedItemRecord>();
                    temp[poseKey] = list;
                }

                list.Add(record);
                imported++;
            }

            _byPosePath.Clear();
            foreach (var kvp in temp)
                _byPosePath[kvp.Key] = kvp.Value;
            return true;
        }

        private static Vector3 ParseVec3(string[] cols, int start, IFormatProvider inv) => new Vector3(
            ParseFloat(cols, start, inv),
            ParseFloat(cols, start + 1, inv),
            ParseFloat(cols, start + 2, inv));

        private static Quaternion ParseQuat(string[] cols, int start, IFormatProvider inv)
        {
            var q = new Quaternion(
                ParseFloat(cols, start, inv),
                ParseFloat(cols, start + 1, inv),
                ParseFloat(cols, start + 2, inv),
                ParseFloat(cols, start + 3, inv));
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 1e-8f) return Quaternion.identity;
            if (Mathf.Abs(mag - 1f) > 1e-4f)
                q = new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
            return q;
        }

        private static byte[]? TryParseBlob(string cell)
        {
            if (string.IsNullOrWhiteSpace(cell))
                return null;
            try
            {
                return Convert.FromBase64String(cell.Trim());
            }
            catch
            {
                return null;
            }
        }

        private static float ParseFloat(string[] cols, int index, IFormatProvider inv)
        {
            if (index >= cols.Length) return 0f;
            float.TryParse(cols[index], NumberStyles.Float, inv, out float v);
            return v;
        }

        private static string NormalizeStorageKey(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return relPath;
            return relPath.Replace('\\', '/').TrimStart('/');
        }

        private string GetKey(PoseGridItem item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.FilePath)) return "";
                string full = Path.GetFullPath(item.FilePath);
                if (full.Length >= _poseRoot.Length &&
                    full.StartsWith(_poseRoot, StringComparison.OrdinalIgnoreCase))
                {
                    int i = _poseRoot.Length;
                    if (i < full.Length && (full[i] == Path.DirectorySeparatorChar || full[i] == Path.AltDirectorySeparatorChar))
                        i++;
                    return NormalizeStorageKey(full.Substring(i));
                }

                return NormalizeStorageKey(full);
            }
            catch
            {
                return NormalizeStorageKey(item.FilePath);
            }
        }
    }
}
