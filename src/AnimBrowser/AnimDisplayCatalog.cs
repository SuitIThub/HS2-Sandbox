using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Builds the displayed category tree and content entries by layering user merges and
    /// display groups on top of the raw <see cref="AnimCatalogService"/>. The raw catalog is never
    /// mutated. Results are cached and only rebuilt on invalidation (IMGUI draw-path rule).</summary>
    internal sealed class AnimDisplayCatalog
    {
        private readonly AnimCatalogService _catalog;
        private readonly AnimGroupStore _store;
        private bool _hideNonStudioCatalogAnimations;

        private readonly List<AnimViewNode> _rootGroups = new List<AnimViewNode>();
        private readonly Dictionary<string, List<AnimDisplayEntry>> _entriesByNodeId =
            new Dictionary<string, List<AnimDisplayEntry>>(StringComparer.Ordinal);
        private readonly Dictionary<AnimCatalogRef, string> _contextNameByRef =
            new Dictionary<AnimCatalogRef, string>();
        private readonly Dictionary<AnimCatalogRef, string> _groupNameByRef =
            new Dictionary<AnimCatalogRef, string>();
        private readonly Dictionary<AnimCatalogRef, string> _categoryNameByRef =
            new Dictionary<AnimCatalogRef, string>();
        private readonly Dictionary<AnimCatalogRef, AnimCatalogPath> _originalPathByRef =
            new Dictionary<AnimCatalogRef, AnimCatalogPath>();
        private readonly Dictionary<AnimCatalogRef, AnimCatalogPath> _currentPathByRef =
            new Dictionary<AnimCatalogRef, AnimCatalogPath>();
        private readonly List<AnimViewNode> _warmupNodeScratch = new List<AnimViewNode>();
        private bool _treeBuilt;
        private bool _entriesWarmupComplete;

        private const int DefaultCategoriesPerWarmupSlice = 4;
        private const int DefaultEntryNodesPerWarmupSlice = 3;

        public bool TreeBuilt => _treeBuilt;
        public bool EntriesWarmupComplete => _entriesWarmupComplete;
        public bool WarmupComplete => _treeBuilt && _entriesWarmupComplete;
        public bool WarmupInProgress { get; private set; }
        public float WarmupProgress { get; private set; }
        public string WarmupStatusText { get; private set; } = string.Empty;

        private int _warmupCategoriesTotal;
        private int _warmupCategoriesDone;
        private int _warmupNodesTotal;
        private int _warmupNodesDone;
        private int _warmupResidualCategoriesTotal;
        private int _warmupResidualCategoriesDone;

        public AnimDisplayCatalog(AnimCatalogService catalog, AnimGroupStore store)
        {
            _catalog = catalog;
            _store = store;
        }

        public void SetHideNonStudioCatalogAnimations(bool hide)
        {
            if (_hideNonStudioCatalogAnimations == hide)
                return;
            _hideNonStudioCatalogAnimations = hide;
            InvalidateTree();
        }

        public IList<AnimViewNode> RootGroups
        {
            get
            {
                EnsureTree();
                return _rootGroups;
            }
        }

        public void InvalidateTree()
        {
            _treeBuilt = false;
            _entriesWarmupComplete = false;
            WarmupInProgress = false;
            WarmupProgress = 0f;
            WarmupStatusText = string.Empty;
            _rootGroups.Clear();
            _entriesByNodeId.Clear();
            _contextNameByRef.Clear();
            _groupNameByRef.Clear();
            _categoryNameByRef.Clear();
            _originalPathByRef.Clear();
            _currentPathByRef.Clear();
        }

        public void InvalidateEntries()
        {
            _entriesByNodeId.Clear();
        }

        /// <summary>Pre-builds the display tree and entry lists across frames so opening mod subcategories does not hitch.</summary>
        public IEnumerator WarmupCoroutine(
            int categoriesPerSlice = DefaultCategoriesPerWarmupSlice,
            int entryNodesPerSlice = DefaultEntryNodesPerWarmupSlice)
        {
            if (!_catalog.BuildComplete)
                yield break;

            if (_entriesWarmupComplete && _treeBuilt)
                yield break;

            WarmupInProgress = true;
            WarmupProgress = 0f;
            try
            {
                if (!_treeBuilt)
                {
                    WarmupStatusText = "Tree";
                    yield return BuildTreeCoroutine(categoriesPerSlice);
                }

                if (_entriesWarmupComplete)
                    yield break;

                WarmupStatusText = "Cache";
                yield return PrewarmEntriesCoroutine(entryNodesPerSlice);
                _entriesWarmupComplete = true;
                WarmupProgress = 1f;
            }
            finally
            {
                WarmupInProgress = false;
                WarmupStatusText = string.Empty;
            }
        }

        public AnimCatalogPath GetOriginalPath(AnimGridItem item)
        {
            EnsureTree();
            var reference = new AnimCatalogRef(item.Group, item.Category, item.No);
            if (_originalPathByRef.TryGetValue(reference, out AnimCatalogPath? path))
                return path;
            return AnimCatalogPath.FromPair(
                _catalog.GetGroupName(item.Group),
                _catalog.GetCategoryName(item.Group, item.Category));
        }

        public AnimCatalogPath GetCurrentPath(AnimGridItem item)
        {
            EnsureTree();
            var reference = new AnimCatalogRef(item.Group, item.Category, item.No);
            if (_currentPathByRef.TryGetValue(reference, out AnimCatalogPath? path))
                return path;
            return GetOriginalPath(item);
        }

        /// <summary>Context label used to infer gender for an item (gendered category name, else group name).</summary>
        public string? GetGenderContextName(AnimGridItem item)
        {
            EnsureTree();
            var reference = new AnimCatalogRef(item.Group, item.Category, item.No);
            if (_contextNameByRef.TryGetValue(reference, out string context) &&
                AnimGroupHeuristics.DetectGenderFromContext(context) != AnimGender.Unknown)
            {
                return context;
            }
            if (_groupNameByRef.TryGetValue(reference, out string groupName))
                return groupName;
            return _catalog.GetGroupName(item.Group);
        }

        public string GetCategoryName(AnimGridItem item)
        {
            EnsureTree();
            var reference = new AnimCatalogRef(item.Group, item.Category, item.No);
            string catalog = _categoryNameByRef.TryGetValue(reference, out string name)
                ? name
                : _catalog.GetCategoryName(item.Group, item.Category);
            return _store.ResolveCatalogName(AnimDisplayNameKeys.Category(item.Group, item.Category), catalog);
        }

        public string GetItemDisplayLabel(AnimGridItem item) => _store.GetAnimationDisplayLabel(item);

        public GUIContent GetItemDisplayContent(AnimGridItem item) =>
            item.GetDisplayContentForResolvedLabel(GetItemDisplayLabel(item));

        public string GetTreeNodeRenameSeed(AnimViewNode node)
        {
            if (node.IsMerged && node.MergeRuleId.Length > 0 &&
                (node.Id.StartsWith("mg:", StringComparison.Ordinal) ||
                 node.Id.StartsWith("cm:", StringComparison.Ordinal)))
            {
                AnimTreeMergeRule? rule = _store.FindTreeMerge(node.MergeRuleId);
                return rule?.Name ?? node.Name;
            }

            if (TryParseGroupNodeId(node.Id, out int groupId))
            {
                return _store.GetDisplayNameOverride(AnimDisplayNameKeys.Group(groupId))
                    ?? _catalog.GetGroupName(groupId);
            }

            if (TryParseCategoryNodeId(node.Id, out int catGroupId, out int categoryId))
            {
                return _store.GetDisplayNameOverride(AnimDisplayNameKeys.Category(catGroupId, categoryId))
                    ?? _catalog.GetCategoryName(catGroupId, categoryId);
            }

            return _store.GetDisplayNameOverride(AnimDisplayNameKeys.TreeNode(node.Id)) ?? node.Name;
        }

        public string GetDisplayGroupRenameSeed(AnimDisplayGroup group) => group.Name;

        public string GetAnimationRenameSeed(AnimGridItem item) =>
            _store.GetDisplayNameOverride(AnimDisplayNameKeys.Animation(item)) ?? item.DisplayName;

        /// <summary>Current display path (reflects virtual merges). Falls back to the original catalog path.</summary>
        public string GetCatalogPath(AnimGridItem item) => GetCurrentPath(item).Display;

        /// <summary>Original Studio catalog path before any virtual merge.</summary>
        public string GetOriginalCatalogPath(AnimGridItem item) => GetOriginalPath(item).Display;

        /// <summary>Legacy alias for gender context.</summary>
        public string? GetContextName(AnimGridItem item) => GetGenderContextName(item);

        public List<AnimDisplayEntry> GetEntries(AnimViewNode node)
        {
            EnsureTree();
            if (_entriesByNodeId.TryGetValue(node.Id, out var cached))
                return cached;

            var entries = BuildEntries(node);
            _entriesByNodeId[node.Id] = entries;
            return entries;
        }

        /// <summary>Subcategory bucket keys for group-merge review, respecting existing category merges.</summary>
        public Dictionary<AnimCatalogRef, string> BuildSubcategoryBucketKeyMap(IReadOnlyCollection<int> groupIds)
        {
            var result = new Dictionary<AnimCatalogRef, string>();
            if (groupIds == null || groupIds.Count == 0)
                return result;

            var categoryMerges = new List<AnimTreeMergeRule>();
            var coveredCategories = new HashSet<long>();
            foreach (AnimTreeMergeRule rule in _store.TreeMerges)
            {
                if (rule.Kind != AnimTreeMergeKind.Category)
                    continue;
                categoryMerges.Add(rule);
                for (int s = 0; s < rule.Sources.Count; s++)
                {
                    AnimCatalogRef src = rule.Sources[s];
                    coveredCategories.Add(CategoryKey(src.Group, src.Category));
                }
            }

            IList<AnimCategoryNode> raw = _catalog.RootGroups;
            var unitScratch = new List<SubcategoryUnit>();
            var siblingScratch = new List<(int SortCategoryId, string Name)>();
            foreach (int groupId in groupIds)
            {
                AnimCategoryNode? rawGroup = FindRawGroup(raw, groupId);
                if (rawGroup == null)
                    continue;

                unitScratch.Clear();
                CollectSubcategoryUnitsForGroup(rawGroup, categoryMerges, coveredCategories, null, unitScratch);
                siblingScratch.Clear();
                for (int ui = 0; ui < unitScratch.Count; ui++)
                    siblingScratch.Add((unitScratch[ui].SortCategoryId, unitScratch[ui].BucketName));
                siblingScratch.Sort((a, b) => CompareTranslatedCategoryNames(a.Name, b.Name, a.SortCategoryId, b.SortCategoryId));

                for (int ui = 0; ui < unitScratch.Count; ui++)
                {
                    SubcategoryUnit unit = unitScratch[ui];
                    string key = AnimGroupHeuristics.BuildSubcategoryMergeBucketKey(
                        unit.SortCategoryId,
                        unit.BucketName,
                        siblingScratch);
                    for (int ci = 0; ci < unit.SourceCategories.Count; ci++)
                        result[unit.SourceCategories[ci]] = key;
                }
            }
            return result;
        }

        /// <summary>Subcategory bucket key for one category under a group merge (matches <see cref="BuildMergedGroupNode"/>).</summary>
        public bool TryResolveSubcategoryBucketKey(
            AnimTreeMergeRule groupMergeRule,
            AnimCatalogRef categoryRef,
            out string bucketKey)
        {
            bucketKey = string.Empty;
            if (groupMergeRule.Kind != AnimTreeMergeKind.Group)
                return false;

            var categoryMerges = new List<AnimTreeMergeRule>();
            var coveredCategories = new HashSet<long>();
            foreach (AnimTreeMergeRule rule in _store.TreeMerges)
            {
                if (rule.Kind != AnimTreeMergeKind.Category)
                    continue;
                categoryMerges.Add(rule);
                for (int s = 0; s < rule.Sources.Count; s++)
                {
                    AnimCatalogRef src = rule.Sources[s];
                    coveredCategories.Add(CategoryKey(src.Group, src.Category));
                }
            }

            IList<AnimCategoryNode> raw = _catalog.RootGroups;
            AnimCategoryNode? rawGroup = FindRawGroup(raw, categoryRef.Group);
            if (rawGroup == null)
                return false;

            var unitScratch = new List<SubcategoryUnit>();
            var siblingScratch = new List<(int SortCategoryId, string Name)>();
            CollectSubcategoryUnitsForGroup(rawGroup, categoryMerges, coveredCategories, groupMergeRule, unitScratch);
            siblingScratch.Clear();
            for (int ui = 0; ui < unitScratch.Count; ui++)
                siblingScratch.Add((unitScratch[ui].SortCategoryId, unitScratch[ui].BucketName));
            siblingScratch.Sort((a, b) => CompareTranslatedCategoryNames(a.Name, b.Name, a.SortCategoryId, b.SortCategoryId));

            for (int ui = 0; ui < unitScratch.Count; ui++)
            {
                SubcategoryUnit unit = unitScratch[ui];
                for (int ci = 0; ci < unit.SourceCategories.Count; ci++)
                {
                    if (!AnimCatalogRefUtil.SameCategory(unit.SourceCategories[ci], categoryRef))
                        continue;
                    string key = AnimGroupHeuristics.BuildSubcategoryMergeBucketKey(
                        unit.SortCategoryId,
                        unit.BucketName,
                        siblingScratch);
                    bucketKey = groupMergeRule.ResolveSubcategoryBucketKey(key);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Raw items for a node, used by the grouping flow before any grouping is applied.</summary>
        public List<AnimGridItem> GetRawItems(AnimViewNode node)
        {
            EnsureTree();
            var list = new List<AnimGridItem>();
            CollectItems(node, list);
            return list;
        }

        public bool TryGetCachedEntries(string nodeId, out List<AnimDisplayEntry> entries) =>
            _entriesByNodeId.TryGetValue(nodeId, out entries!);

        /// <summary>Scans displayed items in a category leaf; returns on first match (no entry-list build).</summary>
        public bool CategoryContainsItemMatch(AnimViewNode categoryNode, Predicate<AnimGridItem> match)
        {
            EnsureTree();
            if (categoryNode.IsGroup)
                return false;

            AnimTreeMergeRule? mergeRule = null;
            if (categoryNode.MergeRuleId.Length > 0)
                mergeRule = _store.FindTreeMerge(categoryNode.MergeRuleId);

            for (int i = 0; i < categoryNode.SourceCategories.Count; i++)
            {
                AnimCatalogRef src = categoryNode.SourceCategories[i];
                var items = _catalog.GetItemsForSelection(src.Group, src.Category);
                for (int j = 0; j < items.Count; j++)
                {
                    AnimGridItem item = items[j];
                    if (!ShouldIncludeItem(item, categoryNode, mergeRule))
                        continue;
                    if (match(item))
                        return true;
                }
            }

            return false;
        }

        private void RegisterCurrentPath(AnimCatalogRef animationRef, AnimViewNode node)
        {
            if (node.PlacementKind == AnimNodePlacementKind.ResidualExcluded)
                return;
            if (_store.IsAnimationExcludedFromMerge(animationRef))
                return;

            if (!_currentPathByRef.TryGetValue(animationRef, out AnimCatalogPath? path))
            {
                path = new AnimCatalogPath();
                _currentPathByRef[animationRef] = path;
            }
            path.SetFrom(node.DisplayPathSegments);
        }

        private void CollectItems(AnimViewNode node, List<AnimGridItem> output)
        {
            if (node.IsGroup)
            {
                for (int i = 0; i < node.Children.Count; i++)
                    AddCategoryItems(node.Children[i], output);
            }
            else
            {
                AddCategoryItems(node, output);
            }
        }

        private void AddCategoryItems(AnimViewNode categoryNode, List<AnimGridItem> output)
        {
            AnimTreeMergeRule? mergeRule = null;
            if (categoryNode.MergeRuleId.Length > 0)
                mergeRule = _store.FindTreeMerge(categoryNode.MergeRuleId);

            for (int i = 0; i < categoryNode.SourceCategories.Count; i++)
            {
                AnimCatalogRef src = categoryNode.SourceCategories[i];
                var items = _catalog.GetItemsForSelection(src.Group, src.Category);
                for (int j = 0; j < items.Count; j++)
                {
                    AnimGridItem item = items[j];
                    if (ShouldIncludeItem(item, categoryNode, mergeRule))
                        output.Add(item);
                }
            }
        }

        private bool ShouldIncludeItem(AnimGridItem item, AnimViewNode categoryNode, AnimTreeMergeRule? mergeRule)
        {
            if (_hideNonStudioCatalogAnimations && !item.IsStudioListed)
                return false;

            var animationRef = new AnimCatalogRef(item.Group, item.Category, item.No);
            var categoryRef = AnimCatalogRefUtil.CategoryRef(item.Group, item.Category);

            if (categoryNode.PlacementKind == AnimNodePlacementKind.ResidualExcluded)
                return IsResidualItem(animationRef, categoryRef, mergeRule);

            if (categoryNode.PlacementKind == AnimNodePlacementKind.MergedContent && mergeRule != null)
            {
                if (_store.IsCategoryExcludedFromRule(mergeRule, categoryRef))
                    return false;
                if (IsAnimationExcluded(mergeRule, animationRef))
                    return false;
            }

            return true;
        }

        private static bool IsAnimationExcluded(AnimTreeMergeRule rule, AnimCatalogRef animationRef)
        {
            for (int i = 0; i < rule.ExcludedAnimationRefs.Count; i++)
            {
                if (rule.ExcludedAnimationRefs[i].Equals(animationRef))
                    return true;
            }
            return false;
        }

        private bool IsResidualItem(AnimCatalogRef animationRef, AnimCatalogRef categoryRef, AnimTreeMergeRule? mergeRule)
        {
            if (mergeRule == null)
                return false;

            if (_store.IsCategoryExcludedFromRule(mergeRule, categoryRef))
                return true;

            return IsAnimationExcluded(mergeRule, animationRef);
        }

        private AnimDisplayGroup ResolveDisplayGroup(AnimDisplayGroupData data)
        {
            var group = new AnimDisplayGroup { Id = data.Id, Name = data.Name };
            int minSort = int.MaxValue;
            for (int i = 0; i < data.Members.Count; i++)
            {
                var member = data.Members[i];
                AnimGridItem? item = _catalog.TryGetItem(member.Ref);
                if (item == null)
                    continue;
                group.Slots.Add(new AnimGroupSlot
                {
                    Item = item,
                    Phase = member.Phase,
                    Gender = member.Gender,
                    GenderOrdinal = member.GenderOrdinal
                });
                if (item.Sort < minSort)
                    minSort = item.Sort;
            }
            group.Sort = minSort == int.MaxValue ? 0 : minSort;
            group.Recompute();
            return group;
        }

        private static int CompareEntries(AnimDisplayEntry a, AnimDisplayEntry b)
        {
            int cmp = string.Compare(GetEntrySortLabel(a), GetEntrySortLabel(b), StringComparison.CurrentCultureIgnoreCase);
            if (cmp != 0)
                return cmp;
            int na = a.Single?.No ?? (a.Group != null && a.Group.Slots.Count > 0 ? a.Group.Slots[0].Item.No : 0);
            int nb = b.Single?.No ?? (b.Group != null && b.Group.Slots.Count > 0 ? b.Group.Slots[0].Item.No : 0);
            return na.CompareTo(nb);
        }

        private static string GetEntrySortLabel(AnimDisplayEntry entry)
        {
            if (entry.Group != null)
                return StudioAutoTranslation.Resolve(entry.Group.Name);
            if (entry.Single != null)
                return entry.Single.GetDisplayLabel();
            return string.Empty;
        }

        private static int CompareTranslatedCategoryNames(string nameA, string nameB, int tieBreakA, int tieBreakB)
        {
            int cmp = string.Compare(
                StudioAutoTranslation.Resolve(nameA),
                StudioAutoTranslation.Resolve(nameB),
                StringComparison.CurrentCultureIgnoreCase);
            if (cmp != 0)
                return cmp;
            return tieBreakA.CompareTo(tieBreakB);
        }

        private static int CompareSubcategoryUnits(SubcategoryUnit a, SubcategoryUnit b) =>
            CompareTranslatedCategoryNames(a.BucketName, b.BucketName, a.SortCategoryId, b.SortCategoryId);

        private static int CompareViewNodesByDisplayName(AnimViewNode a, AnimViewNode b)
        {
            int cmp = string.Compare(a.GetDisplayLabel(), b.GetDisplayLabel(), StringComparison.CurrentCultureIgnoreCase);
            if (cmp != 0)
                return cmp;
            return string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        }

        private void SortDisplayTreeByName()
        {
            _rootGroups.Sort(CompareViewNodesByDisplayName);
            for (int i = 0; i < _rootGroups.Count; i++)
                SortViewNodeChildrenRecursive(_rootGroups[i]);
        }

        private static void SortViewNodeChildrenRecursive(AnimViewNode node)
        {
            if (node.Children.Count <= 1)
                return;
            node.Children.Sort(CompareViewNodesByDisplayName);
            for (int i = 0; i < node.Children.Count; i++)
                SortViewNodeChildrenRecursive(node.Children[i]);
        }

        private List<AnimDisplayEntry> BuildEntries(AnimViewNode node)
        {
            var rawItems = new List<AnimGridItem>();
            CollectItems(node, rawItems);

            var entries = new List<AnimDisplayEntry>();
            var seenGroups = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rawItems.Count; i++)
            {
                AnimGridItem item = rawItems[i];
                var animationRef = new AnimCatalogRef(item.Group, item.Category, item.No);
                RegisterCurrentPath(animationRef, node);

                var data = _store.GetGroupForRef(animationRef);
                if (data != null)
                {
                    if (seenGroups.Add(data.Id))
                        entries.Add(AnimDisplayEntry.ForGroup(ResolveDisplayGroup(data)));
                }
                else
                {
                    entries.Add(AnimDisplayEntry.ForSingle(item));
                }
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private void EnsureTree()
        {
            if (_treeBuilt)
                return;

            ResetTreeCollections();
            IList<AnimCategoryNode> raw = _catalog.RootGroups;
            BuildContextNames(raw);
            BuildTreeStructure(raw);
            RegisterResidualCurrentPathsSync();
            _treeBuilt = true;
        }

        private void ResetTreeCollections()
        {
            _rootGroups.Clear();
            _entriesByNodeId.Clear();
            _contextNameByRef.Clear();
            _groupNameByRef.Clear();
            _categoryNameByRef.Clear();
            _originalPathByRef.Clear();
            _currentPathByRef.Clear();
        }

        private IEnumerator BuildTreeCoroutine(int categoriesPerSlice)
        {
            if (_treeBuilt)
                yield break;

            ResetTreeCollections();
            IList<AnimCategoryNode> raw = _catalog.RootGroups;
            _warmupCategoriesTotal = CountRawCategories(raw);
            _warmupCategoriesDone = 0;
            _warmupResidualCategoriesTotal = 0;
            _warmupResidualCategoriesDone = 0;
            UpdateWarmupProgress(treePhaseOnly: true);
            yield return BuildContextNamesCoroutine(raw, categoriesPerSlice);
            if (_treeBuilt)
                yield break;

            BuildTreeStructure(raw);
            _warmupResidualCategoriesTotal = CountResidualCategories();
            _warmupResidualCategoriesDone = 0;
            UpdateWarmupProgress(treePhaseOnly: true);
            yield return RegisterResidualCurrentPathsCoroutine();
            if (_treeBuilt)
                yield break;

            _treeBuilt = true;
            UpdateWarmupProgress(treePhaseOnly: true);
        }

        private int CountRawCategories(IList<AnimCategoryNode> raw)
        {
            int total = 0;
            for (int gi = 0; gi < raw.Count; gi++)
                total += raw[gi].Children.Count;
            return total;
        }

        private int CountResidualCategories()
        {
            if (_residualGroupsForPathRegistration == null)
                return 0;
            int total = 0;
            foreach (var kvp in _residualGroupsForPathRegistration)
                total += kvp.Value.Children.Count;
            return total;
        }

        private void UpdateWarmupProgress(bool treePhaseOnly)
        {
            float treePart = 0f;
            if (_warmupCategoriesTotal > 0)
                treePart += 0.85f * (_warmupCategoriesDone / (float)_warmupCategoriesTotal);
            else
                treePart += 0.85f;

            if (_warmupResidualCategoriesTotal > 0)
                treePart += 0.15f * (_warmupResidualCategoriesDone / (float)_warmupResidualCategoriesTotal);
            else if (_warmupCategoriesDone >= _warmupCategoriesTotal && _warmupCategoriesTotal >= 0)
                treePart += 0.15f;

            treePart = Mathf.Clamp01(treePart);
            if (treePhaseOnly)
            {
                WarmupProgress = treePart * 0.35f;
                return;
            }

            float cachePart = _warmupNodesTotal > 0
                ? _warmupNodesDone / (float)_warmupNodesTotal
                : 1f;
            WarmupProgress = Mathf.Clamp01(0.35f + cachePart * 0.65f);
        }

        private IEnumerator BuildContextNamesCoroutine(IList<AnimCategoryNode> raw, int categoriesPerSlice)
        {
            int sinceYield = 0;
            for (int gi = 0; gi < raw.Count; gi++)
            {
                if (_treeBuilt)
                    yield break;

                AnimCategoryNode group = raw[gi];
                for (int ci = 0; ci < group.Children.Count; ci++)
                {
                    if (_treeBuilt)
                        yield break;

                    RegisterContextForCategory(group, group.Children[ci]);
                    _warmupCategoriesDone++;
                    UpdateWarmupProgress(treePhaseOnly: true);
                    sinceYield++;
                    if (sinceYield >= categoriesPerSlice)
                    {
                        sinceYield = 0;
                        yield return null;
                    }
                }
            }
        }

        private IEnumerator PrewarmEntriesCoroutine(int nodesPerSlice)
        {
            if (!_treeBuilt)
                yield break;

            CollectAllSelectableNodes(_warmupNodeScratch);
            _warmupNodesTotal = _warmupNodeScratch.Count;
            _warmupNodesDone = 0;
            UpdateWarmupProgress(treePhaseOnly: false);
            int sinceYield = 0;
            for (int i = 0; i < _warmupNodeScratch.Count; i++)
            {
                AnimViewNode node = _warmupNodeScratch[i];
                if (!_entriesByNodeId.ContainsKey(node.Id))
                    _entriesByNodeId[node.Id] = BuildEntries(node);

                _warmupNodesDone++;
                UpdateWarmupProgress(treePhaseOnly: false);
                sinceYield++;
                if (sinceYield >= nodesPerSlice)
                {
                    sinceYield = 0;
                    yield return null;
                }
            }
        }

        private void CollectAllSelectableNodes(List<AnimViewNode> output)
        {
            output.Clear();
            for (int i = 0; i < _rootGroups.Count; i++)
                CollectSelectableNodesRecursive(_rootGroups[i], output);
        }

        private static void CollectSelectableNodesRecursive(AnimViewNode node, List<AnimViewNode> output)
        {
            output.Add(node);
            for (int i = 0; i < node.Children.Count; i++)
                CollectSelectableNodesRecursive(node.Children[i], output);
        }

        private void RegisterContextForCategory(AnimCategoryNode group, AnimCategoryNode category)
        {
            string context = AnimGroupHeuristics.DetectGenderFromContext(category.Name) != AnimGender.Unknown
                ? category.Name
                : group.Name;
            var items = _catalog.GetItemsForSelection(group.GroupId, category.CategoryId);
            for (int ii = 0; ii < items.Count; ii++)
            {
                AnimGridItem item = items[ii];
                var reference = new AnimCatalogRef(item.Group, item.Category, item.No);
                _contextNameByRef[reference] = context;
                _groupNameByRef[reference] = group.Name;
                _categoryNameByRef[reference] = category.Name;
                _originalPathByRef[reference] = AnimCatalogPath.FromPair(group.Name, category.Name);
            }
        }

        private void BuildContextNames(IList<AnimCategoryNode> raw)
        {
            for (int gi = 0; gi < raw.Count; gi++)
            {
                AnimCategoryNode group = raw[gi];
                for (int ci = 0; ci < group.Children.Count; ci++)
                    RegisterContextForCategory(group, group.Children[ci]);
            }
        }

        private void BuildTreeStructure(IList<AnimCategoryNode> raw)
        {
            var coveredGroups = new HashSet<int>();
            var coveredCategories = new HashSet<long>();
            var groupMerges = new List<AnimTreeMergeRule>();
            var categoryMerges = new List<AnimTreeMergeRule>();

            foreach (var rule in _store.TreeMerges)
            {
                if (rule.Kind == AnimTreeMergeKind.Group)
                {
                    groupMerges.Add(rule);
                    foreach (var src in rule.Sources)
                        coveredGroups.Add(src.Group);
                }
                else
                {
                    categoryMerges.Add(rule);
                    foreach (var src in rule.Sources)
                        coveredCategories.Add(CategoryKey(src.Group, src.Category));
                }
            }

            var residualGroupsById = new Dictionary<int, AnimViewNode>();

            foreach (var rule in groupMerges)
            {
                AnimViewNode merged = BuildMergedGroupNode(rule, raw, categoryMerges, coveredCategories);
                if (merged.Children.Count > 0)
                    _rootGroups.Add(merged);
                MergeResidualGroups(residualGroupsById, BuildResidualGroupsForRule(rule, raw));
            }

            foreach (var rule in categoryMerges)
                MergeResidualGroups(residualGroupsById, BuildResidualGroupsForRule(rule, raw));

            foreach (var kvp in residualGroupsById)
                _rootGroups.Add(kvp.Value);

            for (int i = 0; i < raw.Count; i++)
            {
                AnimCategoryNode rawGroup = raw[i];
                if (coveredGroups.Contains(rawGroup.GroupId))
                    continue;
                _rootGroups.Add(BuildGroupNode(rawGroup, categoryMerges, coveredCategories));
            }

            _residualGroupsForPathRegistration = residualGroupsById;
            SortDisplayTreeByName();
            if (_hideNonStudioCatalogAnimations)
            {
                PruneEmptyDisplayTree();
                SortDisplayTreeByName();
            }
        }

        private void PruneEmptyDisplayTree()
        {
            for (int gi = _rootGroups.Count - 1; gi >= 0; gi--)
            {
                AnimViewNode groupNode = _rootGroups[gi];
                if (groupNode.IsGroup)
                    PruneEmptyCategoryChildren(groupNode);
                if (groupNode.IsGroup && groupNode.Children.Count == 0)
                    _rootGroups.RemoveAt(gi);
            }
        }

        private void PruneEmptyCategoryChildren(AnimViewNode groupNode)
        {
            for (int ci = groupNode.Children.Count - 1; ci >= 0; ci--)
            {
                if (!CategoryNodeHasVisibleItems(groupNode.Children[ci]))
                    groupNode.Children.RemoveAt(ci);
            }
        }

        private bool CategoryNodeHasVisibleItems(AnimViewNode categoryNode)
        {
            var scratch = new List<AnimGridItem>();
            AddCategoryItems(categoryNode, scratch);
            return scratch.Count > 0;
        }

        private Dictionary<int, AnimViewNode>? _residualGroupsForPathRegistration;

        private void RegisterResidualCurrentPathsSync()
        {
            if (_residualGroupsForPathRegistration == null)
                return;
            foreach (var kvp in _residualGroupsForPathRegistration)
            {
                AnimViewNode groupNode = kvp.Value;
                for (int ci = 0; ci < groupNode.Children.Count; ci++)
                    RegisterResidualPathsForCategoryNode(groupNode.Children[ci]);
            }
            _residualGroupsForPathRegistration = null;
        }

        private IEnumerator RegisterResidualCurrentPathsCoroutine()
        {
            if (_treeBuilt)
                yield break;
            if (_residualGroupsForPathRegistration == null)
                yield break;

            int sinceYield = 0;
            foreach (var kvp in _residualGroupsForPathRegistration)
            {
                AnimViewNode groupNode = kvp.Value;
                for (int ci = 0; ci < groupNode.Children.Count; ci++)
                {
                    RegisterResidualPathsForCategoryNode(groupNode.Children[ci]);
                    _warmupResidualCategoriesDone++;
                    UpdateWarmupProgress(treePhaseOnly: true);
                    sinceYield++;
                    if (sinceYield >= DefaultCategoriesPerWarmupSlice)
                    {
                        sinceYield = 0;
                        yield return null;
                    }
                }
            }

            _residualGroupsForPathRegistration = null;
        }

        private void RegisterResidualPathsForCategoryNode(AnimViewNode catNode)
        {
            var items = new List<AnimGridItem>();
            CollectItems(catNode, items);
            for (int ii = 0; ii < items.Count; ii++)
            {
                AnimGridItem item = items[ii];
                var animationRef = new AnimCatalogRef(item.Group, item.Category, item.No);
                if (!_originalPathByRef.TryGetValue(animationRef, out AnimCatalogPath? original))
                    continue;
                if (!_currentPathByRef.TryGetValue(animationRef, out AnimCatalogPath? current))
                {
                    current = new AnimCatalogPath();
                    _currentPathByRef[animationRef] = current;
                }
                current.CopyFrom(original);
            }
        }

        private static void MergeResidualGroups(Dictionary<int, AnimViewNode> target, List<AnimViewNode> incoming)
        {
            for (int i = 0; i < incoming.Count; i++)
            {
                AnimViewNode node = incoming[i];
                if (node.RawGroupId < 0)
                    continue;
                if (!target.TryGetValue(node.RawGroupId, out AnimViewNode? existing))
                {
                    target[node.RawGroupId] = node;
                    continue;
                }
                for (int c = 0; c < node.Children.Count; c++)
                {
                    AnimViewNode child = node.Children[c];
                    if (!ContainsChildCategory(existing, child))
                        existing.Children.Add(child);
                }
            }
        }

        private static bool ContainsChildCategory(AnimViewNode groupNode, AnimViewNode categoryNode)
        {
            if (categoryNode.SourceCategories.Count == 0)
                return false;
            AnimCatalogRef key = categoryNode.SourceCategories[0];
            for (int i = 0; i < groupNode.Children.Count; i++)
            {
                AnimViewNode existing = groupNode.Children[i];
                for (int s = 0; s < existing.SourceCategories.Count; s++)
                {
                    if (AnimCatalogRefUtil.SameCategory(existing.SourceCategories[s], key))
                        return true;
                }
            }
            return false;
        }

        private AnimViewNode BuildGroupNode(
            AnimCategoryNode rawGroup,
            List<AnimTreeMergeRule> categoryMerges,
            HashSet<long> coveredCategories)
        {
            var node = new AnimViewNode
            {
                Id = "g:" + rawGroup.GroupId,
                Depth = 0,
                IsGroup = true,
                IsExpanded = rawGroup.IsExpanded,
                RawGroupId = rawGroup.GroupId,
                PlacementKind = AnimNodePlacementKind.Normal
            };
            SetGroupDisplay(node, rawGroup.GroupId, rawGroup.Name);

            foreach (var rule in categoryMerges)
            {
                if (rule.Sources.Count == 0 || rule.Sources[0].Group != rawGroup.GroupId)
                    continue;
                var merged = new AnimViewNode
                {
                    Id = "cm:" + rule.Id,
                    Depth = 1,
                    IsMerged = true,
                    MergeRuleId = rule.Id,
                    PlacementKind = AnimNodePlacementKind.MergedContent
                };
                SetMergedCategoryDisplay(merged, rawGroup.GroupId, rawGroup.Name, rule.Name);
                foreach (var src in rule.Sources)
                {
                    if (_store.IsCategoryExcludedFromRule(rule, AnimCatalogRefUtil.CategoryRef(src.Group, src.Category)))
                        continue;
                    merged.SourceCategories.Add(new AnimCatalogRef(src.Group, src.Category, -1));
                }
                if (merged.SourceCategories.Count >= 2)
                    node.Children.Add(merged);
            }

            for (int ci = 0; ci < rawGroup.Children.Count; ci++)
            {
                AnimCategoryNode category = rawGroup.Children[ci];
                if (coveredCategories.Contains(CategoryKey(rawGroup.GroupId, category.CategoryId)))
                    continue;
                var catNode = new AnimViewNode
                {
                    Id = "c:" + rawGroup.GroupId + "." + category.CategoryId,
                    Depth = 1,
                    PlacementKind = AnimNodePlacementKind.Normal
                };
                SetCategoryDisplay(catNode, rawGroup.GroupId, category.CategoryId, rawGroup.Name, category.Name);
                catNode.SourceCategories.Add(new AnimCatalogRef(rawGroup.GroupId, category.CategoryId, -1));
                node.Children.Add(catNode);
            }

            return node;
        }

        private readonly struct SubcategoryUnit
        {
            public readonly int SortCategoryId;
            public readonly string BucketName;
            public readonly List<AnimCatalogRef> SourceCategories;

            public SubcategoryUnit(int sortCategoryId, string bucketName, List<AnimCatalogRef> sourceCategories)
            {
                SortCategoryId = sortCategoryId;
                BucketName = bucketName;
                SourceCategories = sourceCategories;
            }
        }

        /// <summary>Display-level subcategory rows for one top-level group: one row per raw category
        /// or per existing category merge (<c>cm:</c>), never both.</summary>
        private void CollectSubcategoryUnitsForGroup(
            AnimCategoryNode rawGroup,
            IList<AnimTreeMergeRule> categoryMerges,
            HashSet<long> coveredCategories,
            AnimTreeMergeRule? groupMergeRule,
            List<SubcategoryUnit> output)
        {
            int groupId = rawGroup.GroupId;
            var consumedCategories = new HashSet<long>();

            for (int ri = 0; ri < categoryMerges.Count; ri++)
            {
                AnimTreeMergeRule cmRule = categoryMerges[ri];
                if (cmRule.Sources.Count == 0 || cmRule.Sources[0].Group != groupId)
                    continue;

                var activeSources = new List<AnimCatalogRef>();
                int minCategoryId = int.MaxValue;
                for (int si = 0; si < cmRule.Sources.Count; si++)
                {
                    AnimCatalogRef src = cmRule.Sources[si];
                    var catRef = AnimCatalogRefUtil.CategoryRef(src.Group, src.Category);
                    if (groupMergeRule != null && _store.IsCategoryExcludedFromRule(groupMergeRule, catRef))
                        continue;
                    activeSources.Add(catRef);
                    if (src.Category < minCategoryId)
                        minCategoryId = src.Category;
                }

                if (activeSources.Count >= 2)
                {
                    output.Add(new SubcategoryUnit(minCategoryId, cmRule.Name, activeSources));
                    for (int ai = 0; ai < activeSources.Count; ai++)
                        consumedCategories.Add(CategoryKey(activeSources[ai].Group, activeSources[ai].Category));
                    continue;
                }

                if (activeSources.Count == 1)
                {
                    AnimCatalogRef lone = activeSources[0];
                    AnimCategoryNode? loneCategory = FindRawCategory(rawGroup, lone.Category);
                    string bucketName = loneCategory != null ? loneCategory.Name : cmRule.Name;
                    output.Add(new SubcategoryUnit(lone.Category, bucketName, activeSources));
                    consumedCategories.Add(CategoryKey(lone.Group, lone.Category));
                }
            }

            for (int ci = 0; ci < rawGroup.Children.Count; ci++)
            {
                AnimCategoryNode category = rawGroup.Children[ci];
                long catKey = CategoryKey(groupId, category.CategoryId);
                if (coveredCategories.Contains(catKey) || consumedCategories.Contains(catKey))
                    continue;

                var catRef = AnimCatalogRefUtil.CategoryRef(groupId, category.CategoryId);
                if (groupMergeRule != null && _store.IsCategoryExcludedFromRule(groupMergeRule, catRef))
                    continue;

                output.Add(new SubcategoryUnit(
                    category.CategoryId,
                    category.Name,
                    new List<AnimCatalogRef> { catRef }));
            }

            output.Sort(CompareSubcategoryUnits);
        }

        private AnimViewNode BuildMergedGroupNode(
            AnimTreeMergeRule rule,
            IList<AnimCategoryNode> raw,
            IList<AnimTreeMergeRule> categoryMerges,
            HashSet<long> coveredCategories)
        {
            var node = new AnimViewNode
            {
                Id = "mg:" + rule.Id,
                Name = rule.Name,
                Depth = 0,
                IsGroup = true,
                IsMerged = true,
                MergeRuleId = rule.Id,
                PlacementKind = AnimNodePlacementKind.MergedContent
            };
            node.DisplayPathSegments.Add(rule.Name);

            var buckets = new Dictionary<string, SubcategoryBucketAccum>(StringComparer.OrdinalIgnoreCase);
            var orderedKeys = new List<string>();
            var unitScratch = new List<SubcategoryUnit>();
            var siblingScratch = new List<(int SortCategoryId, string Name)>();

            foreach (var src in rule.Sources)
            {
                AnimCategoryNode? rawGroup = FindRawGroup(raw, src.Group);
                if (rawGroup == null)
                    continue;

                unitScratch.Clear();
                CollectSubcategoryUnitsForGroup(rawGroup, categoryMerges, coveredCategories, rule, unitScratch);
                siblingScratch.Clear();
                for (int ui = 0; ui < unitScratch.Count; ui++)
                    siblingScratch.Add((unitScratch[ui].SortCategoryId, unitScratch[ui].BucketName));
                siblingScratch.Sort((a, b) => CompareTranslatedCategoryNames(a.Name, b.Name, a.SortCategoryId, b.SortCategoryId));

                for (int ui = 0; ui < unitScratch.Count; ui++)
                {
                    SubcategoryUnit unit = unitScratch[ui];
                    string key = AnimGroupHeuristics.BuildSubcategoryMergeBucketKey(
                        unit.SortCategoryId,
                        unit.BucketName,
                        siblingScratch);
                    key = rule.ResolveSubcategoryBucketKey(key);
                    if (!buckets.TryGetValue(key, out SubcategoryBucketAccum? accum))
                    {
                        accum = new SubcategoryBucketAccum(key);
                        buckets[key] = accum;
                        orderedKeys.Add(key);
                    }
                    accum.Units.Add(unit);
                }
            }

            for (int i = 0; i < orderedKeys.Count; i++)
            {
                SubcategoryBucketAccum accum = buckets[orderedKeys[i]];
                if (accum.Units.Count == 0)
                    continue;

                if (CountDistinctSourceGroups(accum.Units) >= 2)
                {
                    AnimViewNode? mgc = CreateCrossGroupSubcategoryNode(rule, accum);
                    if (mgc != null)
                        node.Children.Add(mgc);
                    continue;
                }

                accum.Units.Sort(CompareSubcategoryUnits);
                for (int ui = 0; ui < accum.Units.Count; ui++)
                    AddSubcategoryUnitUnderMergedGroup(node, rule.Name, accum.Units[ui], raw, categoryMerges);
            }

            return node;
        }

        private sealed class SubcategoryBucketAccum
        {
            public readonly string Key;
            public readonly List<SubcategoryUnit> Units = new List<SubcategoryUnit>();

            public SubcategoryBucketAccum(string key) => Key = key;
        }

        private static int CountDistinctSourceGroups(List<SubcategoryUnit> units)
        {
            var groups = new HashSet<int>();
            for (int ui = 0; ui < units.Count; ui++)
            {
                SubcategoryUnit unit = units[ui];
                for (int ci = 0; ci < unit.SourceCategories.Count; ci++)
                    groups.Add(unit.SourceCategories[ci].Group);
            }
            return groups.Count;
        }

        private AnimViewNode? CreateCrossGroupSubcategoryNode(AnimTreeMergeRule rule, SubcategoryBucketAccum accum)
        {
            SubcategoryUnit first = accum.Units[0];
            AnimGroupHeuristics.DetectGender(first.BucketName, out string displayName);
            displayName = displayName.Trim();
            string builtName = AnimGroupHeuristics.FormatMergedSubcategoryDisplayName(
                displayName.Length > 0 ? displayName : first.BucketName,
                accum.Key);

            var merged = new AnimViewNode
            {
                Id = "mgc:" + rule.Id + ":" + accum.Key,
                Depth = 1,
                IsMerged = true,
                MergeRuleId = rule.Id,
                PlacementKind = AnimNodePlacementKind.MergedContent
            };
            SetMergedSubcategoryDisplay(merged, rule.Name, builtName);

            for (int ui = 0; ui < accum.Units.Count; ui++)
            {
                SubcategoryUnit unit = accum.Units[ui];
                for (int sci = 0; sci < unit.SourceCategories.Count; sci++)
                    merged.SourceCategories.Add(unit.SourceCategories[sci]);
            }

            return merged.SourceCategories.Count > 0 ? merged : null;
        }

        private void AddSubcategoryUnitUnderMergedGroup(
            AnimViewNode mergedGroupNode,
            string mergedGroupName,
            SubcategoryUnit unit,
            IList<AnimCategoryNode> raw,
            IList<AnimTreeMergeRule> categoryMerges)
        {
            if (unit.SourceCategories.Count >= 2)
            {
                AnimTreeMergeRule? cmRule = FindCategoryMergeForSources(categoryMerges, unit.SourceCategories);
                var child = new AnimViewNode
                {
                    Id = cmRule != null ? "cm:" + cmRule.Id : "c:" + unit.SourceCategories[0].Group + "." + unit.SourceCategories[0].Category,
                    Depth = 1,
                    IsMerged = cmRule != null,
                    MergeRuleId = cmRule?.Id ?? string.Empty,
                    PlacementKind = cmRule != null
                        ? AnimNodePlacementKind.MergedContent
                        : AnimNodePlacementKind.Normal
                };
                for (int ci = 0; ci < unit.SourceCategories.Count; ci++)
                    child.SourceCategories.Add(unit.SourceCategories[ci]);
                SetSubcategoryUnderMergedGroupDisplay(child, mergedGroupName, unit.BucketName);
                mergedGroupNode.Children.Add(child);
                return;
            }

            if (unit.SourceCategories.Count != 1)
                return;

            AnimCatalogRef catRef = unit.SourceCategories[0];
            AnimCategoryNode? rawGroup = FindRawGroup(raw, catRef.Group);
            AnimCategoryNode? rawCategory = rawGroup != null ? FindRawCategory(rawGroup, catRef.Category) : null;
            string catalogName = rawCategory != null ? rawCategory.Name : unit.BucketName;

            var catNode = new AnimViewNode
            {
                Id = "c:" + catRef.Group + "." + catRef.Category,
                Depth = 1,
                PlacementKind = AnimNodePlacementKind.Normal
            };
            SetSubcategoryUnderMergedGroupDisplay(
                catNode,
                mergedGroupName,
                ResolveCategoryDisplayName(catRef.Group, catRef.Category, catalogName));
            catNode.SourceCategories.Add(catRef);
            mergedGroupNode.Children.Add(catNode);
        }

        private static AnimTreeMergeRule? FindCategoryMergeForSources(
            IList<AnimTreeMergeRule> categoryMerges,
            List<AnimCatalogRef> sources)
        {
            if (sources.Count < 2)
                return null;

            var want = new HashSet<AnimCatalogRef>();
            for (int i = 0; i < sources.Count; i++)
                want.Add(AnimCatalogRefUtil.CategoryRef(sources[i].Group, sources[i].Category));

            for (int ri = 0; ri < categoryMerges.Count; ri++)
            {
                AnimTreeMergeRule rule = categoryMerges[ri];
                if (rule.Sources.Count < 2)
                    continue;

                var have = new HashSet<AnimCatalogRef>();
                for (int si = 0; si < rule.Sources.Count; si++)
                {
                    AnimCatalogRef src = rule.Sources[si];
                    have.Add(AnimCatalogRefUtil.CategoryRef(src.Group, src.Category));
                }
                if (have.Count == want.Count && want.IsSubsetOf(have))
                    return rule;
            }
            return null;
        }

        private void SetSubcategoryUnderMergedGroupDisplay(AnimViewNode node, string mergedGroupName, string subcategoryName)
        {
            node.Name = ResolveTreeNodeDisplayName(node.Id, subcategoryName);
            node.DisplayPathSegments.Clear();
            node.DisplayPathSegments.Add(mergedGroupName);
            node.DisplayPathSegments.Add(node.Name);
        }

        private List<AnimViewNode> BuildResidualGroupsForRule(AnimTreeMergeRule rule, IList<AnimCategoryNode> raw)
        {
            var result = new List<AnimViewNode>();
            var byGroupId = new Dictionary<int, AnimViewNode>();

            void EnsureCategory(AnimCategoryNode rawGroup, AnimCategoryNode category)
            {
                if (!byGroupId.TryGetValue(rawGroup.GroupId, out AnimViewNode? groupNode))
                {
                    groupNode = new AnimViewNode
                    {
                        Id = "g:" + rawGroup.GroupId,
                        Depth = 0,
                        IsGroup = true,
                        RawGroupId = rawGroup.GroupId,
                        PlacementKind = AnimNodePlacementKind.Normal
                    };
                    SetGroupDisplay(groupNode, rawGroup.GroupId, rawGroup.Name);
                    byGroupId[rawGroup.GroupId] = groupNode;
                    result.Add(groupNode);
                }

                var catRef = AnimCatalogRefUtil.CategoryRef(rawGroup.GroupId, category.CategoryId);
                var probe = new AnimViewNode();
                probe.SourceCategories.Add(catRef);
                if (ContainsChildCategory(groupNode, probe))
                    return;

                var catNode = new AnimViewNode
                {
                    Id = "c:" + rawGroup.GroupId + "." + category.CategoryId,
                    Depth = 1,
                    MergeRuleId = rule.Id,
                    PlacementKind = AnimNodePlacementKind.ResidualExcluded
                };
                SetCategoryDisplay(catNode, rawGroup.GroupId, category.CategoryId, rawGroup.Name, category.Name);
                catNode.SourceCategories.Add(catRef);
                groupNode.Children.Add(catNode);
            }

            for (int i = 0; i < rule.ExcludedSources.Count; i++)
            {
                AnimCatalogRef excluded = rule.ExcludedSources[i];
                AnimCategoryNode? rawGroup = FindRawGroup(raw, excluded.Group);
                AnimCategoryNode? category = rawGroup != null ? FindRawCategory(rawGroup, excluded.Category) : null;
                if (rawGroup != null && category != null)
                    EnsureCategory(rawGroup, category);
            }

            for (int i = 0; i < rule.ExcludedAnimationRefs.Count; i++)
            {
                AnimCatalogRef animRef = rule.ExcludedAnimationRefs[i];
                AnimCategoryNode? rawGroup = FindRawGroup(raw, animRef.Group);
                AnimCategoryNode? category = rawGroup != null ? FindRawCategory(rawGroup, animRef.Category) : null;
                if (rawGroup != null && category != null)
                    EnsureCategory(rawGroup, category);
            }

            return result;
        }

        private static AnimCategoryNode? FindRawGroup(IList<AnimCategoryNode> raw, int groupId)
        {
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i].GroupId == groupId)
                    return raw[i];
            }
            return null;
        }

        private static AnimCategoryNode? FindRawCategory(AnimCategoryNode rawGroup, int categoryId)
        {
            for (int i = 0; i < rawGroup.Children.Count; i++)
            {
                if (rawGroup.Children[i].CategoryId == categoryId)
                    return rawGroup.Children[i];
            }
            return null;
        }

        private static long CategoryKey(int group, int category) => ((long)group << 32) | (uint)category;

        private string ResolveGroupDisplayName(int groupId, string catalogName) =>
            _store.ResolveCatalogName(AnimDisplayNameKeys.Group(groupId), catalogName);

        private string ResolveCategoryDisplayName(int groupId, int categoryId, string catalogName) =>
            _store.ResolveCatalogName(AnimDisplayNameKeys.Category(groupId, categoryId), catalogName);

        private string ResolveTreeNodeDisplayName(string nodeId, string builtName) =>
            _store.ResolveCatalogName(AnimDisplayNameKeys.TreeNode(nodeId), builtName);

        private void SetGroupDisplay(AnimViewNode node, int groupId, string catalogGroupName)
        {
            node.Name = ResolveGroupDisplayName(groupId, catalogGroupName);
            node.DisplayPathSegments.Clear();
            node.DisplayPathSegments.Add(node.Name);
        }

        private void SetCategoryDisplay(
            AnimViewNode node,
            int groupId,
            int categoryId,
            string catalogGroupName,
            string catalogCategoryName)
        {
            string groupDisplay = ResolveGroupDisplayName(groupId, catalogGroupName);
            node.Name = ResolveCategoryDisplayName(groupId, categoryId, catalogCategoryName);
            node.DisplayPathSegments.Clear();
            node.DisplayPathSegments.Add(groupDisplay);
            node.DisplayPathSegments.Add(node.Name);
        }

        private void SetMergedCategoryDisplay(AnimViewNode node, int groupId, string catalogGroupName, string ruleName)
        {
            string groupDisplay = ResolveGroupDisplayName(groupId, catalogGroupName);
            node.Name = ruleName;
            node.DisplayPathSegments.Clear();
            node.DisplayPathSegments.Add(groupDisplay);
            node.DisplayPathSegments.Add(ruleName);
        }

        private void SetMergedSubcategoryDisplay(AnimViewNode node, string mergeGroupName, string builtName)
        {
            node.Name = ResolveTreeNodeDisplayName(node.Id, builtName);
            node.DisplayPathSegments.Clear();
            node.DisplayPathSegments.Add(mergeGroupName);
            node.DisplayPathSegments.Add(node.Name);
        }

        private static bool TryParseGroupNodeId(string nodeId, out int groupId)
        {
            groupId = 0;
            if (!nodeId.StartsWith("g:", StringComparison.Ordinal))
                return false;
            return int.TryParse(nodeId.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out groupId);
        }

        private static bool TryParseCategoryNodeId(string nodeId, out int groupId, out int categoryId)
        {
            groupId = 0;
            categoryId = 0;
            if (!nodeId.StartsWith("c:", StringComparison.Ordinal))
                return false;
            int dot = nodeId.IndexOf('.', 2);
            if (dot < 0)
                return false;
            if (!int.TryParse(nodeId.Substring(2, dot - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out groupId))
                return false;
            return int.TryParse(nodeId.Substring(dot + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out categoryId);
        }
    }
}
