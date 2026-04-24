using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    /// <summary>
    /// Handles deferred visibility restoration for locked nodes.
    /// Called by Harmony postfixes to restore visibility after tree operations complete.
    /// </summary>
    internal sealed class WorkspaceTreeLockRestorer : MonoBehaviour
    {
        private static WorkspaceTreeLockRestorer? _instance;

        private static readonly MethodInfo? SetStateVisibleMethod =
            typeof(TreeNodeObject).GetMethod("SetStateVisible", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void EnsureInstance(GameObject host)
        {
            if (_instance == null)
                _instance = host.AddComponent<WorkspaceTreeLockRestorer>();
        }

        /// <summary>
        /// Request visibility restoration. Runs via coroutine at end of frame.
        /// </summary>
        internal static void RequestRestore()
        {
            if (_instance != null)
            {
                _instance.StartCoroutine(_instance.RestoreCoroutine());
            }
        }

        private IEnumerator RestoreCoroutine()
        {
            // Wait for end of frame to ensure all UI updates are done
            yield return new WaitForEndOfFrame();
            
            RestoreLockedNodesVisibility();
            
            // Also restore next frame in case of delayed updates
            yield return null;
            RestoreLockedNodesVisibility();
        }

        private static void RestoreLockedNodesVisibility()
        {
            IReadOnlyCollection<TreeNodeObject> locked = WorkspaceTreeLockRegistry.GetLockedNodes();
            if (locked.Count == 0)
                return;

            foreach (TreeNodeObject node in locked)
            {
                if (!node)
                    continue;

                if (node.rectNode == null)
                    continue;

                // Make the row visible
                if (SetStateVisibleMethod != null)
                    SetStateVisibleMethod.Invoke(node, new object[] { true });

                if (!node.rectNode.gameObject.activeSelf)
                    node.rectNode.gameObject.SetActive(true);

                // Fix arrow button: hide it if node has no children
                if (node.buttonState != null)
                {
                    bool hasChildren = node.childCount > 0;
                    node.buttonState.gameObject.SetActive(hasChildren);
                }
            }
        }
    }
}
