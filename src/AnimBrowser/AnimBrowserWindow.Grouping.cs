using System;
using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const float RoleButtonRowHeightBase = 18f;
        private const float ReviewPaneDefaultWidthBase = 340f;
        private float ReviewPaneDefaultWidth => AnimBrowserScale.Px(ReviewPaneDefaultWidthBase);

        // Tree multi-select (node ids), grid item multi-select (refs), group-card select (ids).
        private readonly HashSet<string> _selectedTreeNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<AnimCatalogRef> _selectedItemRefs = new HashSet<AnimCatalogRef>();
        private readonly HashSet<string> _selectedGroupIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _collapsedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, AnimPhase> _activePhaseByGroupId = new Dictionary<string, AnimPhase>(StringComparer.Ordinal);
        private bool _pendingUngroupConfirm;

        // Review docked window state.
        private bool _showReviewPane;
        private Rect _reviewWindowRect;
        private Vector2 _reviewScroll;
        private readonly List<AnimDisplayGroupData> _reviewGroups = new List<AnimDisplayGroupData>();
        private AnimTreeMergeRule? _pendingMergeRule;
        private bool _pendingMergeRuleAlreadyStored;
        private readonly List<AnimCatalogRef> _pendingMergeReinclude = new List<AnimCatalogRef>();
        private bool _pendingMgcBucketMerge;
        private string _pendingMgcBucketMergeRuleId = string.Empty;
        private string _pendingMgcBucketMergeTargetKey = string.Empty;
        private readonly List<string> _pendingMgcBucketMergeAliasKeys = new List<string>();
        private string _reviewHeading = string.Empty;
        private readonly List<ReviewSectionLayout> _reviewSections = new List<ReviewSectionLayout>();
        private bool _reviewSectionsDirty = true;
        private readonly List<ReviewGroupRowCache> _reviewGroupCaches = new List<ReviewGroupRowCache>();
        private readonly List<ReviewVirtualBlock> _reviewVirtualBlocks = new List<ReviewVirtualBlock>();
        private string[] _reviewSectionHeadings = Array.Empty<string>();
        private bool _reviewDisplayCachesDirty = true;
        private bool _reviewVirtualBlocksDirty = true;
        private float _reviewScrollViewportH;
        private readonly HashSet<string> _reviewCollapsedSectionKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<AnimCatalogRef> _reviewSkippedRefs = new HashSet<AnimCatalogRef>();
        private readonly HashSet<string> _reviewSinglesInMergeGroupIds = new HashSet<string>(StringComparer.Ordinal);

        private static readonly GUIContent GcReviewSkip = new GUIContent("Skip", "Keep at original category; do not group this animation.");
        private static readonly GUIContent GcReviewUnskip = new GUIContent("Undo", "Include this animation in the group again.");
        private static readonly GUIContent GcReviewSkipAll = new GUIContent("Skip all", "Keep every animation at its original category; do not create this group.");
        private static readonly GUIContent GcReviewSinglesInMerge = new GUIContent(
            "As singles",
            "Do not group these animations, but show them separately inside the merged category.");
        private static readonly GUIContent GcReviewRestoreGroup = new GUIContent("Restore group", "Include this proposal in the merge again as a grouped card.");

        private const float ReviewSectionHeaderHBase = 26f;
        private const float ReviewGroupHeaderHBase = 54f;
        private const float ReviewGroupActionRowHBase = 24f;
        private const float ReviewMemberRowHBase = 28f;
        private const float ReviewGroupPadHBase = 8f;

        private sealed class ReviewSectionLayout
        {
            public string SectionKey = string.Empty;
            public string SectionLabel = string.Empty;
            public readonly List<int> GroupIndices = new List<int>();
        }

        private sealed class ReviewGroupRowCache
        {
            public string TranslatedName = string.Empty;
            public bool SinglesInMerge;
            public bool AllSkipped;
            public readonly List<ReviewMemberRowCache> Members = new List<ReviewMemberRowCache>();
        }

        private sealed class ReviewMemberRowCache
        {
            public AnimGroupMemberData Member = null!;
            public float GenderButtonWidth;
            public float PhaseButtonWidth;
            public float SkipButtonWidth;
            public bool IsSkipped;
            public GUIContent NameContent = GUIContent.none;
            public GUIContent GenderContent = GUIContent.none;
            public GUIContent PhaseContent = GUIContent.none;
            public GUIContent SkipContent = GUIContent.none;
        }

        private sealed class ReviewVirtualBlock
        {
            public bool IsSection;
            public int SectionIndex;
            public int GroupIndex;
            public float Height;
        }

        // ---- Expansion ------------------------------------------------------

        private bool IsNodeExpanded(AnimViewNode node) => !_collapsedNodeIds.Contains(node.Id);

        private void ToggleNodeExpanded(AnimViewNode node)
        {
            if (!_collapsedNodeIds.Remove(node.Id))
                _collapsedNodeIds.Add(node.Id);
        }

        // ---- Selection ------------------------------------------------------

        private void OnTreeNodeClicked(AnimViewNode node, Event ev)
        {
            if (ev.control || ev.command)
            {
                if (!_selectedTreeNodeIds.Remove(node.Id))
                    _selectedTreeNodeIds.Add(node.Id);
                _selectedTreeNode = node;
                if (GetSingleSelectedTreeNode() == null)
                    CancelTreeRename();
                InvalidateContentViewCaches();
            }
            else
            {
                SelectTreeNode(node);
            }
        }

        private bool IsItemSelected(AnimGridItem item) =>
            _selectedItemRefs.Contains(new AnimCatalogRef(item.Group, item.Category, item.No));

        private void ToggleItemSelection(AnimGridItem item)
        {
            var reference = new AnimCatalogRef(item.Group, item.Category, item.No);
            if (!_selectedItemRefs.Remove(reference))
                _selectedItemRefs.Add(reference);
        }

        private bool IsGroupSelected(AnimDisplayGroup group) => _selectedGroupIds.Contains(group.Id);

        private void ToggleGroupSelection(AnimDisplayGroup group)
        {
            if (!_selectedGroupIds.Remove(group.Id))
                _selectedGroupIds.Add(group.Id);
        }

        private void ClearGridSelection()
        {
            _selectedItemRefs.Clear();
            _selectedGroupIds.Clear();
            _pendingUngroupConfirm = false;
            _lastClickedVisibleIndex = -1;
            CancelContentRename();
        }

        private void HandleDeselectHotkey()
        {
            Event ev = Event.current;
            if (ev.type != EventType.KeyDown || ev.keyCode != KeyCode.Escape)
                return;
            if (_selectedItemRefs.Count > 0 || _selectedGroupIds.Count > 0)
            {
                ClearGridSelection();
                ev.Use();
            }
        }

        // ---- Tree action bar ------------------------------------------------

        private void DrawTreeActionBar()
        {
            int categoryCount = 0;
            int groupCount = 0;
            int selectedTreeCount = 0;
            bool anyMerged = false;
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                AnimViewNode node = _flatTreeNodes[i];
                if (!_selectedTreeNodeIds.Contains(node.Id))
                    continue;
                selectedTreeCount++;
                if (node.IsMerged)
                    anyMerged = true;
                if (node.IsGroup)
                {
                    if (node.RawGroupId >= 0)
                        groupCount++;
                }
                else
                {
                    categoryCount++;
                }
            }

            bool canMergeCategories = categoryCount >= 2;
            bool canMergeGroups = groupCount >= 2;
            bool canRenameTree = selectedTreeCount == 1;
            if (!canMergeCategories && !canMergeGroups && !anyMerged && !canRenameTree && !_treeRenameActive)
                return;

            GUILayout.Space(3f);
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), AnimBrowserScale.H(1f));
            GUILayout.Space(3f);

            float btnH = AnimBrowserScale.Px(22f);

            if (_treeRenameActive)
            {
                DrawTreeRenamePanel(btnH);
                return;
            }

            if (canRenameTree &&
                GUILayout.Button(new GUIContent("Rename…", "Change the display name for the selected tree node."), GUILayout.Height(btnH)))
            {
                BeginTreeRename();
            }
            if (canMergeCategories &&
                GUILayout.Button(new GUIContent(
                    "Merge categories…",
                    "Combine the selected sub-categories into one node (including already-merged categories)."),
                    GUILayout.Height(btnH)))
            {
                BeginTreeMerge(AnimTreeMergeKind.Category);
            }
            if (canMergeGroups &&
                GUILayout.Button(new GUIContent("Merge groups…", "Combine the selected groups, matching sub-categories by name."), GUILayout.Height(btnH)))
            {
                BeginTreeMerge(AnimTreeMergeKind.Group);
            }
            if (anyMerged &&
                GUILayout.Button(new GUIContent(
                        HasOnlyMgcSelection() ? "Unmerge subcategory" : "Unmerge",
                        HasOnlyMgcSelection()
                            ? "Move the selected subcategory back to its original group/category."
                            : "Remove the merge for the selected node(s)."),
                    GUILayout.Height(btnH)))
            {
                UnmergeSelectedNodes();
            }
        }

        // ---- Content action bar ---------------------------------------------

        private void DrawContentActionBar(float wrapWidth)
        {
            int items = _selectedItemRefs.Count;
            int groups = _selectedGroupIds.Count;
            if (items == 0 && groups == 0 && !_contentRenameActive)
                return;

            GUILayout.Space(3f);
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), AnimBrowserScale.H(1f));
            GUILayout.Space(3f);

            float btnH = AnimBrowserScale.Px(22f);
            float minW = AnimBrowserScale.Px(90f);

            if (_contentRenameActive)
            {
                DrawContentRenamePanel(btnH);
                return;
            }

            if (_pendingUngroupConfirm && groups > 0)
            {
                InitCharacterHintStyle();
                GUILayout.Label(
                    "Dissolve " + groups + " group card(s)? Animations stay in the catalog.",
                    _characterHintStyle ?? GUI.skin.label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm ungroup", GUILayout.Height(btnH), GUILayout.MinWidth(minW)))
                    ExecuteUngroupSelected();
                if (GUILayout.Button("Cancel", GUILayout.Height(btnH), AnimBrowserScale.MinW(60f)))
                    _pendingUngroupConfirm = false;
                GUILayout.EndHorizontal();
                return;
            }

            var bar = new ActionBarWrapLayout();
            bar.Begin(wrapWidth);
            bar.AddLabel(items + " selected", btnH);
            bool canRenameContent = (items == 1 && groups == 0) || (items == 0 && groups == 1);
            if (canRenameContent)
            {
                bar.AddButton("Rename…", btnH, minW, BeginContentRename,
                    tooltip: items == 1
                        ? "Change the display name for the selected animation."
                        : "Change the display name for the selected group card.");
            }
            bar.AddButton("Group selected…", btnH, minW, BeginGridGroup, enabled: items >= 2,
                tooltip: "Combine the selected animations into one card.");
            if (groups > 0)
                bar.AddButton("Ungroup", btnH, minW, RequestUngroupSelected, tooltip: "Dissolve the selected group card(s).");
            bar.AddButton("Clear", btnH, AnimBrowserScale.Px(60f), ClearGridSelection);
            bar.End();
        }

        // ---- Merge / group flows --------------------------------------------

        private void BeginTreeMerge(AnimTreeMergeKind kind)
        {
            var sourceNodes = new List<AnimViewNode>();
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                AnimViewNode node = _flatTreeNodes[i];
                if (!_selectedTreeNodeIds.Contains(node.Id))
                    continue;
                if (kind == AnimTreeMergeKind.Group && (!node.IsGroup || node.RawGroupId < 0))
                    continue;
                if (kind == AnimTreeMergeKind.Category && node.IsGroup)
                    continue;
                sourceNodes.Add(node);
            }
            if (sourceNodes.Count < 2)
                return;

            _pendingMergeReinclude.Clear();
            _pendingMergeRuleAlreadyStored = false;
            ClearPendingMgcBucketMerge();

            AnimTreeMergeRule rule;
            var items = new List<AnimGridItem>();
            string reviewHeading;

            if (kind == AnimTreeMergeKind.Group)
            {
                var groupIds = new HashSet<int>();
                for (int i = 0; i < sourceNodes.Count; i++)
                    groupIds.Add(sourceNodes[i].RawGroupId);

                AnimTreeMergeRule? existing = _groupStore.FindGroupMergeBySourceGroups(groupIds);
                if (existing != null)
                {
                    rule = existing;
                    _pendingMergeRuleAlreadyStored = true;
                    CollectExcludedCategoriesToReinclude(existing, groupIds, _pendingMergeReinclude);
                    if (_pendingMergeReinclude.Count == 0)
                        return;
                    CollectItemsForCategories(_pendingMergeReinclude, items);
                    reviewHeading = "Review re-merge: " + rule.Name;
                }
                else
                {
                    rule = new AnimTreeMergeRule
                    {
                        Id = AnimGroupStore.NewId(),
                        Kind = kind,
                        Name = SuggestMergeName(sourceNodes)
                    };
                    for (int i = 0; i < sourceNodes.Count; i++)
                        rule.Sources.Add(new AnimCatalogRef(sourceNodes[i].RawGroupId, -1, -1));
                    for (int i = 0; i < sourceNodes.Count; i++)
                        items.AddRange(_displayCatalog.GetRawItems(sourceNodes[i]));
                    reviewHeading = "Review merge: " + rule.Name;
                }
            }
            else
            {
                if (TryBeginGroupMergeSubcategoryMerge(sourceNodes))
                    return;
                if (!TryValidateSameGroupCategoryMerge(sourceNodes, out _))
                    return;

                var selectedCategories = new HashSet<AnimCatalogRef>();
                for (int i = 0; i < sourceNodes.Count; i++)
                {
                    IList<AnimCatalogRef> src = sourceNodes[i].SourceCategories;
                    for (int s = 0; s < src.Count; s++)
                        selectedCategories.Add(AnimCatalogRefUtil.CategoryRef(src[s].Group, src[s].Category));
                }
                if (selectedCategories.Count < 2)
                    return;

                AnimTreeMergeRule? existingGroupMerge =
                    _groupStore.FindGroupMergeForExcludedCategories(selectedCategories);
                if (existingGroupMerge != null)
                {
                    rule = existingGroupMerge;
                    _pendingMergeRuleAlreadyStored = true;
                    foreach (AnimCatalogRef catRef in selectedCategories)
                    {
                        if (AnimCatalogRefUtil.ContainsCategory(rule.ExcludedSources, catRef))
                            _pendingMergeReinclude.Add(catRef);
                    }
                    if (_pendingMergeReinclude.Count == 0)
                        return;
                    CollectItemsForCategories(_pendingMergeReinclude, items);
                    reviewHeading = "Review re-merge: " + rule.Name;
                }
                else
                {
                    AnimTreeMergeRule? existing = _groupStore.FindCategoryMergeBySources(selectedCategories);
                    if (existing != null)
                    {
                        rule = existing;
                        _pendingMergeRuleAlreadyStored = true;
                        CollectExcludedCategoriesToReinclude(existing, selectedCategories, _pendingMergeReinclude);
                        if (_pendingMergeReinclude.Count == 0)
                            return;
                        CollectItemsForCategories(_pendingMergeReinclude, items);
                        reviewHeading = "Review re-merge: " + rule.Name;
                    }
                    else
                    {
                        AnimTreeMergeRule? subset = _groupStore.FindCategoryMergeSubsetOf(selectedCategories);
                        if (subset != null)
                        {
                            rule = subset;
                            _pendingMergeRuleAlreadyStored = true;
                        }
                        else
                        {
                            rule = new AnimTreeMergeRule
                            {
                                Id = AnimGroupStore.NewId(),
                                Kind = kind,
                                Name = SuggestMergeName(sourceNodes)
                            };
                        }

                        _groupStore.ReplaceCategoryMergeSources(rule, selectedCategories);
                        rule.Name = SuggestMergeName(sourceNodes);
                        for (int i = 0; i < sourceNodes.Count; i++)
                            items.AddRange(_displayCatalog.GetRawItems(sourceNodes[i]));
                        reviewHeading = "Review merge: " + rule.Name;
                    }
                }
            }

            bool pairWithinSubcategory = rule.Kind == AnimTreeMergeKind.Group;
            Dictionary<AnimCatalogRef, string>? bucketKeys = pairWithinSubcategory
                ? BuildSubcategoryBucketKeyMap(items)
                : null;
            var proposals = new List<AnimDisplayGroupData>();
            AppendMergeReviewProposals(items, pairWithinSubcategory, bucketKeys, proposals);
            OpenReview(reviewHeading, proposals, rule);
        }

        private static void CollectExcludedCategoriesToReinclude(
            AnimTreeMergeRule rule,
            HashSet<int> selectedGroupIds,
            List<AnimCatalogRef> output)
        {
            for (int i = 0; i < rule.ExcludedSources.Count; i++)
            {
                AnimCatalogRef excluded = rule.ExcludedSources[i];
                if (!selectedGroupIds.Contains(excluded.Group))
                    continue;
                output.Add(AnimCatalogRefUtil.CategoryRef(excluded.Group, excluded.Category));
            }
        }

        private static void CollectExcludedCategoriesToReinclude(
            AnimTreeMergeRule rule,
            HashSet<AnimCatalogRef> selectedCategories,
            List<AnimCatalogRef> output)
        {
            for (int i = 0; i < rule.ExcludedSources.Count; i++)
            {
                AnimCatalogRef excluded = rule.ExcludedSources[i];
                var catRef = AnimCatalogRefUtil.CategoryRef(excluded.Group, excluded.Category);
                if (!selectedCategories.Contains(catRef))
                    continue;
                output.Add(catRef);
            }
        }

        private void CollectItemsForCategories(IList<AnimCatalogRef> categoryRefs, List<AnimGridItem> output)
        {
            for (int i = 0; i < categoryRefs.Count; i++)
            {
                AnimCatalogRef cat = categoryRefs[i];
                IList<AnimGridItem> catItems = _catalog.GetItemsForSelection(cat.Group, cat.Category);
                for (int j = 0; j < catItems.Count; j++)
                    output.Add(catItems[j]);
            }
        }

        private void BeginGridGroup()
        {
            var items = new List<AnimGridItem>();
            foreach (var reference in _selectedItemRefs)
            {
                AnimGridItem? item = _catalog.TryGetItem(reference);
                if (item != null)
                    items.Add(item);
            }
            if (items.Count < 2)
                return;

            var single = AnimGroupDetector.DetectSingleGroup(
                items,
                GetGenderContextName,
                GetCategoryName,
                GetOriginalCatalogPath);
            if (single == null)
                return;
            var proposals = new List<AnimDisplayGroupData> { single };
            OpenReview("Review group", proposals, null);
        }

        private void RequestUngroupSelected()
        {
            if (_selectedGroupIds.Count == 0)
                return;
            _pendingUngroupConfirm = true;
        }

        private void ExecuteUngroupSelected()
        {
            if (_selectedGroupIds.Count == 0)
            {
                _pendingUngroupConfirm = false;
                return;
            }
            foreach (var id in new List<string>(_selectedGroupIds))
                _groupStore.RemoveDisplayGroup(id);
            _selectedGroupIds.Clear();
            _pendingUngroupConfirm = false;
        }

        private void UnmergeSelectedNodes()
        {
            var affectedCategories = new List<AnimCatalogRef>();
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                AnimViewNode node = _flatTreeNodes[i];
                if (!_selectedTreeNodeIds.Contains(node.Id) || !node.IsMerged)
                    continue;

                if (node.Id.StartsWith("mgc:", StringComparison.Ordinal))
                {
                    string? ruleId = ExtractMergeRuleId(node.Id);
                    if (ruleId != null)
                        _groupStore.PartialUnmergeSubcategory(ruleId, node.SourceCategories);
                    continue;
                }

                string? fullRuleId = ExtractMergeRuleId(node.Id);
                if (fullRuleId == null)
                    continue;
                AnimTreeMergeRule? rule = _groupStore.FindTreeMerge(fullRuleId);
                if (rule == null)
                    continue;

                CollectCategoriesForTreeMerge(rule, affectedCategories);
                _groupStore.RemoveDisplayGroupsTouchingCategories(affectedCategories);
                _groupStore.RemoveTreeMerge(fullRuleId);
            }

            PruneStaleGroupSelection();
            _selectedTreeNodeIds.Clear();
        }

        private void CollectCategoriesForTreeMerge(AnimTreeMergeRule rule, List<AnimCatalogRef> output)
        {
            output.Clear();
            if (rule.Kind == AnimTreeMergeKind.Category)
            {
                for (int i = 0; i < rule.Sources.Count; i++)
                {
                    AnimCatalogRef src = rule.Sources[i];
                    output.Add(AnimCatalogRefUtil.CategoryRef(src.Group, src.Category));
                }
                return;
            }

            IList<AnimCategoryNode> roots = _catalog.RootGroups;
            for (int i = 0; i < rule.Sources.Count; i++)
            {
                int groupId = rule.Sources[i].Group;
                AnimCategoryNode? group = FindRawCatalogGroup(roots, groupId);
                if (group == null)
                    continue;
                for (int ci = 0; ci < group.Children.Count; ci++)
                {
                    AnimCategoryNode category = group.Children[ci];
                    output.Add(AnimCatalogRefUtil.CategoryRef(groupId, category.CategoryId));
                }
            }
        }

        private static AnimCategoryNode? FindRawCatalogGroup(IList<AnimCategoryNode> raw, int groupId)
        {
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i].GroupId == groupId)
                    return raw[i];
            }
            return null;
        }

        private void PruneStaleGroupSelection()
        {
            if (_selectedGroupIds.Count == 0)
                return;
            _selectedGroupIds.RemoveWhere(id => _groupStore.FindDisplayGroup(id) == null);
        }

        private bool HasOnlyMgcSelection()
        {
            bool any = false;
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                AnimViewNode node = _flatTreeNodes[i];
                if (!_selectedTreeNodeIds.Contains(node.Id) || !node.IsMerged)
                    continue;
                any = true;
                if (!node.Id.StartsWith("mgc:", StringComparison.Ordinal))
                    return false;
            }
            return any;
        }

        private static string? ExtractMergeRuleId(string nodeId)
        {
            int colon = nodeId.IndexOf(':');
            if (colon < 0)
                return null;
            string prefix = nodeId.Substring(0, colon);
            string rest = nodeId.Substring(colon + 1);
            switch (prefix)
            {
                case "mg":
                case "cm":
                    return rest;
                case "mgc":
                    int second = rest.IndexOf(':');
                    return second < 0 ? rest : rest.Substring(0, second);
                default:
                    return null;
            }
        }

        private string SuggestMergeName(List<AnimViewNode> nodes)
        {
            string first = nodes[0].Name;
            AnimGroupHeuristics.DetectGender(first, out string baseName);
            baseName = baseName.Trim();
            return baseName.Length > 0 ? baseName : first;
        }

        private string? GetGenderContextName(AnimGridItem item) => _displayCatalog.GetGenderContextName(item);

        private string GetCategoryName(AnimGridItem item) => _displayCatalog.GetCategoryName(item);

        private string GetCatalogPath(AnimGridItem item) => _displayCatalog.GetCatalogPath(item);

        private string GetOriginalCatalogPath(AnimGridItem item) => _displayCatalog.GetOriginalCatalogPath(item);

        private static string GetSubcategoryBucketKey(
            AnimGridItem item,
            Dictionary<AnimCatalogRef, string> bucketKeys)
        {
            var catRef = AnimCatalogRefUtil.CategoryRef(item.Group, item.Category);
            if (bucketKeys.TryGetValue(catRef, out string? key))
                return key;
            return AnimGroupHeuristics.NormalizeCategoryKey(item.Category.ToString());
        }

        private Dictionary<AnimCatalogRef, string> BuildSubcategoryBucketKeyMap(IList<AnimGridItem> items)
        {
            var groupIds = new HashSet<int>();
            for (int i = 0; i < items.Count; i++)
                groupIds.Add(items[i].Group);
            return _displayCatalog.BuildSubcategoryBucketKeyMap(groupIds);
        }

        /// <summary>Builds review proposals: keeps fully-scoped existing display groups intact and
        /// only auto-detects roles for animations not already grouped.</summary>
        private void AppendMergeReviewProposals(
            List<AnimGridItem> scopedItems,
            bool pairWithinSubcategory,
            Dictionary<AnimCatalogRef, string>? bucketKeys,
            List<AnimDisplayGroupData> proposals)
        {
            var scopedRefs = new HashSet<AnimCatalogRef>();
            for (int i = 0; i < scopedItems.Count; i++)
            {
                AnimGridItem item = scopedItems[i];
                scopedRefs.Add(new AnimCatalogRef(item.Group, item.Category, item.No));
            }

            var preservedRefs = new HashSet<AnimCatalogRef>();
            var seenGroupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnimCatalogRef animRef in scopedRefs)
            {
                AnimDisplayGroupData? existing = _groupStore.GetGroupForRef(animRef);
                if (existing == null || !seenGroupIds.Add(existing.Id))
                    continue;

                bool allInScope = true;
                for (int m = 0; m < existing.Members.Count; m++)
                {
                    if (!scopedRefs.Contains(existing.Members[m].Ref))
                    {
                        allInScope = false;
                        break;
                    }
                }
                if (!allInScope || existing.Members.Count < 2)
                    continue;

                proposals.Add(CloneDisplayGroupForReview(existing, bucketKeys, pairWithinSubcategory));
                for (int m = 0; m < existing.Members.Count; m++)
                    preservedRefs.Add(existing.Members[m].Ref);
            }

            var ungroupedItems = new List<AnimGridItem>();
            for (int i = 0; i < scopedItems.Count; i++)
            {
                AnimGridItem item = scopedItems[i];
                var animRef = new AnimCatalogRef(item.Group, item.Category, item.No);
                if (!preservedRefs.Contains(animRef))
                    ungroupedItems.Add(item);
            }

            if (ungroupedItems.Count < 2)
                return;

            proposals.AddRange(AnimGroupDetector.Detect(
                ungroupedItems,
                GetGenderContextName,
                GetCategoryName,
                GetOriginalCatalogPath,
                pairWithinSubcategory,
                bucketKeys != null ? item => GetSubcategoryBucketKey(item, bucketKeys) : null));
        }

        private AnimDisplayGroupData CloneDisplayGroupForReview(
            AnimDisplayGroupData source,
            Dictionary<AnimCatalogRef, string>? bucketKeys,
            bool pairWithinSubcategory)
        {
            var clone = new AnimDisplayGroupData
            {
                Id = source.Id,
                Name = source.Name
            };
            for (int m = 0; m < source.Members.Count; m++)
            {
                AnimGroupMemberData member = source.Members[m];
                clone.Members.Add(new AnimGroupMemberData
                {
                    Ref = member.Ref,
                    Phase = member.Phase,
                    Gender = member.Gender,
                    GenderOrdinal = member.GenderOrdinal
                });
            }

            AnimGridItem? anchor = _catalog.TryGetItem(source.Members[0].Ref);
            if (anchor == null)
                return clone;

            clone.ReviewSectionLabel = GetOriginalCatalogPath(anchor);
            if (pairWithinSubcategory && bucketKeys != null)
            {
                var catRef = AnimCatalogRefUtil.CategoryRef(anchor.Group, anchor.Category);
                if (bucketKeys.TryGetValue(catRef, out string? key))
                    clone.ReviewSectionKey = key;
            }
            else
            {
                string key = AnimGroupHeuristics.NormalizeBase(anchor.DisplayName);
                clone.ReviewSectionKey = key.Length > 0 ? key : anchor.CatalogKey;
            }
            return clone;
        }

        private string ResolveReviewPathLabel(AnimDisplayGroupData data)
        {
            if (data.Members.Count == 0)
                return string.Empty;
            AnimGridItem? item = _catalog.TryGetItem(data.Members[0].Ref);
            return item != null ? GetOriginalCatalogPath(item) : string.Empty;
        }

        // ---- Review docked window -------------------------------------------

        private void OpenReview(string heading, List<AnimDisplayGroupData> proposals, AnimTreeMergeRule? mergeRule)
        {
            _reviewGroups.Clear();
            _reviewGroups.AddRange(proposals);
            _pendingMergeRule = mergeRule;
            _reviewHeading = heading;
            _reviewScroll = Vector2.zero;
            _reviewSectionsDirty = true;
            _reviewDisplayCachesDirty = true;
            _reviewVirtualBlocksDirty = true;
            _reviewCollapsedSectionKeys.Clear();
            _reviewSkippedRefs.Clear();
            _reviewSinglesInMergeGroupIds.Clear();
            PrefetchReviewTranslations();

            if (_reviewGroups.Count == 0 && mergeRule == null)
                return;

            _showReviewPane = true;
            if (_reviewWindowRect.width < 1f)
                _reviewWindowRect = new Rect(windowRect.xMax + DockedPaneGap, windowRect.y, ReviewPaneDefaultWidth, windowRect.height);
        }

        private void PrefetchReviewTranslations()
        {
            var strings = new List<string>(_reviewGroups.Count * 4);
            for (int i = 0; i < _reviewGroups.Count; i++)
            {
                AnimDisplayGroupData data = _reviewGroups[i];
                if (!string.IsNullOrEmpty(data.Name))
                    strings.Add(data.Name);
                if (!string.IsNullOrEmpty(data.ReviewSectionLabel))
                    strings.Add(data.ReviewSectionLabel);
                for (int m = 0; m < data.Members.Count; m++)
                {
                    AnimGridItem? item = _catalog.TryGetItem(data.Members[m].Ref);
                    if (item != null && !string.IsNullOrEmpty(item.DisplayName))
                        strings.Add(item.DisplayName);
                }
            }
            StudioAutoTranslation.Prefetch(strings);
        }

        private void RebuildReviewSectionsIfNeeded()
        {
            if (!_reviewSectionsDirty)
                return;
            _reviewSections.Clear();
            var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int g = 0; g < _reviewGroups.Count; g++)
            {
                AnimDisplayGroupData data = _reviewGroups[g];
                string pathLabel = data.ReviewSectionLabel;
                if (pathLabel.Length == 0)
                    pathLabel = ResolveReviewPathLabel(data);
                string key = data.ReviewSectionKey;
                if (key.Length == 0)
                    key = pathLabel;
                if (key.Length == 0)
                    key = "_ungrouped";
                string sectionLabel = BuildReviewSectionLabel(data, pathLabel);
                if (!indexByKey.TryGetValue(key, out int sectionIndex))
                {
                    sectionIndex = _reviewSections.Count;
                    indexByKey[key] = sectionIndex;
                    _reviewSections.Add(new ReviewSectionLayout
                    {
                        SectionKey = key,
                        SectionLabel = sectionLabel
                    });
                }
                else if (_reviewSections[sectionIndex].SectionLabel.Length == 0 && sectionLabel.Length > 0)
                {
                    _reviewSections[sectionIndex].SectionLabel = sectionLabel;
                }
                _reviewSections[sectionIndex].GroupIndices.Add(g);
            }
            _reviewSectionsDirty = false;
        }

        private static string BuildReviewSectionLabel(AnimDisplayGroupData data, string pathLabel)
        {
            string suffix = AnimGroupHeuristics.FormatSubcategoryDisambiguatorSuffix(data.ReviewSectionKey);
            if (pathLabel.Length > 0)
                return suffix.Length > 0 ? pathLabel + suffix : pathLabel;
            if (data.ReviewSectionKey.Length > 0)
                return data.ReviewSectionKey;
            return pathLabel;
        }

        private void ConfirmReview()
        {
            var commit = new List<AnimDisplayGroupData>();
            for (int i = 0; i < _reviewGroups.Count; i++)
            {
                AnimDisplayGroupData data = _reviewGroups[i];
                if (_reviewSinglesInMergeGroupIds.Contains(data.Id))
                    continue;

                data.Members.RemoveAll(m => _reviewSkippedRefs.Contains(m.Ref));
                if (data.Members.Count < 2)
                    continue;
                AnimGroupDetector.ReassignOrdinals(data);
                commit.Add(data);
            }

            if (_pendingMergeRule != null && _reviewSkippedRefs.Count > 0)
            {
                foreach (var skipped in _reviewSkippedRefs)
                {
                    if (IsRefInSinglesInMergeGroup(skipped))
                        continue;

                    bool found = false;
                    for (int i = 0; i < _pendingMergeRule.ExcludedAnimationRefs.Count; i++)
                    {
                        if (_pendingMergeRule.ExcludedAnimationRefs[i].Equals(skipped))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        _pendingMergeRule.ExcludedAnimationRefs.Add(skipped);
                }
            }

            if (_pendingMergeReinclude.Count > 0 && _pendingMergeRule != null)
                _groupStore.ReincludeCategories(_pendingMergeRule, _pendingMergeReinclude);

            if (_pendingMgcBucketMerge)
            {
                _groupStore.MergeSubcategoryBuckets(
                    _pendingMgcBucketMergeRuleId,
                    _pendingMgcBucketMergeTargetKey,
                    _pendingMgcBucketMergeAliasKeys);
            }

            _groupStore.Commit(_pendingMergeRule, commit, _pendingMergeRuleAlreadyStored);
            CloseReview();
            _selectedTreeNodeIds.Clear();
            ClearGridSelection();
        }

        private void CloseReview()
        {
            _showReviewPane = false;
            _reviewGroups.Clear();
            _reviewSections.Clear();
            _reviewGroupCaches.Clear();
            _reviewVirtualBlocks.Clear();
            _reviewSectionHeadings = Array.Empty<string>();
            _reviewSectionsDirty = true;
            _reviewDisplayCachesDirty = true;
            _reviewVirtualBlocksDirty = true;
            _reviewCollapsedSectionKeys.Clear();
            _reviewSkippedRefs.Clear();
            _reviewSinglesInMergeGroupIds.Clear();
            _pendingMergeRule = null;
            _pendingMergeRuleAlreadyStored = false;
            _pendingMergeReinclude.Clear();
            ClearPendingMgcBucketMerge();
        }

        private void ClearPendingMgcBucketMerge()
        {
            _pendingMgcBucketMerge = false;
            _pendingMgcBucketMergeRuleId = string.Empty;
            _pendingMgcBucketMergeTargetKey = string.Empty;
            _pendingMgcBucketMergeAliasKeys.Clear();
        }

        private bool TryBeginGroupMergeSubcategoryMerge(List<AnimViewNode> sourceNodes)
        {
            if (sourceNodes.Count < 2)
                return false;

            AnimTreeMergeRule? groupMergeRule = null;
            var bucketKeys = new List<string>(sourceNodes.Count);

            for (int i = 0; i < sourceNodes.Count; i++)
            {
                AnimViewNode node = sourceNodes[i];
                if (node.IsGroup)
                    return false;

                AnimTreeMergeRule? nodeRule;
                string resolvedKey;

                if (node.Id.StartsWith("mgc:", StringComparison.Ordinal))
                {
                    if (!TryParseMgcNodeId(node.Id, out string ruleId, out string bucketKey))
                        return false;
                    nodeRule = _groupStore.FindTreeMerge(ruleId);
                    if (nodeRule == null || nodeRule.Kind != AnimTreeMergeKind.Group)
                        return false;
                    resolvedKey = nodeRule.ResolveSubcategoryBucketKey(bucketKey);
                }
                else
                {
                    if (node.SourceCategories.Count == 0)
                        return false;
                    AnimCatalogRef anchor = AnimCatalogRefUtil.CategoryRef(
                        node.SourceCategories[0].Group,
                        node.SourceCategories[0].Category);
                    nodeRule = _groupStore.FindGroupMergeForCategory(anchor);
                    if (nodeRule == null)
                        return false;
                    if (!_displayCatalog.TryResolveSubcategoryBucketKey(nodeRule, anchor, out resolvedKey))
                        return false;
                }

                if (groupMergeRule == null)
                    groupMergeRule = nodeRule;
                else if (!string.Equals(groupMergeRule.Id, nodeRule.Id, StringComparison.Ordinal))
                    return false;

                bucketKeys.Add(resolvedKey);
            }

            var distinctKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < bucketKeys.Count; i++)
                distinctKeys.Add(bucketKeys[i]);
            if (distinctKeys.Count < 2)
                return false;

            _pendingMgcBucketMerge = true;
            _pendingMgcBucketMergeRuleId = groupMergeRule!.Id;
            _pendingMgcBucketMergeTargetKey = bucketKeys[0];
            _pendingMgcBucketMergeAliasKeys.Clear();
            for (int i = 1; i < bucketKeys.Count; i++)
            {
                string alias = bucketKeys[i];
                if (string.Equals(alias, _pendingMgcBucketMergeTargetKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_pendingMgcBucketMergeAliasKeys.Contains(alias))
                    continue;
                _pendingMgcBucketMergeAliasKeys.Add(alias);
            }
            if (_pendingMgcBucketMergeAliasKeys.Count == 0)
                return false;

            _pendingMergeRule = groupMergeRule;
            _pendingMergeRuleAlreadyStored = true;

            var items = new List<AnimGridItem>();
            for (int i = 0; i < sourceNodes.Count; i++)
                items.AddRange(_displayCatalog.GetRawItems(sourceNodes[i]));

            Dictionary<AnimCatalogRef, string> bucketKeysMap = BuildSubcategoryBucketKeyMap(items);
            var proposals = new List<AnimDisplayGroupData>();
            AppendMergeReviewProposals(items, pairWithinSubcategory: true, bucketKeysMap, proposals);
            OpenReview("Review merge subcategories: " + SuggestMergeName(sourceNodes), proposals, groupMergeRule);
            return true;
        }

        private static bool TryValidateSameGroupCategoryMerge(IList<AnimViewNode> nodes, out int groupId)
        {
            groupId = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                AnimViewNode node = nodes[i];
                if (node.IsGroup || node.Id.StartsWith("mgc:", StringComparison.Ordinal))
                    return false;
                if (node.SourceCategories.Count == 0)
                    return false;
                for (int s = 0; s < node.SourceCategories.Count; s++)
                {
                    AnimCatalogRef src = node.SourceCategories[s];
                    if (groupId < 0)
                        groupId = src.Group;
                    else if (src.Group != groupId)
                        return false;
                }
            }
            return groupId >= 0;
        }

        private static bool TryParseMgcNodeId(string nodeId, out string ruleId, out string bucketKey)
        {
            ruleId = string.Empty;
            bucketKey = string.Empty;
            if (!nodeId.StartsWith("mgc:", StringComparison.Ordinal))
                return false;
            string rest = nodeId.Substring(4);
            int colon = rest.IndexOf(':');
            if (colon < 0)
            {
                ruleId = rest;
                return false;
            }
            ruleId = rest.Substring(0, colon);
            bucketKey = rest.Substring(colon + 1);
            return ruleId.Length > 0 && bucketKey.Length > 0;
        }

        private void ToggleReviewSectionCollapsed(string sectionKey)
        {
            if (!_reviewCollapsedSectionKeys.Remove(sectionKey))
                _reviewCollapsedSectionKeys.Add(sectionKey);
            _reviewVirtualBlocksDirty = true;
        }

        private void ToggleReviewSkip(AnimCatalogRef reference)
        {
            if (!_reviewSkippedRefs.Remove(reference))
                _reviewSkippedRefs.Add(reference);
            _reviewDisplayCachesDirty = true;
            _reviewVirtualBlocksDirty = true;
        }

        private bool IsRefInSinglesInMergeGroup(AnimCatalogRef reference)
        {
            for (int g = 0; g < _reviewGroups.Count; g++)
            {
                AnimDisplayGroupData data = _reviewGroups[g];
                if (!_reviewSinglesInMergeGroupIds.Contains(data.Id))
                    continue;
                for (int m = 0; m < data.Members.Count; m++)
                {
                    if (data.Members[m].Ref.Equals(reference))
                        return true;
                }
            }
            return false;
        }

        private bool IsReviewGroupAllSkipped(AnimDisplayGroupData data)
        {
            if (data.Members.Count == 0)
                return false;
            for (int m = 0; m < data.Members.Count; m++)
            {
                if (!_reviewSkippedRefs.Contains(data.Members[m].Ref))
                    return false;
            }
            return true;
        }

        private void SkipReviewGroup(int groupIndex)
        {
            AnimDisplayGroupData data = _reviewGroups[groupIndex];
            _reviewSinglesInMergeGroupIds.Remove(data.Id);
            for (int m = 0; m < data.Members.Count; m++)
                _reviewSkippedRefs.Add(data.Members[m].Ref);
            _reviewDisplayCachesDirty = true;
            _reviewVirtualBlocksDirty = true;
        }

        private void SetReviewGroupSinglesInMerge(int groupIndex)
        {
            AnimDisplayGroupData data = _reviewGroups[groupIndex];
            _reviewSinglesInMergeGroupIds.Add(data.Id);
            for (int m = 0; m < data.Members.Count; m++)
                _reviewSkippedRefs.Remove(data.Members[m].Ref);
            _reviewDisplayCachesDirty = true;
            _reviewVirtualBlocksDirty = true;
        }

        private void RestoreReviewGroup(int groupIndex)
        {
            AnimDisplayGroupData data = _reviewGroups[groupIndex];
            _reviewSinglesInMergeGroupIds.Remove(data.Id);
            for (int m = 0; m < data.Members.Count; m++)
                _reviewSkippedRefs.Remove(data.Members[m].Ref);
            _reviewDisplayCachesDirty = true;
            _reviewVirtualBlocksDirty = true;
        }

        private void InvalidateReviewDisplayCaches()
        {
            if (!_showReviewPane)
                return;
            _reviewDisplayCachesDirty = true;
            _reviewVirtualBlocksDirty = true;
        }

        private void RebuildReviewDisplayCachesIfNeeded()
        {
            if (!_reviewDisplayCachesDirty)
                return;

            InitStyles();
            while (_reviewGroupCaches.Count < _reviewGroups.Count)
                _reviewGroupCaches.Add(new ReviewGroupRowCache());
            if (_reviewGroupCaches.Count > _reviewGroups.Count)
                _reviewGroupCaches.RemoveRange(_reviewGroups.Count, _reviewGroupCaches.Count - _reviewGroups.Count);

            for (int g = 0; g < _reviewGroups.Count; g++)
                RebuildReviewGroupCache(g);

            RebuildReviewSectionsIfNeeded();
            _reviewSectionHeadings = new string[_reviewSections.Count];
            for (int si = 0; si < _reviewSections.Count; si++)
            {
                ReviewSectionLayout section = _reviewSections[si];
                string heading = TranslateCatalogPath(section.SectionLabel);
                if (heading.Length == 0)
                    heading = section.SectionLabel;
                _reviewSectionHeadings[si] = heading.Length > 0 ? heading : section.SectionKey;
            }

            _reviewDisplayCachesDirty = false;
            _reviewVirtualBlocksDirty = true;
        }

        private void RebuildReviewGroupCache(int groupIndex)
        {
            AnimDisplayGroupData data = _reviewGroups[groupIndex];
            ReviewGroupRowCache cache = _reviewGroupCaches[groupIndex];
            cache.TranslatedName = StudioAutoTranslation.Resolve(data.Name);
            cache.SinglesInMerge = _reviewSinglesInMergeGroupIds.Contains(data.Id);
            cache.AllSkipped = !cache.SinglesInMerge && IsReviewGroupAllSkipped(data);
            cache.Members.Clear();

            CountGenderSlots(data, out int maleCount, out int femaleCount);
            float minRoleW = AnimBrowserScale.Px(32f);
            float skipW = AnimBrowserScale.Px(44f);
            for (int m = 0; m < data.Members.Count; m++)
            {
                AnimGroupMemberData member = data.Members[m];
                AnimGridItem? item = _catalog.TryGetItem(member.Ref);
                string rawName = item?.DisplayName ?? member.Ref.Key;
                string shownName = StudioAutoTranslation.Resolve(rawName);
                string path = item != null ? GetOriginalCatalogPath(item) : string.Empty;
                string translatedPath = TranslateCatalogPath(path);
                string rowLabel = BuildReviewMemberLabel(shownName, translatedPath, path);
                string genderRole = AnimRoleText.ReviewGenderRoleLabel(member, maleCount, femaleCount);
                string phaseRole = AnimRoleText.ReviewPhaseRoleLabel(member.Phase);
                string tooltip = BuildMemberTooltip(member, shownName, rawName, translatedPath);
                float genderW = Mathf.Max(minRoleW, _roleButtonStyle!.CalcSize(new GUIContent(genderRole)).x + AnimBrowserScale.Px(10f));
                float phaseW = Mathf.Max(minRoleW, _roleButtonStyle!.CalcSize(new GUIContent(phaseRole)).x + AnimBrowserScale.Px(10f));
                bool skipped = _reviewSkippedRefs.Contains(member.Ref);

                cache.Members.Add(new ReviewMemberRowCache
                {
                    Member = member,
                    GenderButtonWidth = genderW,
                    PhaseButtonWidth = phaseW,
                    SkipButtonWidth = skipW,
                    IsSkipped = skipped,
                    NameContent = new GUIContent(rowLabel, tooltip),
                    GenderContent = new GUIContent(genderRole, "Gender / slot: number → m → f"),
                    PhaseContent = new GUIContent(phaseRole, "Phase: none → in → loop → out"),
                    SkipContent = skipped ? GcReviewUnskip : GcReviewSkip
                });
            }
        }

        private void RebuildReviewVirtualBlocksIfNeeded()
        {
            if (!_reviewVirtualBlocksDirty)
                return;

            RebuildReviewDisplayCachesIfNeeded();
            _reviewVirtualBlocks.Clear();

            float sectionH = AnimBrowserScale.Px(ReviewSectionHeaderHBase);
            float groupHeaderH = AnimBrowserScale.Px(ReviewGroupHeaderHBase + ReviewGroupActionRowHBase);
            float memberH = AnimBrowserScale.Px(ReviewMemberRowHBase);
            float groupPad = AnimBrowserScale.Px(ReviewGroupPadHBase);

            for (int si = 0; si < _reviewSections.Count; si++)
            {
                ReviewSectionLayout section = _reviewSections[si];
                bool collapsed = _reviewCollapsedSectionKeys.Contains(section.SectionKey);
                _reviewVirtualBlocks.Add(new ReviewVirtualBlock
                {
                    IsSection = true,
                    SectionIndex = si,
                    GroupIndex = -1,
                    Height = sectionH
                });

                if (collapsed)
                    continue;

                for (int gi = 0; gi < section.GroupIndices.Count; gi++)
                {
                    int groupIndex = section.GroupIndices[gi];
                    int memberCount = _reviewGroups[groupIndex].Members.Count;
                    bool singlesInMerge = _reviewSinglesInMergeGroupIds.Contains(_reviewGroups[groupIndex].Id);
                    float hintH = singlesInMerge ? AnimBrowserScale.Px(18f) : 0f;
                    float blockH = groupHeaderH + hintH + memberCount * memberH + groupPad;
                    _reviewVirtualBlocks.Add(new ReviewVirtualBlock
                    {
                        IsSection = false,
                        SectionIndex = si,
                        GroupIndex = groupIndex,
                        Height = blockH
                    });
                }
            }

            _reviewVirtualBlocksDirty = false;
        }

        private void DrawGroupReviewWindowContent(int id)
        {
            InitStyles();
            GUILayout.Label(_reviewHeading, GUILayout.ExpandWidth(true));
            GUILayout.Label(
                "Click section headers to collapse. Skip keeps an animation at its original category. " +
                "As singles moves animations into the merged category without grouping them. " +
                "Gender button: slot number → m → f. Phase button: none → in → loop → out.",
                _characterHintStyle ?? GUI.skin.label);
            GUILayout.Space(4f);

            RebuildReviewVirtualBlocksIfNeeded();
            _reviewScroll = GUILayout.BeginScrollView(_reviewScroll, false, true, GUILayout.ExpandHeight(true));

            if (_reviewGroups.Count == 0)
            {
                GUILayout.Label("No groups were detected. You can still confirm the merge.");
            }
            else
            {
                DrawVirtualizedReviewBlocks();
            }

            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
            {
                Rect svRect = GUILayoutUtility.GetLastRect();
                if (svRect.height > 1f)
                    _reviewScrollViewportH = svRect.height;
            }

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Confirm", GUILayout.Height(AnimBrowserScale.Px(26f))))
                ConfirmReview();
            if (GUILayout.Button("Cancel", GUILayout.Height(AnimBrowserScale.Px(26f))))
                CloseReview();
            GUILayout.EndHorizontal();
        }

        private void DrawVirtualizedReviewBlocks()
        {
            if (_reviewVirtualBlocks.Count == 0)
                return;

            float viewportH = _reviewScrollViewportH > 1f ? _reviewScrollViewportH : _reviewWindowRect.height;
            viewportH = Mathf.Max(120f, viewportH);
            float scrollY = _reviewScroll.y;

            int first = -1;
            int last = -1;
            float y = 0f;
            for (int i = 0; i < _reviewVirtualBlocks.Count; i++)
            {
                ReviewVirtualBlock block = _reviewVirtualBlocks[i];
                if (y + block.Height >= scrollY && first < 0)
                    first = i;
                if (y <= scrollY + viewportH)
                    last = i;
                y += block.Height;
            }

            first = Mathf.Max(0, first - 1);
            last = Mathf.Min(_reviewVirtualBlocks.Count - 1, last + 1);

            float spaceBefore = 0f;
            for (int i = 0; i < first; i++)
                spaceBefore += _reviewVirtualBlocks[i].Height;
            if (spaceBefore > 0f)
                GUILayout.Space(spaceBefore);

            for (int i = first; i <= last; i++)
            {
                ReviewVirtualBlock block = _reviewVirtualBlocks[i];
                if (block.IsSection)
                    DrawReviewSectionHeader(block.SectionIndex);
                else
                    DrawReviewGroupRowCached(block.GroupIndex);
            }

            float spaceAfter = 0f;
            for (int i = last + 1; i < _reviewVirtualBlocks.Count; i++)
                spaceAfter += _reviewVirtualBlocks[i].Height;
            if (spaceAfter > 0f)
                GUILayout.Space(spaceAfter);
        }

        private void DrawReviewSectionHeader(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _reviewSectionHeadings.Length)
                return;
            if (sectionIndex >= _reviewSections.Count)
                return;

            ReviewSectionLayout section = _reviewSections[sectionIndex];
            bool collapsed = _reviewCollapsedSectionKeys.Contains(section.SectionKey);
            string arrow = collapsed ? "►" : "▼";
            int groupCount = section.GroupIndices.Count;
            string heading = _reviewSectionHeadings[sectionIndex];
            string label = arrow + " " + heading + " (" + groupCount + (groupCount == 1 ? " group)" : " groups)");

            if (GUILayout.Button(label, _reviewSectionTitleStyle ?? GUI.skin.label, GUILayout.ExpandWidth(true)))
                ToggleReviewSectionCollapsed(section.SectionKey);
            GUILayout.Space(2f);
        }

        private void DrawReviewGroupRowCached(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= _reviewGroupCaches.Count)
                return;

            AnimDisplayGroupData data = _reviewGroups[groupIndex];
            ReviewGroupRowCache cache = _reviewGroupCaches[groupIndex];
            float roleH = AnimBrowserScale.Px(ReviewMemberRowHBase);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Group", AnimBrowserScale.W(42f));
            GUILayout.Label(cache.TranslatedName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Save as", AnimBrowserScale.W(42f));
            data.Name = GUILayout.TextField(data.Name, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            float actionBtnH = AnimBrowserScale.Px(ReviewGroupActionRowHBase - 4f);
            GUILayout.BeginHorizontal();
            if (cache.SinglesInMerge)
            {
                if (GUILayout.Button(GcReviewRestoreGroup, GUILayout.Height(actionBtnH)))
                    RestoreReviewGroup(groupIndex);
            }
            else if (cache.AllSkipped)
            {
                if (GUILayout.Button(GcReviewRestoreGroup, GUILayout.Height(actionBtnH)))
                    RestoreReviewGroup(groupIndex);
            }
            else
            {
                if (GUILayout.Button(GcReviewSkipAll, GUILayout.Height(actionBtnH)))
                    SkipReviewGroup(groupIndex);
                if (_pendingMergeRule != null &&
                    GUILayout.Button(GcReviewSinglesInMerge, GUILayout.Height(actionBtnH)))
                {
                    SetReviewGroupSinglesInMerge(groupIndex);
                }
            }
            GUILayout.EndHorizontal();

            if (cache.SinglesInMerge)
            {
                GUILayout.Label(
                    "These animations will appear separately in the merged category.",
                    _characterHintStyle ?? GUI.skin.label);
            }

            for (int m = 0; m < cache.Members.Count; m++)
            {
                ReviewMemberRowCache row = cache.Members[m];
                GUILayout.BeginHorizontal();
                bool prevEnabled = GUI.enabled;
                bool rowDisabled = row.IsSkipped || cache.SinglesInMerge;
                if (rowDisabled)
                    GUI.enabled = false;
                GUILayout.Label(row.NameContent, GUILayout.ExpandWidth(true), GUILayout.Height(roleH));
                GUI.enabled = prevEnabled && !rowDisabled;
                if (GUILayout.Button(row.GenderContent, _roleButtonStyle!, GUILayout.Width(row.GenderButtonWidth), GUILayout.Height(roleH)))
                {
                    CycleMemberGenderRole(row.Member, m);
                    RebuildReviewGroupCache(groupIndex);
                    _reviewVirtualBlocksDirty = true;
                }
                if (GUILayout.Button(row.PhaseContent, _roleButtonStyle!, GUILayout.Width(row.PhaseButtonWidth), GUILayout.Height(roleH)))
                {
                    CycleMemberPhase(row.Member);
                    RebuildReviewGroupCache(groupIndex);
                    _reviewVirtualBlocksDirty = true;
                }
                GUI.enabled = prevEnabled && !cache.SinglesInMerge;
                if (GUILayout.Button(row.SkipContent, _roleButtonStyle!, GUILayout.Width(row.SkipButtonWidth), GUILayout.Height(roleH)))
                {
                    ToggleReviewSkip(row.Member.Ref);
                    RebuildReviewGroupCache(groupIndex);
                    _reviewVirtualBlocksDirty = true;
                }
                GUI.enabled = prevEnabled;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Space(AnimBrowserScale.Px(ReviewGroupPadHBase));
        }

        private static void CountGenderSlots(AnimDisplayGroupData data, out int maleCount, out int femaleCount)
        {
            maleCount = 0;
            femaleCount = 0;
            for (int i = 0; i < data.Members.Count; i++)
            {
                AnimGroupMemberData member = data.Members[i];
                if (member.Gender == AnimGender.Male)
                    maleCount = Math.Max(maleCount, member.GenderOrdinal + 1);
                else if (member.Gender == AnimGender.Female)
                    femaleCount = Math.Max(femaleCount, member.GenderOrdinal + 1);
            }
        }

        private static string TranslateCatalogPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            int slash = path.IndexOf(" / ", StringComparison.Ordinal);
            if (slash < 0)
                return StudioAutoTranslation.Resolve(path);
            string group = path.Substring(0, slash);
            string category = path.Substring(slash + 3);
            return StudioAutoTranslation.Resolve(group) + " / " + StudioAutoTranslation.Resolve(category);
        }

        private static string BuildReviewMemberLabel(string shownName, string translatedPath, string rawPath)
        {
            if (string.IsNullOrEmpty(translatedPath) && string.IsNullOrEmpty(rawPath))
                return shownName;
            string path = translatedPath.Length > 0 ? translatedPath : rawPath;
            return path + " — " + shownName;
        }

        private static string BuildMemberTooltip(
            AnimGroupMemberData member,
            string shownName,
            string rawName,
            string translatedPath)
        {
            var sb = new System.Text.StringBuilder(shownName.Length + 64);
            if (translatedPath.Length > 0)
                sb.Append(translatedPath);
            if (!string.Equals(shownName, rawName, StringComparison.Ordinal))
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(rawName);
            }
            string role = member.Gender != AnimGender.Unknown ? AnimRoleText.GenderName(member.Gender) : string.Empty;
            if (member.Phase != AnimPhase.None)
                role = (role.Length > 0 ? role + " " : string.Empty) + AnimRoleText.PhaseName(member.Phase);
            if (role.Length > 0)
                sb.Append('(').Append(role).Append(')');
            return sb.ToString();
        }

        private static void CycleMemberGenderRole(AnimGroupMemberData member, int memberIndex)
        {
            if (member.Gender == AnimGender.Male)
            {
                member.Gender = AnimGender.Female;
                return;
            }
            if (member.Gender == AnimGender.Female)
            {
                member.Gender = AnimGender.Unknown;
                member.Phase = AnimPhase.None;
                member.GenderOrdinal = memberIndex;
                return;
            }
            member.Gender = AnimGender.Male;
            member.GenderOrdinal = 0;
        }

        private static void CycleMemberPhase(AnimGroupMemberData member)
        {
            switch (member.Phase)
            {
                case AnimPhase.None:
                    member.Phase = AnimPhase.In;
                    break;
                case AnimPhase.In:
                    member.Phase = AnimPhase.Loop;
                    break;
                case AnimPhase.Loop:
                    member.Phase = AnimPhase.Out;
                    break;
                default:
                    member.Phase = AnimPhase.None;
                    break;
            }
        }

        // ---- Grouped grid card ----------------------------------------------

        private void DrawGroupGridCard(AnimDisplayEntry entry, int visibleIndex, float cellInnerW, float cardOuterH, bool searchDimmed)
        {
            AnimDisplayGroup group = entry.Group!;
            InitStyles();
            GUIStyle cardBox = IsEntrySelected(entry) ? _animCardSelectedStyle! : _animGroupCardStyle!;
            float innerW = Mathf.Max(40f, cellInnerW);
            float rowH = AnimBrowserScale.Px(RoleButtonRowHeightBase);
            int rows = (group.HasPhases ? 1 : 0) + (group.HasGenders ? 1 : 0) + (group.HasSlotIndexButtons ? 1 : 0);
            float thumbH = Mathf.Max(24f, innerW - rows * rowH);

            GUILayout.BeginVertical(
                cardBox,
                GUILayout.Width(cellInnerW),
                GUILayout.MaxWidth(cellInnerW),
                GUILayout.MinHeight(cardOuterH),
                GUILayout.Height(cardOuterH),
                GUILayout.ExpandWidth(false));

            Rect thumbRect = GUILayoutUtility.GetRect(innerW, thumbH, GUILayout.Width(innerW), GUILayout.Height(thumbH));
            Rect cbRect = GridCheckboxRect(thumbRect);
            Texture2D? tex = GetGroupThumbnail(group, Mathf.RoundToInt(innerW));
            Event ev = Event.current;
            Color prevGuiColor = GUI.color;
            BeginSearchDimDraw(searchDimmed, ref prevGuiColor);
            if (ev.type == EventType.Repaint)
            {
                if (tex != null)
                    GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit, false);
                else
                    GUI.Box(thumbRect, GUIContent.none);
                DrawCheckboxVisual(cbRect, IsEntrySelected(entry));
            }
            else if (ev.type == EventType.MouseDown && (ev.button == 0 || ev.button == 1))
            {
                if (ev.button == 0 && TryHandleEntryCheckbox(entry, visibleIndex, cbRect, ev))
                {
                }
                else if (thumbRect.Contains(ev.mousePosition))
                {
                    HandleEntryActivate(entry, visibleIndex);
                    ev.Use();
                }
            }

            EndSearchDimDraw(searchDimmed, prevGuiColor);

            if (group.HasPhases)
            {
                Rect phaseRow = GUILayoutUtility.GetRect(innerW, rowH, GUILayout.Width(innerW), GUILayout.Height(rowH));
                _activePhaseByGroupId.TryGetValue(group.Id, out AnimPhase active);
                bool hasActive = _activePhaseByGroupId.ContainsKey(group.Id);
                GUIContent[] phaseContents = group.PhaseContents;
                float gap = AnimBrowserScale.Px(2f);
                float w = ColumnWidth(phaseRow.width, phaseContents.Length, gap);
                for (int i = 0; i < phaseContents.Length; i++)
                {
                    var r = new Rect(phaseRow.x + i * (w + gap), phaseRow.y, w, phaseRow.height);
                    GUIStyle style = hasActive && group.Phases[i] == active ? _roleButtonActiveStyle! : _roleButtonStyle!;
                    if (GUI.Button(r, phaseContents[i], style))
                        OnPhaseButtonClicked(group, group.Phases[i], Event.current);
                }
            }

            if (group.HasGenders)
            {
                Rect genderRow = GUILayoutUtility.GetRect(innerW, rowH, GUILayout.Width(innerW), GUILayout.Height(rowH));
                bool gendersEnabled = !group.HasPhases || _activePhaseByGroupId.ContainsKey(group.Id);
                GUIContent[] genderContents = group.GenderContents;
                float gap = AnimBrowserScale.Px(2f);
                float w = ColumnWidth(genderRow.width, genderContents.Length, gap);
                bool prevEnabled = GUI.enabled;
                GUI.enabled = gendersEnabled;
                for (int i = 0; i < genderContents.Length; i++)
                {
                    var r = new Rect(genderRow.x + i * (w + gap), genderRow.y, w, genderRow.height);
                    if (GUI.Button(r, genderContents[i], _roleButtonStyle!))
                        OnGenderButtonClicked(group, group.GenderParticipants[i]);
                }
                GUI.enabled = prevEnabled;
            }

            if (group.HasSlotIndexButtons)
            {
                Rect indexRow = GUILayoutUtility.GetRect(innerW, rowH, GUILayout.Width(innerW), GUILayout.Height(rowH));
                GUIContent[] indexContents = group.SlotIndexContents;
                float gap = AnimBrowserScale.Px(2f);
                float w = ColumnWidth(indexRow.width, indexContents.Length, gap);
                for (int i = 0; i < indexContents.Length; i++)
                {
                    var r = new Rect(indexRow.x + i * (w + gap), indexRow.y, w, indexRow.height);
                    if (GUI.Button(r, indexContents[i], _roleButtonStyle!))
                        OnSlotIndexButtonClicked(group, i);
                }
            }

            Rect nameRect = GUILayoutUtility.GetRect(innerW, CardNameRowH, GUILayout.Width(innerW), GUILayout.Height(CardNameRowH));
            if (ev.type == EventType.Repaint)
            {
                float nameW = Mathf.Max(20f, nameRect.width - CardTextPadH * 2f);
                var inner = new Rect(nameRect.x + CardTextPadH, nameRect.y, nameW, nameRect.height);
                string displayLabel = group.GetDisplayLabel();
                string tooltip = searchDimmed
                    ? displayLabel + SearchDimmedEntryTooltipSuffix
                    : displayLabel;
                Color prevNameColor = GUI.color;
                BeginSearchDimDraw(searchDimmed, ref prevNameColor);
                GUI.Label(inner, new GUIContent(displayLabel, tooltip), _animCardNameStyle!);
                EndSearchDimDraw(searchDimmed, prevNameColor);
            }

            GUILayout.EndVertical();
        }

        private static float ColumnWidth(float total, int count, float gap) =>
            count <= 0 ? total : (total - gap * (count - 1)) / count;

        private void OnPhaseButtonClicked(AnimDisplayGroup group, AnimPhase phase, Event ev)
        {
            _activePhaseByGroupId[group.Id] = phase;
            if (!(ev.control || ev.command))
                ApplyGroupPhase(group, phase);
        }

        private void OnGenderButtonClicked(AnimDisplayGroup group, AnimGroupSlot participant)
        {
            if (group.HasPhases && !_activePhaseByGroupId.ContainsKey(group.Id))
                return;
            AnimPhase phase = group.HasPhases ? _activePhaseByGroupId[group.Id] : AnimPhase.None;
            ApplyGroupMember(group, phase, participant);
        }

        private void OnSlotIndexButtonClicked(AnimDisplayGroup group, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= group.Slots.Count)
                return;
            IList<OCIChar> selection = GetSelectionForApply();
            if (selection.Count == 0)
                return;
            AnimGroupSlot slot = group.Slots[slotIndex];
            List<OCIChar> chars = ResolveGenderChars(selection, AnimGender.Unknown);
            if (chars.Count == 0)
                return;
            OCIChar target = slotIndex < chars.Count ? chars[slotIndex] : chars[0];
            AnimPlaybackService.ApplyAnimation(slot.Item, new List<OCIChar> { target });
            SyncControlsFromSelectionIfChanged(force: true);
        }

        private static Texture2D? GetGroupThumbnail(AnimDisplayGroup group, int sizePx)
        {
            AnimGridItem? item = group.Slots.Count > 0 ? group.Slots[0].Item : null;
            if (item == null)
                return null;
            if (item.Thumbnail == null && !item.ThumbnailFailed && !item.ThumbnailRequested)
            {
                item.ThumbnailRequested = true;
                item.Thumbnail = AnimThumbnailService.GetPlaceholder(item, sizePx);
            }
            return item.Thumbnail;
        }

        // ---- List rows ------------------------------------------------------

        private void DrawListEntryRow(AnimDisplayEntry entry, int visibleIndex, bool searchDimmed)
        {
            if (entry.IsGroup)
                DrawGroupListRow(entry, visibleIndex, searchDimmed);
            else
                DrawSingleListRow(entry, visibleIndex, searchDimmed);
        }

        private void DrawSingleListRow(AnimDisplayEntry entry, int visibleIndex, bool searchDimmed)
        {
            AnimGridItem item = entry.Single!;
            float rowH = AnimBrowserScale.Px(ListRowHeightBase);
            GUILayout.BeginHorizontal(AnimBrowserScale.H(ListRowHeightBase));
            var cbRect = GUILayoutUtility.GetRect(20f, rowH, GUILayout.Width(20f), GUILayout.Height(rowH));
            Event ev = Event.current;
            if (ev.type == EventType.Repaint)
                DrawCheckboxVisual(cbRect, IsEntrySelected(entry));
            else if (ev.type == EventType.MouseDown && ev.button == 0)
                TryHandleEntryCheckbox(entry, visibleIndex, cbRect, ev);

            Color prev = GUI.backgroundColor;
            if (IsEntrySelected(entry))
                GUI.backgroundColor = new Color(0.22f, 0.48f, 0.98f, 1f);
            GUIContent rowContent = _displayCatalog.GetItemDisplayContent(item);
            if (searchDimmed)
                rowContent = new GUIContent(rowContent.text, rowContent.text + SearchDimmedEntryTooltipSuffix);
            Color prevGuiColor = GUI.color;
            BeginSearchDimDraw(searchDimmed, ref prevGuiColor);
            if (GUILayout.Button(rowContent, _listRowStyle!, GUILayout.ExpandWidth(true), AnimBrowserScale.H(ListRowHeightBase)))
                HandleEntryActivate(entry, visibleIndex);
            EndSearchDimDraw(searchDimmed, prevGuiColor);
            GUI.backgroundColor = prev;
            GUILayout.EndHorizontal();
        }

        private void DrawGroupListRow(AnimDisplayEntry entry, int visibleIndex, bool searchDimmed)
        {
            AnimDisplayGroup group = entry.Group!;
            float rowH = AnimBrowserScale.Px(ListRowHeightBase);
            GUILayout.BeginHorizontal(AnimBrowserScale.H(ListRowHeightBase));
            var cbRect = GUILayoutUtility.GetRect(20f, rowH, GUILayout.Width(20f), GUILayout.Height(rowH));
            Event ev = Event.current;
            if (ev.type == EventType.Repaint)
                DrawCheckboxVisual(cbRect, IsEntrySelected(entry));
            else if (ev.type == EventType.MouseDown && ev.button == 0)
                TryHandleEntryCheckbox(entry, visibleIndex, cbRect, ev);

            Color prev = GUI.backgroundColor;
            if (IsEntrySelected(entry))
                GUI.backgroundColor = new Color(0.22f, 0.48f, 0.98f, 1f);
            GUIContent rowContent = group.GetListContent("Apply main phase; checkbox or Ctrl/Shift for selection");
            if (searchDimmed)
                rowContent = new GUIContent(rowContent.text, rowContent.text + SearchDimmedEntryTooltipSuffix);
            Color prevGuiColor = GUI.color;
            BeginSearchDimDraw(searchDimmed, ref prevGuiColor);
            if (GUILayout.Button(rowContent, _listRowStyle!, GUILayout.ExpandWidth(true), AnimBrowserScale.H(ListRowHeightBase)))
                HandleEntryActivate(entry, visibleIndex);
            EndSearchDimDraw(searchDimmed, prevGuiColor);
            GUI.backgroundColor = prev;

            float roleH = AnimBrowserScale.Px(ListRowHeightBase - 2f);
            float roleW = AnimBrowserScale.Px(28f);
            if (group.HasPhases)
            {
                var phaseContents = group.PhaseContents;
                _activePhaseByGroupId.TryGetValue(group.Id, out AnimPhase active);
                bool hasActive = _activePhaseByGroupId.ContainsKey(group.Id);
                for (int i = 0; i < phaseContents.Length; i++)
                {
                    GUIStyle style = hasActive && group.Phases[i] == active ? _roleButtonActiveStyle! : _roleButtonStyle!;
                    if (GUILayout.Button(phaseContents[i], style, GUILayout.Width(roleW), GUILayout.Height(roleH)))
                        OnPhaseButtonClicked(group, group.Phases[i], Event.current);
                }
            }
            if (group.HasGenders)
            {
                var genderContents = group.GenderContents;
                bool gendersEnabled = !group.HasPhases || _activePhaseByGroupId.ContainsKey(group.Id);
                bool prevEnabled = GUI.enabled;
                GUI.enabled = gendersEnabled;
                for (int i = 0; i < genderContents.Length; i++)
                {
                    if (GUILayout.Button(genderContents[i], _roleButtonStyle!, GUILayout.Width(roleW), GUILayout.Height(roleH)))
                        OnGenderButtonClicked(group, group.GenderParticipants[i]);
                }
                GUI.enabled = prevEnabled;
            }
            if (group.HasSlotIndexButtons)
            {
                var indexContents = group.SlotIndexContents;
                for (int i = 0; i < indexContents.Length; i++)
                {
                    if (GUILayout.Button(indexContents[i], _roleButtonStyle!, GUILayout.Width(roleW), GUILayout.Height(roleH)))
                        OnSlotIndexButtonClicked(group, i);
                }
            }
            GUILayout.EndHorizontal();
        }

        // ---- Apply ----------------------------------------------------------

        private void ApplyGroupPhase(AnimDisplayGroup group, AnimPhase phase)
        {
            IList<OCIChar> selection = GetSelectionForApply();
            if (selection.Count == 0)
                return;

            var maleSlots = new List<AnimGroupSlot>();
            var femaleSlots = new List<AnimGroupSlot>();
            var unknownSlots = new List<AnimGroupSlot>();
            for (int i = 0; i < group.Slots.Count; i++)
            {
                AnimGroupSlot slot = group.Slots[i];
                if (group.HasPhases && slot.Phase != phase)
                    continue;
                switch (slot.Gender)
                {
                    case AnimGender.Male: maleSlots.Add(slot); break;
                    case AnimGender.Female: femaleSlots.Add(slot); break;
                    default: unknownSlots.Add(slot); break;
                }
            }

            maleSlots.Sort((a, b) => a.GenderOrdinal.CompareTo(b.GenderOrdinal));
            femaleSlots.Sort((a, b) => a.GenderOrdinal.CompareTo(b.GenderOrdinal));

            bool distributed = false;
            distributed |= ApplySlotStream(maleSlots, ResolveGenderChars(selection, AnimGender.Male));
            distributed |= ApplySlotStream(femaleSlots, ResolveGenderChars(selection, AnimGender.Female));

            if (!distributed && unknownSlots.Count > 0)
            {
                List<OCIChar> any = ResolveGenderChars(selection, AnimGender.Unknown);
                if (unknownSlots.Count == 1)
                    AnimPlaybackService.ApplyAnimation(unknownSlots[0].Item, any);
                else
                    ApplySlotStream(unknownSlots, any);
            }

            SyncControlsFromSelectionIfChanged(force: true);
        }

        private void ApplyGroupMember(AnimDisplayGroup group, AnimPhase phase, AnimGroupSlot participant)
        {
            AnimGroupSlot? slot = group.FindSlot(phase, participant.Gender, participant.GenderOrdinal);
            if (slot == null)
                return;
            IList<OCIChar> selection = GetSelectionForApply();
            if (selection.Count == 0)
                return;
            List<OCIChar> chars = ResolveGenderChars(selection, slot.Gender);
            if (chars.Count == 0)
                return;
            OCIChar target = slot.GenderOrdinal < chars.Count ? chars[slot.GenderOrdinal] : chars[0];
            AnimPlaybackService.ApplyAnimation(slot.Item, new List<OCIChar> { target });
            SyncControlsFromSelectionIfChanged(force: true);
        }

        private static bool ApplySlotStream(List<AnimGroupSlot> slots, List<OCIChar> chars)
        {
            bool any = false;
            for (int i = 0; i < slots.Count && i < chars.Count; i++)
            {
                AnimPlaybackService.ApplyAnimation(slots[i].Item, new List<OCIChar> { chars[i] });
                any = true;
            }
            return any;
        }

        private IList<OCIChar> GetSelectionForApply()
        {
            RefreshStudioSelectionCacheIfDue(force: true);
            var selection = GetCachedStudioSelectedCharacters();
            if (selection.Count > 0)
                _characterConfig.ReloadFromDisk();
            return selection;
        }

        private List<OCIChar> ResolveGenderChars(IList<OCIChar> selection, AnimGender gender)
        {
            var result = new List<OCIChar>();
            if (gender == AnimGender.Unknown)
            {
                foreach (var c in StudioCharacterPriorityResolver.ResolveForApply(
                             _characterConfig, selection, StudioCharacterGenderFilter.Any, appendUnlistedSelected: true))
                    result.Add(c);
                return result;
            }

            bool wantFemale = gender == AnimGender.Female;
            var priority = ((IStudioCharacterPriorityList)_characterConfig).Priority;
            if (priority != null && priority.Count > 0)
            {
                var filter = wantFemale ? StudioCharacterGenderFilter.Female : StudioCharacterGenderFilter.Male;
                foreach (var c in StudioCharacterPriorityResolver.ResolveForApply(
                             _characterConfig, selection, filter, appendUnlistedSelected: false))
                    result.Add(c);
            }

            if (result.Count == 0)
            {
                for (int i = 0; i < selection.Count; i++)
                {
                    if (StudioCharacterSelection.IsFemaleCharacter(selection[i]) == wantFemale)
                        result.Add(selection[i]);
                }
            }
            return result;
        }
    }
}
