using System;
using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    /// <summary>In-memory store of user-defined animation groups and tree merges, backed by
    /// <c>anim_browser_groups.json</c>. Owns lookups used by the display catalog and apply logic.</summary>
    internal sealed class AnimGroupStore
    {
        private readonly List<AnimTreeMergeRule> _treeMerges = new List<AnimTreeMergeRule>();
        private readonly List<AnimDisplayGroupData> _displayGroups = new List<AnimDisplayGroupData>();
        private readonly Dictionary<string, string> _displayNameOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<AnimCatalogRef, AnimDisplayGroupData> _groupByRef =
            new Dictionary<AnimCatalogRef, AnimDisplayGroupData>();

        public IList<AnimTreeMergeRule> TreeMerges => _treeMerges;
        public IList<AnimDisplayGroupData> DisplayGroups => _displayGroups;

        /// <summary>Raised whenever the store mutates so views/persistence can react.</summary>
        public event Action? Changed;

        public void Load()
        {
            _treeMerges.Clear();
            _displayGroups.Clear();
            _displayNameOverrides.Clear();
            AnimGroupPersistence.Load(_treeMerges, _displayGroups, _displayNameOverrides);
            RebuildIndex();
            Changed?.Invoke();
        }

        public string? GetDisplayNameOverride(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            return _displayNameOverrides.TryGetValue(key, out string? name) ? name : null;
        }

        public void SetDisplayNameOverride(string key, string? displayName)
        {
            if (string.IsNullOrEmpty(key))
                return;
            string trimmed = (displayName ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                if (!_displayNameOverrides.Remove(key))
                    return;
            }
            else if (_displayNameOverrides.TryGetValue(key, out string? existing) &&
                     string.Equals(existing, trimmed, StringComparison.Ordinal))
            {
                return;
            }
            else
            {
                _displayNameOverrides[key] = trimmed;
            }
            Save();
            Changed?.Invoke();
        }

        public string ResolveCatalogName(string? overrideKey, string catalogName)
        {
            string? ov = overrideKey != null ? GetDisplayNameOverride(overrideKey) : null;
            return ov ?? catalogName;
        }

        public string GetAnimationDisplayLabel(AnimGridItem item)
        {
            string name = ResolveCatalogName(AnimDisplayNameKeys.Animation(item), item.DisplayName);
            return StudioAutoTranslation.Resolve(name);
        }

        public bool RenameTreeMergeRule(string ruleId, string newName)
        {
            AnimTreeMergeRule? rule = FindTreeMerge(ruleId);
            if (rule == null)
                return false;
            string trimmed = newName.Trim();
            if (trimmed.Length == 0)
                return false;
            if (string.Equals(rule.Name, trimmed, StringComparison.Ordinal))
                return true;
            rule.Name = trimmed;
            Save();
            Changed?.Invoke();
            return true;
        }

        public bool RenameDisplayGroup(string groupId, string newName)
        {
            for (int i = 0; i < _displayGroups.Count; i++)
            {
                AnimDisplayGroupData group = _displayGroups[i];
                if (!string.Equals(group.Id, groupId, StringComparison.Ordinal))
                    continue;
                string trimmed = newName.Trim();
                if (trimmed.Length == 0)
                    return false;
                if (string.Equals(group.Name, trimmed, StringComparison.Ordinal))
                    return true;
                group.Name = trimmed;
                Save();
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public void Save() => AnimGroupPersistence.Save(_treeMerges, _displayGroups, _displayNameOverrides);

        public AnimDisplayGroupData? GetGroupForRef(AnimCatalogRef reference) =>
            _groupByRef.TryGetValue(reference, out var group) ? group : null;

        public AnimDisplayGroupData? FindDisplayGroup(string id)
        {
            for (int i = 0; i < _displayGroups.Count; i++)
            {
                AnimDisplayGroupData group = _displayGroups[i];
                if (string.Equals(group.Id, id, StringComparison.Ordinal))
                    return group;
            }
            return null;
        }

        public bool IsGrouped(AnimCatalogRef reference) => _groupByRef.ContainsKey(reference);

        public void AddDisplayGroups(IEnumerable<AnimDisplayGroupData> groups)
        {
            foreach (var group in groups)
            {
                if (group.Members.Count < 2)
                    continue;
                RemoveMembersFromExistingGroups(group);
                _displayGroups.Add(group);
            }
            RebuildIndex();
            Save();
            Changed?.Invoke();
        }

        public void RemoveDisplayGroup(string id)
        {
            _displayGroups.RemoveAll(g => string.Equals(g.Id, id, StringComparison.Ordinal));
            RebuildIndex();
            Save();
            Changed?.Invoke();
        }

        /// <summary>Removes display groups that contain any animation from the given categories.</summary>
        public void RemoveDisplayGroupsTouchingCategories(IList<AnimCatalogRef> categories)
        {
            if (categories == null || categories.Count == 0)
                return;

            int removed = _displayGroups.RemoveAll(group =>
            {
                for (int m = 0; m < group.Members.Count; m++)
                {
                    AnimCatalogRef memberRef = group.Members[m].Ref;
                    var memberCategory = AnimCatalogRefUtil.CategoryRef(memberRef.Group, memberRef.Category);
                    if (AnimCatalogRefUtil.ContainsCategory(categories, memberCategory))
                        return true;
                }
                return false;
            });

            if (removed == 0)
                return;

            RebuildIndex();
            Save();
            Changed?.Invoke();
        }

        public void AddTreeMerge(AnimTreeMergeRule rule)
        {
            _treeMerges.Add(rule);
            Save();
            Changed?.Invoke();
        }

        public void RemoveTreeMerge(string id)
        {
            _treeMerges.RemoveAll(r => string.Equals(r.Id, id, StringComparison.Ordinal));
            Save();
            Changed?.Invoke();
        }

        /// <summary>Removes one virtual subcategory bucket from a group merge by excluding its source categories.</summary>
        public void PartialUnmergeSubcategory(string mergeRuleId, IList<AnimCatalogRef> sourceCategories)
        {
            AnimTreeMergeRule? rule = FindTreeMerge(mergeRuleId);
            if (rule == null || sourceCategories.Count == 0)
                return;

            RemoveDisplayGroupsTouchingCategories(sourceCategories);

            for (int i = 0; i < sourceCategories.Count; i++)
            {
                AnimCatalogRef src = sourceCategories[i];
                var catRef = AnimCatalogRefUtil.CategoryRef(src.Group, src.Category);
                if (!AnimCatalogRefUtil.ContainsCategory(rule.ExcludedSources, catRef))
                    rule.ExcludedSources.Add(catRef);
            }

            Save();
            Changed?.Invoke();
        }

        public AnimTreeMergeRule? FindTreeMerge(string id)
        {
            for (int i = 0; i < _treeMerges.Count; i++)
            {
                if (string.Equals(_treeMerges[i].Id, id, StringComparison.Ordinal))
                    return _treeMerges[i];
            }
            return null;
        }

        public bool ContainsTreeMerge(string id) => FindTreeMerge(id) != null;

        /// <summary>Finds a group merge whose source top-level groups match <paramref name="groupIds"/> exactly.</summary>
        public AnimTreeMergeRule? FindGroupMergeBySourceGroups(IReadOnlyCollection<int> groupIds)
        {
            if (groupIds == null || groupIds.Count == 0)
                return null;
            var want = new HashSet<int>(groupIds);
            for (int i = 0; i < _treeMerges.Count; i++)
            {
                AnimTreeMergeRule rule = _treeMerges[i];
                if (rule.Kind != AnimTreeMergeKind.Group)
                    continue;
                var have = new HashSet<int>();
                for (int s = 0; s < rule.Sources.Count; s++)
                    have.Add(rule.Sources[s].Group);
                if (have.Count == want.Count && want.IsSubsetOf(have))
                    return rule;
            }
            return null;
        }

        /// <summary>Finds a category merge whose source categories match <paramref name="categoryRefs"/> exactly.</summary>
        public AnimTreeMergeRule? FindCategoryMergeBySources(IReadOnlyCollection<AnimCatalogRef> categoryRefs)
        {
            if (categoryRefs == null || categoryRefs.Count == 0)
                return null;
            var want = new HashSet<AnimCatalogRef>(categoryRefs);
            for (int i = 0; i < _treeMerges.Count; i++)
            {
                AnimTreeMergeRule rule = _treeMerges[i];
                if (rule.Kind != AnimTreeMergeKind.Category)
                    continue;
                var have = new HashSet<AnimCatalogRef>();
                for (int s = 0; s < rule.Sources.Count; s++)
                {
                    AnimCatalogRef src = rule.Sources[s];
                    have.Add(AnimCatalogRefUtil.CategoryRef(src.Group, src.Category));
                }
                if (have.Count == want.Count && want.IsSubsetOf(have))
                    return rule;
            }
            return null;
        }

        /// <summary>Largest existing category merge whose sources are all contained in <paramref name="categoryRefs"/>.</summary>
        public AnimTreeMergeRule? FindCategoryMergeSubsetOf(IReadOnlyCollection<AnimCatalogRef> categoryRefs)
        {
            if (categoryRefs == null || categoryRefs.Count == 0)
                return null;

            var want = new HashSet<AnimCatalogRef>(categoryRefs);
            AnimTreeMergeRule? best = null;
            int bestCount = 0;
            for (int i = 0; i < _treeMerges.Count; i++)
            {
                AnimTreeMergeRule rule = _treeMerges[i];
                if (rule.Kind != AnimTreeMergeKind.Category || rule.Sources.Count == 0)
                    continue;

                bool allContained = true;
                for (int s = 0; s < rule.Sources.Count; s++)
                {
                    AnimCatalogRef src = rule.Sources[s];
                    if (!want.Contains(AnimCatalogRefUtil.CategoryRef(src.Group, src.Category)))
                    {
                        allContained = false;
                        break;
                    }
                }
                if (!allContained || rule.Sources.Count <= bestCount)
                    continue;
                best = rule;
                bestCount = rule.Sources.Count;
            }
            return best;
        }

        public void SupersedeCategoryMerges(HashSet<AnimCatalogRef> unitedCategories, string? exceptRuleId = null)
        {
            if (unitedCategories == null || unitedCategories.Count == 0)
                return;

            bool removed = _treeMerges.RemoveAll(rule =>
            {
                if (rule.Kind != AnimTreeMergeKind.Category)
                    return false;
                if (exceptRuleId != null && string.Equals(rule.Id, exceptRuleId, StringComparison.Ordinal))
                    return false;
                if (rule.Sources.Count == 0)
                    return false;
                for (int i = 0; i < rule.Sources.Count; i++)
                {
                    AnimCatalogRef src = rule.Sources[i];
                    if (!unitedCategories.Contains(AnimCatalogRefUtil.CategoryRef(src.Group, src.Category)))
                        return false;
                }
                return true;
            }) > 0;

            if (removed)
            {
                Save();
                Changed?.Invoke();
            }
        }

        public void ReplaceCategoryMergeSources(AnimTreeMergeRule rule, IReadOnlyCollection<AnimCatalogRef> categoryRefs)
        {
            rule.Sources.Clear();
            var sorted = new List<AnimCatalogRef>(categoryRefs);
            sorted.Sort((a, b) =>
            {
                int cmp = a.Group.CompareTo(b.Group);
                return cmp != 0 ? cmp : a.Category.CompareTo(b.Category);
            });
            for (int i = 0; i < sorted.Count; i++)
            {
                AnimCatalogRef cat = sorted[i];
                rule.Sources.Add(new AnimCatalogRef(cat.Group, cat.Category, -1));
            }
        }

        public void MergeSubcategoryBuckets(string groupMergeRuleId, string targetBucketKey, IList<string> aliasBucketKeys)
        {
            AnimTreeMergeRule? rule = FindTreeMerge(groupMergeRuleId);
            if (rule == null || rule.Kind != AnimTreeMergeKind.Group || string.IsNullOrEmpty(targetBucketKey))
                return;

            bool changed = false;
            for (int i = 0; i < aliasBucketKeys.Count; i++)
            {
                string alias = aliasBucketKeys[i];
                if (string.IsNullOrEmpty(alias) ||
                    string.Equals(alias, targetBucketKey, StringComparison.Ordinal))
                {
                    continue;
                }
                rule.SubcategoryBucketAliases[alias] = targetBucketKey;
                changed = true;
            }
            if (!changed)
                return;
            Save();
            Changed?.Invoke();
        }

        /// <summary>When re-merging excluded subcategories, brings them back into an existing merge rule.</summary>
        public void ReincludeCategories(AnimTreeMergeRule rule, IEnumerable<AnimCatalogRef> categoryRefs)
        {
            bool changed = false;
            foreach (AnimCatalogRef cat in categoryRefs)
            {
                int removed = rule.ExcludedSources.RemoveAll(
                    existing => AnimCatalogRefUtil.SameCategory(existing, cat));
                if (removed > 0)
                    changed = true;
            }
            if (!changed)
                return;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Finds a group merge that partially excluded every category in <paramref name="categoryRefs"/>.</summary>
        public AnimTreeMergeRule? FindGroupMergeForExcludedCategories(IReadOnlyCollection<AnimCatalogRef> categoryRefs)
        {
            if (categoryRefs == null || categoryRefs.Count == 0)
                return null;

            AnimTreeMergeRule? best = null;
            int bestMatchCount = 0;
            for (int i = 0; i < _treeMerges.Count; i++)
            {
                AnimTreeMergeRule rule = _treeMerges[i];
                if (rule.Kind != AnimTreeMergeKind.Group)
                    continue;

                int matchCount = 0;
                foreach (AnimCatalogRef cat in categoryRefs)
                {
                    if (AnimCatalogRefUtil.ContainsCategory(rule.ExcludedSources, cat))
                        matchCount++;
                }
                if (matchCount == 0 || matchCount < categoryRefs.Count)
                    continue;
                if (matchCount > bestMatchCount)
                {
                    best = rule;
                    bestMatchCount = matchCount;
                }
            }
            return best;
        }

        public bool IsAnimationExcludedFromMerge(AnimCatalogRef animationRef)
        {
            for (int r = 0; r < _treeMerges.Count; r++)
            {
                AnimTreeMergeRule rule = _treeMerges[r];
                for (int i = 0; i < rule.ExcludedAnimationRefs.Count; i++)
                {
                    if (rule.ExcludedAnimationRefs[i].Equals(animationRef))
                        return true;
                }
            }
            return false;
        }

        public bool IsCategoryExcludedFromRule(AnimTreeMergeRule rule, AnimCatalogRef categoryRef)
        {
            return AnimCatalogRefUtil.ContainsCategory(rule.ExcludedSources, categoryRef);
        }

        /// <summary>Group merge that still includes <paramref name="categoryRef"/> (not excluded).</summary>
        public AnimTreeMergeRule? FindGroupMergeForCategory(AnimCatalogRef categoryRef)
        {
            for (int i = 0; i < _treeMerges.Count; i++)
            {
                AnimTreeMergeRule rule = _treeMerges[i];
                if (rule.Kind != AnimTreeMergeKind.Group)
                    continue;

                bool groupInMerge = false;
                for (int s = 0; s < rule.Sources.Count; s++)
                {
                    if (rule.Sources[s].Group == categoryRef.Group)
                    {
                        groupInMerge = true;
                        break;
                    }
                }
                if (!groupInMerge || IsCategoryExcludedFromRule(rule, categoryRef))
                    continue;
                return rule;
            }
            return null;
        }

        public void Commit(AnimTreeMergeRule? mergeRule, IEnumerable<AnimDisplayGroupData> groups, bool mergeRuleAlreadyStored = false)
        {
            if (mergeRule != null && mergeRule.Kind == AnimTreeMergeKind.Category && mergeRule.Sources.Count >= 2)
            {
                var united = new HashSet<AnimCatalogRef>();
                for (int i = 0; i < mergeRule.Sources.Count; i++)
                {
                    AnimCatalogRef src = mergeRule.Sources[i];
                    united.Add(AnimCatalogRefUtil.CategoryRef(src.Group, src.Category));
                }
                SupersedeCategoryMerges(united, mergeRuleAlreadyStored ? mergeRule.Id : null);
            }

            if (mergeRule != null && !mergeRuleAlreadyStored && !ContainsTreeMerge(mergeRule.Id))
                _treeMerges.Add(mergeRule);
            foreach (var group in groups)
            {
                if (group.Members.Count < 2)
                    continue;
                RemoveMembersFromExistingGroups(group);
                _displayGroups.Add(group);
            }
            RebuildIndex();
            Save();
            Changed?.Invoke();
        }

        public void RemoveAllDisplayGroups()
        {
            if (_displayGroups.Count == 0)
                return;
            _displayGroups.Clear();
            RebuildIndex();
            Save();
            Changed?.Invoke();
        }

        /// <summary>Clears all tree merges and display groups (debug / reset).</summary>
        public void ClearAllGrouping()
        {
            if (_treeMerges.Count == 0 && _displayGroups.Count == 0 && _displayNameOverrides.Count == 0)
                return;
            _treeMerges.Clear();
            _displayGroups.Clear();
            _displayNameOverrides.Clear();
            RebuildIndex();
            Save();
            Changed?.Invoke();
        }

        public int DisplayGroupCount => _displayGroups.Count;

        public int TreeMergeCount => _treeMerges.Count;

        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

        private void RemoveMembersFromExistingGroups(AnimDisplayGroupData incoming)
        {
            var incomingRefs = new HashSet<AnimCatalogRef>();
            foreach (var m in incoming.Members)
                incomingRefs.Add(m.Ref);

            for (int i = _displayGroups.Count - 1; i >= 0; i--)
            {
                var existing = _displayGroups[i];
                existing.Members.RemoveAll(m => incomingRefs.Contains(m.Ref));
                if (existing.Members.Count < 2)
                    _displayGroups.RemoveAt(i);
            }
        }

        private void RebuildIndex()
        {
            _groupByRef.Clear();
            foreach (var group in _displayGroups)
            {
                foreach (var member in group.Members)
                    _groupByRef[member.Ref] = group;
            }
        }
    }
}
