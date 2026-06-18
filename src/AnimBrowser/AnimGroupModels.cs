using System;
using System.Collections.Generic;
using System.Globalization;

namespace HS2SandboxPlugin
{
    /// <summary>Phase role of an animation inside a sequence group (intro / loop / outro).</summary>
    internal enum AnimPhase
    {
        None = 0,
        In = 1,
        Loop = 2,
        Out = 3
    }

    /// <summary>Inferred gender role of an animation inside a multi-character group.</summary>
    internal enum AnimGender
    {
        Unknown = 0,
        Male = 1,
        Female = 2
    }

    /// <summary>Stable reference into the Studio animation catalog.</summary>
    internal readonly struct AnimCatalogRef : IEquatable<AnimCatalogRef>
    {
        public readonly int Group;
        public readonly int Category;
        public readonly int No;

        public AnimCatalogRef(int group, int category, int no)
        {
            Group = group;
            Category = category;
            No = no;
        }

        public string Key => Group.ToString(CultureInfo.InvariantCulture) + "/" +
                             Category.ToString(CultureInfo.InvariantCulture) + "/" +
                             No.ToString(CultureInfo.InvariantCulture);

        public bool Equals(AnimCatalogRef other) =>
            Group == other.Group && Category == other.Category && No == other.No;

        public override bool Equals(object? obj) => obj is AnimCatalogRef other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Group;
                hash = hash * 397 + Category;
                hash = hash * 397 + No;
                return hash;
            }
        }

        public static bool TryParse(string? token, out AnimCatalogRef result)
        {
            result = default;
            if (string.IsNullOrEmpty(token))
                return false;
            string[] parts = token!.Split('/');
            if (parts.Length != 3)
                return false;
            if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int g) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                result = new AnimCatalogRef(g, c, n);
                return true;
            }
            return false;
        }
    }

    /// <summary>Persisted role assignment of one animation inside a display group.</summary>
    internal sealed class AnimGroupMemberData
    {
        public AnimCatalogRef Ref;
        public AnimPhase Phase;
        public AnimGender Gender;
        public int GenderOrdinal;
    }

    /// <summary>Persisted display group: several catalog animations sharing one card.</summary>
    internal sealed class AnimDisplayGroupData
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public readonly List<AnimGroupMemberData> Members = new List<AnimGroupMemberData>();

        /// <summary>Review UI only (not persisted): normalized sub-category bucket key.</summary>
        public string ReviewSectionKey = string.Empty;

        /// <summary>Review UI only (not persisted): raw catalog path label, e.g. "Group / Subcat".</summary>
        public string ReviewSectionLabel = string.Empty;
    }

    internal enum AnimTreeMergeKind
    {
        Category = 0,
        Group = 1
    }

    /// <summary>How a tree node relates to merge exclusions when resolving items.</summary>
    internal enum AnimNodePlacementKind
    {
        /// <summary>Plain raw catalog node.</summary>
        Normal = 0,
        /// <summary>Merged node: shows non-excluded items from merged sources.</summary>
        MergedContent = 1,
        /// <summary>Residual node under an original group: shows excluded categories/animations only.</summary>
        ResidualExcluded = 2
    }

    /// <summary>Persisted virtual merge of catalog tree nodes into a single displayed node.</summary>
    internal sealed class AnimTreeMergeRule
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public AnimTreeMergeKind Kind;

        /// <summary>For <see cref="AnimTreeMergeKind.Category"/>: (group, category) of each source.
        /// For <see cref="AnimTreeMergeKind.Group"/>: source group ids (category = -1).</summary>
        public readonly List<AnimCatalogRef> Sources = new List<AnimCatalogRef>();

        /// <summary>Category sources removed from this merge (partial unmerge / skipped subcategory).</summary>
        public readonly List<AnimCatalogRef> ExcludedSources = new List<AnimCatalogRef>();

        /// <summary>Individual animations that stay at their original catalog path and skip card grouping.</summary>
        public readonly List<AnimCatalogRef> ExcludedAnimationRefs = new List<AnimCatalogRef>();

        /// <summary>For group merges: maps a subcategory bucket key onto another bucket key (joined subcategories).</summary>
        public readonly Dictionary<string, string> SubcategoryBucketAliases =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public string ResolveSubcategoryBucketKey(string bucketKey)
        {
            if (string.IsNullOrEmpty(bucketKey))
                return bucketKey;
            string current = bucketKey;
            for (int i = 0; i < 16; i++)
            {
                if (!SubcategoryBucketAliases.TryGetValue(current, out string? target) ||
                    string.IsNullOrEmpty(target) ||
                    string.Equals(target, current, StringComparison.Ordinal))
                {
                    return current;
                }
                current = target;
            }
            return current;
        }
    }
}
