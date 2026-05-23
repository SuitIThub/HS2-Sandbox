using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Scans pose <c>.png</c> / <c>.dat</c> files, detects broken layouts (mislabeled extension,
    /// duplicated embedded PNG previews, etc.), and rebuilds a canonical file on demand.
    /// </summary>
    public static class PoseFileRepair
    {
        private const string PoseMagic = "【pose】";
        private const string BackupFolder = "!_AutoBackup";
        private const int MaxUnityStringBytes = 512;
        private const int FilesPerFrame = 4;
        private static readonly byte[] PngHeader = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly byte[] PoseMagicUtf8 = Encoding.UTF8.GetBytes(PoseMagic);

        public enum PoseFileIssue
        {
            None,
            MislabeledDat,
            DuplicatePngPrefix,
            MissingPoseData,
            UnrecoverableLayout
        }

        public sealed class RepairProgress
        {
            public int TotalFiles;
            public int FilesScanned;
            public int FaultyFound;
            public int Repaired;
            public int Broken;
            public int Failed;
            public bool IsRunning;
            public string Phase = "";
        }

        public sealed class FileAnalysis
        {
            public string Path = "";
            public bool IsPng;
            public PoseFileIssue Issue;
            public bool CanRepair;
            public int PoseDataOffset;
            public int ThumbnailOffset;
            public int ThumbnailSize;
            public string? PoseName;
            public string Summary = "";
        }

        public sealed class RepairResult
        {
            public int FilesScanned;
            public int AlreadyOk;
            public int Repaired;
            public int Broken;
            public int Failed;
            public readonly List<string> RepairedRelativePaths = new List<string>();
            public readonly List<string> BrokenRelativePaths = new List<string>();
            public readonly List<string> Errors = new List<string>();
        }

        public static List<string> CollectPoseFiles(string poseRootPath)
        {
            var list = new List<string>();
            string root = Path.GetFullPath(poseRootPath);
            if (!Directory.Exists(root))
                return list;

            foreach (string path in EnumeratePoseFiles(root))
                list.Add(path);
            return list;
        }

        public static IEnumerator RepairLibraryCoroutine(
            string poseRootPath,
            PoseDataService dataService,
            PoseTagDatabase? tagDb,
            PoseGroupDatabase? groupDb,
            RepairProgress progress,
            RepairResult result,
            int filesPerFrame = FilesPerFrame)
        {
            result.Repaired = 0;
            result.AlreadyOk = 0;
            result.Broken = 0;
            result.Failed = 0;
            result.FilesScanned = 0;
            result.RepairedRelativePaths.Clear();
            result.BrokenRelativePaths.Clear();
            result.Errors.Clear();

            progress.IsRunning = true;
            progress.FilesScanned = 0;
            progress.FaultyFound = 0;
            progress.Repaired = 0;
            progress.Broken = 0;
            progress.Failed = 0;
            progress.Phase = "Collecting files…";

            string root = Path.GetFullPath(poseRootPath);
            List<string> files;
            try
            {
                files = CollectPoseFiles(root);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Could not enumerate pose files: {ex.Message}");
                progress.IsRunning = false;
                progress.Phase = "Failed";
                yield break;
            }

            progress.TotalFiles = files.Count;
            progress.Phase = "Scanning and repairing…";
            yield return null;

            int sinceYield = 0;
            for (int i = 0; i < files.Count; i++)
            {
                string path = files[i];
                result.FilesScanned++;
                progress.FilesScanned = result.FilesScanned;

                FileAnalysis analysis = Analyze(path);
                string rel = PoseGroupDatabase.NormalizeMemberPath(GetRelativePath(root, path));

                if (analysis.Issue == PoseFileIssue.None)
                {
                    result.AlreadyOk++;
                }
                else
                {
                    progress.FaultyFound++;
                    if (!analysis.CanRepair)
                    {
                        result.Broken++;
                        progress.Broken = result.Broken;
                        result.BrokenRelativePaths.Add(rel);
                        SandboxServices.Log.LogWarning(
                            $"PoseBrowser: Broken pose file (not repaired): {rel} — {analysis.Summary}");
                    }
                    else if (TryRepairAnalysis(root, analysis, dataService, tagDb, groupDb, out string? newPath, out string? error))
                    {
                        result.Repaired++;
                        progress.Repaired = result.Repaired;
                        result.RepairedRelativePaths.Add(rel);
                        SandboxServices.Log.LogInfo(
                            $"PoseBrowser: Repaired pose file: {rel} — {analysis.Summary}" +
                            (string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase)
                                ? ""
                                : $" → {PoseGroupDatabase.NormalizeMemberPath(GetRelativePath(root, newPath!))}"));
                    }
                    else
                    {
                        result.Failed++;
                        progress.Failed = result.Failed;
                        result.Errors.Add($"{rel}: {error ?? "unknown error"}");
                        SandboxServices.Log.LogWarning(
                            $"PoseBrowser: Failed to repair pose file: {rel} — {error ?? "unknown error"}");
                    }
                }

                sinceYield++;
                if (sinceYield >= filesPerFrame)
                {
                    sinceYield = 0;
                    yield return null;
                }
            }

            if (result.Broken > 0)
            {
                SandboxServices.Log.LogWarning(
                    $"PoseBrowser: {result.Broken} broken pose file(s) could not be repaired (see log above).");
            }

            progress.IsRunning = false;
            progress.Phase = "Complete";
        }

        public static FileAnalysis Analyze(string path)
        {
            var analysis = new FileAnalysis
            {
                Path = path,
                IsPng = string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)
            };

            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                analysis.Issue = PoseFileIssue.UnrecoverableLayout;
                analysis.Summary = $"cannot read file: {ex.Message}";
                return analysis;
            }

            return AnalyzeBytes(path, analysis.IsPng, data);
        }

        internal static FileAnalysis AnalyzeBytes(string path, bool isPng, byte[] data)
        {
            var analysis = new FileAnalysis
            {
                Path = path,
                IsPng = isPng
            };

            if (data.Length == 0)
            {
                analysis.Issue = PoseFileIssue.UnrecoverableLayout;
                analysis.Summary = "empty file";
                return analysis;
            }

            if (TryReadPoseHeaderAt(data, 0, out string? nameAtZero))
            {
                analysis.PoseDataOffset = 0;
                analysis.PoseName = nameAtZero;
                analysis.Issue = PoseFileIssue.None;
                analysis.Summary = analysis.IsPng ? "valid .png with unexpected dat-only payload" : "valid .dat";
                return analysis;
            }

            int poseOffset = FindPoseDataOffset(data);
            if (poseOffset < 0)
            {
                analysis.Issue = PoseFileIssue.MissingPoseData;
                analysis.Summary = "no valid pose data block found";
                return analysis;
            }

            analysis.PoseDataOffset = poseOffset;
            TryReadPoseHeaderAt(data, poseOffset, out analysis.PoseName);

            if (TryFindThumbnailBeforePose(data, poseOffset, out int thumbStart, out int thumbSize))
            {
                analysis.ThumbnailOffset = thumbStart;
                analysis.ThumbnailSize = thumbSize;

                if (!analysis.IsPng)
                {
                    analysis.Issue = PoseFileIssue.MislabeledDat;
                    analysis.CanRepair = true;
                    analysis.Summary = thumbStart == 0 && thumbSize == poseOffset
                        ? "mislabeled .dat (PNG + pose data)"
                        : "mislabeled .dat (multiple embedded PNG previews before pose data)";
                    return analysis;
                }

                if (thumbStart == 0 && thumbSize == poseOffset && LoadsWithStandardLayout(path))
                {
                    analysis.Issue = PoseFileIssue.None;
                    analysis.Summary = "valid .png";
                    return analysis;
                }

                analysis.Issue = PoseFileIssue.DuplicatePngPrefix;
                analysis.CanRepair = true;
                analysis.Summary = thumbStart == 0
                    ? "broken .png (extra data before pose block)"
                    : "broken .png (multiple embedded PNG previews before pose data)";
                return analysis;
            }

            if (poseOffset > 0 && !analysis.IsPng)
            {
                analysis.Issue = PoseFileIssue.MislabeledDat;
                analysis.CanRepair = true;
                analysis.Summary = "mislabeled .dat (pose data after unrecognized prefix; recovering pose-only .dat)";
                return analysis;
            }

            analysis.Issue = PoseFileIssue.UnrecoverableLayout;
            analysis.Summary = analysis.IsPng
                ? "broken .png (pose block found but no valid thumbnail region)"
                : "broken .dat (pose block found but layout is not recoverable)";
            return analysis;
        }

        private static bool TryRepairAnalysis(
            string poseRoot,
            FileAnalysis analysis,
            PoseDataService dataService,
            PoseTagDatabase? tagDb,
            PoseGroupDatabase? groupDb,
            out string newPath,
            out string? error)
        {
            newPath = analysis.Path;
            error = null;

            byte[] data;
            try
            {
                data = File.ReadAllBytes(analysis.Path);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            byte[] rebuilt;
            bool outputIsPng;

            if (TryFindThumbnailBeforePose(data, analysis.PoseDataOffset, out int thumbStart, out int thumbSize))
            {
                rebuilt = new byte[thumbSize + (data.Length - analysis.PoseDataOffset)];
                Buffer.BlockCopy(data, thumbStart, rebuilt, 0, thumbSize);
                Buffer.BlockCopy(data, analysis.PoseDataOffset, rebuilt, thumbSize, data.Length - analysis.PoseDataOffset);
                outputIsPng = true;
            }
            else if (!analysis.IsPng && analysis.PoseDataOffset > 0)
            {
                rebuilt = new byte[data.Length - analysis.PoseDataOffset];
                Buffer.BlockCopy(data, analysis.PoseDataOffset, rebuilt, 0, rebuilt.Length);
                outputIsPng = false;
            }
            else
            {
                error = "layout is not repairable";
                return false;
            }

            try
            {
                string oldRel = PoseGroupDatabase.NormalizeMemberPath(GetRelativePath(poseRoot, analysis.Path));
                string targetPath = analysis.Path;

                if (outputIsPng)
                {
                    if (!analysis.IsPng)
                    {
                        targetPath = Path.ChangeExtension(analysis.Path, ".png");
                        targetPath = PoseDataService.GetUniqueFilePath(targetPath);
                    }
                }
                else if (analysis.IsPng)
                {
                    targetPath = Path.ChangeExtension(analysis.Path, ".dat");
                    targetPath = PoseDataService.GetUniqueFilePath(targetPath);
                }

                dataService.BackupFile(analysis.Path);

                string? dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string tempPath = targetPath + ".repair.tmp";
                File.WriteAllBytes(tempPath, rebuilt);
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                File.Move(tempPath, targetPath);

                if (!string.Equals(analysis.Path, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(analysis.Path); }
                    catch { /* best effort */ }

                    var item = new PoseGridItem { FilePath = targetPath };
                    if (!string.IsNullOrEmpty(oldRel))
                    {
                        tagDb?.OnItemPathChanged(oldRel, item);
                        groupDb?.OnItemPathChanged(oldRel, item);
                    }
                }

                newPath = targetPath;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool LoadsWithStandardLayout(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long pngSize = PoseDataService.GetPngSize(fs);
                if (pngSize <= 0)
                    return false;
                int offset = (int)pngSize;
                byte[] tail = ReadFileTailFrom(fs, offset);
                return TryReadPoseHeaderAt(tail, 0, out _);
            }
            catch
            {
                return false;
            }
        }

        private static byte[] ReadFileTailFrom(FileStream fs, int offset)
        {
            long remaining = fs.Length - offset;
            if (remaining <= 0 || remaining > int.MaxValue)
                return Array.Empty<byte>();
            var bytes = new byte[remaining];
            fs.Seek(offset, SeekOrigin.Begin);
            fs.Read(bytes, 0, bytes.Length);
            return bytes;
        }

        private static int FindPoseDataOffset(byte[] data)
        {
            int lastValid = -1;
            int searchLimit = data.Length - PoseMagicUtf8.Length;
            for (int magicIdx = 0; magicIdx <= searchLimit; magicIdx++)
            {
                if (!BytesMatchAt(data, magicIdx, PoseMagicUtf8))
                    continue;

                int prefixStart = Math.Max(0, magicIdx - 5);
                for (int offset = prefixStart; offset <= magicIdx; offset++)
                {
                    if (TryReadPoseHeaderAt(data, offset, out _))
                        lastValid = offset;
                }
            }

            return lastValid;
        }

        private static bool TryFindThumbnailBeforePose(byte[] data, int poseOffset, out int thumbStart, out int thumbSize)
        {
            thumbStart = 0;
            thumbSize = 0;
            if (poseOffset <= 0)
                return false;

            for (int pos = poseOffset - 8; pos >= 0; pos--)
            {
                if (!IsPngHeaderAt(data, pos))
                    continue;

                long size = GetPngSize(data, pos);
                if (size <= 0)
                    continue;
                if (pos + size != poseOffset)
                    continue;

                thumbStart = pos;
                thumbSize = (int)size;
                return true;
            }

            return false;
        }

        private static bool TryReadPoseHeaderAt(byte[] data, int offset, out string? poseName)
        {
            poseName = null;
            if (offset < 0 || offset >= data.Length)
                return false;

            if (!TryReadUnityStringAt(data, offset, out string? marker, out int afterMarker))
                return false;
            if (!string.Equals(marker, PoseMagic, StringComparison.Ordinal))
                return false;
            if (afterMarker + 8 > data.Length)
                return false;

            int afterInts = afterMarker + 8;
            if (!TryReadUnityStringAt(data, afterInts, out poseName, out _))
                return false;

            return !string.IsNullOrEmpty(poseName);
        }

        private static bool TryReadUnityStringAt(byte[] data, int offset, out string? value, out int nextOffset)
        {
            value = null;
            nextOffset = offset;
            if (!TryRead7BitEncodedLength(data, offset, out int length, out int afterLength))
                return false;
            if (length == 0)
            {
                value = string.Empty;
                nextOffset = afterLength;
                return true;
            }

            if (afterLength + length > data.Length)
                return false;

            try
            {
                value = Encoding.UTF8.GetString(data, afterLength, length);
            }
            catch
            {
                return false;
            }

            nextOffset = afterLength + length;
            return true;
        }

        private static bool TryRead7BitEncodedLength(byte[] data, int offset, out int length, out int nextOffset)
        {
            length = 0;
            nextOffset = offset;
            int shift = 0;
            int bytesRead = 0;
            while (offset + bytesRead < data.Length && bytesRead < 5)
            {
                byte b = data[offset + bytesRead];
                bytesRead++;
                length |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    if (length < 0 || length > MaxUnityStringBytes)
                        return false;
                    nextOffset = offset + bytesRead;
                    return true;
                }

                shift += 7;
                if (shift > 28)
                    return false;
            }

            return false;
        }

        private static bool BytesMatchAt(byte[] data, int offset, byte[] pattern)
        {
            if (offset < 0 || offset + pattern.Length > data.Length)
                return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i])
                    return false;
            }

            return true;
        }

        private static bool IsPngHeaderAt(byte[] data, int offset)
        {
            return BytesMatchAt(data, offset, PngHeader);
        }

        private static long GetPngSize(byte[] data, int start)
        {
            using var ms = new MemoryStream(data, start, data.Length - start, writable: false);
            return PoseDataService.GetPngSize(ms);
        }

        private static IEnumerable<string> EnumeratePoseFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                IEnumerable<string> files;
                string[] dirs;
                try
                {
                    files = Directory.EnumerateFiles(dir)
                        .Where(f =>
                        {
                            string ext = Path.GetExtension(f);
                            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                                || ext.Equals(".dat", StringComparison.OrdinalIgnoreCase);
                        })
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                    dirs = Directory.GetDirectories(dir);
                }
                catch
                {
                    continue;
                }

                foreach (string file in files)
                    yield return file;

                foreach (string sub in dirs.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(Path.GetFileName(sub), BackupFolder, StringComparison.OrdinalIgnoreCase))
                        continue;
                    stack.Push(sub);
                }
            }
        }

        private static string GetRelativePath(string rootPath, string filePath)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(filePath);
            if (full.Length >= root.Length && full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                int i = root.Length;
                if (i < full.Length && (full[i] == Path.DirectorySeparatorChar || full[i] == Path.AltDirectorySeparatorChar))
                    i++;
                return full.Substring(i);
            }

            return full;
        }
    }
}
