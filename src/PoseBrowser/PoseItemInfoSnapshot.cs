using System;
using System.Globalization;
using System.IO;
using Studio;

namespace HS2SandboxPlugin
{
    /// <summary>Serialize <see cref="OIItemInfo"/> the same way Studio scene files do.</summary>
    internal static class PoseItemInfoSnapshot
    {
        public static bool TryCapture(OCIItem item, out byte[] blob, out string versionText, out int kind, out int[] kinds)
        {
            blob = new byte[0];
            versionText = string.Empty;
            kind = 0;
            kinds = new int[0];

            if (item?.itemInfo == null)
                return false;

            try
            {
                var studio = Singleton<Studio.Studio>.Instance;
                Version version = studio.sceneInfo.version;
                OIItemInfo info = item.itemInfo;
                kind = info.kind;
                kinds = info.kinds ?? new int[0];

                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                info.Save(bw, version);
                blob = ms.ToArray();
                versionText = version.ToString();
                return blob.Length > 0;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not snapshot item info: {ex.Message}");
                return false;
            }
        }

        public static OIItemInfo? TryRestore(
            byte[] blob,
            string? versionText,
            int kind,
            int group,
            int category,
            int itemNo)
        {
            if (blob == null || blob.Length == 0)
                return null;

            try
            {
                Version version = TryParseVersion(versionText)
                    ?? Singleton<Studio.Studio>.Instance.sceneInfo.version;
                var info = new OIItemInfo(kind, group, category, itemNo);
                using var ms = new MemoryStream(blob);
                using var br = new BinaryReader(ms);
                info.Load(br, version, true, true);
                return info;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not restore item info snapshot: {ex.Message}");
                return null;
            }
        }

        public static Version? TryParseVersion(string? text)
        {
            if (StringEx.IsNullOrWhiteSpace(text))
                return null;
            try
            {
                return new Version(text.Trim());
            }
            catch
            {
                return null;
            }
        }

        public static string FormatKinds(int[]? kinds)
        {
            if (kinds == null || kinds.Length == 0)
                return string.Empty;
            return string.Join(",", Array.ConvertAll(kinds, i => i.ToString(CultureInfo.InvariantCulture)));
        }

        public static int[] ParseKinds(string? text)
        {
            if (StringEx.IsNullOrWhiteSpace(text))
                return new int[0];

            string[] parts = text.Split(',');
            var list = new int[parts.Length];
            int count = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int v))
                    list[count++] = v;
            }

            if (count == parts.Length)
                return list;

            var trimmed = new int[count];
            Array.Copy(list, trimmed, count);
            return trimmed;
        }
    }
}
