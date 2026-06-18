using System.Collections.Generic;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>Catalog location as a ordered path of display segments (group / category / …).
    /// Animations keep a stable <see cref="Original"/> path from the raw Studio catalog and a
    /// <see cref="Current"/> path that reflects virtual merges (updated when the display tree is built).</summary>
    internal sealed class AnimCatalogPath
    {
        public readonly List<string> Segments = new List<string>();

        public string Display
        {
            get
            {
                if (Segments.Count == 0)
                    return string.Empty;
                if (Segments.Count == 1)
                    return Segments[0];
                var sb = new StringBuilder(Segments[0]);
                for (int i = 1; i < Segments.Count; i++)
                {
                    sb.Append(" / ");
                    sb.Append(Segments[i]);
                }
                return sb.ToString();
            }
        }

        public void SetFrom(IReadOnlyList<string> segments)
        {
            Segments.Clear();
            for (int i = 0; i < segments.Count; i++)
                Segments.Add(segments[i]);
        }

        public void CopyFrom(AnimCatalogPath other) => SetFrom(other.Segments);

        public static AnimCatalogPath FromPair(string groupName, string categoryName)
        {
            var path = new AnimCatalogPath();
            path.Segments.Add(groupName);
            path.Segments.Add(categoryName);
            return path;
        }
    }

    internal static class AnimCatalogRefUtil
    {
        public static AnimCatalogRef CategoryRef(int group, int category) => new AnimCatalogRef(group, category, -1);

        public static bool IsCategoryRef(AnimCatalogRef reference) => reference.No < 0 && reference.Category >= 0;

        public static bool SameCategory(AnimCatalogRef a, AnimCatalogRef b) =>
            a.Group == b.Group && a.Category == b.Category;

        public static bool ContainsCategory(IList<AnimCatalogRef> list, AnimCatalogRef categoryRef)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (SameCategory(list[i], categoryRef))
                    return true;
            }
            return false;
        }
    }
}
