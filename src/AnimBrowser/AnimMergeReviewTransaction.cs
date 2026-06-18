using System;
using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    /// <summary>Captures the full intended outcome of one merge/group review as an in-memory delta.
    /// Nothing touches <see cref="AnimGroupStore"/> until <see cref="Apply"/> runs, so cancelling a
    /// review (<see cref="Reset"/>) is a true rollback. Consolidates the previously scattered
    /// <c>_pendingMerge*</c> / <c>_pendingMgc*</c> window fields into one transactional unit.</summary>
    internal sealed class AnimMergeReviewTransaction
    {
        /// <summary>Tree-merge rule being created or edited; null for a pure display-group review.</summary>
        public AnimTreeMergeRule? Rule;

        /// <summary>True when <see cref="Rule"/> already lives in the store (edit), false for a new rule.</summary>
        public bool RuleAlreadyStored;

        /// <summary>Deferred category-merge source set. Applied via
        /// <see cref="AnimGroupStore.ReplaceCategoryMergeSources"/> only at commit (never eagerly).</summary>
        public List<AnimCatalogRef>? CategoryMergeSources;

        /// <summary>Deferred rule name. Applied only at commit so a cancelled review leaves the
        /// stored rule untouched.</summary>
        public string? RuleName;

        /// <summary>Group ids to add to an existing group merge (additive group merge), applied at commit.</summary>
        public List<int>? GroupMergeAddedGroupIds;

        /// <summary>Excluded category sources to bring back into <see cref="Rule"/> at commit.</summary>
        public readonly List<AnimCatalogRef> Reinclude = new List<AnimCatalogRef>();

        /// <summary>Subcategory bucket alias merge (join two buckets inside a group merge).</summary>
        public bool BucketMerge;
        public string BucketMergeRuleId = string.Empty;
        public string BucketMergeTargetKey = string.Empty;
        public readonly List<string> BucketMergeAliasKeys = new List<string>();

        public void Reset()
        {
            Rule = null;
            RuleAlreadyStored = false;
            CategoryMergeSources = null;
            RuleName = null;
            GroupMergeAddedGroupIds = null;
            Reinclude.Clear();
            ClearBucketMerge();
        }

        public void ClearBucketMerge()
        {
            BucketMerge = false;
            BucketMergeRuleId = string.Empty;
            BucketMergeTargetKey = string.Empty;
            BucketMergeAliasKeys.Clear();
        }

        /// <summary>Applies every pending edit plus the resolved display groups in a single atomic
        /// store batch (one Save / one Changed notification).</summary>
        /// <param name="commitGroups">Display groups that survived review (already trimmed to &gt;= 2 members).</param>
        /// <param name="skippedRefs">Animations the user chose to keep at their original category.</param>
        /// <param name="isInSinglesGroup">True when a skipped ref belongs to an "as singles" group (not an exclusion).</param>
        public void Apply(
            AnimGroupStore store,
            List<AnimDisplayGroupData> commitGroups,
            ICollection<AnimCatalogRef> skippedRefs,
            Predicate<AnimCatalogRef> isInSinglesGroup)
        {
            using (store.BeginBatch())
            {
                if (Rule != null)
                {
                    if (CategoryMergeSources != null)
                        store.ReplaceCategoryMergeSources(Rule, CategoryMergeSources);
                    if (GroupMergeAddedGroupIds != null)
                        store.AddSourceGroupsToGroupMerge(Rule, GroupMergeAddedGroupIds);
                    if (RuleName != null)
                        Rule.Name = RuleName;

                    if (skippedRefs.Count > 0)
                    {
                        foreach (AnimCatalogRef skipped in skippedRefs)
                        {
                            if (isInSinglesGroup(skipped))
                                continue;
                            if (!ContainsRef(Rule.ExcludedAnimationRefs, skipped))
                                Rule.ExcludedAnimationRefs.Add(skipped);
                        }
                    }

                    if (Reinclude.Count > 0)
                        store.ReincludeCategories(Rule, Reinclude);
                }

                if (BucketMerge)
                    store.MergeSubcategoryBuckets(BucketMergeRuleId, BucketMergeTargetKey, BucketMergeAliasKeys);

                store.Commit(Rule, commitGroups, RuleAlreadyStored);
            }
        }

        private static bool ContainsRef(List<AnimCatalogRef> list, AnimCatalogRef value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Equals(value))
                    return true;
            }
            return false;
        }
    }
}
