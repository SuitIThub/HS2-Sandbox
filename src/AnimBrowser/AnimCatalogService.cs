using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal sealed class AnimCatalogService
    {
        private const int GroupsPerWarmupSlice = 2;
        private const int CategoriesPerWarmupSlice = 4;

        private static FieldInfo? _animeSortField;
        private static bool _animeSortFieldResolved;

        private bool _built;
        private bool _buildInProgress;
        private bool _hadCatalogSourceAtLastBuild;
        private readonly List<AnimCategoryNode> _rootGroups = new List<AnimCategoryNode>();
        private readonly Dictionary<int, Dictionary<int, List<AnimGridItem>>> _itemsByGroupCategory =
            new Dictionary<int, Dictionary<int, List<AnimGridItem>>>();
        private readonly Dictionary<AnimCatalogRef, AnimGridItem> _itemsByRef =
            new Dictionary<AnimCatalogRef, AnimGridItem>();

        public bool BuildComplete => _built;
        public bool BuildInProgress => _buildInProgress;
        public bool RequiresRebuild => NeedsRebuild();
        public float BuildProgress { get; private set; }

        public IList<AnimCategoryNode> RootGroups
        {
            get
            {
                EnsureBuilt();
                return _rootGroups;
            }
        }

        public void Invalidate()
        {
            _built = false;
            _hadCatalogSourceAtLastBuild = false;
            BuildProgress = 0f;
            _rootGroups.Clear();
            _itemsByGroupCategory.Clear();
            _itemsByRef.Clear();
        }

        public AnimGridItem? TryGetItem(AnimCatalogRef reference)
        {
            EnsureBuilt();
            return _itemsByRef.TryGetValue(reference, out var item) ? item : null;
        }

        public string GetGroupName(int groupId)
        {
            EnsureBuilt();
            for (int gi = 0; gi < _rootGroups.Count; gi++)
            {
                AnimCategoryNode group = _rootGroups[gi];
                if (group.GroupId == groupId)
                    return group.Name;
            }
            return "Group " + groupId;
        }

        public string GetCategoryName(int groupId, int categoryId)
        {
            EnsureBuilt();
            if (!_itemsByGroupCategory.TryGetValue(groupId, out Dictionary<int, List<AnimGridItem>>? byCategory))
                return "Category " + categoryId;
            foreach (var kvp in byCategory)
            {
                if (kvp.Key != categoryId || kvp.Value.Count == 0)
                    continue;
                for (int gi = 0; gi < _rootGroups.Count; gi++)
                {
                    AnimCategoryNode group = _rootGroups[gi];
                    if (group.GroupId != groupId)
                        continue;
                    for (int ci = 0; ci < group.Children.Count; ci++)
                    {
                        AnimCategoryNode category = group.Children[ci];
                        if (category.CategoryId == categoryId)
                            return category.Name;
                    }
                }
            }
            return "Category " + categoryId;
        }

        public string GetCatalogPath(int groupId, int categoryId) =>
            GetGroupName(groupId) + " / " + GetCategoryName(groupId, categoryId);

        public string GetCatalogPath(AnimGridItem item) =>
            GetCatalogPath(item.Group, item.Category);

        public void EnsureBuilt()
        {
            if (_built && !NeedsRebuild())
                return;
            if (_buildInProgress)
                return;
            Build();
            _built = true;
        }

        /// <summary>Builds the catalog incrementally across frames so Studio startup stays responsive.</summary>
        public IEnumerator WarmupCoroutine()
        {
            if (_built && !NeedsRebuild())
                yield break;
            if (_buildInProgress)
            {
                while (_buildInProgress)
                    yield return null;
                yield break;
            }

            _buildInProgress = true;
            BuildProgress = 0f;
            try
            {
                yield return BuildCoroutine();
                _built = true;
                BuildProgress = 1f;
            }
            finally
            {
                _buildInProgress = false;
            }
        }

        private IEnumerator BuildCoroutine()
        {
            _rootGroups.Clear();
            _itemsByGroupCategory.Clear();
            _itemsByRef.Clear();
            _hadCatalogSourceAtLastBuild = false;

            Info? info = null;
            try
            {
                info = Singleton<Info>.Instance;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimBrowser: Could not access animation catalog: " + ex.Message);
                yield break;
            }

            if (info?.dicAnimeLoadInfo == null)
                yield break;

            _hadCatalogSourceAtLastBuild = info.dicAnimeLoadInfo.Count > 0;

            var groupIds = new List<int>(info.dicAnimeLoadInfo.Keys);
            groupIds.Sort();
            int totalGroups = groupIds.Count;

            int groupsSinceYield = 0;
            for (int gi = 0; gi < groupIds.Count; gi++)
            {
                int groupId = groupIds[gi];
                if (!info.dicAnimeLoadInfo.TryGetValue(groupId, out var byCategory) || byCategory == null)
                {
                    BuildProgress = totalGroups > 0 ? (gi + 1) / (float)totalGroups : 1f;
                    continue;
                }

                var groupNode = new AnimCategoryNode
                {
                    GroupId = groupId,
                    CategoryId = -1,
                    Name = TryGetGroupName(info, groupId),
                    Depth = 0,
                    IsGroup = true,
                    IsExpanded = false
                };

                var categoryIds = new List<int>(byCategory.Keys);
                categoryIds.Sort();
                var itemsByCategory = new Dictionary<int, List<AnimGridItem>>();
                int categoriesSinceYield = 0;

                for (int ci = 0; ci < categoryIds.Count; ci++)
                {
                    int categoryId = categoryIds[ci];
                    var items = BuildItemsForCategory(info, groupId, categoryId, byCategory);
                    if (items.Count == 0)
                        continue;

                    itemsByCategory[categoryId] = items;
                    for (int ii = 0; ii < items.Count; ii++)
                    {
                        var it = items[ii];
                        _itemsByRef[new AnimCatalogRef(it.Group, it.Category, it.No)] = it;
                    }
                    groupNode.Children.Add(new AnimCategoryNode
                    {
                        GroupId = groupId,
                        CategoryId = categoryId,
                        Name = TryGetCategoryName(info, groupId, categoryId),
                        Depth = 1,
                        IsGroup = false,
                        IsExpanded = false
                    });

                    categoriesSinceYield++;
                    if (categoriesSinceYield >= CategoriesPerWarmupSlice)
                    {
                        categoriesSinceYield = 0;
                        yield return null;
                    }
                }

                if (groupNode.Children.Count == 0)
                {
                    BuildProgress = totalGroups > 0 ? (gi + 1) / (float)totalGroups : 1f;
                    continue;
                }

                groupNode.Children.Sort(AnimCategoryNode.CompareByDisplayLabel);
                _itemsByGroupCategory[groupId] = itemsByCategory;
                _rootGroups.Add(groupNode);
                BuildProgress = totalGroups > 0 ? (gi + 1) / (float)totalGroups : 1f;

                groupsSinceYield++;
                if (groupsSinceYield >= GroupsPerWarmupSlice)
                {
                    groupsSinceYield = 0;
                    yield return null;
                }
            }

            _rootGroups.Sort(AnimCategoryNode.CompareByDisplayLabel);
            BuildProgress = 1f;
        }

        private bool NeedsRebuild()
        {
            if (_rootGroups.Count > 0)
                return false;

            return TryGetCatalogGroupCount(out int count) && count > 0 && !_hadCatalogSourceAtLastBuild;
        }

        public IList<AnimGridItem> GetItemsForSelection(int groupId, int categoryId)
        {
            EnsureBuilt();
            var result = new List<AnimGridItem>();
            if (groupId < 0)
                return result;

            if (!_itemsByGroupCategory.TryGetValue(groupId, out var byCategory))
                return result;

            if (categoryId >= 0)
            {
                if (byCategory.TryGetValue(categoryId, out var list))
                    result.AddRange(list);
                return result;
            }

            foreach (var kvp in byCategory)
                result.AddRange(kvp.Value);
            result.Sort(AnimGridItem.Compare);
            return result;
        }

        private void Build()
        {
            _rootGroups.Clear();
            _itemsByGroupCategory.Clear();
            _itemsByRef.Clear();
            _hadCatalogSourceAtLastBuild = false;

            try
            {
                var info = Singleton<Info>.Instance;
                if (info.dicAnimeLoadInfo == null)
                    return;

                _hadCatalogSourceAtLastBuild = info.dicAnimeLoadInfo.Count > 0;

                var groupIds = new List<int>(info.dicAnimeLoadInfo.Keys);
                groupIds.Sort();

                for (int gi = 0; gi < groupIds.Count; gi++)
                {
                    int groupId = groupIds[gi];
                    if (!info.dicAnimeLoadInfo.TryGetValue(groupId, out var byCategory) || byCategory == null)
                        continue;

                    var groupNode = new AnimCategoryNode
                    {
                        GroupId = groupId,
                        CategoryId = -1,
                        Name = TryGetGroupName(info, groupId),
                        Depth = 0,
                        IsGroup = true,
                        IsExpanded = false
                    };

                    var categoryIds = new List<int>(byCategory.Keys);
                    categoryIds.Sort();
                    var itemsByCategory = new Dictionary<int, List<AnimGridItem>>();

                    for (int ci = 0; ci < categoryIds.Count; ci++)
                    {
                        int categoryId = categoryIds[ci];
                        var items = BuildItemsForCategory(info, groupId, categoryId, byCategory);
                        if (items.Count == 0)
                            continue;

                        itemsByCategory[categoryId] = items;
                        for (int ii = 0; ii < items.Count; ii++)
                        {
                            var it = items[ii];
                            _itemsByRef[new AnimCatalogRef(it.Group, it.Category, it.No)] = it;
                        }
                        groupNode.Children.Add(new AnimCategoryNode
                        {
                            GroupId = groupId,
                            CategoryId = categoryId,
                            Name = TryGetCategoryName(info, groupId, categoryId),
                            Depth = 1,
                            IsGroup = false,
                            IsExpanded = false
                        });
                    }

                    if (groupNode.Children.Count == 0)
                        continue;

                    groupNode.Children.Sort(AnimCategoryNode.CompareByDisplayLabel);
                    _itemsByGroupCategory[groupId] = itemsByCategory;
                    _rootGroups.Add(groupNode);
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimBrowser: Could not build animation catalog: " + ex.Message);
            }

            BuildProgress = 1f;
            _rootGroups.Sort(AnimCategoryNode.CompareByDisplayLabel);
        }

        public void InvalidateDisplayCaches()
        {
            for (int gi = 0; gi < _rootGroups.Count; gi++)
            {
                var group = _rootGroups[gi];
                group.InvalidateDisplayCaches();
                for (int ci = 0; ci < group.Children.Count; ci++)
                    group.Children[ci].InvalidateDisplayCaches();
            }

            foreach (var byCategory in _itemsByGroupCategory.Values)
            {
                foreach (var list in byCategory.Values)
                {
                    for (int i = 0; i < list.Count; i++)
                        list[i].InvalidateDisplayCaches();
                }
            }
        }

        private static List<AnimGridItem> BuildItemsForCategory(
            Info info,
            int groupId,
            int categoryId,
            Dictionary<int, Dictionary<int, Info.AnimeLoadInfo>> byCategory)
        {
            var items = new List<AnimGridItem>();
            if (!byCategory.TryGetValue(categoryId, out var byNo) || byNo == null)
                return items;

            foreach (var kvp in byNo)
            {
                Info.AnimeLoadInfo? loadInfo = kvp.Value;
                if (loadInfo == null)
                    continue;
                bool isStudioListed = AnimCatalogResolve.IsStudioListedCategory(groupId, categoryId);
                items.Add(new AnimGridItem(
                    groupId,
                    categoryId,
                    kvp.Key,
                    loadInfo.name,
                    TryGetAnimeSort(loadInfo),
                    AnimCatalogResolve.IsMainGameLoadInfo(loadInfo),
                    isStudioListed));
            }

            items.Sort(AnimGridItem.Compare);
            return items;
        }

        private static string TryGetGroupName(Info info, int groupId)
        {
            try
            {
                if (info.dicAGroupCategory != null &&
                    info.dicAGroupCategory.TryGetValue(groupId, out Info.GroupInfo? groupInfo) &&
                    groupInfo != null &&
                    !string.IsNullOrEmpty(groupInfo.name))
                {
                    return groupInfo.name;
                }
            }
            catch
            {
                // ignored
            }

            return "Group " + groupId;
        }

        private static string TryGetCategoryName(Info info, int groupId, int categoryId)
        {
            try
            {
                if (info.dicAGroupCategory != null &&
                    info.dicAGroupCategory.TryGetValue(groupId, out Info.GroupInfo? groupInfo) &&
                    groupInfo != null &&
                    groupInfo.dicCategory != null)
                {
                    if (groupInfo.dicCategory.TryGetValue(categoryId, out Info.CategoryInfo? categoryInfo) &&
                        categoryInfo != null &&
                        !string.IsNullOrEmpty(categoryInfo.name))
                    {
                        return categoryInfo.name;
                    }

                    var idict = groupInfo.dicCategory as System.Collections.IDictionary;
                    if (idict != null && idict.Contains(categoryId))
                    {
                        object? entry = idict[categoryId];
                        if (entry is string strName && !string.IsNullOrEmpty(strName))
                            return strName;
                        if (entry != null)
                        {
                            string? name = ReadNameMember(entry);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            return "Category " + categoryId;
        }

        private static string? ReadNameMember(object entry)
        {
            try
            {
                PropertyInfo? prop = entry.GetType().GetProperty(
                    "name",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (prop != null && prop.PropertyType == typeof(string))
                    return prop.GetValue(entry, null) as string;
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static int TryGetAnimeSort(Info.AnimeLoadInfo loadInfo)
        {
            if (!_animeSortFieldResolved)
            {
                _animeSortFieldResolved = true;
                try
                {
                    _animeSortField = loadInfo.GetType().GetField(
                        "sort",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch
                {
                    _animeSortField = null;
                }
            }

            if (_animeSortField == null || _animeSortField.FieldType != typeof(int))
                return 0;

            try
            {
                return (int)_animeSortField.GetValue(loadInfo);
            }
            catch
            {
                return 0;
            }
        }

        private static bool TryGetCatalogGroupCount(out int count)
        {
            count = 0;
            try
            {
                var info = Singleton<Info>.Instance;
                if (info?.dicAnimeLoadInfo == null)
                    return false;
                count = info.dicAnimeLoadInfo.Count;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
