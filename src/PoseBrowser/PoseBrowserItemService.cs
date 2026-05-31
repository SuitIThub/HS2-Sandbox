using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class PoseBrowserItemService
    {
        public const float PoseMatchAnimTimeEpsilon = 0.02f;

        public static IEnumerable<OCIItem> GetSelectedItems()
        {
            var list = new List<OCIItem>();
            try
            {
                var gom = Singleton<GuideObjectManager>.Instance;
                if (gom == null)
                    return list;

                foreach (var key in gom.selectObjectKey)
                {
                    try
                    {
                        if (Studio.Studio.GetCtrlInfo(key) is OCIItem oci)
                            AddUniqueItem(list, oci);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                try
                {
                    if (gom.selectObject is GuideObject go && go.dicKey != 0)
                    {
                        if (Studio.Studio.GetCtrlInfo(go.dicKey) is OCIItem oci)
                            AddUniqueItem(list, oci);
                    }
                }
                catch
                {
                    // ignored
                }

                AddTreeSelectedItems(list);
            }
            catch
            {
                // ignored
            }

            return list;
        }

        private static void AddTreeSelectedItems(List<OCIItem> list)
        {
            try
            {
                var selectedNodes = PoseDataService.TryGetStudioTreeNodeCtrl()?.selectNodes;
                if (selectedNodes == null || !selectedNodes.Any())
                    return;

                var studio = Singleton<Studio.Studio>.Instance;
                foreach (var node in selectedNodes)
                {
                    if (node == null)
                        continue;
                    if (studio.dicInfo.TryGetValue(node, out ObjectCtrlInfo info) && info is OCIItem oci)
                        AddUniqueItem(list, oci);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static void AddUniqueItem(List<OCIItem> list, OCIItem? oci)
        {
            if (oci == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], oci))
                    return;
            }

            list.Add(oci);
        }

        public static string GetItemDisplayName(OCIItem oci)
        {
            try
            {
                var tn = oci.treeNodeObject;
                if (tn != null && !string.IsNullOrWhiteSpace(tn.textName))
                    return tn.textName.Trim();
            }
            catch
            {
                // ignored
            }

            try
            {
                var info = oci.itemInfo;
                return $"{info.group}/{info.category}/{info.no}";
            }
            catch
            {
                return "Item";
            }
        }

        public static bool TryGetItemWorldTransform(OCIItem oci, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
            try
            {
                if (oci.guideObject?.transformTarget != null)
                {
                    Transform t = oci.guideObject.transformTarget;
                    position = t.position;
                    rotation = t.rotation;
                    scale = oci.guideObject.changeAmount.scale;
                    return true;
                }

                if (oci.objectItem != null)
                {
                    Transform t = oci.objectItem.transform;
                    position = t.position;
                    rotation = t.rotation;
                    scale = t.localScale;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public static bool TryGetItemObjectScale(OCIItem oci, out Vector3 scale)
        {
            scale = Vector3.one;
            try
            {
                if (oci.guideObject?.changeAmount != null)
                {
                    scale = oci.guideObject.changeAmount.scale;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public static bool TrySetItemWorldTransform(OCIItem oci, Vector3 worldPosition, Quaternion worldRotation, Vector3 scale)
        {
            if (oci.guideObject == null)
                return false;

            try
            {
                var ca = oci.guideObject.changeAmount;
                Transform? parent = oci.guideObject.parent;
                if (parent != null)
                {
                    ca.pos = parent.InverseTransformPoint(worldPosition);
                    ca.rot = (Quaternion.Inverse(parent.rotation) * worldRotation).eulerAngles;
                }
                else
                {
                    if (TryGetItemWorldTransform(oci, out Vector3 currentPos, out _, out _))
                    {
                        Vector3 delta = worldPosition - currentPos;
                        if (delta.sqrMagnitude > 1e-12f)
                            oci.guideObject.MoveWorld(delta);
                    }

                    ca.rot = worldRotation.eulerAngles;
                }

                ca.scale = scale;
                try
                {
                    ca.OnChange();
                }
                catch
                {
                    // ignored
                }

                oci.guideObject.SetScale();
                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not set item transform: {ex.Message}");
                return false;
            }
        }

        /// <summary>Sets guide <see cref="ChangeAmount"/> in the item's current parent-local space (after tree attach).</summary>
        public static bool TrySetItemParentLocalTransform(
            OCIItem oci,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 scale)
        {
            if (oci.guideObject == null)
                return false;

            try
            {
                var ca = oci.guideObject.changeAmount;
                ca.pos = localPosition;
                ca.rot = localRotation.eulerAngles;
                ca.scale = scale;
                return TrySetItemGuideLocalTransform(oci, localRotation, scale);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not set item local transform: {ex.Message}");
                return false;
            }
        }

        private static bool TrySetItemGuideLocalTransform(OCIItem oci, Quaternion localRotation, Vector3 scale)
        {
            if (oci.guideObject == null)
                return false;

            try
            {
                var ca = oci.guideObject.changeAmount;
                ca.rot = localRotation.eulerAngles;
                ca.scale = scale;
                try
                {
                    ca.OnChange();
                }
                catch
                {
                    // ignored
                }

                oci.guideObject.SetScale();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void StoreItemTransformInRecord(
            PoseAssociatedItemRecord record,
            OCIItem item,
            OCIChar anchor,
            Vector3 itemWorldPos,
            Quaternion itemWorldRot,
            Vector3 itemScale)
        {
            record.ItemScale = itemScale;
            record.HasAttachChangeAmount = false;

            if (!string.IsNullOrWhiteSpace(record.ParentTreePath) &&
                TryGetItemGuideChangeAmount(item, out Vector3 attachPos, out Vector3 attachRotEuler))
            {
                record.HasAttachChangeAmount = true;
                record.AttachChangePosition = attachPos;
                record.AttachChangeRotation = NormalizeQuaternion(Quaternion.Euler(attachRotEuler));
            }

            if (!PoseDataService.TryGetCharacterWorldPosition(anchor, out Vector3 anchorPos) ||
                !PoseDataService.TryGetCharacterWorldRotation(anchor, out Quaternion anchorRot))
            {
                record.LocalPosition = itemWorldPos;
                record.LocalRotation = itemWorldRot;
                return;
            }

            record.LocalPosition = PoseBrowserCharacterApply.RelativePositionOffset(anchorRot, anchorPos, itemWorldPos);
            record.LocalRotation = PoseBrowserCharacterApply.RelativeRotation(anchorRot, itemWorldRot);
        }

        private static bool TryGetItemGuideChangeAmount(OCIItem item, out Vector3 position, out Vector3 rotationEuler)
        {
            position = Vector3.zero;
            rotationEuler = Vector3.zero;
            try
            {
                if (item.guideObject?.changeAmount == null)
                    return false;

                var ca = item.guideObject.changeAmount;
                position = ca.pos;
                rotationEuler = ca.rot;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 1e-8f)
                return Quaternion.identity;
            if (Mathf.Abs(mag - 1f) > 1e-4f)
                return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
            return q;
        }

        /// <summary>Label for UI warnings; transform name when known, else workspace tree row text.</summary>
        public static string? DetectParentBodyPartName(OCIItem item, OCIChar anchor)
        {
            try
            {
                if (item.parentInfo is OCIChar parentChar && !ReferenceEquals(parentChar, anchor))
                    return null;

                string? treePath = PoseItemTreeAttach.CaptureParentTreePath(item, anchor);
                if (!string.IsNullOrEmpty(treePath))
                {
                    int sep = treePath.LastIndexOf('|');
                    string leaf = sep >= 0 ? treePath.Substring(sep + 1) : treePath;
                    if (!string.IsNullOrWhiteSpace(leaf))
                        return leaf.Trim();
                }

                Transform? guideParent = item.guideObject?.parent;
                if (guideParent != null && anchor.charInfo != null)
                {
                    Transform charRoot = anchor.charInfo.transform;
                    if (guideParent != charRoot && guideParent.IsChildOf(charRoot))
                        return guideParent.name;
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        public static PoseAssociatedItemRecord? TryCreateRecordFromWorkspaceItem(
            OCIItem item,
            OCIChar anchor,
            out string? error)
        {
            error = null;
            if (!PoseDataService.TryGetCharacterWorldPosition(anchor, out Vector3 anchorPos))
            {
                error = "Cannot read character world position.";
                return null;
            }

            if (!PoseDataService.TryGetCharacterWorldRotation(anchor, out Quaternion anchorRot))
            {
                error = "Cannot read character world rotation.";
                return null;
            }

            if (!TryGetItemWorldTransform(item, out Vector3 itemPos, out Quaternion itemRot, out Vector3 itemScale))
            {
                error = "Cannot read item world transform.";
                return null;
            }

            PoseDataService.TryGetCharacterBodyHeight(anchor, out float anchorH);
            PoseDataService.TryGetCharacterObjectScale(anchor, out Vector3 anchorScale);

            var info = item.itemInfo;
            var record = new PoseAssociatedItemRecord
            {
                ItemKind = info.kind,
                ItemKinds = info.kinds ?? Array.Empty<int>(),
                DisplayName = GetItemDisplayName(item),
                ParentObjectName = DetectParentBodyPartName(item, anchor),
                SavedAnchorBodyHeight = anchorH,
                SavedAnchorObjectScale = anchorScale
            };

            record.ParentTreePath = PoseItemTreeAttach.CaptureParentTreePath(item, anchor);
            if (string.IsNullOrEmpty(record.ParentTreePath) && !string.IsNullOrEmpty(record.ParentObjectName))
                record.ParentTreePath = PoseItemTreeAttach.TryBuildTreePathForDescendant(anchor, record.ParentObjectName);

            StoreItemTransformInRecord(record, item, anchor, itemPos, itemRot, itemScale);

            PoseItemCatalogResolve.FillCatalogPathsFromWorkspace(item, record);

            if (PoseItemInfoSnapshot.TryCapture(item, out byte[] blob, out string? versionText, out _, out _))
            {
                record.ItemInfoBlob = blob;
                record.ItemInfoVersion = versionText;
            }

            return record;
        }

        public static bool TryApplyRecordToCharacter(
            PoseAssociatedItemRecord record,
            OCIChar anchor,
            bool adjustForBodyHeight,
            bool adjustForObjectScale,
            out string? rowWarning) =>
            TryApplyRecordToCharacter(
                record,
                anchor,
                adjustForBodyHeight,
                adjustForObjectScale,
                new PoseItemLoadOptions(),
                out rowWarning);

        public static bool TryApplyRecordToCharacter(
            PoseAssociatedItemRecord record,
            OCIChar anchor,
            bool adjustForBodyHeight,
            bool adjustForObjectScale,
            PoseItemLoadOptions options,
            out string? rowWarning)
        {
            rowWarning = null;
            OCIItem? oci = TrySpawnItem(record);
            if (oci == null)
                return false;

            bool forceFree = options.ForceFreePlacement;
            TreeNodeObject? parentTree = forceFree ? null : ResolveParentTreeNode(anchor, record);
            bool attachedToBodyPart = parentTree != null;
            if (attachedToBodyPart && !PoseItemTreeAttach.TryApplyTreeParent(oci, parentTree))
            {
                rowWarning = "Workspace tree parenting failed — item placed freely";
                attachedToBodyPart = false;
            }

            if (!options.AppliesAnyTransform)
            {
                if (!attachedToBodyPart && !forceFree &&
                    (!string.IsNullOrWhiteSpace(record.ParentTreePath) ||
                     !string.IsNullOrWhiteSpace(record.ParentObjectName)))
                {
                    string label = record.ParentObjectName ?? record.ParentTreePath ?? "parent";
                    rowWarning = $"Body part '{label}' not found in workspace tree — placed freely";
                }

                return true;
            }

            if (!TryApplyRecordTransform(
                    oci,
                    anchor,
                    record,
                    attachedToBodyPart,
                    forceFree,
                    adjustForBodyHeight,
                    adjustForObjectScale,
                    options,
                    out string? transformWarn))
            {
                if (!string.IsNullOrEmpty(transformWarn))
                    rowWarning = transformWarn;
                return false;
            }

            if (!string.IsNullOrEmpty(transformWarn))
                rowWarning = transformWarn;

            if (!attachedToBodyPart && !forceFree &&
                (!string.IsNullOrWhiteSpace(record.ParentTreePath) ||
                 !string.IsNullOrWhiteSpace(record.ParentObjectName)))
            {
                string label = record.ParentObjectName ?? record.ParentTreePath ?? "parent";
                rowWarning = $"Body part '{label}' not found in workspace tree — placed freely";
            }

            return true;
        }

        private static bool TryApplyRecordTransform(
            OCIItem oci,
            OCIChar anchor,
            PoseAssociatedItemRecord record,
            bool attachedToBodyPart,
            bool forceFree,
            bool adjustForBodyHeight,
            bool adjustForObjectScale,
            PoseItemLoadOptions options,
            out string? transformWarning)
        {
            transformWarning = null;
            bool storedAsAttached = PoseItemTreeAttach.IsAttachedToBodyPart(record);
            bool legacyAttachStorage = storedAsAttached && !record.HasAttachChangeAmount;

            if (!TryComputeFreeWorldTransform(
                    anchor,
                    record,
                    adjustForBodyHeight,
                    adjustForObjectScale,
                    out Vector3 worldPosition,
                    out Quaternion worldRotation,
                    out Vector3 scale) &&
                !(legacyAttachStorage && TryComputeLegacyBoneWorldTransform(
                    anchor, record, out worldPosition, out worldRotation)))
                return false;

            Vector3 pos = worldPosition;
            Quaternion rot = worldRotation;
            Vector3 itemScale = scale;

            bool applyAsAttachLocal = attachedToBodyPart && !forceFree;
            if (applyAsAttachLocal)
            {
                if (!TryWorldToAttachChangeAmount(oci, worldPosition, worldRotation, out Vector3 caPos, out Quaternion caRot))
                {
                    transformWarning =
                        "Attach parent not ready — item placed in world space";
                    applyAsAttachLocal = false;
                }
                else
                {
                    pos = caPos;
                    rot = caRot;
                }
            }

            if (!options.LoadPosition || !options.LoadRotation || !options.LoadScale)
            {
                if (applyAsAttachLocal && TryGetItemGuideLocalTransform(oci, out Vector3 curPos, out Quaternion curRot, out Vector3 curScale))
                {
                    if (!options.LoadPosition) pos = curPos;
                    if (!options.LoadRotation) rot = curRot;
                    if (!options.LoadScale) itemScale = curScale;
                }
                else if (TryGetItemWorldTransform(oci, out Vector3 curWPos, out Quaternion curWRot, out Vector3 curWScale))
                {
                    if (!options.LoadPosition) pos = curWPos;
                    if (!options.LoadRotation) rot = curWRot;
                    if (!options.LoadScale) itemScale = curWScale;
                    applyAsAttachLocal = false;
                }
            }

            return applyAsAttachLocal
                ? TrySetItemParentLocalTransform(oci, pos, rot, itemScale)
                : TrySetItemWorldTransform(oci, pos, rot, itemScale);
        }

        /// <summary>Maps desired world pose into Studio attach <see cref="ChangeAmount"/> space (parent = item childRoot after attach).</summary>
        private static bool TryWorldToAttachChangeAmount(
            OCIItem oci,
            Vector3 worldPosition,
            Quaternion worldRotation,
            out Vector3 changePosition,
            out Quaternion changeRotation)
        {
            changePosition = Vector3.zero;
            changeRotation = Quaternion.identity;
            try
            {
                Transform? parent = oci.guideObject?.parent;
                if (parent == null)
                    return false;

                changePosition = parent.InverseTransformPoint(worldPosition);
                changeRotation = NormalizeQuaternion(Quaternion.Inverse(parent.rotation) * worldRotation);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetItemGuideLocalTransform(
            OCIItem oci,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
            try
            {
                if (oci.guideObject?.changeAmount == null)
                    return false;

                var ca = oci.guideObject.changeAmount;
                position = ca.pos;
                rotation = Quaternion.Euler(ca.rot);
                scale = ca.scale;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Pre-v5 rows stored parent-local offsets in <see cref="PoseAssociatedItemRecord.LocalPosition"/>.</summary>
        private static bool TryComputeLegacyBoneWorldTransform(
            OCIChar anchor,
            PoseAssociatedItemRecord record,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            if (!PoseItemTreeAttach.TryGetBodyPartBoneTransform(
                    anchor,
                    record.ParentTreePath,
                    record.ParentObjectName,
                    out Transform boneTransform))
                return false;

            Vector3 boneLocal = ScaleStoredVectorWithCharacter(
                record.LocalPosition, record, anchor, adjustForObjectScale: true);
            worldPosition = boneTransform.TransformPoint(boneLocal);
            worldRotation = boneTransform.rotation * record.LocalRotation;
            return true;
        }

        private static bool TryComputeFreeWorldTransform(
            OCIChar anchor,
            PoseAssociatedItemRecord record,
            bool adjustForBodyHeight,
            bool adjustForObjectScale,
            out Vector3 worldPosition,
            out Quaternion worldRotation,
            out Vector3 scale)
        {
            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            scale = record.ItemScale;

            if (!PoseDataService.TryGetCharacterWorldPosition(anchor, out Vector3 anchorPos) ||
                !PoseDataService.TryGetCharacterWorldRotation(anchor, out Quaternion anchorRot))
                return false;

            Vector3 offset = record.LocalPosition;
            Vector3 currentAnchorScale = Vector3.one;
            bool haveCurrentScale = PoseDataService.TryGetCharacterObjectScale(anchor, out currentAnchorScale);

            if (adjustForObjectScale && haveCurrentScale)
            {
                offset = ScaleAnchorRelativePositionWithCharacter(
                    record.LocalPosition, anchorRot, record, currentAnchorScale);
                scale = ScaleStoredItemScaleWithCharacter(record.ItemScale, record, currentAnchorScale);
            }

            if (adjustForBodyHeight &&
                PoseDataService.TryGetCharacterBodyHeight(anchor, out float currentAnchorH))
            {
                offset.y = PoseBrowserCharacterApply.ScaleRelativeOffsetComponent(
                    offset.y,
                    record.SavedAnchorBodyHeight,
                    record.SavedAnchorBodyHeight,
                    currentAnchorH,
                    currentAnchorH);
            }

            worldPosition = PoseBrowserCharacterApply.WorldPositionFromRelativeOffset(anchorRot, anchorPos, offset);
            worldRotation = anchorRot * record.LocalRotation;
            return true;
        }

        /// <summary>
        /// Scales anchor-relative layout offset with character object-scale ratio (world distance from anchor, then back to local).
        /// </summary>
        private static Vector3 ScaleAnchorRelativePositionWithCharacter(
            Vector3 savedAnchorRelative,
            Quaternion anchorRot,
            PoseAssociatedItemRecord record,
            Vector3 currentAnchorScale)
        {
            Vector3 savedAnchorScale = NormalizeSavedAnchorScale(record.SavedAnchorObjectScale);
            Vector3 ratio = ComputeCharacterScaleRatio(savedAnchorScale, currentAnchorScale);
            Vector3 worldOffset = anchorRot * savedAnchorRelative;
            worldOffset = Vector3.Scale(worldOffset, ratio);
            return Quaternion.Inverse(anchorRot) * worldOffset;
        }

        /// <summary>Scales a vector in bone-local or attach-local space (axis-aligned with that transform's parent chain).</summary>
        private static Vector3 ScaleStoredVectorWithCharacter(
            Vector3 saved,
            PoseAssociatedItemRecord record,
            OCIChar anchor,
            bool adjustForObjectScale)
        {
            if (!adjustForObjectScale ||
                !PoseDataService.TryGetCharacterObjectScale(anchor, out Vector3 currentAnchorScale))
                return saved;

            Vector3 savedAnchorScale = NormalizeSavedAnchorScale(record.SavedAnchorObjectScale);
            Vector3 ratio = ComputeCharacterScaleRatio(savedAnchorScale, currentAnchorScale);
            return Vector3.Scale(saved, ratio);
        }

        private static Vector3 ComputeCharacterScaleRatio(Vector3 savedAnchorScale, Vector3 currentAnchorScale)
        {
            return new Vector3(
                ScaleAxisRatio(savedAnchorScale.x, currentAnchorScale.x),
                ScaleAxisRatio(savedAnchorScale.y, currentAnchorScale.y),
                ScaleAxisRatio(savedAnchorScale.z, currentAnchorScale.z));
        }

        private static float ScaleAxisRatio(float saved, float current) =>
            Mathf.Abs(saved) >= 1e-6f ? current / saved : 1f;

        private static Vector3 ScaleStoredItemScaleWithCharacter(
            Vector3 savedItemScale,
            PoseAssociatedItemRecord record,
            Vector3 currentAnchorScale)
        {
            Vector3 savedAnchorScale = NormalizeSavedAnchorScale(record.SavedAnchorObjectScale);
            return PoseBrowserCharacterApply.ScaleRelativeOffset(
                savedItemScale,
                savedAnchorScale,
                savedAnchorScale,
                currentAnchorScale,
                currentAnchorScale);
        }

        private static Vector3 NormalizeSavedAnchorScale(Vector3 savedAnchorScale)
        {
            if (savedAnchorScale.sqrMagnitude < 1e-8f)
                return Vector3.one;
            return savedAnchorScale;
        }

        private static TreeNodeObject? ResolveParentTreeNode(OCIChar anchor, PoseAssociatedItemRecord record)
        {
            TreeNodeObject? node = PoseItemTreeAttach.ResolveParentTreeNode(anchor, record.ParentTreePath);
            if (node != null)
                return node;

            if (string.IsNullOrWhiteSpace(record.ParentObjectName))
                return null;

            return PoseItemTreeAttach.FindDescendantUnderCharacter(anchor, record.ParentObjectName);
        }

        private static OCIItem? TrySpawnItem(PoseAssociatedItemRecord record)
        {
            try
            {
                if (!PoseItemCatalogResolve.TryResolveSpawnIndices(
                        record.ItemGroup,
                        record.ItemCategory,
                        record.ItemNo,
                        record.BundlePath,
                        record.AssetName,
                        record.Manifest,
                        out int group,
                        out int category,
                        out int localSlot))
                {
                    SandboxServices.Log.LogWarning(
                        $"PoseBrowser: Could not resolve item catalog slot for '{record.DisplayName}' " +
                        $"(bundle={record.BundlePath}, asset={record.AssetName}).");
                    return null;
                }

                var studio = Singleton<Studio.Studio>.Instance;
                var before = new HashSet<int>(studio.dicObjectCtrl.Keys);
                studio.AddItem(group, category, localSlot);

                OCIItem? added = FindSpawnedItem(studio, before, group, category, localSlot);

                if (added == null)
                {
                    SandboxServices.Log.LogWarning(
                        $"PoseBrowser: AddItem({group},{category},{localSlot}) did not create an item.");
                    return null;
                }

                return added;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: AddItem failed: {ex.Message}");
                return null;
            }
        }

        private static OCIItem? FindSpawnedItem(
            Studio.Studio studio,
            HashSet<int> keysBeforeSpawn,
            int group,
            int category,
            int itemNo)
        {
            OCIItem? match = null;
            foreach (var kvp in studio.dicObjectCtrl)
            {
                if (keysBeforeSpawn.Contains(kvp.Key))
                    continue;
                if (kvp.Value is not OCIItem oci)
                    continue;
                if (oci.itemInfo.group != group ||
                    oci.itemInfo.category != category ||
                    oci.itemInfo.no != itemNo)
                    continue;

                match = oci;
            }

            return match;
        }

        public static bool IsPoseAppliedOnCharacter(PoseGridItem pose, OCIChar character)
        {
            try
            {
                if (!TryLoadPoseFileInfo(pose, out var fromFile))
                    return false;
                var current = new PauseCtrl.FileInfo(character);
                return PoseFileInfoMatches(fromFile, current);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryLoadPoseFileInfo(PoseGridItem pose, out PauseCtrl.FileInfo fileInfo)
        {
            fileInfo = new PauseCtrl.FileInfo(null);
            try
            {
                using var fs = new FileStream(pose.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(pose.DataPosition, SeekOrigin.Begin);
                using var br = new BinaryReader(fs);
                string marker = br.ReadString();
                if (string.Compare(marker, "【pose】", StringComparison.Ordinal) != 0)
                    return false;
                int version = br.ReadInt32();
                br.ReadInt32();
                br.ReadString();
                fileInfo.Load(br, version);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool PoseFileInfoMatches(PauseCtrl.FileInfo a, PauseCtrl.FileInfo b)
        {
            if (a.group != b.group || a.category != b.category || a.no != b.no)
                return false;
            if (Mathf.Abs(a.normalizedTime - b.normalizedTime) > PoseMatchAnimTimeEpsilon)
                return false;
            if (a.enableIK != b.enableIK || a.enableFK != b.enableFK)
                return false;
            return true;
        }
    }
}
