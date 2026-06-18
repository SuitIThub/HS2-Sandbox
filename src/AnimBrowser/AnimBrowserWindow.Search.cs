using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private string _searchText = string.Empty;
        private bool _searchUseRegex;
        private string _searchRegexError = string.Empty;
        private Regex? _searchRegex;
        private bool _searchHitCacheDirty = true;
        private readonly HashSet<string> _searchVisibleNodeIds = new HashSet<string>(StringComparer.Ordinal);

        private static readonly GUIContent GcSearchLabel =
            new GUIContent("Search:", "Filter the tree and grid by animation/category name.");
        private static readonly GUIContent GcSearchRegex =
            new GUIContent(".*", "Treat search text as a regular expression (case-insensitive).");
        private static readonly GUIContent GcSearchClear = new GUIContent("✕", "Clear search");
        private static readonly GUIContent GcSelectSubcategory =
            new GUIContent("Select a subcategory to view animations.");

        private bool IsSearchActive => !StringEx.IsNullOrWhiteSpace(_searchText);

        private void InvalidateSearchHitCache() => _searchHitCacheDirty = true;

        private void DrawAnimSearchBar()
        {
            GUILayout.Label(GcSearchLabel, AnimBrowserScale.W(46f));
            if (DrawSearchFieldWithClear(ref _searchText, 22f, AnimBrowserScale.MinW(100f), GUILayout.ExpandWidth(true)))
                OnSearchInputChanged();

            bool newRegex = GUILayout.Toggle(_searchUseRegex, GcSearchRegex, AnimBrowserScale.W(36f));
            if (newRegex != _searchUseRegex)
            {
                _searchUseRegex = newRegex;
                OnSearchInputChanged();
            }

            if (!string.IsNullOrEmpty(_searchRegexError))
            {
                var c = GUI.color;
                GUI.color = new Color(1f, 0.5f, 0.45f);
                GUILayout.Label(_searchRegexError, GUILayout.MaxWidth(180f));
                GUI.color = c;
            }
        }

        private static bool DrawSearchFieldWithClear(ref string text, float height, params GUILayoutOption[] textFieldOptions)
        {
            GUILayout.BeginHorizontal();
            var fieldOpts = new List<GUILayoutOption> { AnimBrowserScale.H(height) };
            if (textFieldOptions != null && textFieldOptions.Length > 0)
                fieldOpts.AddRange(textFieldOptions);
            string newText = GUILayout.TextField(text, fieldOpts.ToArray());
            bool changed = !string.Equals(newText, text, StringComparison.Ordinal);
            text = newText;
            GUI.enabled = text.Length > 0;
            if (GUILayout.Button(GcSearchClear, AnimBrowserScale.W(24f), AnimBrowserScale.H(height)))
            {
                if (text.Length > 0)
                {
                    text = string.Empty;
                    changed = true;
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            return changed;
        }

        private void OnSearchInputChanged()
        {
            EnsureSearchRegexCompiled();
            EnsureSelectedSubcategoryParentExpanded();
            InvalidateSearchHitCache();
            InvalidateContentViewCaches();
            _gridScroll = Vector2.zero;
            _listScroll = Vector2.zero;
        }

        private void EnsureSearchRegexCompiled()
        {
            _searchRegexError = string.Empty;
            _searchRegex = null;
            if (!_searchUseRegex || !IsSearchActive)
                return;
            try
            {
                _searchRegex = new Regex(_searchText.Trim(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (Exception ex)
            {
                _searchRegexError = ex.Message;
            }
        }

        /// <summary>
        /// Recomputes which tree node ids have search hits. The display tree structure is unchanged —
        /// <see cref="RefreshTreeDrawSnapshotIfNeeded"/> applies this as a visibility filter only.
        /// </summary>
        private void RebuildSearchHitCacheIfNeeded()
        {
            if (!_searchHitCacheDirty)
                return;

            if (!IsSearchActive)
            {
                _searchHitCacheDirty = false;
                _searchVisibleNodeIds.Clear();
                return;
            }

            if (!_catalog.BuildComplete)
                return;

            EnsureSearchRegexCompiled();
            if (_searchUseRegex && _searchRegex == null)
            {
                _searchHitCacheDirty = false;
                _searchVisibleNodeIds.Clear();
                return;
            }

            _searchHitCacheDirty = false;
            _searchVisibleNodeIds.Clear();

            PinSelectedSubcategoryForSearchFilter();

            IList<AnimViewNode> roots = _displayCatalog.RootGroups;
            for (int gi = 0; gi < roots.Count; gi++)
            {
                AnimViewNode group = roots[gi];
                bool groupNameHit = NodeNameMatchesSearch(group.Name);
                bool anyChildVisible = false;
                for (int ci = 0; ci < group.Children.Count; ci++)
                {
                    AnimViewNode category = group.Children[ci];
                    if (_searchVisibleNodeIds.Contains(category.Id))
                    {
                        anyChildVisible = true;
                        continue;
                    }

                    if (!CategoryNodeHasSearchHits(category))
                        continue;

                    _searchVisibleNodeIds.Add(category.Id);
                    anyChildVisible = true;
                }

                if (groupNameHit || anyChildVisible || _searchVisibleNodeIds.Contains(group.Id))
                    _searchVisibleNodeIds.Add(group.Id);
            }
        }

        /// <summary>Keep the active subcategory and its parent group visible even with zero grid hits.</summary>
        private void PinSelectedSubcategoryForSearchFilter()
        {
            if (!IsLeafContentNode(_selectedTreeNode))
                return;

            string selectedId = _selectedTreeNode!.Id;
            _searchVisibleNodeIds.Add(selectedId);

            IList<AnimViewNode> roots = _displayCatalog.RootGroups;
            for (int gi = 0; gi < roots.Count; gi++)
            {
                AnimViewNode group = roots[gi];
                for (int ci = 0; ci < group.Children.Count; ci++)
                {
                    if (!string.Equals(group.Children[ci].Id, selectedId, StringComparison.Ordinal))
                        continue;
                    _searchVisibleNodeIds.Add(group.Id);
                    return;
                }
            }
        }

        private void EnsureSelectedSubcategoryParentExpanded()
        {
            if (!IsLeafContentNode(_selectedTreeNode))
                return;

            string selectedId = _selectedTreeNode!.Id;
            IList<AnimViewNode> roots = _displayCatalog.RootGroups;
            for (int gi = 0; gi < roots.Count; gi++)
            {
                AnimViewNode group = roots[gi];
                for (int ci = 0; ci < group.Children.Count; ci++)
                {
                    if (!string.Equals(group.Children[ci].Id, selectedId, StringComparison.Ordinal))
                        continue;
                    if (_collapsedNodeIds.Remove(group.Id))
                        _pendingFlatTreeRebuild = true;
                    return;
                }
            }
        }

        private bool IsTreeNodeVisibleForSearch(AnimViewNode node)
        {
            if (!IsSearchActive)
                return true;
            return _searchVisibleNodeIds.Contains(node.Id);
        }

        private static bool IsLeafContentNode(AnimViewNode? node) => node != null && !node.IsGroup;

        private bool CategoryNodeHasSearchHits(AnimViewNode categoryNode)
        {
            if (NodeNameMatchesSearch(categoryNode.Name))
                return true;

            if (_displayCatalog.TryGetCachedEntries(categoryNode.Id, out List<AnimDisplayEntry>? entries))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (EntryMatchesSearch(entries[i]))
                        return true;
                }
                return false;
            }

            return _displayCatalog.CategoryContainsItemMatch(categoryNode, ItemNameMatchesSearch);
        }

        private static readonly Color SearchDimGuiColor = new Color(1f, 1f, 1f, 0.4f);
        private const string SearchDimmedEntryTooltipSuffix = "\n(Shown because the subcategory name matched the search.)";

        private bool SelectedSubcategoryNameMatchesSearch()
        {
            if (!IsSearchActive || !IsLeafContentNode(_selectedTreeNode))
                return false;
            return NodeNameMatchesSearch(_selectedTreeNode!.Name);
        }

        private static void BeginSearchDimDraw(bool dimmed, ref Color restoreColor)
        {
            restoreColor = GUI.color;
            if (dimmed && Event.current.type == EventType.Repaint)
                GUI.color = SearchDimGuiColor;
        }

        private static void EndSearchDimDraw(bool dimmed, Color restoreColor)
        {
            if (dimmed && Event.current.type == EventType.Repaint)
                GUI.color = restoreColor;
        }

        private bool NodeNameMatchesSearch(string name) => TextMatchesSearch(name);

        private bool EntryMatchesSearch(AnimDisplayEntry entry)
        {
            if (!IsSearchActive)
                return true;

            if (entry.IsGroup)
            {
                AnimDisplayGroup group = entry.Group!;
                if (TextMatchesSearch(group.Name))
                    return true;
                for (int i = 0; i < group.Slots.Count; i++)
                {
                    if (ItemNameMatchesSearch(group.Slots[i].Item))
                        return true;
                }
                return false;
            }

            return ItemNameMatchesSearch(entry.Single!);
        }

        private bool ItemNameMatchesSearch(AnimGridItem item) => TextMatchesSearch(item.DisplayName);

        private bool TextMatchesSearch(string? text)
        {
            if (!IsSearchActive || string.IsNullOrEmpty(text))
                return false;

            string trimmed = _searchText.Trim();
            if (trimmed.Length == 0)
                return false;

            if (TextMatchesSearchCore(text, trimmed))
                return true;

            // Match already-cached translations only — never trigger sync/async lookup during search.
            if (StudioAutoTranslation.TryGetCached(text, out string cached) &&
                !string.Equals(cached, text, StringComparison.Ordinal))
                return TextMatchesSearchCore(cached, trimmed);
            return false;
        }

        private bool TextMatchesSearchCore(string text, string trimmed)
        {
            if (_searchUseRegex)
            {
                if (_searchRegex == null)
                    return false;
                return _searchRegex.IsMatch(text);
            }
            return text.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
