using System;
using System.Globalization;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private bool _treeRenameActive;
        private string _treeRenameNodeId = string.Empty;
        private string _treeRenameText = string.Empty;

        private bool _contentRenameActive;
        private bool _contentRenameIsGroup;
        private string _contentRenameKey = string.Empty;
        private string _contentRenameText = string.Empty;

        private void CancelTreeRename()
        {
            _treeRenameActive = false;
            _treeRenameNodeId = string.Empty;
            _treeRenameText = string.Empty;
        }

        private void CancelContentRename()
        {
            _contentRenameActive = false;
            _contentRenameKey = string.Empty;
            _contentRenameText = string.Empty;
        }

        private AnimViewNode? GetSingleSelectedTreeNode()
        {
            AnimViewNode? found = null;
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                AnimViewNode node = _flatTreeNodes[i];
                if (!_selectedTreeNodeIds.Contains(node.Id))
                    continue;
                if (found != null)
                    return null;
                found = node;
            }
            return found;
        }

        private void BeginTreeRename()
        {
            AnimViewNode? node = GetSingleSelectedTreeNode();
            if (node == null)
                return;
            _treeRenameNodeId = node.Id;
            _treeRenameText = _displayCatalog.GetTreeNodeRenameSeed(node);
            _treeRenameActive = true;
        }

        private void BeginContentRename()
        {
            if (_selectedItemRefs.Count == 1 && _selectedGroupIds.Count == 0)
            {
                AnimCatalogRef reference = default;
                bool found = false;
                foreach (AnimCatalogRef candidate in _selectedItemRefs)
                {
                    reference = candidate;
                    found = true;
                    break;
                }
                if (!found)
                    return;
                AnimGridItem? item = _catalog.TryGetItem(reference);
                if (item == null)
                    return;
                _contentRenameIsGroup = false;
                _contentRenameKey = AnimDisplayNameKeys.Animation(reference);
                _contentRenameText = _displayCatalog.GetAnimationRenameSeed(item);
                _contentRenameActive = true;
                return;
            }

            if (_selectedGroupIds.Count == 1 && _selectedItemRefs.Count == 0)
            {
                string groupId = string.Empty;
                foreach (string candidate in _selectedGroupIds)
                {
                    groupId = candidate;
                    break;
                }
                if (groupId.Length == 0)
                    return;
                AnimDisplayGroupData? data = _groupStore.FindDisplayGroup(groupId);
                if (data == null)
                    return;
                _contentRenameIsGroup = true;
                _contentRenameKey = groupId;
                _contentRenameText = data.Name;
                _contentRenameActive = true;
            }
        }

        private void ApplyTreeRename()
        {
            string trimmed = _treeRenameText.Trim();
            if (trimmed.Length == 0)
                return;

            AnimViewNode? node = null;
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                if (string.Equals(_flatTreeNodes[i].Id, _treeRenameNodeId, StringComparison.Ordinal))
                {
                    node = _flatTreeNodes[i];
                    break;
                }
            }
            if (node == null)
                return;

            if (node.IsMerged && node.MergeRuleId.Length > 0 &&
                (node.Id.StartsWith("mg:", StringComparison.Ordinal) ||
                 node.Id.StartsWith("cm:", StringComparison.Ordinal)))
            {
                _groupStore.RenameTreeMergeRule(node.MergeRuleId, trimmed);
            }
            else if (TryParseGroupNodeId(node.Id, out int groupId))
            {
                _groupStore.SetDisplayNameOverride(AnimDisplayNameKeys.Group(groupId), trimmed);
            }
            else if (TryParseCategoryNodeId(node.Id, out int catGroupId, out int categoryId))
            {
                _groupStore.SetDisplayNameOverride(AnimDisplayNameKeys.Category(catGroupId, categoryId), trimmed);
            }
            else if (node.Id.StartsWith("mgc:", StringComparison.Ordinal))
            {
                _groupStore.SetDisplayNameOverride(AnimDisplayNameKeys.TreeNode(node.Id), trimmed);
            }
            else
            {
                return;
            }

            CancelTreeRename();
        }

        private void ApplyContentRename()
        {
            string trimmed = _contentRenameText.Trim();
            if (trimmed.Length == 0)
                return;

            if (_contentRenameIsGroup)
            {
                if (!_groupStore.RenameDisplayGroup(_contentRenameKey, trimmed))
                    return;
            }
            else
            {
                _groupStore.SetDisplayNameOverride(_contentRenameKey, trimmed);
            }

            CancelContentRename();
        }

        private void DrawTreeRenamePanel(float btnH)
        {
            InitCharacterHintStyle();
            GUILayout.Label("Display name (browser only):", _characterHintStyle ?? GUI.skin.label);
            _treeRenameText = GUILayout.TextField(_treeRenameText);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", GUILayout.Height(btnH), AnimBrowserScale.MinW(60f)))
                ApplyTreeRename();
            if (GUILayout.Button("Cancel", GUILayout.Height(btnH), AnimBrowserScale.MinW(60f)))
                CancelTreeRename();
            GUILayout.EndHorizontal();
        }

        private void DrawContentRenamePanel(float btnH)
        {
            InitCharacterHintStyle();
            string hint = _contentRenameIsGroup
                ? "Rename group card (browser only):"
                : "Display name (browser only):";
            GUILayout.Label(hint, _characterHintStyle ?? GUI.skin.label);
            _contentRenameText = GUILayout.TextField(_contentRenameText);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", GUILayout.Height(btnH), AnimBrowserScale.MinW(60f)))
                ApplyContentRename();
            if (GUILayout.Button("Cancel", GUILayout.Height(btnH), AnimBrowserScale.MinW(60f)))
                CancelContentRename();
            GUILayout.EndHorizontal();
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
