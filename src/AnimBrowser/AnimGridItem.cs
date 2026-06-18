using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal sealed class AnimCategoryNode
    {
        public int GroupId;
        public int CategoryId = -1;
        public string Name = string.Empty;
        public int Depth;
        public bool IsExpanded = true;
        public bool IsGroup;
        public readonly List<AnimCategoryNode> Children = new List<AnimCategoryNode>();
        public string? CachedTruncatedName;
        public float CachedTruncatedWidth = -1f;
        public string? CachedTruncatedLabelSource;

        public string GetDisplayLabel() => StudioAutoTranslation.Resolve(Name);

        public static int CompareByDisplayLabel(AnimCategoryNode a, AnimCategoryNode b)
        {
            int cmp = string.Compare(a.GetDisplayLabel(), b.GetDisplayLabel(), System.StringComparison.CurrentCultureIgnoreCase);
            if (cmp != 0)
                return cmp;
            if (a.IsGroup != b.IsGroup)
                return a.IsGroup ? -1 : 1;
            if (a.IsGroup)
                return a.GroupId.CompareTo(b.GroupId);
            return a.CategoryId.CompareTo(b.CategoryId);
        }

        internal void InvalidateDisplayCaches()
        {
            CachedTruncatedName = null;
            CachedTruncatedWidth = -1f;
            CachedTruncatedLabelSource = null;
        }
    }

    internal sealed class AnimGridItem
    {
        public readonly int Group;
        public readonly int Category;
        public readonly int No;
        public readonly string DisplayName;
        public readonly int Sort;
        public readonly bool IsMainGame;
        public readonly bool IsStudioListed;

        private GUIContent? _displayContent;
        private string _cachedLabel = string.Empty;

        public AnimGridItem(int group, int category, int no, string? displayName, int sort, bool isMainGame, bool isStudioListed)
        {
            Group = group;
            Category = category;
            No = no;
            DisplayName = string.IsNullOrEmpty(displayName) ? BuildFallbackName(group, category, no) : displayName;
            Sort = sort;
            IsMainGame = isMainGame;
            IsStudioListed = isStudioListed;
        }

        public string CatalogKey => Group + "/" + Category + "/" + No;

        public string GetDisplayLabel() => StudioAutoTranslation.Resolve(DisplayName);

        public static int Compare(AnimGridItem a, AnimGridItem b)
        {
            int c = string.Compare(a.GetDisplayLabel(), b.GetDisplayLabel(), System.StringComparison.CurrentCultureIgnoreCase);
            if (c != 0)
                return c;
            return a.No.CompareTo(b.No);
        }

        public GUIContent GetDisplayContent()
        {
            string label = GetDisplayLabel();
            return GetDisplayContentForResolvedLabel(label);
        }

        public GUIContent GetDisplayContentForResolvedLabel(string resolvedLabel)
        {
            if (_displayContent != null && _cachedLabel == resolvedLabel)
                return _displayContent;
            _cachedLabel = resolvedLabel;
            _displayContent = new GUIContent(resolvedLabel, "Apply to selected characters");
            return _displayContent;
        }

        internal void InvalidateDisplayCaches()
        {
            _displayContent = null;
            _cachedLabel = string.Empty;
            CachedTruncatedName = null;
            CachedTruncatedNameWidth = -1f;
            CachedTruncatedNameSource = null;
        }

        public Texture2D? Thumbnail;
        public bool ThumbnailRequested;
        public bool ThumbnailFailed;

        public string? CachedTruncatedName;
        public float CachedTruncatedNameWidth = -1f;
        public string? CachedTruncatedNameSource;

        private static string BuildFallbackName(int group, int category, int no) =>
            "Anim " + group + "/" + category + "/" + no;
    }
}
