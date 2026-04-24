using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    /// <summary>
    /// Tracks workspace tree nodes pinned via middle-click so collapse logic leaves their rows visible.
    /// </summary>
    internal static class WorkspaceTreeLockRegistry
    {
        private static readonly HashSet<TreeNodeObject> Locked = new HashSet<TreeNodeObject>();

        internal static bool IsLocked(TreeNodeObject? node)
        {
            // UnityEngine.Object: destroyed instances are treated as missing by `!node`.
            if (!node)
                return false;
            return Locked.Contains(node!);
        }

        /// <returns>True if the node is locked after the toggle; false if it was unlocked.</returns>
        internal static bool ToggleLock(TreeNodeObject node)
        {
            if (IsLocked(node))
            {
                Unlock(node);
                return false;
            }

            Lock(node);
            return true;
        }

        private static void Lock(TreeNodeObject node)
        {
            if (!Locked.Add(node))
                return;

            WorkspaceTreeLockBorder.Attach(node);
            SandboxServices.Log.LogInfo($"Workspace tree lock: pinned \"{node.textName}\".");
        }

        private static void Unlock(TreeNodeObject node)
        {
            if (!Locked.Remove(node))
                return;

            WorkspaceTreeLockBorder.RemoveIfPresent(node);
            WorkspaceTreeLockVisibility.ApplyAfterUnlock(node);
            SandboxServices.Log.LogInfo($"Workspace tree lock: unpinned \"{node.textName}\".");
        }

        internal static void ForgetNode(TreeNodeObject node)
        {
            Locked.Remove(node);
            WorkspaceTreeLockBorder.RemoveIfPresent(node);
        }

        /// <summary>
        /// Returns all currently locked nodes (for iteration by Harmony postfixes).
        /// </summary>
        internal static IReadOnlyCollection<TreeNodeObject> GetLockedNodes() => Locked;
    }
}
