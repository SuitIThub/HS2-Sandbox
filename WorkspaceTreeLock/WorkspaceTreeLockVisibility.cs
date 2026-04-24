using System.Reflection;
using Studio;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    /// <summary>
    /// Applies row visibility after unlock without calling <see cref="TreeNodeCtrl.RefreshHierachy"/>,
    /// which re-hides pinned rows because Studio issues <see cref="TreeNodeObject.SetVisible"/> without
    /// going through <see cref="WorkspaceTreeLockHarmony"/> propagation depth.
    /// </summary>
    internal static class WorkspaceTreeLockVisibility
    {
        private static readonly MethodInfo? SetStateVisibleMethod =
            typeof(TreeNodeObject).GetMethod("SetStateVisible", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// If any ancestor folder is collapsed (<see cref="TreeNodeObject.treeState"/> <see cref="TreeNodeObject.TreeState.Close"/>),
        /// hide this row now that it is no longer pinned.
        /// </summary>
        internal static void ApplyAfterUnlock(TreeNodeObject node)
        {
            if (!node)
                return;

            if (!HasCollapsedAncestor(node))
                return;

            // Hide the row UI. Do not toggle the node's actual visible/checked value.
            if (SetStateVisibleMethod != null)
                SetStateVisibleMethod.Invoke(node, new object[] { false });

            // Also directly deactivate as fallback
            if (node.rectNode != null)
                node.rectNode.gameObject.SetActive(false);
        }

        private static bool HasCollapsedAncestor(TreeNodeObject node)
        {
            for (TreeNodeObject? p = node.parent; p != null; p = p.parent)
            {
                if (p.treeState == TreeNodeObject.TreeState.Close)
                    return true;
            }

            return false;
        }
    }
}
