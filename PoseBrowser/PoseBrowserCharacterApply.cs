using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Planned first-pass pose-to-character mapping (one character per pose).
        /// </summary>
        public static List<(PoseGridItem pose, OCIChar? character)> BuildPlannedPoseAssignments(
            PoseBrowserCharacterConfig config,
            IReadOnlyList<PoseGridItem> poses,
            IReadOnlyList<OCIChar> studioSelection)
        {
            var result = new List<(PoseGridItem, OCIChar?)>(poses.Count);
            if (poses.Count == 0 || studioSelection.Count == 0)
                return result;

            var selected = new List<OCIChar>(studioSelection);
            var posed = new HashSet<OCIChar>();
            var priorityOrder = BuildOrderedSelectedCharacters(
                selected, config, PoseGenderTag.Untagged, appendUnlisted: true);

            int maleCursor = 0;
            int femaleCursor = 0;
            int untaggedCharIndex = 0;

            foreach (var pose in poses)
            {
                if (TryPickTargetForPose(
                        pose, selected, config, priorityOrder, posed,
                        ref maleCursor, ref femaleCursor, ref untaggedCharIndex,
                        out var target) &&
                    target != null)
                    posed.Add(target);
                result.Add((pose, target));
            }

            return result;
        }

        /// <summary>
        /// Full planned apply (first pass + second pass), grouped by pose display order.
        /// </summary>
        public static List<(PoseGridItem pose, List<OCIChar> characters)> BuildFullPlannedPoseAssignmentPlan(
            PoseBrowserCharacterConfig config,
            IReadOnlyList<PoseGridItem> poses,
            IReadOnlyList<OCIChar> studioSelection)
        {
            var charsPerPose = new List<OCIChar>[poses.Count];
            for (int i = 0; i < poses.Count; i++)
                charsPerPose[i] = new List<OCIChar>();

            if (poses.Count == 0 || studioSelection.Count == 0)
            {
                var empty = new List<(PoseGridItem, List<OCIChar>)>(poses.Count);
                foreach (var pose in poses)
                    empty.Add((pose, new List<OCIChar>()));
                return empty;
            }

            var selected = new List<OCIChar>(studioSelection);
            var posed = new HashSet<OCIChar>();
            var priorityOrder = BuildOrderedSelectedCharacters(
                selected, config, PoseGenderTag.Untagged, appendUnlisted: true);

            int maleCursor = 0;
            int femaleCursor = 0;
            int untaggedCharIndex = 0;

            for (int pi = 0; pi < poses.Count; pi++)
            {
                if (TryPickTargetForPose(
                        poses[pi], selected, config, priorityOrder, posed,
                        ref maleCursor, ref femaleCursor, ref untaggedCharIndex,
                        out var target) &&
                    target != null)
                {
                    posed.Add(target);
                    charsPerPose[pi].Add(target);
                }
            }

            for (int i = 0; i < priorityOrder.Count; i++)
            {
                var oci = priorityOrder[i];
                if (posed.Contains(oci))
                    continue;

                int pi = i % poses.Count;
                var pose = poses[pi];
                if (!IsCharacterEligibleForPose(oci, pose, selected, config))
                    continue;

                posed.Add(oci);
                charsPerPose[pi].Add(oci);
            }

            var result = new List<(PoseGridItem, List<OCIChar>)>(poses.Count);
            for (int i = 0; i < poses.Count; i++)
                result.Add((poses[i], charsPerPose[i]));
            return result;
        }

        private static bool TryPickTargetForPose(
            PoseGridItem pose,
            List<OCIChar> selected,
            PoseBrowserCharacterConfig config,
            List<OCIChar> priorityOrder,
            HashSet<OCIChar> posed,
            ref int maleCursor,
            ref int femaleCursor,
            ref int untaggedCharIndex,
            out OCIChar? target)
        {
            target = null;
            var tag = GetPoseGenderTag(pose);

            if (tag == PoseGenderTag.Untagged)
            {
                while (untaggedCharIndex < priorityOrder.Count)
                {
                    var candidate = priorityOrder[untaggedCharIndex++];
                    if (posed.Contains(candidate))
                        continue;
                    target = candidate;
                    return true;
                }
            }
            else
            {
                var pool = BuildOrderedSelectedCharacters(selected, config, tag, appendUnlisted: false);
                if (pool.Count == 0)
                    return false;

                ref int cursor = ref (tag == PoseGenderTag.Male ? ref maleCursor : ref femaleCursor);
                target = PickNextUnposedCharacter(pool, posed, ref cursor);
                return target != null;
            }

            return false;
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

            var planned = BuildPlannedPoseAssignments(config, poses, studioSelection);
            if (planned.Count != poses.Count || planned.Any(p => p.character == null))
                return false;

            assignments = planned.Select(p => (p.pose, p.character!)).ToList();
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
                    group.MemberRelativeOffsets.TryGetValue(rel, out Vector3 localOffset))
                {
                    if (useHeights &&
                        group.MemberBodyHeights.TryGetValue(rel, out float savedMemberH) &&
                        PoseDataService.TryGetCharacterBodyHeight(assignments[i].character, out float currentMemberH))
                    {
                        float ryScaled = ScaleRelativeOffsetY(
                            localOffset.y, savedAnchorH, savedMemberH, currentAnchorH, currentMemberH);
                        localOffset.y = ryScaled;
                    }

                    Vector3 target = WorldPositionFromRelativeOffset(
                        haveAnchorRot ? anchorRot : Quaternion.identity,
                        anchorPos,
                        localOffset);

                    if (PoseDataService.TrySetCharacterWorldPosition(assignments[i].character, target))
                        applied = true;
                }

                if (haveAnchorRot &&
                    group.MemberRelativeRotations.TryGetValue(rel, out Quaternion relativeRot))
                {
                    Quaternion targetRot = anchorRot * relativeRot;
                    if (PoseDataService.TrySetCharacterWorldRotation(assignments[i].character, targetRot))
                        applied = true;
                }

                if (applied)
                    moved++;
            }

            return moved;
        }

        internal static Vector3 RelativePositionOffset(Quaternion anchorRot, Vector3 anchorPos, Vector3 memberPos) =>
            Quaternion.Inverse(anchorRot) * (memberPos - anchorPos);

        internal static Vector3 WorldPositionFromRelativeOffset(
            Quaternion anchorRot,
            Vector3 anchorPos,
            Vector3 localOffset) =>
            anchorPos + anchorRot * localOffset;

        internal static Quaternion RelativeRotation(Quaternion anchorRot, Quaternion memberRot)
        {
            Quaternion relative = Quaternion.Inverse(anchorRot) * memberRot;
            float mag = Mathf.Sqrt(
                relative.x * relative.x + relative.y * relative.y +
                relative.z * relative.z + relative.w * relative.w);
            if (mag < 1e-8f)
                return Quaternion.identity;
            if (Mathf.Abs(mag - 1f) > 1e-4f)
                relative = new Quaternion(
                    relative.x / mag, relative.y / mag, relative.z / mag, relative.w / mag);
            return relative;
        }

        internal static bool IsNearIdentityRelativeRotation(Quaternion relative) =>
            Quaternion.Angle(relative, Quaternion.identity) < 0.05f;

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

            foreach (var slot in config.Priority)
            {
                if (tagFilter == PoseGenderTag.Male && slot.IsFemale)
                    continue;
                if (tagFilter == PoseGenderTag.Female && !slot.IsFemale)
                    continue;
                if (!PoseBrowserCharacterSlot.TryResolveInScene(slot, out var oci) || !selectedSet.Contains(oci))
                    continue;
                if (used.Add(oci))
                    result.Add(oci);
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
