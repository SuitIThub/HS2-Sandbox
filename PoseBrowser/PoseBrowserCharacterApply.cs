using System;
using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class PoseBrowserCharacterApply
    {
        /// <summary>Min |H_member − H_anchor| at save to use spread ratio for Y scaling (else anchor-only / averaged).</summary>
        private const float BodyHeightSpreadEpsilon = 0.001f;

        private enum PoseGenderTag
        {
            Untagged,
            Male,
            Female
        }

        public static int ApplyPosesToSelectedCharacters(
            PoseDataService dataService,
            PoseBrowserCharacterConfig config,
            IReadOnlyList<PoseGridItem> poses,
            IReadOnlyList<OCIChar> studioSelection,
            Action<PoseGridItem>? onPoseApplied = null)
        {
            if (poses.Count == 0 || studioSelection.Count == 0)
                return 0;

            var selected = new List<OCIChar>(studioSelection);
            var posed = new HashSet<OCIChar>();
            var priorityOrder = BuildOrderedSelectedCharacters(
                selected, config, PoseGenderTag.Untagged, appendUnlisted: true);

            int maleCursor = 0;
            int femaleCursor = 0;
            int untaggedCharIndex = 0;
            int applied = 0;

            foreach (var pose in poses)
            {
                var tag = GetPoseGenderTag(pose);
                OCIChar? target = null;

                if (tag == PoseGenderTag.Untagged)
                {
                    while (untaggedCharIndex < priorityOrder.Count)
                    {
                        var candidate = priorityOrder[untaggedCharIndex++];
                        if (posed.Contains(candidate))
                            continue;
                        target = candidate;
                        break;
                    }
                }
                else
                {
                    var pool = BuildOrderedSelectedCharacters(selected, config, tag, appendUnlisted: false);
                    if (pool.Count == 0)
                        continue;

                    ref int cursor = ref (tag == PoseGenderTag.Male ? ref maleCursor : ref femaleCursor);
                    target = PickNextUnposedCharacter(pool, posed, ref cursor);
                }

                if (target == null)
                    continue;

                if (!dataService.ApplyPose(pose, target))
                    continue;

                posed.Add(target);
                applied++;
                onPoseApplied?.Invoke(pose);
            }

            for (int i = 0; i < priorityOrder.Count; i++)
            {
                var oci = priorityOrder[i];
                if (posed.Contains(oci))
                    continue;

                var pose = poses[i % poses.Count];
                if (!IsCharacterEligibleForPose(oci, pose, selected, config))
                    continue;

                if (!dataService.ApplyPose(pose, oci))
                    continue;

                posed.Add(oci);
                applied++;
                onPoseApplied?.Invoke(pose);
            }

            return applied;
        }

        /// <summary>
        /// Returns whether every pose can be assigned to a distinct selected character using the same rules as apply.
        /// Requires an equal pose and character count.
        /// </summary>
        public static bool CanApplyPosesOneToOne(
            PoseBrowserCharacterConfig config,
            IReadOnlyList<PoseGridItem> poses,
            IReadOnlyList<OCIChar> studioSelection)
        {
            return TryBuildPoseCharacterAssignments(config, poses, studioSelection, out _);
        }

        /// <summary>
        /// Builds pose-to-character assignments in pose order when a full one-to-one apply is possible.
        /// </summary>
        public static bool TryBuildPoseCharacterAssignments(
            PoseBrowserCharacterConfig config,
            IReadOnlyList<PoseGridItem> poses,
            IReadOnlyList<OCIChar> studioSelection,
            out List<(PoseGridItem pose, OCIChar character)>? assignments)
        {
            assignments = null;
            if (poses.Count == 0 || studioSelection.Count != poses.Count)
                return false;

            var selected = new List<OCIChar>(studioSelection);
            var posed = new HashSet<OCIChar>();
            var priorityOrder = BuildOrderedSelectedCharacters(
                selected, config, PoseGenderTag.Untagged, appendUnlisted: true);
            var result = new List<(PoseGridItem, OCIChar)>(poses.Count);

            int maleCursor = 0;
            int femaleCursor = 0;
            int untaggedCharIndex = 0;

            foreach (var pose in poses)
            {
                var tag = GetPoseGenderTag(pose);
                OCIChar? target = null;

                if (tag == PoseGenderTag.Untagged)
                {
                    while (untaggedCharIndex < priorityOrder.Count)
                    {
                        var candidate = priorityOrder[untaggedCharIndex++];
                        if (posed.Contains(candidate))
                            continue;
                        target = candidate;
                        break;
                    }
                }
                else
                {
                    var pool = BuildOrderedSelectedCharacters(selected, config, tag, appendUnlisted: false);
                    if (pool.Count == 0)
                        return false;

                    ref int cursor = ref (tag == PoseGenderTag.Male ? ref maleCursor : ref femaleCursor);
                    target = PickNextUnposedCharacter(pool, posed, ref cursor);
                }

                if (target == null)
                    return false;

                posed.Add(target);
                result.Add((pose, target));
            }

            if (result.Count != poses.Count)
                return false;

            assignments = result;
            return true;
        }

        /// <summary>
        /// Applies stored relative layout (position and/or rotation) for non-anchor characters from the anchor (first pose / assignment).
        /// </summary>
        public static int ApplyGroupRelativePositions(
            PoseGroup group,
            PoseBrowserCharacterConfig config,
            IReadOnlyList<PoseGridItem> poses,
            IReadOnlyList<OCIChar> studioSelection,
            string poseRootPath,
            bool adjustForBodyHeight = false)
        {
            if ((group.MemberRelativeOffsets.Count == 0 && group.MemberRelativeRotations.Count == 0) ||
                poses.Count == 0)
                return 0;

            if (!TryBuildPoseCharacterAssignments(config, poses, studioSelection, out var assignments) ||
                assignments == null ||
                assignments.Count == 0)
                return 0;

            bool haveAnchorPos = PoseDataService.TryGetCharacterWorldPosition(
                assignments[0].character, out Vector3 anchorPos);
            bool haveAnchorRot = PoseDataService.TryGetCharacterWorldRotation(
                assignments[0].character, out Quaternion anchorRot);

            string anchorRel = PoseGroupDatabase.NormalizeMemberPath(
                assignments[0].pose.RelativePath(poseRootPath));
            float savedAnchorH = 0f;
            float currentAnchorH = 0f;
            bool useHeights = adjustForBodyHeight &&
                              group.MemberBodyHeights.Count > 0 &&
                              !string.IsNullOrEmpty(anchorRel) &&
                              group.MemberBodyHeights.TryGetValue(anchorRel, out savedAnchorH) &&
                              PoseDataService.TryGetCharacterBodyHeight(assignments[0].character, out currentAnchorH);

            int moved = 0;
            for (int i = 1; i < assignments.Count; i++)
            {
                string rel = PoseGroupDatabase.NormalizeMemberPath(
                    assignments[i].pose.RelativePath(poseRootPath));
                if (string.IsNullOrEmpty(rel))
                    continue;

                bool applied = false;
                if (haveAnchorPos &&
                    group.MemberRelativeOffsets.TryGetValue(rel, out Vector3 offset))
                {
                    Vector3 target = anchorPos + offset;

                    if (useHeights &&
                        group.MemberBodyHeights.TryGetValue(rel, out float savedMemberH) &&
                        PoseDataService.TryGetCharacterBodyHeight(assignments[i].character, out float currentMemberH))
                    {
                        float ryScaled = ScaleRelativeOffsetY(
                            offset.y, savedAnchorH, savedMemberH, currentAnchorH, currentMemberH);
                        target.y = anchorPos.y + ryScaled;
                    }

                    if (PoseDataService.TrySetCharacterWorldPosition(assignments[i].character, target))
                        applied = true;
                }

                if (haveAnchorRot &&
                    group.MemberRelativeRotations.TryGetValue(rel, out Vector3 relativeEuler))
                {
                    Quaternion targetRot = anchorRot * Quaternion.Euler(relativeEuler);
                    if (PoseDataService.TrySetCharacterWorldRotation(assignments[i].character, targetRot))
                        applied = true;
                }

                if (applied)
                    moved++;
            }

            return moved;
        }

        internal static Vector3 RelativeRotationEuler(Quaternion anchorRot, Quaternion memberRot) =>
            (Quaternion.Inverse(anchorRot) * memberRot).eulerAngles;

        internal static bool IsNearIdentityRelativeRotation(Vector3 relativeEuler) =>
            Quaternion.Angle(Quaternion.Euler(relativeEuler), Quaternion.identity) < 0.05f;

        public static List<OCIChar> BuildEligiblePoolForApply(
            PoseGridItem pose,
            IReadOnlyList<OCIChar> studioSelection,
            PoseBrowserCharacterConfig config)
        {
            return BuildOrderedSelectedCharacters(
                studioSelection, config, GetPoseGenderTag(pose), appendUnlisted: false);
        }

        private static bool IsCharacterEligibleForPose(
            OCIChar oci,
            PoseGridItem pose,
            IReadOnlyList<OCIChar> studioSelection,
            PoseBrowserCharacterConfig config)
        {
            var pool = BuildOrderedSelectedCharacters(
                studioSelection, config, GetPoseGenderTag(pose), appendUnlisted: false);
            return pool.Contains(oci);
        }

        private static List<OCIChar> BuildOrderedSelectedCharacters(
            IReadOnlyList<OCIChar> studioSelection,
            PoseBrowserCharacterConfig config,
            PoseGenderTag tagFilter,
            bool appendUnlisted)
        {
            var selectedSet = new HashSet<OCIChar>();
            foreach (var oci in studioSelection)
                selectedSet.Add(oci);

            var result = new List<OCIChar>();
            var used = new HashSet<OCIChar>();

            void AddFromSlots(IEnumerable<PoseBrowserCharacterSlot> slots)
            {
                foreach (var slot in slots)
                {
                    if (!PoseBrowserCharacterSlot.TryResolveInScene(slot, out var oci) || !selectedSet.Contains(oci))
                        continue;
                    if (used.Add(oci))
                        result.Add(oci);
                }
            }

            switch (tagFilter)
            {
                case PoseGenderTag.Male:
                    AddFromSlots(config.Male);
                    break;
                case PoseGenderTag.Female:
                    AddFromSlots(config.Female);
                    break;
                default:
                    AppendInterleavedByRank(
                        config, selectedSet, used, result, config.UntaggedInterleaveFemaleFirst);
                    break;
            }

            if (appendUnlisted)
            {
                foreach (var oci in studioSelection)
                {
                    if (used.Add(oci))
                        result.Add(oci);
                }
            }

            return result;
        }

        /// <summary>
        /// Scales the saved relative Y offset from maker body-height ratios (no fixed world multiplier).
        /// Uses spread ratio when save had distinct anchor/member heights; otherwise anchor scale or average.
        /// </summary>
        internal static float ScaleRelativeOffsetY(
            float rySaved,
            float savedAnchorH,
            float savedMemberH,
            float currentAnchorH,
            float currentMemberH)
        {
            float savedSpread = savedMemberH - savedAnchorH;
            if (Mathf.Abs(savedSpread) >= BodyHeightSpreadEpsilon)
            {
                float currentSpread = currentMemberH - currentAnchorH;
                return rySaved * (currentSpread / savedSpread);
            }

            if (Mathf.Abs(savedAnchorH) >= BodyHeightSpreadEpsilon)
                return rySaved * (currentAnchorH / savedAnchorH);

            float scaleAnchor = Mathf.Abs(savedAnchorH) >= BodyHeightSpreadEpsilon
                ? currentAnchorH / savedAnchorH
                : 1f;
            float scaleMember = Mathf.Abs(savedMemberH) >= BodyHeightSpreadEpsilon
                ? currentMemberH / savedMemberH
                : 1f;
            return rySaved * 0.5f * (scaleAnchor + scaleMember);
        }

        private static void AppendInterleavedByRank(
            PoseBrowserCharacterConfig config,
            HashSet<OCIChar> selectedSet,
            HashSet<OCIChar> used,
            List<OCIChar> result,
            bool femaleFirst)
        {
            int maleCount = config.Male.Count;
            int femaleCount = config.Female.Count;
            int maxRank = Math.Max(maleCount, femaleCount);
            for (int r = 0; r < maxRank; r++)
            {
                if (femaleFirst)
                {
                    TryAddSlotAtRank(config.Female, femaleCount, r, selectedSet, used, result);
                    TryAddSlotAtRank(config.Male, maleCount, r, selectedSet, used, result);
                }
                else
                {
                    TryAddSlotAtRank(config.Male, maleCount, r, selectedSet, used, result);
                    TryAddSlotAtRank(config.Female, femaleCount, r, selectedSet, used, result);
                }
            }
        }

        private static void TryAddSlotAtRank(
            IReadOnlyList<PoseBrowserCharacterSlot> slots,
            int slotCount,
            int rank,
            HashSet<OCIChar> selectedSet,
            HashSet<OCIChar> used,
            List<OCIChar> result)
        {
            if (rank >= slotCount)
                return;

            var slot = slots[rank];
            if (PoseBrowserCharacterSlot.TryResolveInScene(slot, out var oci) &&
                selectedSet.Contains(oci) && used.Add(oci))
                result.Add(oci);
        }

        /// <summary>
        /// Next eligible character in pool order, skipping anyone already posed in this apply batch.
        /// </summary>
        private static OCIChar? PickNextUnposedCharacter(
            IReadOnlyList<OCIChar> pool,
            HashSet<OCIChar> posed,
            ref int cursor)
        {
            if (pool.Count == 0)
                return null;

            for (int i = 0; i < pool.Count; i++)
            {
                var candidate = pool[(cursor + i) % pool.Count];
                if (posed.Contains(candidate))
                    continue;

                cursor = (cursor + i + 1) % pool.Count;
                return candidate;
            }

            return null;
        }

        private static PoseGenderTag GetPoseGenderTag(PoseGridItem pose)
        {
            bool hasMale = false;
            bool hasFemale = false;
            foreach (var t in pose.Tags)
            {
                if (string.Equals(t, "male", StringComparison.OrdinalIgnoreCase))
                    hasMale = true;
                else if (string.Equals(t, "female", StringComparison.OrdinalIgnoreCase))
                    hasFemale = true;
            }

            if (hasMale && !hasFemale) return PoseGenderTag.Male;
            if (hasFemale && !hasMale) return PoseGenderTag.Female;
            return PoseGenderTag.Untagged;
        }
    }
}
