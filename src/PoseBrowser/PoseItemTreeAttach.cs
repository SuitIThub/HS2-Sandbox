using System;
using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Workspace tree parenting via <see cref="TreeNodeCtrl.SetParent"/> (same path as Studio UI and
    /// <see cref="WorkspaceTreeLock"/> — uses <see cref="TreeNodeObject.parent"/> / <see cref="TreeNodeObject.child"/>).
    /// </summary>
    internal static class PoseItemTreeAttach
    {
        private const char PathSeparator = '|';

        public static string? CaptureParentTreePath(OCIItem item, OCIChar anchor)
        {
            try
            {
                TreeNodeObject? charRoot = anchor.treeNodeObject;
                TreeNodeObject? parent = item.treeNodeObject?.parent;
                if (charRoot == null || parent == null || ReferenceEquals(parent, charRoot))
                    return null;

                return BuildPathFromCharRoot(charRoot, parent);
            }
            catch
            {
                return null;
            }
        }

        public static TreeNodeObject? ResolveParentTreeNode(OCIChar anchor, string? parentTreePath)
        {
            if (StringEx.IsNullOrWhiteSpace(parentTreePath) || anchor.treeNodeObject == null)
                return null;

            TreeNodeObject? node = anchor.treeNodeObject;
            foreach (string segment in parentTreePath.Split(PathSeparator))
            {
                if (string.IsNullOrEmpty(segment))
                    continue;
                node = FindDirectChildByText(node, segment);
                if (node == null)
                    return null;
            }

            return node;
        }

        /// <summary>Reparent item in the workspace tree; Studio runs <c>OnParentage</c> → <c>OnAttach</c>.</summary>
        public static bool TryApplyTreeParent(OCIItem item, TreeNodeObject parentTreeNode)
        {
            if (item.treeNodeObject == null)
                return false;

            try
            {
                var tree = Singleton<Studio.Studio>.Instance.treeNodeCtrl;
                tree.SetParent(item.treeNodeObject, parentTreeNode);
                return ReferenceEquals(item.treeNodeObject.parent, parentTreeNode);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: SetParent failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Find a descendant workspace node under the character by row text (FK / body-part rows).</summary>
        public static TreeNodeObject? FindDescendantUnderCharacter(OCIChar anchor, string text)
        {
            if (anchor.treeNodeObject == null || StringEx.IsNullOrWhiteSpace(text))
                return null;

            string key = text.Trim();
            TreeNodeObject? found = FindDescendantByText(anchor.treeNodeObject, key);
            if (found != null)
                return found;

            try
            {
                foreach (OCIChar.BoneInfo bone in anchor.listBones)
                {
                    Transform? t = bone.guideObject?.transformTarget;
                    if (t == null || !string.Equals(t.name, key, StringComparison.Ordinal))
                        continue;
                    found = FindDescendantByText(anchor.treeNodeObject, t.name);
                    if (found != null)
                        return found;
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        /// <summary>FK / body-part bone transform for an attached item (never falls back to character root).</summary>
        public static bool TryGetBodyPartBoneTransform(
            OCIChar anchor,
            string? parentTreePath,
            string? parentObjectName,
            out Transform boneTransform)
        {
            boneTransform = null!;
            if (anchor.charInfo == null)
                return false;

            if (!StringEx.IsNullOrWhiteSpace(parentObjectName) &&
                TryFindBoneTransform(anchor, parentObjectName.Trim(), out Transform? fromName) &&
                fromName != null &&
                !IsCharacterRootTransform(anchor, fromName))
            {
                boneTransform = fromName;
                return true;
            }

            string? leaf = GetTreePathLeaf(parentTreePath);
            if (!string.IsNullOrEmpty(leaf) &&
                TryFindBoneTransform(anchor, leaf, out Transform? fromLeaf) &&
                fromLeaf != null &&
                !IsCharacterRootTransform(anchor, fromLeaf))
            {
                boneTransform = fromLeaf;
                return true;
            }

            if (!StringEx.IsNullOrWhiteSpace(parentTreePath) || !StringEx.IsNullOrWhiteSpace(parentObjectName))
            {
                TreeNodeObject? node = ResolveParentTreeNode(anchor, parentTreePath);
                if (node == null && !StringEx.IsNullOrWhiteSpace(parentObjectName))
                    node = FindDescendantUnderCharacter(anchor, parentObjectName!);

                if (node != null &&
                    TryGetTransformForTreeNode(anchor, node, out Transform? fromNode) &&
                    fromNode != null &&
                    !IsCharacterRootTransform(anchor, fromNode))
                {
                    boneTransform = fromNode;
                    return true;
                }
            }

            return false;
        }

        /// <summary>World transform of the attach parent (body-part bone, or character root only when not attached).</summary>
        public static bool TryGetAttachParentTransform(
            OCIChar anchor,
            string? parentTreePath,
            string? parentObjectName,
            out Transform parentTransform)
        {
            if (TryGetBodyPartBoneTransform(anchor, parentTreePath, parentObjectName, out parentTransform))
                return true;

            parentTransform = null!;
            if (anchor.charInfo == null)
                return false;

            Transform charRoot = anchor.charInfo.transform;
            if (anchor.guideObject?.transformTarget != null)
            {
                parentTransform = anchor.guideObject.transformTarget;
                return true;
            }

            parentTransform = charRoot;
            return true;
        }

        public static string? GetTreePathLeaf(string? parentTreePath)
        {
            if (StringEx.IsNullOrWhiteSpace(parentTreePath))
                return null;

            string path = parentTreePath.Trim();
            int sep = path.LastIndexOf(PathSeparator);
            string leaf = sep >= 0 ? path.Substring(sep + 1) : path;
            return StringEx.IsNullOrWhiteSpace(leaf) ? null : leaf.Trim();
        }

        private static bool IsCharacterRootTransform(OCIChar anchor, Transform transform)
        {
            if (anchor.guideObject?.transformTarget != null &&
                ReferenceEquals(transform, anchor.guideObject.transformTarget))
                return true;

            return anchor.charInfo != null && ReferenceEquals(transform, anchor.charInfo.transform);
        }

        public static bool IsAttachedToBodyPart(PoseAssociatedItemRecord record) =>
            !StringEx.IsNullOrWhiteSpace(record.ParentTreePath);

        private static bool TryGetTransformForTreeNode(OCIChar anchor, TreeNodeObject node, out Transform? transform)
        {
            transform = null;
            string? text = node.textName?.Trim();
            if (string.IsNullOrEmpty(text))
                return false;

            if (TryFindBoneTransform(anchor, text, out transform))
                return true;

            return false;
        }

        private static bool TryFindBoneTransform(OCIChar anchor, string boneName, out Transform? transform)
        {
            transform = null;
            try
            {
                foreach (OCIChar.BoneInfo bone in anchor.listBones)
                {
                    Transform? t = bone.guideObject?.transformTarget;
                    if (t != null && string.Equals(t.name, boneName, StringComparison.Ordinal))
                    {
                        transform = t;
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }

            if (anchor.charInfo == null)
                return false;

            Transform? found = FindChildTransformByName(anchor.charInfo.transform, boneName);
            if (found != null)
            {
                transform = found;
                return true;
            }

            return false;
        }

        private static Transform? FindChildTransformByName(Transform root, string exactName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, exactName, StringComparison.Ordinal))
                    return t;
            }

            return null;
        }

        /// <summary>When only the guide is parented, locate the matching workspace row under the character.</summary>
        public static string? TryBuildTreePathForDescendant(OCIChar anchor, string descendantText)
        {
            TreeNodeObject? node = FindDescendantUnderCharacter(anchor, descendantText);
            if (node == null || anchor.treeNodeObject == null)
                return null;
            return BuildPathFromCharRoot(anchor.treeNodeObject, node);
        }

        private static string BuildPathFromCharRoot(TreeNodeObject charRoot, TreeNodeObject parent)
        {
            var segments = new List<string>();
            for (TreeNodeObject? n = parent; n != null && !ReferenceEquals(n, charRoot); n = n.parent)
            {
                string label = n.textName?.Trim() ?? string.Empty;
                if (label.Length > 0)
                    segments.Add(label);
            }

            segments.Reverse();
            return segments.Count == 0 ? string.Empty : string.Join(PathSeparator.ToString(), segments.ToArray());
        }

        private static TreeNodeObject? FindDirectChildByText(TreeNodeObject parent, string text)
        {
            if (parent.child == null || parent.child.Count == 0)
                return null;

            for (int i = 0; i < parent.child.Count; i++)
            {
                TreeNodeObject child = parent.child[i];
                if (child != null && string.Equals(child.textName, text, StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            return null;
        }

        private static TreeNodeObject? FindDescendantByText(TreeNodeObject root, string text)
        {
            if (string.Equals(root.textName, text, StringComparison.OrdinalIgnoreCase))
                return root;

            if (root.child == null)
                return null;

            for (int i = 0; i < root.child.Count; i++)
            {
                TreeNodeObject? child = root.child[i];
                if (child == null)
                    continue;
                TreeNodeObject? found = FindDescendantByText(child, text);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
