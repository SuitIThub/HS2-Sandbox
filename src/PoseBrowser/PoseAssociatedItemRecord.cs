using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Workspace item layout saved against a pose library entry (anchor = character root guide).</summary>
    public sealed class PoseAssociatedItemRecord
    {
        public int ItemGroup { get; set; }
        public int ItemCategory { get; set; }
        public int ItemNo { get; set; }

        /// <summary>Stable asset path from <see cref="Info.dicItemLoadInfo"/> (used to re-resolve slot after restart).</summary>
        public string BundlePath { get; set; } = string.Empty;

        public string AssetName { get; set; } = string.Empty;

        public string Manifest { get; set; } = string.Empty;

        /// <summary>Studio object kind (<see cref="ObjectInfo.kind"/>).</summary>
        public int ItemKind { get; set; }

        /// <summary>Category path indices from the item list UI.</summary>
        public int[] ItemKinds { get; set; } = new int[0];

        /// <summary>Studio <see cref="OIItemInfo"/> binary from <see cref="OIItemInfo.Save"/>.</summary>
        public byte[]? ItemInfoBlob { get; set; }

        public string? ItemInfoVersion { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Position relative to character anchor (used for free placement and as world source for attach load).</summary>
        public Vector3 LocalPosition { get; set; }

        /// <summary>Rotation relative to character anchor.</summary>
        public Quaternion LocalRotation { get; set; } = Quaternion.identity;

        /// <summary>Studio <see cref="GuideObject.changeAmount"/> at save when parented in the workspace tree.</summary>
        public bool HasAttachChangeAmount { get; set; }

        public Vector3 AttachChangePosition { get; set; }

        public Quaternion AttachChangeRotation { get; set; } = Quaternion.identity;

        /// <summary>Studio guide scale at save (denormalized with anchor scale on apply).</summary>
        public Vector3 ItemScale { get; set; } = Vector3.one;

        /// <summary>Body-part transform name when parented; null/empty = free-placed.</summary>
        public string? ParentObjectName { get; set; }

        /// <summary>
        /// Path from character tree root to parent row (<see cref="TreeNodeObject.textName"/> segments, '|'-separated).
        /// </summary>
        public string? ParentTreePath { get; set; }

        public float SavedAnchorBodyHeight { get; set; }
        public Vector3 SavedAnchorObjectScale { get; set; } = Vector3.one;

        public string CatalogKey => FormatCatalogKey(ItemGroup, ItemCategory, ItemNo);

        public static string FormatCatalogKey(int group, int category, int no) =>
            $"{group}/{category}/{no}";

        public bool TryParseCatalogKey(string key, out int group, out int category, out int no)
        {
            group = category = no = 0;
            if (string.IsNullOrEmpty(key)) return false;
            string[] parts = key.Split('/');
            if (parts.Length != 3) return false;
            return int.TryParse(parts[0], out group) &&
                   int.TryParse(parts[1], out category) &&
                   int.TryParse(parts[2], out no);
        }
    }
}
