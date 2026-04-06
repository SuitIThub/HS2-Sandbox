using System;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Resolves <see cref="TreeNodeObject"/> instances from the Studio object list scroll content.
    /// Includes nodes with no parent (top-level) or whose immediate parent's
    /// <see cref="TreeNodeObject.textName"/> contains "Folder" (case-insensitive). Nodes under a
    /// parent whose name does not contain "Folder" are excluded.
    /// </summary>
    public static class StudioObjectTreeResolution
    {
        public const string ContentPath = "StudioScene/Canvas Object List/Image Bar/Scroll View/Viewport/Content";
        public const int TreeNodeComponentIndex = 2;

        public static TreeNodeObject? FindTreeNodeByName(string name)
        {
            GameObject? content = GameObject.Find(ContentPath);
            if (content == null) return null;

            foreach (Transform child in content.transform)
            {
                if (!TryGetSelectableObjectListNode(child.gameObject, out TreeNodeObject? node)) continue;
                if (string.Equals(node!.textName, name, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }

        public static bool TryGetSelectableObjectListNode(GameObject go, out TreeNodeObject? node)
        {
            node = null;
            if (go.name != "Node(Clone)") return false;

            Component[] components = go.GetComponents<Component>();
            if (components.Length <= TreeNodeComponentIndex) return false;

            node = components[TreeNodeComponentIndex] as TreeNodeObject;
            if (node == null) return false;

            if (node.parent != null)
            {
                string parentName = node.parent.textName ?? "";
                if (parentName.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }
    }
}
