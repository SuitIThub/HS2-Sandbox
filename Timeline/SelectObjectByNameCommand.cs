using System;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Selects a root workspace object by name. Resolves the list of selectable objects from
    /// "StudioScene/Canvas Object List/Image Bar/Scroll View/Viewport/Content" each time the
    /// command executes: keeps only "Node(Clone)" children whose TreeNodeObject (component index 2)
    /// has a null parent, then matches by textName.
    /// Selection is performed by calling TreeNodeObject.OnClickSelect().
    /// </summary>
    public class SelectObjectByNameCommand : TimelineCommand
    {
        private const string ContentPath = "StudioScene/Canvas Object List/Image Bar/Scroll View/Viewport/Content";
        private const int TreeNodeComponentIndex = 2;

        public override string TypeId => "select_object_by_name";
        public override string GetDisplayLabel() => "Select Object";

        private string _objectName = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label("Object", GUILayout.Width(45));
            _objectName = GUILayout.TextField(_objectName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolvedName = ctx.Variables.Interpolate(_objectName ?? "");

            TreeNodeObject? target = FindRootNodeByName(resolvedName);

            if (target == null)
            {
                HS2SandboxPlugin.Log.LogWarning($"SelectObjectByName: No root object found with name '{resolvedName}'.");
                onComplete();
                return;
            }

            try
            {
                target.OnClickSelect();
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogWarning($"SelectObjectByName: OnClickSelect failed for '{resolvedName}'. {ex.Message}");
            }

            onComplete();
        }

        private static TreeNodeObject? FindRootNodeByName(string name)
        {
            GameObject? content = GameObject.Find(ContentPath);
            if (content == null) return null;

            foreach (Transform child in content.transform)
            {
                if (!TryGetRootNode(child.gameObject, out TreeNodeObject? node)) continue;
                if (string.Equals(node!.textName, name, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }

        private static bool TryGetRootNode(GameObject go, out TreeNodeObject? node)
        {
            node = null;
            if (go.name != "Node(Clone)") return false;

            Component[] components = go.GetComponents<Component>();
            if (components.Length <= TreeNodeComponentIndex) return false;

            node = components[TreeNodeComponentIndex] as TreeNodeObject;
            if (node == null) return false;
            if (node.parent != null) return false;

            return true;
        }

        public override string SerializePayload() => _objectName ?? "";

        public override void DeserializePayload(string payload)
        {
            _objectName = payload ?? "";
        }
    }
}
