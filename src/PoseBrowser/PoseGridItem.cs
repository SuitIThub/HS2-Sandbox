using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PoseGridItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsPng { get; set; }
        public int DataPosition { get; set; }
        public DateTime LastWriteTime { get; set; }
        /// <summary>File creation time (UTC, from filesystem).</summary>
        public DateTime CreationTimeUtc { get; set; }
        /// <summary>When the pose was last applied in Studio (UTC); <see cref="DateTime.MinValue"/> if unknown.</summary>
        public DateTime LastUsedUtc { get; set; }
        public Texture2D? Thumbnail { get; set; }
        public bool IsSelected { get; set; }
        public bool IsFavorite { get; set; }
        public HashSet<string> Tags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>When non-null, this row is an import preview backed by <see cref="PosePackExchange"/> entry id.</summary>
        public string? ImportPackEntryId { get; set; }
        /// <summary>When non-null, pose belongs to this group id (<see cref="PoseGroupDatabase"/>).</summary>
        public string? GroupId { get; set; }

        // --- Render string caches (Maßnahme 4 + 9) ---
        internal string? CachedTruncatedName;
        internal float CachedTruncatedNameWidth;
        internal string? CachedTruncatedNameSource;
        internal string? CachedTagString;
        internal int CachedTagCount;
        internal float CachedTagBlockHeight;
        internal float CachedTagBlockWidth;
        internal int CachedTagBlockTagCount = -1;

        /// <summary>Frame number when this item's thumbnail was last displayed (for LRU eviction).</summary>
        internal int ThumbnailLastUsedFrame;

        internal void InvalidateRenderCaches()
        {
            CachedTruncatedName = null;
            CachedTagString = null;
            CachedTagCount = -1;
            CachedTagBlockTagCount = -1;
        }

        internal string GetOrBuildTagString()
        {
            if (CachedTagString != null && CachedTagCount == Tags.Count)
                return CachedTagString;

            if (Tags.Count == 0)
            {
                CachedTagString = "";
                CachedTagCount = 0;
                return CachedTagString;
            }

            CachedTagString = string.Join(" · ",
                Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
            CachedTagCount = Tags.Count;
            return CachedTagString;
        }

        /// <summary>Path relative to pose root (same boundary rules as <see cref="PoseTagDatabase"/> storage keys).</summary>
        public string RelativePath(string rootPath)
        {
            try
            {
                if (string.IsNullOrEmpty(FilePath)) return "";
                string full = System.IO.Path.GetFullPath(FilePath);
                string root = System.IO.Path.GetFullPath(rootPath).TrimEnd(
                    System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                if (full.Length >= root.Length && full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    int i = root.Length;
                    if (i < full.Length && (full[i] == System.IO.Path.DirectorySeparatorChar ||
                                            full[i] == System.IO.Path.AltDirectorySeparatorChar))
                        i++;
                    return full.Substring(i);
                }

                return full;
            }
            catch
            {
                return FilePath;
            }
        }
    }
}
