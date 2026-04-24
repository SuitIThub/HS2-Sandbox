using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    /// <summary>
    /// Prevents Studio from hiding pinned <see cref="TreeNodeObject"/> rows when ancestors collapse.
    /// Uses postfix hooks to restore visibility after tree operations complete.
    /// </summary>
    internal static class WorkspaceTreeLockHarmony
    {
        internal const string HarmonyId = "com.hs2.sandbox.workspacetreelock";

        internal static void Install()
        {
            var harmony = new Harmony(HarmonyId);
            var treeNodeObj = typeof(TreeNodeObject);
            var treeNodeCtrl = typeof(TreeNodeCtrl);

            // Block direct SetVisible(false) on locked nodes during collapse operations
            harmony.Patch(
                AccessTools.Method(treeNodeObj, "SetVisibleChild", new[] { typeof(TreeNodeObject), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SetVisibleChild_Prefix)));

            harmony.Patch(
                AccessTools.Method(treeNodeObj, "SetVisibleLoop", new[] { typeof(TreeNodeObject), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SetVisibleLoop_Prefix)));

            // After tree refresh operations, force all locked nodes visible
            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, nameof(TreeNodeCtrl.RefreshHierachy)),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(RefreshHierachy_Postfix)));

            // Clean up when nodes are deleted
            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, nameof(TreeNodeCtrl.DeleteNode), new[] { typeof(TreeNodeObject) }),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(DeleteNode_Postfix)));

            // After SetParent operations (which can trigger visibility changes)
            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, nameof(TreeNodeCtrl.SetParent), new System.Type[0]),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(TreeOperation_Postfix)));

            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, nameof(TreeNodeCtrl.SetParent), new[] { typeof(TreeNodeObject), typeof(TreeNodeObject) }),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(TreeOperation_Postfix)));

            // After adding nodes (copy operations)
            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, "AddNode", new[] { typeof(string), typeof(TreeNodeObject) }),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(TreeOperation_Postfix)));

            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, "AddNode", new[] { typeof(TreeNodeObject) }),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(TreeOperation_Postfix)));

            // After removing nodes
            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, "RemoveNode", new[] { typeof(TreeNodeObject) }),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(TreeOperation_Postfix)));

            // Fix: include locked nodes in shift+click selection
            harmony.Patch(
                AccessTools.Method(treeNodeCtrl, "SelectMultiple", new[] { typeof(TreeNodeObject), typeof(TreeNodeObject) }),
                prefix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SelectMultiple_Prefix)),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SelectMultiple_Postfix)));

            // Auto-pin copies of pinned nodes
            var studioType = typeof(TreeNodeObject).Assembly.GetType("Studio.Studio");
            if (studioType != null)
            {
                var duplicateMethod = AccessTools.Method(studioType, "Duplicate");
                if (duplicateMethod != null)
                {
                    harmony.Patch(
                        duplicateMethod,
                        prefix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(Duplicate_Prefix)),
                        postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(Duplicate_Postfix)));
                }
            }
        }

        #region TreeNodeObject patches

        private static bool SetVisibleChild_Prefix(TreeNodeObject _source, bool _visible)
        {
            if (!_visible && WorkspaceTreeLockRegistry.IsLocked(_source))
                return false;
            return true;
        }

        private static bool SetVisibleLoop_Prefix(TreeNodeObject _source, bool _visible)
        {
            if (!_visible && WorkspaceTreeLockRegistry.IsLocked(_source))
                return false;
            return true;
        }

        #endregion

        #region TreeNodeCtrl patches

        private static void DeleteNode_Postfix(TreeNodeObject _node)
        {
            if (_node != null)
                WorkspaceTreeLockRegistry.ForgetNode(_node);
        }

        private static void RefreshHierachy_Postfix()
        {
            RestoreLockedNodesVisibility();
        }

        private static void TreeOperation_Postfix()
        {
            RestoreLockedNodesVisibility();
        }

        private static void SelectMultiple_Prefix(TreeNodeObject _start, TreeNodeObject _end, out (float min, float max) __state)
        {
            float startY = _start?.rectNode?.anchoredPosition.y ?? 0f;
            float endY = _end?.rectNode?.anchoredPosition.y ?? 0f;
            __state = (Mathf.Min(startY, endY), Mathf.Max(startY, endY));
        }

        private static void SelectMultiple_Postfix(TreeNodeCtrl __instance, (float min, float max) __state)
        {
            // After normal SelectMultiple completes, add any locked nodes that are in the Y range
            // but were skipped because their parent is collapsed
            var locked = WorkspaceTreeLockRegistry.GetLockedNodes();
            if (locked.Count == 0)
                return;

            foreach (TreeNodeObject node in locked)
            {
                if (!node || node.rectNode == null)
                    continue;

                float y = node.rectNode.anchoredPosition.y;
                if (y >= __state.min && y <= __state.max)
                {
                    if (__instance.selectNodes != null && !__instance.selectNodes.Contains(node))
                        __instance.AddSelectNode(node, true);
                }
            }
        }

        // Track pinned nodes and their indices before duplication
        private static List<(TreeNodeObject node, TreeNodeObject? parent, int index)>? _pinnedNodeInfoBefore;

        private static void Duplicate_Prefix()
        {
            _pinnedNodeInfoBefore = null;
            
            var treeNodeCtrl = Object.FindObjectOfType<TreeNodeCtrl>();
            if (treeNodeCtrl == null || treeNodeCtrl.selectNodes == null)
                return;

            foreach (var node in treeNodeCtrl.selectNodes)
            {
                if (node != null && WorkspaceTreeLockRegistry.IsLocked(node))
                {
                    var parent = node.parent;
                    int index = -1;
                    
                    if (parent != null && parent.child != null)
                        index = parent.child.IndexOf(node);
                    
                    _pinnedNodeInfoBefore ??= new List<(TreeNodeObject, TreeNodeObject?, int)>();
                    _pinnedNodeInfoBefore.Add((node, parent, index));
                }
            }
        }

        private static void Duplicate_Postfix()
        {
            if (_pinnedNodeInfoBefore == null || _pinnedNodeInfoBefore.Count == 0)
                return;

            foreach (var (original, parent, originalIndex) in _pinnedNodeInfoBefore)
            {
                if (parent == null || parent.child == null || originalIndex < 0)
                    continue;

                // The copy should be right after the original (index + 1)
                int copyIndex = originalIndex + 1;
                if (copyIndex >= parent.child.Count)
                    continue;

                var potentialCopy = parent.child[copyIndex];
                if (potentialCopy == null || potentialCopy == original)
                    continue;

                // Verify it has the same name and isn't already pinned
                if (potentialCopy.textName == original.textName && !WorkspaceTreeLockRegistry.IsLocked(potentialCopy))
                    WorkspaceTreeLockRegistry.ToggleLock(potentialCopy);
            }

            _pinnedNodeInfoBefore = null;
            RestoreLockedNodesVisibility();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Requests deferred visibility restoration via the Restorer component.
        /// </summary>
        private static void RestoreLockedNodesVisibility()
        {
            WorkspaceTreeLockRestorer.RequestRestore();
        }

        #endregion
    }
}
