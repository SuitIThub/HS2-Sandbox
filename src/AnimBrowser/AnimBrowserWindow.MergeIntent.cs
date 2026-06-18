using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    /// <summary>What the tree "merge categories" button will do for the current selection.</summary>
    internal enum AnimTreeMergeAction
    {
        None = 0,
        /// <summary>Combine sub-categories of one top-level group into a single category node.</summary>
        MergeCategories = 1,
        /// <summary>Join two subcategory buckets inside an existing group merge (bucket alias).</summary>
        JoinSubcategories = 2,
        /// <summary>Combine two or more top-level groups into a single merged group.</summary>
        MergeGroups = 3,
        /// <summary>Add the selected group(s) to an existing merged group (additive group merge).</summary>
        ExtendGroupMerge = 4
    }

    /// <summary>Resolved availability + presentation of a tree merge action, so the action bar can
    /// enable/disable the button and explain why instead of silently no-op'ing on click.</summary>
    internal readonly struct AnimTreeMergeAvailability
    {
        public readonly AnimTreeMergeAction Action;
        public readonly bool Enabled;
        public readonly string Label;
        public readonly string Tooltip;

        public AnimTreeMergeAvailability(AnimTreeMergeAction action, bool enabled, string label, string tooltip)
        {
            Action = action;
            Enabled = enabled;
            Label = label;
            Tooltip = tooltip;
        }

        /// <summary>Whether the action bar should render the button at all (even if disabled).</summary>
        public bool Visible => Action != AnimTreeMergeAction.None;
    }

    public partial class AnimBrowserWindow
    {
        /// <summary>Classifies what merging the selected non-group tree nodes would do, and whether it
        /// is possible. Drives the "Merge categories…/Join subcategories…" button and removes the old
        /// silent-no-op click paths (cross-group merges now show a disabled button with a reason).</summary>
        private AnimTreeMergeAvailability ResolveCategoryMergeAvailability(List<AnimViewNode> selectedNonGroupNodes)
        {
            if (selectedNonGroupNodes.Count < 2)
                return new AnimTreeMergeAvailability(AnimTreeMergeAction.None, false, "Merge categories…", string.Empty);

            // Same-source-group subcategories are a real category merge (cm), even inside a group merge.
            // Only a selection spanning multiple source groups is a bucket join. Mirror the executor order.
            bool sameSourceGroup = AllNodesShareOneSourceGroup(selectedNonGroupNodes);

            // Cross-group nodes that belong to one group merge → joining their subcategory buckets.
            if (!sameSourceGroup &&
                TryClassifyBucketJoin(selectedNonGroupNodes, out _, out _, out int distinctBuckets) &&
                distinctBuckets >= 2)
            {
                return new AnimTreeMergeAvailability(
                    AnimTreeMergeAction.JoinSubcategories,
                    true,
                    "Join subcategories…",
                    "Merge the selected subcategory buckets inside the group merge into one.");
            }

            // Plain category merge requires all sub-categories to share one top-level group.
            if (TryValidateSameGroupCategoryMerge(selectedNonGroupNodes, out _))
            {
                return new AnimTreeMergeAvailability(
                    AnimTreeMergeAction.MergeCategories,
                    true,
                    "Merge categories…",
                    "Combine the selected sub-categories into one node (including already-merged categories).");
            }

            // Cross-group subcategories without a group merge: explain instead of doing nothing.
            return new AnimTreeMergeAvailability(
                AnimTreeMergeAction.MergeCategories,
                false,
                "Merge categories…",
                "These subcategories live in different top-level groups. Merge the parent groups first, " +
                "then join their subcategories inside the merged group.");
        }

        /// <summary>Classifies the group-level merge button: new merge of raw groups, or an additive
        /// extension of one already-merged group with the selected raw groups.</summary>
        private static AnimTreeMergeAvailability ResolveGroupMergeAvailability(int mergedGroupNodeCount, int rawGroupNodeCount)
        {
            if (mergedGroupNodeCount == 1 && rawGroupNodeCount >= 1)
            {
                return new AnimTreeMergeAvailability(
                    AnimTreeMergeAction.ExtendGroupMerge,
                    true,
                    "Add to group merge…",
                    "Add the selected group(s) to the already-merged group.");
            }

            if (mergedGroupNodeCount == 0 && rawGroupNodeCount >= 2)
            {
                return new AnimTreeMergeAvailability(
                    AnimTreeMergeAction.MergeGroups,
                    true,
                    "Merge groups…",
                    "Combine the selected groups, matching sub-categories by name.");
            }

            if (mergedGroupNodeCount >= 2)
            {
                return new AnimTreeMergeAvailability(
                    AnimTreeMergeAction.MergeGroups,
                    false,
                    "Merge groups…",
                    "Select two or more raw groups, or one merged group plus the raw groups to add to it.");
            }

            return new AnimTreeMergeAvailability(AnimTreeMergeAction.None, false, "Merge groups…", string.Empty);
        }
    }
}
