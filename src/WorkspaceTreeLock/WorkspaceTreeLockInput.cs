using System.Collections.Generic;
using Studio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    /// <summary>
    /// Middle-click on a nested workspace list row toggles a visibility lock (see <see cref="WorkspaceTreeLockRegistry"/>).
    /// Uses <see cref="RectTransformUtility.RectangleContainsScreenPoint"/> on each <see cref="TreeNodeObject.rectNode"/>
    /// because <see cref="UnityEngine.EventSystems.EventSystem"/>.RaycastAll often misses rows (wrong camera, draw order, etc.).
    /// </summary>
    internal sealed class WorkspaceTreeLockInput : MonoBehaviour
    {
        private static readonly List<TreeNodeObject> NodeScratch = new List<TreeNodeObject>(256);
        private static readonly HashSet<TreeNodeObject> NodeDedupe = new HashSet<TreeNodeObject>();
        private static readonly List<RaycastResult> GraphicRaycastScratch = new List<RaycastResult>(32);

        /// <summary>Studio object/workspace tree scroll roots (self-contained; keep independent of Timeline).</summary>
        private static readonly string[] ObjectTreeContentPaths =
        {
            "StudioScene/Canvas Object List/Image Bar/Scroll View/Viewport/Content",
            "StudioScene/Canvas Work Space/Image Bar/Scroll View/Viewport/Content",
            "StudioScene/CanvasWorkSpace/Image Bar/Scroll View/Viewport/Content",
        };

        private static float _lastNoNodesLogTime;

        private void Update()
        {
            if (!Input.GetMouseButtonDown(2))
                return;

            Vector2 screen = Input.mousePosition;

            if (TryPickViaWorkspaceGraphicRaycast(screen, out TreeNodeObject? picked) && picked != null && picked.parent != null)
            {
                WorkspaceTreeLockRegistry.ToggleLock(picked);
                return;
            }

            CollectCandidateNodes(NodeScratch);
            if (NodeScratch.Count == 0)
            {
                if (Time.unscaledTime - _lastNoNodesLogTime > 4f)
                {
                    _lastNoNodesLogTime = Time.unscaledTime;
                    SandboxServices.Log.LogWarning(
                        "Workspace tree lock: middle-click found no TreeNodeObject instances under known UI paths. " +
                        "If your Studio build uses a different hierarchy, report the path to the workspace list Content.");
                }

                return;
            }

            // Prefer the deepest row under the cursor so nested hits resolve to the intended node.
            NodeScratch.Sort(CompareDeepestFirst);

            for (int i = 0; i < NodeScratch.Count; i++)
            {
                TreeNodeObject? node = NodeScratch[i];
                if (!node || node.parent == null)
                    continue;

                if (!ScreenPointHitsTreeRow(node, screen))
                    continue;

                WorkspaceTreeLockRegistry.ToggleLock(node);
                break;
            }
        }

        /// <summary>
        /// Raycasts only the object-list canvas so <see cref="EventSystem"/>.RaycastAll is not skewed by other canvases.
        /// Unity 2018 <see cref="PointerEventData"/> does not expose a writable event camera here; the raycaster uses the canvas configuration.
        /// </summary>
        private static bool TryPickViaWorkspaceGraphicRaycast(Vector2 screen, out TreeNodeObject? node)
        {
            node = null;
            if (EventSystem.current == null)
                return false;

            GameObject? contentRoot = null;
            for (int p = 0; p < ObjectTreeContentPaths.Length; p++)
            {
                contentRoot = GameObject.Find(ObjectTreeContentPaths[p]);
                if (contentRoot != null)
                    break;
            }

            if (contentRoot == null)
                return false;

            Canvas? canvas = contentRoot.GetComponentInParent<Canvas>();
            if (canvas == null)
                return false;

            Canvas rootCanvas = canvas.rootCanvas;
            GraphicRaycaster? raycaster = rootCanvas.GetComponent<GraphicRaycaster>()
                ?? rootCanvas.GetComponentInChildren<GraphicRaycaster>(true);
            if (raycaster == null)
                return false;

            // GraphicRaycaster for Screen Space - Camera returns no hits when worldCamera is null.
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && rootCanvas.worldCamera == null)
                return false;

            var ped = new PointerEventData(EventSystem.current) { position = screen };

            GraphicRaycastScratch.Clear();
            raycaster.Raycast(ped, GraphicRaycastScratch);
            if (GraphicRaycastScratch.Count == 0)
                return false;

            for (int i = 0; i < GraphicRaycastScratch.Count; i++)
            {
                GameObject go = GraphicRaycastScratch[i].gameObject;
                if (!TryGetTreeNodeFromHierarchy(go.transform, out TreeNodeObject? found) || found == null)
                    continue;
                node = found;
                return true;
            }

            return false;
        }

        private static bool TryGetTreeNodeFromHierarchy(Transform? tr, out TreeNodeObject? node)
        {
            node = null;
            while (tr != null)
            {
                node = tr.GetComponent<TreeNodeObject>();
                if (node != null)
                    return true;
                tr = tr.parent;
            }

            return false;
        }

        private static void CollectCandidateNodes(List<TreeNodeObject> dst)
        {
            dst.Clear();
            NodeDedupe.Clear();

            for (int p = 0; p < ObjectTreeContentPaths.Length; p++)
            {
                GameObject? content = GameObject.Find(ObjectTreeContentPaths[p]);
                if (content == null)
                    continue;

                TreeNodeObject[] fromContent = content.GetComponentsInChildren<TreeNodeObject>(true);
                for (int i = 0; i < fromContent.Length; i++)
                {
                    TreeNodeObject? n = fromContent[i];
                    if (n && NodeDedupe.Add(n))
                        dst.Add(n);
                }
            }

            if (dst.Count > 0)
                return;

            var ctrl = Object.FindObjectOfType<TreeNodeCtrl>();
            if (ctrl != null)
            {
                TreeNodeObject[] fromCtrl = ctrl.GetComponentsInChildren<TreeNodeObject>(true);
                for (int i = 0; i < fromCtrl.Length; i++)
                {
                    TreeNodeObject? n = fromCtrl[i];
                    if (n && NodeDedupe.Add(n))
                        dst.Add(n);
                }
            }

            if (dst.Count > 0)
                return;

            TreeNodeObject[] all = Resources.FindObjectsOfTypeAll<TreeNodeObject>();
            for (int i = 0; i < all.Length; i++)
            {
                TreeNodeObject? n = all[i];
                if (!n || !n.gameObject.scene.IsValid())
                    continue;
                if (NodeDedupe.Add(n))
                    dst.Add(n);
            }
        }

        private static int CompareDeepestFirst(TreeNodeObject a, TreeNodeObject b)
        {
            return GetTransformDepth(b!.rectNode).CompareTo(GetTransformDepth(a!.rectNode));
        }

        private static int GetTransformDepth(Transform? t)
        {
            int d = 0;
            while (t != null)
            {
                d++;
                t = t.parent;
            }

            return d;
        }

        private static bool ScreenPointHitsTreeRow(TreeNodeObject node, Vector2 screen)
        {
            if (RectContainsScreenPoint(node.rectNode, screen))
                return true;

            if (node.transform is RectTransform rootRt && rootRt != node.rectNode && RectContainsScreenPoint(rootRt, screen))
                return true;

            return false;
        }

        private static bool RectContainsScreenPoint(RectTransform? rt, Vector2 screen)
        {
            if (!rt)
                return false;

            RectTransform row = rt!;
            if (!TryGetRectScreenCamera(row, out Camera? cam))
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(row, screen, cam);
        }

        private static bool TryGetRectScreenCamera(RectTransform row, out Camera? cam)
        {
            cam = null;
            Canvas? canvas = row.GetComponentInParent<Canvas>();
            if (canvas == null)
                return true;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return true;

            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            return true;
        }

    }
}
