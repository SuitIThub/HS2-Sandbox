using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    /// <summary>
    /// Draws a non-interactive colored frame on a tree row (<see cref="TreeNodeObject.rectNode"/>).
    /// </summary>
    internal static class WorkspaceTreeLockBorder
    {
        private const string BorderRootName = "WorkspaceTreeLockBorder";
        private const float Thickness = 2f;

        private static readonly Color BorderColor = new Color(0.1f, 0.75f, 1f, 1f);

        private static Sprite? _whiteSprite;

        internal static void RemoveIfPresent(TreeNodeObject node)
        {
            if (!node || !node.rectNode)
                return;

            Transform? t = node.rectNode.Find(BorderRootName);
            if (t != null)
                Object.Destroy(t.gameObject);
        }

        internal static void Attach(TreeNodeObject node)
        {
            if (!node || !node.rectNode)
                return;

            RemoveIfPresent(node);

            RectTransform parent = node.rectNode;
            var root = new GameObject(BorderRootName, typeof(RectTransform), typeof(CanvasGroup));
            var rootRt = (RectTransform)root.transform;
            rootRt.SetParent(parent, false);
            rootRt.SetAsLastSibling();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.localScale = Vector3.one;

            var cg = root.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Sprite sprite = GetWhiteSprite();
            AddEdge(rootRt, "Top", sprite, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, Thickness));
            AddEdge(rootRt, "Bottom", sprite, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Thickness));
            AddEdge(rootRt, "Left", sprite, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(Thickness, 0f));
            AddEdge(rootRt, "Right", sprite, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(Thickness, 0f));
        }

        private static void AddEdge(
            RectTransform parent,
            string name,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.color = BorderColor;
            img.raycastTarget = false;
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;

            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return _whiteSprite;
        }
    }
}
