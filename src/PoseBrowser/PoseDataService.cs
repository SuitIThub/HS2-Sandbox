using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
#if HS2 || AI
using AIChara;
#endif
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PoseDataService
    {
        private const string PoseMagic = "【pose】";
        private const int PoseVersion = 101;
        private const int ThumbnailSize = 256;
        internal const int MaxThumbnailDisplaySize = ThumbnailSize;
        private const string BackupFolder = "!_AutoBackup";

        private static readonly byte[] PngHeader = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly int IendMagic = BitConverter.ToInt32(new byte[] { 0x49, 0x45, 0x4E, 0x44 }, 0);

        private Action<string, OCIChar>? _setExHookValue;

        public string PoseRootPath { get; }

        public PoseDataService(string poseRootPath)
        {
            PoseRootPath = poseRootPath;
            InitExtendedSaveHook();
        }

        private void InitExtendedSaveHook()
        {
            try
            {
                var extSaveType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                    .FirstOrDefault(t => t.FullName == "ExtensibleSaveFormat.ExtendedSave");

                if (extSaveType == null) return;

                var hooksType = extSaveType.GetNestedType("Hooks", BindingFlags.NonPublic | BindingFlags.Public);
                if (hooksType == null) return;

                var poseCharField = hooksType.GetField("PoseChar", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var poseNameField = hooksType.GetField("PoseName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

                if (poseCharField != null && poseNameField != null)
                {
                    _setExHookValue = (name, chara) =>
                    {
                        poseNameField.SetValue(null, name);
                        poseCharField.SetValue(null, chara);
                    };
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Failed to init ExtendedSave hook: {ex.Message}");
            }
        }

        public List<PoseGridItem> LoadPosesFromFolder(string folderPath)
        {
            var items = new List<PoseGridItem>();
            if (!Directory.Exists(folderPath)) return items;

            var dirInfo = new DirectoryInfo(folderPath);
            foreach (var file in dirInfo.GetFiles())
            {
                var item = TryLoadPoseItem(file);
                if (item != null)
                    items.Add(item);
            }

            return items;
        }

        /// <summary>Loads all pose files under <paramref name="rootPath"/> recursively (excludes backup folder).</summary>
        public List<PoseGridItem> LoadPosesRecursive(string rootPath)
        {
            var items = new List<PoseGridItem>();
            if (!Directory.Exists(rootPath)) return items;
            CollectPosesRecursive(rootPath, items);
            return items;
        }

        private void CollectPosesRecursive(string dir, List<PoseGridItem> items)
        {
            try
            {
                foreach (var file in new DirectoryInfo(dir).GetFiles())
                {
                    var item = TryLoadPoseItem(file);
                    if (item != null)
                        items.Add(item);
                }

                foreach (var sub in new DirectoryInfo(dir).GetDirectories()
                             .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(sub.Name, BackupFolder, StringComparison.OrdinalIgnoreCase))
                        continue;
                    CollectPosesRecursive(sub.FullName, items);
                }
            }
            catch
            {
                // ignore inaccessible directories
            }
        }

        /// <summary>Yields between file batches so the index can be built without blocking the main thread.</summary>
        public IEnumerator LoadPosesRecursiveCoroutine(string rootPath, List<PoseGridItem> target, int filesPerYield = 8)
        {
            target.Clear();
            if (!Directory.Exists(rootPath)) yield break;

            var dirStack = new Stack<DirectoryInfo>();
            dirStack.Push(new DirectoryInfo(rootPath));
            int filesThisFrame = 0;

            while (dirStack.Count > 0)
            {
                DirectoryInfo dir = dirStack.Pop();
                FileInfo[] files;
                DirectoryInfo[] subs;
                try
                {
                    files = dir.GetFiles();
                    subs = dir.GetDirectories();
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    var item = TryLoadPoseItem(file);
                    if (item != null)
                        target.Add(item);
                    filesThisFrame++;
                    if (filesThisFrame >= filesPerYield)
                    {
                        filesThisFrame = 0;
                        yield return null;
                    }
                }

                foreach (var sub in subs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(sub.Name, BackupFolder, StringComparison.OrdinalIgnoreCase))
                        continue;
                    dirStack.Push(sub);
                }
            }
        }

        public static IEnumerable<string> ListSubfoldersRecursive(string rootPath, string backupFolderName)
        {
            if (!Directory.Exists(rootPath)) yield break;
            foreach (var sub in new DirectoryInfo(rootPath).GetDirectories()
                         .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(sub.Name, backupFolderName, StringComparison.OrdinalIgnoreCase))
                    continue;
                yield return sub.FullName;
                foreach (var deeper in ListSubfoldersRecursive(sub.FullName, backupFolderName))
                    yield return deeper;
            }
        }

        public bool RenamePoseDisplayNameAndOptionalFile(
            PoseGridItem item,
            string newDisplayName,
            bool renameFileToMatch,
            PoseTagDatabase tagDb,
            PoseGroupDatabase? groupDb = null,
            PoseItemDatabase? itemDb = null)
        {
            string trimmed = newDisplayName?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed)) return false;

            string oldRel = item.RelativePath(PoseRootPath);
            string oldPath = item.FilePath;
            string? dir = Path.GetDirectoryName(oldPath);
            if (string.IsNullOrEmpty(dir)) return false;

            string ext = Path.GetExtension(oldPath);
            string targetPath = oldPath;
            if (renameFileToMatch)
            {
                string baseName = SanitizeFileName(trimmed);
                if (string.IsNullOrEmpty(baseName)) baseName = "pose";
                targetPath = Path.Combine(dir, baseName + ext);
                targetPath = GetUniqueFilePath(targetPath);
            }

            if (!RebuildPoseDataWithNewName(item, trimmed, targetPath))
                return false;

            if (!string.Equals(oldPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                tagDb.OnItemPathChanged(oldRel, item);
                groupDb?.OnItemPathChanged(oldRel, item);
                itemDb?.OnItemPathChanged(oldRel, item);
                try { File.Delete(oldPath); }
                catch { /* best effort */ }
            }

            tagDb.ApplyToItem(item);
            return true;
        }

        public static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (Array.IndexOf(invalid, c) >= 0) continue;
                if (c < 32) continue;
                sb.Append(c);
            }
            return sb.ToString().TrimEnd(' ', '.');
        }

        private bool RebuildPoseDataWithNewName(PoseGridItem item, string newPoseName, string targetPath)
        {
            try
            {
                byte[]? pngBytes = null;
                if (item.IsPng)
                {
                    pngBytes = LoadPngBytes(item.FilePath);
                    if (pngBytes == null) return false;
                }

                byte[] poseSection;
                using (var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Seek(item.DataPosition, SeekOrigin.Begin);
                    poseSection = new byte[fs.Length - fs.Position];
                    int read = fs.Read(poseSection, 0, poseSection.Length);
                    if (read != poseSection.Length) return false;
                }

                using var ms = new MemoryStream(poseSection, writable: false);
                using var br = new BinaryReader(ms);
                string marker = br.ReadString();
                if (string.Compare(marker, PoseMagic, StringComparison.Ordinal) != 0)
                    return false;
                int ver = br.ReadInt32();
                int sex = br.ReadInt32();
                br.ReadString();
                long afterName = ms.Position;
                int restLen = (int)(ms.Length - afterName);
                byte[] rest = br.ReadBytes(restLen);

                byte[] newTail;
                using (var outMs = new MemoryStream())
                using (var bw = new BinaryWriter(outMs))
                {
                    bw.Write(PoseMagic);
                    bw.Write(ver);
                    bw.Write(sex);
                    bw.Write(newPoseName);
                    bw.Write(rest);
                    newTail = outMs.ToArray();
                }

                BackupFile(item.FilePath);

                if (item.IsPng && pngBytes != null)
                {
                    using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    using var bw = new BinaryWriter(fs);
                    bw.Write(pngBytes);
                    bw.Write(newTail);
                    item.DataPosition = pngBytes.Length;
                }
                else
                {
                    using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    using var bw = new BinaryWriter(fs);
                    bw.Write(newTail);
                    item.DataPosition = 0;
                }

                var fi = new FileInfo(targetPath);
                item.FilePath = fi.FullName;
                item.DisplayName = newPoseName;
                item.IsPng = string.Equals(fi.Extension, ".png", StringComparison.OrdinalIgnoreCase);
                item.LastWriteTime = fi.LastWriteTime;
                item.CreationTimeUtc = fi.CreationTimeUtc;
                if (item.Thumbnail != null)
                {
                    UnityEngine.Object.Destroy(item.Thumbnail);
                    item.Thumbnail = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Rename pose failed: {ex.Message}");
                return false;
            }
        }

        public bool MovePoseFileToFolder(PoseGridItem item, string destFolder, PoseTagDatabase tagDb)
        {
            if (!Directory.Exists(destFolder)) return false;
            string oldRel = item.RelativePath(PoseRootPath);
            string name = Path.GetFileName(item.FilePath);
            string destPath = GetUniqueFilePath(Path.Combine(destFolder, name));
            try
            {
                File.Move(item.FilePath, destPath);
            }
            catch
            {
                try
                {
                    File.Copy(item.FilePath, destPath, false);
                    File.Delete(item.FilePath);
                }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogError($"PoseBrowser: Move failed: {ex.Message}");
                    return false;
                }
            }

            item.FilePath = Path.GetFullPath(destPath);
            if (item.IsPng)
            {
                try
                {
                    using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long pngSize = GetPngSize(fs);
                    fs.Seek(pngSize, SeekOrigin.Current);
                    item.DataPosition = (int)fs.Position;
                }
                catch { }
            }

            var fi = new FileInfo(item.FilePath);
            item.LastWriteTime = fi.LastWriteTime;
            item.CreationTimeUtc = fi.CreationTimeUtc;
            tagDb.OnItemPathChanged(oldRel, item);
            tagDb.ApplyToItem(item);
            return true;
        }

        public PoseGridItem? CopyPoseFileToFolder(PoseGridItem item, string destFolder, PoseTagDatabase tagDb)
        {
            if (!Directory.Exists(destFolder)) return null;
            string destPath = GetUniqueFilePath(Path.Combine(destFolder, Path.GetFileName(item.FilePath)));
            try
            {
                File.Copy(item.FilePath, destPath, false);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Copy failed: {ex.Message}");
                return null;
            }

            var copy = TryLoadPoseItem(new FileInfo(destPath));
            if (copy == null) return null;
            tagDb.CopyTagsFromTo(item, copy);
            tagDb.ApplyToItem(copy);
            return copy;
        }

        public bool RenameFolder(
            string folderFullPath,
            string newName,
            PoseTagDatabase tagDb,
            out string? resultingFullPath,
            PoseGroupDatabase? groupDb = null)
        {
            resultingFullPath = null;
            string trimmed = newName?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed)) return false;
            trimmed = SanitizeFileName(trimmed);
            if (string.IsNullOrEmpty(trimmed)) return false;

            if (!folderFullPath.StartsWith(Path.GetFullPath(PoseRootPath), StringComparison.OrdinalIgnoreCase))
                return false;

            string? parent = Path.GetDirectoryName(folderFullPath);
            if (string.IsNullOrEmpty(parent)) return false;

            string newFull = Path.GetFullPath(Path.Combine(parent, trimmed));
            string oldNorm = Path.GetFullPath(folderFullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (oldNorm.Equals(newFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                resultingFullPath = Path.GetFullPath(folderFullPath);
                return true;
            }

            if (Directory.Exists(newFull)) return false;

            string oldRel = GetRelativePath(PoseRootPath, folderFullPath);
            Directory.Move(folderFullPath, newFull);
            string newRel = GetRelativePath(PoseRootPath, newFull);
            tagDb.OnFolderPathRenamed(oldRel, newRel);
            groupDb?.OnFolderPathRenamed(oldRel, newRel);
            resultingFullPath = newFull;
            return true;
        }

        public static bool IsPoseFolderEmpty(string folderFullPath)
        {
            try
            {
                if (!Directory.Exists(folderFullPath)) return false;
                if (Directory.GetFiles(folderFullPath).Length > 0) return false;
                if (Directory.GetDirectories(folderFullPath).Length > 0) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryCreateChildFolder(string parentFolderFullPath, string childName, out string? createdFullPath, out string? error)
        {
            createdFullPath = null;
            error = null;
            string trimmed = childName?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "Name is empty.";
                return false;
            }

            trimmed = SanitizeFileName(trimmed);
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "Invalid name.";
                return false;
            }

            string root = Path.GetFullPath(PoseRootPath);
            string parent = Path.GetFullPath(parentFolderFullPath);
            if (!parent.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                error = "Invalid parent folder.";
                return false;
            }

            string path = Path.GetFullPath(Path.Combine(parent, trimmed));
            if (Directory.Exists(path))
            {
                error = "Folder already exists.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(path);
                createdFullPath = path;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryDeleteEmptyFolder(string folderFullPath, PoseTagDatabase tagDb, out string? error)
        {
            error = null;
            string root = Path.GetFullPath(PoseRootPath).TrimEnd('\\', '/');
            string full = Path.GetFullPath(folderFullPath).TrimEnd('\\', '/');
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                error = "Cannot delete the pose root folder.";
                return false;
            }

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                error = "Invalid folder.";
                return false;
            }

            if (!IsPoseFolderEmpty(folderFullPath))
            {
                error = "Folder must be empty (no files or subfolders).";
                return false;
            }

            try
            {
                string rel = GetRelativePath(PoseRootPath, folderFullPath);
                tagDb.RemoveAllEntriesUnderFolder(rel);
                Directory.Delete(folderFullPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool DeletePoseFiles(IEnumerable<PoseGridItem> items, PoseTagDatabase tagDb)
        {
            bool ok = true;
            foreach (var item in items)
            {
                try
                {
                    if (!File.Exists(item.FilePath))
                        continue;
                    BackupFile(item.FilePath);
                    tagDb.RemoveEntry(item);
                    File.Delete(item.FilePath);
                }
                catch (Exception ex)
                {
                    ok = false;
                    SandboxServices.Log.LogError($"PoseBrowser: Delete pose failed: {ex.Message}");
                }
            }
            return ok;
        }

        public PoseGridItem? TryLoadPoseItem(FileInfo file) => TryLoadPoseItemCore(file);

        private PoseGridItem? TryLoadPoseItemCore(FileInfo file)
        {
            string ext = file.Extension.ToLowerInvariant();
            if (ext != ".png" && ext != ".dat") return null;

            bool isPng = ext == ".png";
            int dataPos = 0;
            string? name;

            try
            {
                if (isPng)
                    name = LoadPoseName(file.FullName, out dataPos);
                else
                    name = LoadPoseName(file.FullName);

                if (name == null) return null;

                return new PoseGridItem
                {
                    FilePath = file.FullName,
                    DisplayName = name,
                    IsPng = isPng,
                    DataPosition = dataPos,
                    LastWriteTime = file.LastWriteTime,
                    CreationTimeUtc = file.CreationTimeUtc
                };
            }
            catch
            {
                return null;
            }
        }

        public static long GetPngSize(Stream st)
        {
            if (st == null) return 0;
            long startPos = st.Position;
            try
            {
                var header = new byte[8];
                st.Read(header, 0, 8);
                for (int i = 0; i < 8; i++)
                {
                    if (header[i] != PngHeader[i])
                    {
                        st.Seek(startPos, SeekOrigin.Begin);
                        return 0;
                    }
                }

                while (true)
                {
                    var lenBytes = new byte[4];
                    st.Read(lenBytes, 0, 4);
                    Array.Reverse(lenBytes);
                    int chunkLen = BitConverter.ToInt32(lenBytes, 0);

                    var typeBytes = new byte[4];
                    st.Read(typeBytes, 0, 4);

                    bool isIend = BitConverter.ToInt32(typeBytes, 0) == IendMagic;

                    if ((long)(chunkLen + 4) > st.Length - st.Position)
                    {
                        st.Seek(startPos, SeekOrigin.Begin);
                        return 0;
                    }
                    st.Seek(chunkLen + 4, SeekOrigin.Current);

                    if (isIend)
                    {
                        long size = st.Position - startPos;
                        st.Seek(startPos, SeekOrigin.Begin);
                        return size;
                    }
                }
            }
            catch
            {
                st.Seek(startPos, SeekOrigin.Begin);
                return 0;
            }
        }

        public static byte[]? LoadPngBytes(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long size = GetPngSize(fs);
            if (size == 0) return null;
            var bytes = new byte[size];
            fs.Read(bytes, 0, (int)size);
            return bytes;
        }

        private string? LoadPoseName(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var br = new BinaryReader(fs);
                return ReadPoseNameFromBinary(br);
            }
            catch
            {
                return null;
            }
        }

        private string? LoadPoseName(string path, out int dataPosition)
        {
            dataPosition = 0;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long pngSize = GetPngSize(fs);
                fs.Seek(pngSize, SeekOrigin.Current);
                dataPosition = (int)fs.Position;
                using var br = new BinaryReader(fs);
                return ReadPoseNameFromBinary(br);
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadPoseNameFromBinary(BinaryReader br)
        {
            try
            {
                string marker = br.ReadString();
                if (string.Compare(marker, PoseMagic, StringComparison.Ordinal) != 0)
                    return null;
                br.ReadInt32(); // version
                br.ReadInt32(); // sex
                return br.ReadString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Characters implied by workspace / gizmo selection. Includes FK/IK bone guides: resolves
        /// <see cref="ChaControl"/> from <see cref="GuideObject.transformTarget"/> without changing tree or gizmo selection.
        /// </summary>
        public IEnumerable<OCIChar> GetSelectedCharacters() => StudioCharacterSelection.GetSelectedCharacters();

        internal static TreeNodeCtrl? TryGetStudioTreeNodeCtrl() => StudioCharacterSelection.TryGetTreeNodeCtrl();

        public static string GetOCICharDisplayName(OCIChar oci) => StudioCharacterSelection.GetDisplayName(oci);

        public IList<string> GetSelectedCharacterDisplayNames()
        {
            try
            {
                return GetSelectedCharacters().Select(GetOCICharDisplayName).ToList();
            }
            catch
            {
                return new string[0];
            }
        }

        public List<OciDicKeyPair> GetSceneCharacters() => StudioCharacterSelection.GetSceneCharacters();

        public static bool TryGetDicKey(OCIChar oci, out int dicKey) =>
            StudioCharacterSelection.TryGetDicKey(oci, out dicKey);

        public static bool IsFemaleCharacter(OCIChar oci) => StudioCharacterSelection.IsFemaleCharacter(oci);

        public static bool IsMaleCharacter(OCIChar oci) => StudioCharacterSelection.IsMaleCharacter(oci);

        /// <summary>Maker body-height slider (0–1 range, <see cref="ChaFileDefine.BodyShapeIdx.Height"/>).</summary>
        public static bool TryGetCharacterBodyHeight(OCIChar oci, out float height)
        {
            height = 0f;
            try
            {
                int idx = (int)ChaFileDefine.BodyShapeIdx.Height;
                if (oci?.charInfo != null)
                {
                    height = oci.charInfo.GetShapeBodyValue(idx);
                    return true;
                }

                var body = oci?.oiCharInfo?.charFile?.custom?.body;
                if (body?.shapeValueBody != null && body.shapeValueBody.Length > idx)
                {
                    height = body.shapeValueBody[idx];
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        /// <summary>Studio object scale from the character guide (<see cref="ChangeAmount.scale"/>).</summary>
        public static bool TryGetCharacterObjectScale(OCIChar oci, out Vector3 scale)
        {
            scale = Vector3.one;
            try
            {
                if (oci?.guideObject?.changeAmount != null)
                {
                    scale = oci.guideObject.changeAmount.scale;
                    return true;
                }

                if (oci?.guideObject?.transformTarget != null)
                {
                    scale = oci.guideObject.transformTarget.localScale;
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        public static bool TryGetCharacterWorldPosition(OCIChar oci, out Vector3 position)
        {
            position = Vector3.zero;
            try
            {
#if KKS || KK
                if (TryGetCharacterGuideWorldPositionFromChangeAmount(oci, out position))
                    return true;
#endif
                if (oci?.guideObject?.transformTarget != null)
                {
                    position = oci.guideObject.transformTarget.position;
                    return true;
                }

                if (oci?.charInfo != null)
                {
                    position = oci.charInfo.transform.position;
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        public static bool TrySetCharacterWorldPosition(OCIChar oci, Vector3 worldPosition)
        {
            if (oci?.guideObject == null)
                return false;

#if KKS || KK
            if (TrySetCharacterGuideWorldPositionFromChangeAmount(oci, worldPosition))
                return true;
#endif
            if (!TryGetCharacterWorldPosition(oci, out Vector3 current))
                return false;

            Vector3 delta = worldPosition - current;
            if (delta.sqrMagnitude < 1e-12f)
                return true;

            try
            {
                oci.guideObject.MoveWorld(delta);
                try
                {
                    oci.guideObject.changeAmount.OnChange();
                }
                catch
                {
                    // ignore — some Studio builds may not expose OnChange
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

#if KKS || KK
        /// <summary>
        /// KKS character guides often disagree with <see cref="GuideObject.transformTarget"/>; use Studio changeAmount space.
        /// </summary>
        private static bool TryGetCharacterGuideWorldPositionFromChangeAmount(OCIChar oci, out Vector3 position)
        {
            position = Vector3.zero;
            try
            {
                GuideObject? guide = oci?.guideObject;
                ChangeAmount? ca = guide?.changeAmount;
                if (ca == null)
                    return false;

                Transform? parent = guide!.parent;
                position = parent != null ? parent.TransformPoint(ca.pos) : ca.pos;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetCharacterGuideWorldPositionFromChangeAmount(OCIChar oci, Vector3 worldPosition)
        {
            try
            {
                GuideObject? guide = oci?.guideObject;
                ChangeAmount? ca = guide?.changeAmount;
                if (ca == null)
                    return false;

                Transform? parent = guide!.parent;
                Vector3 local = parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
                if ((ca.pos - local).sqrMagnitude < 1e-12f)
                    return true;

                ca.pos = local;
                try
                {
                    ca.OnChange();
                }
                catch
                {
                    // ignore
                }

                try
                {
                    guide.CalcPosition();
                }
                catch
                {
                    // ignore
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
#endif

        /// <summary>World rotation from the guide target (falls back to <see cref="ChangeAmount.rot"/>).</summary>
        public static bool TryGetCharacterWorldRotation(OCIChar oci, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            try
            {
                if (oci?.guideObject?.transformTarget != null)
                {
                    rotation = oci.guideObject.transformTarget.rotation;
                    return true;
                }

                if (oci?.guideObject?.changeAmount != null)
                {
                    rotation = Quaternion.Euler(oci.guideObject.changeAmount.rot);
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        public static bool TrySetCharacterWorldRotation(OCIChar oci, Quaternion worldRotation)
        {
            if (oci?.guideObject?.changeAmount == null)
                return false;

            try
            {
                var ca = oci.guideObject.changeAmount;
                Quaternion current = Quaternion.Euler(ca.rot);
                if (Quaternion.Angle(current, worldRotation) < 0.01f)
                    return true;

                ca.rot = worldRotation.eulerAngles;
                try
                {
                    ca.OnChange();
                }
                catch
                {
                    // ignore
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ApplyPose(PoseGridItem item, OCIChar ociChar)
        {
            try
            {
                // Empty instance: ctor(ociChar) copies IK/FK into dicts and Load() throws duplicate-key (HS2/KKS).
                var fileInfo = new PauseCtrl.FileInfo(null);
                using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(item.DataPosition, SeekOrigin.Begin);
                using var br = new BinaryReader(fs);

                string marker = br.ReadString();
                if (string.Compare(marker, PoseMagic, StringComparison.Ordinal) != 0)
                    return false;

                int version = br.ReadInt32();
                br.ReadInt32(); // sex
                br.ReadString(); // name

                _setExHookValue?.Invoke(item.FilePath, ociChar);
                fileInfo.Load(br, version);
#if KKS || KK
                bool hadGuidePos = TryGetCharacterWorldPosition(ociChar, out Vector3 guidePosBefore);
#endif
                fileInfo.Apply(ociChar);
#if KKS || KK
                if (hadGuidePos)
                    RestoreCharacterGuideWorldPositionAfterPoseApply(ociChar, guidePosBefore);
#endif
                MaybeFreezeAnimationSpeedAfterApply(ociChar);
                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to apply pose: {ex.Message}");
                return false;
            }
        }

#if KKS || KK
        /// <summary>
        /// Pose IK/FK apply can shift the character guide on KKS; keep the pre-apply studio layout position.
        /// </summary>
        private static void RestoreCharacterGuideWorldPositionAfterPoseApply(OCIChar oci, Vector3 worldPositionBefore)
        {
            if (!TryGetCharacterWorldPosition(oci, out Vector3 current))
            {
                TrySetCharacterWorldPosition(oci, worldPositionBefore);
                return;
            }

            if ((current - worldPositionBefore).sqrMagnitude > 1e-6f)
                TrySetCharacterWorldPosition(oci, worldPositionBefore);
        }
#endif

        /// <summary>Sets <see cref="ObjectCtrlInfo.animeSpeed"/> to 0 when the option is enabled in config.</summary>
        internal static void MaybeFreezeAnimationSpeedAfterApply(OCIChar? oci)
        {
            if (oci == null)
                return;

            try
            {
                PoseBrowserConfig.Register(SandboxServices.Config);
                if (PoseBrowserConfig.FreezeAnimationSpeedOnApply is not { Value: true })
                    return;

                oci.animeSpeed = 0f;
                PoseBrowserAnimationUiBridge.TryRefreshAnimationPanel(oci);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not set animation speed: {ex.Message}");
            }
        }

        public void ApplyPoseToSelected(PoseGridItem item)
        {
            foreach (var ociChar in GetSelectedCharacters())
                ApplyPose(item, ociChar);
        }

        public bool SavePose(string filePath, string poseName, byte[] pngBytes, OCIChar ociChar)
        {
            try
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var fileInfo = new PauseCtrl.FileInfo(ociChar);
                _setExHookValue?.Invoke(poseName, ociChar);

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);
                bw.Write(pngBytes);
                bw.Write(PoseMagic);
                bw.Write(PoseVersion);
                bw.Write(ociChar.oiCharInfo.sex);
                bw.Write(poseName);
                fileInfo.Save(bw);

                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to save pose: {ex.Message}");
                return false;
            }
        }

        public bool UpdatePose(PoseGridItem item, OCIChar ociChar, byte[]? newPngBytes)
        {
            try
            {
                BackupFile(item.FilePath);

                if (newPngBytes != null)
                {
                    string savePath = item.FilePath;
                    bool convertingDatToPng = !item.IsPng;
                    if (convertingDatToPng)
                    {
                        savePath = Path.ChangeExtension(item.FilePath, ".png");
                        savePath = GetUniqueFilePath(savePath);
                    }

                    if (!SavePose(savePath, item.DisplayName, newPngBytes, ociChar))
                        return false;

                    if (convertingDatToPng)
                    {
                        if (File.Exists(item.FilePath) && !string.Equals(item.FilePath, savePath, StringComparison.OrdinalIgnoreCase))
                            File.Delete(item.FilePath);
                        item.FilePath = savePath;
                    }

                    SyncPoseFileLayoutFromDisk(item);
                    return true;
                }

                if (item.IsPng)
                {
                    byte[] pngBytes = LoadPngBytes(item.FilePath) ?? new byte[0];
                    if (!SavePose(item.FilePath, item.DisplayName, pngBytes, ociChar))
                        return false;
                    SyncPoseFileLayoutFromDisk(item);
                    return true;
                }

                if (!SavePoseAsDat(item.FilePath, item.DisplayName, ociChar))
                    return false;
                SyncPoseFileLayoutFromDisk(item);
                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to update pose: {ex.Message}");
                return false;
            }
        }

        private bool SavePoseAsDat(string filePath, string poseName, OCIChar ociChar)
        {
            try
            {
                var fileInfo = new PauseCtrl.FileInfo(ociChar);
                _setExHookValue?.Invoke(poseName, ociChar);

                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);
                bw.Write(PoseMagic);
                bw.Write(PoseVersion);
                bw.Write(ociChar.oiCharInfo.sex);
                bw.Write(poseName);
                fileInfo.Save(bw);
                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to save dat pose: {ex.Message}");
                return false;
            }
        }

        public void BackupFile(string filePath)
        {
            try
            {
                string relativePath = GetRelativePath(PoseRootPath, Path.GetDirectoryName(filePath) ?? "");
                string backupDir = PathEx.Combine(PoseRootPath, BackupFolder, relativePath);
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);

                string backupPath = Path.Combine(backupDir, Path.GetFileName(filePath));
                backupPath = GetUniqueFilePath(backupPath);
                File.Copy(filePath, backupPath, false);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Failed to backup file: {ex.Message}");
            }
        }

        public static string GetRelativePath(string rootPath, string fileDirPath)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd('\\', '/');
            string full = Path.GetFullPath(fileDirPath);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && full.Length > root.Length)
                return full.Substring(root.Length + 1);
            return string.Empty;
        }

        public static string GetUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int counter = 1;
            string result;
            do
            {
                result = Path.Combine(dir, $"{name}-{counter:D2}{ext}");
                counter++;
            } while (File.Exists(result));
            return result;
        }

        /// <summary>Returns <paramref name="directoryPath"/> if it does not exist; otherwise a sibling <c>name-01</c>, <c>name-02</c>, …</summary>
        public static string GetUniqueDirectoryPath(string directoryPath)
        {
            directoryPath = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(directoryPath))
                return directoryPath;
            string? parent = Path.GetDirectoryName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent))
                return directoryPath;
            string name = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            int counter = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(parent, $"{name}-{counter:D2}");
                counter++;
            } while (Directory.Exists(candidate));
            return candidate;
        }

        public Texture2D? LoadThumbnailTexture(PoseGridItem item)
        {
            if (!item.IsPng) return null;
            try
            {
                byte[]? pngBytes = LoadPngBytes(item.FilePath);
                if (pngBytes == null) return null;

                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(pngBytes);

                // Only downscale genuinely oversized previews. Resizing goes through a GPU
                // Blit + ReadPixels (a synchronous GPU→CPU stall) that hitches scrolling on weak/
                // integrated GPUs, so we must not trigger it just to convert the pixel format —
                // already-small thumbnails are kept as-is. Aspect ratio is preserved.
                if (tex.width > ThumbnailSize || tex.height > ThumbnailSize)
                {
                    float scale = Mathf.Min((float)ThumbnailSize / tex.width, (float)ThumbnailSize / tex.height);
                    int targetW = Mathf.Max(1, Mathf.RoundToInt(tex.width * scale));
                    int targetH = Mathf.Max(1, Mathf.RoundToInt(tex.height * scale));
                    tex = ResizeTexture(tex, targetW, targetH);
                }

                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch
            {
                return null;
            }
        }

        public static Texture2D CaptureScreenArea(Camera camera, Rect screenRect)
        {
            var rt = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            rt.antiAliasing = 8;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D((int)screenRect.width, (int)screenRect.height, TextureFormat.RGB24, false);
            tex.ReadPixels(screenRect, 0, 0, false);
            tex.Apply();

            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return ResizeTexture(tex, ThumbnailSize, ThumbnailSize);
        }

        public static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            if (source.width == width && source.height == height &&
                source.format == TextureFormat.RGB24)
                return source;

            var rt = RenderTexture.GetTemporary(width, height);
            rt.filterMode = FilterMode.Bilinear;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(source);

            return result;
        }

        /// <summary>Re-reads PNG/dat layout offsets from disk after the file was rewritten.</summary>
        public void SyncPoseFileLayoutFromDisk(PoseGridItem item)
        {
            var fi = new FileInfo(item.FilePath);
            item.IsPng = string.Equals(fi.Extension, ".png", StringComparison.OrdinalIgnoreCase);
            item.LastWriteTime = fi.LastWriteTime;
            item.CreationTimeUtc = fi.CreationTimeUtc;
            if (!item.IsPng)
            {
                item.DataPosition = 0;
                return;
            }

            using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            item.DataPosition = (int)GetPngSize(fs);
        }

        public byte[] ReadPoseDataBytes(PoseGridItem item)
        {
            using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(item.DataPosition, SeekOrigin.Begin);
            int count = (int)(fs.Length - item.DataPosition);
            var bytes = new byte[count];
            fs.Read(bytes, 0, count);
            return bytes;
        }

        public bool ConvertDatToPng(PoseGridItem item, byte[] pngBytes)
        {
            try
            {
                byte[] poseData = ReadPoseDataBytes(item);
                BackupFile(item.FilePath);

                string newPath = Path.ChangeExtension(item.FilePath, ".png");
                newPath = GetUniqueFilePath(newPath);

                using var fs = new FileStream(newPath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);
                bw.Write(pngBytes);
                bw.Write(poseData);

                if (File.Exists(item.FilePath) && !string.Equals(item.FilePath, newPath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(item.FilePath);

                item.FilePath = newPath;
                item.IsPng = true;
                item.DataPosition = pngBytes.Length;
                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to convert dat to png: {ex.Message}");
                return false;
            }
        }

        /// <summary>Absolute character state for pose history (pose skeleton + world transform).</summary>
        internal static bool TryCaptureCharacterSnapshot(OCIChar oci, out PoseCharacterSnapshot snapshot)
        {
            snapshot = new PoseCharacterSnapshot();
            if (oci == null)
                return false;

            try
            {
                var fileInfo = new PauseCtrl.FileInfo(oci);
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                fileInfo.Save(bw);
                snapshot.PoseData = ms.ToArray();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not capture pose snapshot: {ex.Message}");
                return false;
            }

            snapshot.HasPosition = TryGetCharacterWorldPosition(oci, out snapshot.Position);
            snapshot.HasRotation = TryGetCharacterWorldRotation(oci, out snapshot.Rotation);
            return snapshot.PoseData.Length > 0;
        }

        internal static bool TryRestoreCharacterSnapshot(
            OCIChar oci,
            PoseCharacterSnapshot snapshot,
            bool restorePose,
            bool restorePosition,
            bool restoreRotation)
        {
            if (oci == null || snapshot == null)
                return false;

            bool ok = true;
            if (restorePose && snapshot.PoseData.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(snapshot.PoseData);
                    using var br = new BinaryReader(ms);
                    var fileInfo = new PauseCtrl.FileInfo(null);
                    fileInfo.Load(br, PoseVersion);
#if KKS || KK
                    bool hadGuidePos = TryGetCharacterWorldPosition(oci, out Vector3 guidePosBefore);
#endif
                    fileInfo.Apply(oci);
#if KKS || KK
                    if (hadGuidePos)
                        RestoreCharacterGuideWorldPositionAfterPoseApply(oci, guidePosBefore);
#endif
                }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogWarning($"PoseBrowser: Could not restore pose snapshot: {ex.Message}");
                    ok = false;
                }
            }

            if (restorePosition && snapshot.HasPosition &&
                !TrySetCharacterWorldPosition(oci, snapshot.Position))
                ok = false;

            if (restoreRotation && snapshot.HasRotation &&
                !TrySetCharacterWorldRotation(oci, snapshot.Rotation))
                ok = false;

            MaybeFreezeAnimationSpeedAfterApply(oci);
            return ok;
        }
    }

    internal sealed class PoseCharacterSnapshot
    {
        public byte[] PoseData = new byte[0];
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public bool HasPosition;
        public bool HasRotation;

        public bool StateEquals(PoseCharacterSnapshot? other)
        {
            if (other == null)
                return false;
            if (!PoseData.SequenceEqual(other.PoseData))
                return false;
            if (HasPosition != other.HasPosition || HasRotation != other.HasRotation)
                return false;
            if (HasPosition && (Position - other.Position).sqrMagnitude > 1e-8f)
                return false;
            if (HasRotation && Quaternion.Angle(Rotation, other.Rotation) > 0.05f)
                return false;
            return true;
        }
    }
}
