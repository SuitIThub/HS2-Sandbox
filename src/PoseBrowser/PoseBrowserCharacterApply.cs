using System;
using System.Collections.Generic;
using System.Linq;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class PoseBrowserCharacterApply
    {
        /// <summary>Min |member − anchor| at save to use spread ratio for layout scaling (else anchor-only / averaged).</summary>
        private const float LayoutSpreadEpsilon = 0.001f;

        private enum PoseGenderTag
        {
            Untagged,
            Male,
            Female
        }

        public static int ApplyPosesToSelectedCharacters(
            PoseDataService dataService,
            PoseBrowserCharacterConfig config,
            IList<PoseGridItem> poses,
            IList<OCIChar> studioSelection,
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
            IList<PoseGridItem> poses,
            IList<OCIChar> studioSelection)
        {
            return TryBuildPoseCharacterAssignments(config, poses, studioSelection, out _);
        }

        /// <summary>
        /// Planned first-pass pose-to-character mapping (one character per pose).
        /// </summary>
        public static List<PoseOciNullablePair> BuildPlannedPoseAssignments(
            PoseBrowserCharacterConfig config,
            IList<PoseGridItem> poses,
            IList<OCIChar> studioSelection)
        {
            var result = new List<PoseOciNullablePair>(poses.Count);
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
                result.Add(new PoseOciNullablePair(pose, target));
            }

            return result;
        }

        /// <summary>
        /// Full planned apply (first pass + second pass), grouped by pose display order.
        /// </summary>
        public static List<PoseCharListPair> BuildFullPlannedPoseAssignmentPlan(
            PoseBrowserCharacterConfig config,
            IList<PoseGridItem> poses,
            IList<OCIChar> studioSelection)
        {
            var charsPerPose = new List<OCIChar>[poses.Count];
            for (int i = 0; i < poses.Count; i++)
                charsPerPose[i] = new List<OCIChar>();

            if (poses.Count == 0 || studioSelection.Count == 0)
            {
                var empty = new List<PoseCharListPair>(poses.Count);
                foreach (var pose in poses)
                    empty.Add(new PoseCharListPair(pose, new List<OCIChar>()));
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

            var result = new List<PoseCharListPair>(poses.Count);
            for (int i = 0; i < poses.Count; i++)
                result.Add(new PoseCharListPair(poses[i], charsPerPose[i]));
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
            IList<PoseGridItem> poses,
            IList<OCIChar> studioSelection,
            out List<PoseOciPair>? assignments)
        {
            assignments = null;
            if (poses.Count == 0 || studioSelection.Count != poses.Count)
                return false;

            var planned = BuildPlannedPoseAssignments(config, poses, studioSelection);
            if (planned.Count != poses.Count)
                return false;

            assignments = new List<PoseOciPair>(poses.Count);
            for (int i = 0; i < planned.Count; i++)
            {
                OCIChar? character = planned[i].Character;
                if (character == null)
                    return false;
                assignments.Add(new PoseOciPair(planned[i].Pose, character));
            }

            return true;
        }

        /// <summary>
        /// Applies stored relative layout (position and/or rotation) for non-anchor poses from the anchor (first pose).
        /// Uses the same pose-to-character plan as multi-character apply (including when more characters than poses).
        /// Offsets are keyed by pose path; every character that received a given pose gets that pose's saved layout.
        /// When a member has a saved offset but no saved relative rotation (near-identity at save), facing is aligned to the anchor.
        /// </summary>
        public static int ApplyGroupRelativePositions(
            PoseGroup group,
            PoseBrowserCharacterConfig config,
            IList<PoseGridItem> poses,
            IList<OCIChar> studioSelection,
            string poseRootPath,
            bool adjustForBodyHeight = false,
            bool adjustForObjectScale = false)
        {
            if ((group.MemberRelativeOffsets.Count == 0 && group.MemberRelativeRotations.Count == 0) ||
                poses.Count == 0)
                return 0;

            var plan = BuildFullPlannedPoseAssignmentPlan(config, poses, studioSelection);
            if (plan.Count == 0 || plan[0].Characters.Count == 0)
                return 0;

            OCIChar anchorChar = plan[0].Characters[0];
            bool haveAnchorPos = PoseDataService.TryGetCharacterWorldPosition(anchorChar, out Vector3 anchorPos);
            bool haveAnchorRot = PoseDataService.TryGetCharacterWorldRotation(anchorChar, out Quaternion anchorRot);

            string anchorRel = PoseGroupDatabase.NormalizeMemberPath(plan[0].Pose.RelativePath(poseRootPath));
            float savedAnchorH = 0f;
            float currentAnchorH = 0f;
            bool useHeights = adjustForBodyHeight &&
                              group.MemberBodyHeights.Count > 0 &&
                              !string.IsNullOrEmpty(anchorRel) &&
                              group.MemberBodyHeights.TryGetValue(anchorRel, out savedAnchorH) &&
                              PoseDataService.TryGetCharacterBodyHeight(anchorChar, out currentAnchorH);

            Vector3 savedAnchorScale = Vector3.one;
            Vector3 currentAnchorScale = Vector3.one;
            bool useObjectScales = adjustForObjectScale &&
                                   group.MemberObjectScales.Count > 0 &&
                                   !string.IsNullOrEmpty(anchorRel) &&
                                   group.MemberObjectScales.TryGetValue(anchorRel, out savedAnchorScale) &&
                                   PoseDataService.TryGetCharacterObjectScale(anchorChar, out currentAnchorScale);

            int moved = 0;
            for (int pi = 1; pi < plan.Count; pi++)
            {
                PoseCharListPair planEntry = plan[pi];
                PoseGridItem pose = planEntry.Pose;
                List<OCIChar> characters = planEntry.Characters;
                if (characters.Count == 0)
                    continue;

                string rel = PoseGroupDatabase.NormalizeMemberPath(pose.RelativePath(poseRootPath));
                if (string.IsNullOrEmpty(rel))
                    continue;

                bool hasOffset = group.MemberRelativeOffsets.TryGetValue(rel, out Vector3 localOffset);
                bool hasStoredRotation = group.MemberRelativeRotations.TryGetValue(rel, out Quaternion relativeRot);
                if (!hasOffset && !hasStoredRotation)
                    continue;

                foreach (var character in characters)
                {
                    bool applied = false;
                    if (haveAnchorPos && hasOffset)
                    {
                        Vector3 offset = localOffset;
                        if (useObjectScales &&
                            group.MemberObjectScales.TryGetValue(rel, out Vector3 savedMemberScale) &&
                            PoseDataService.TryGetCharacterObjectScale(character, out Vector3 currentMemberScale))
                        {
                            offset = ScaleRelativeOffset(
                                offset,
                                savedAnchorScale,
                                savedMemberScale,
                                currentAnchorScale,
                                currentMemberScale);
                        }

                        if (useHeights &&
                            group.MemberBodyHeights.TryGetValue(rel, out float savedMemberH) &&
                            PoseDataService.TryGetCharacterBodyHeight(character, out float currentMemberH))
                        {
                            offset.y = ScaleRelativeOffsetComponent(
                                offset.y, savedAnchorH, savedMemberH, currentAnchorH, currentMemberH);
                        }

                        Vector3 target = WorldPositionFromRelativeOffset(
                            haveAnchorRot ? anchorRot : Quaternion.identity,
                            anchorPos,
                            offset);

                        if (PoseDataService.TrySetCharacterWorldPosition(character, target))
                            applied = true;
                    }

                    // Apply facing whenever this pose has layout data. Near-identity rotations are not persisted on save;
                    // use identity so a prior group's guide rotation on the member is cleared.
                    if (haveAnchorRot && (hasStoredRotation || hasOffset))
                    {
                        Quaternion memberRelative = hasStoredRotation ? relativeRot : Quaternion.identity;
                        Quaternion targetRot = anchorRot * memberRelative;
                        if (PoseDataService.TrySetCharacterWorldRotation(character, targetRot))
                            applied = true;
                    }

                    if (applied)
                        moved++;
                }
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
            IList<OCIChar> studioSelection,
            PoseBrowserCharacterConfig config)
        {
            return BuildOrderedSelectedCharacters(
                studioSelection, config, GetPoseGenderTag(pose), appendUnlisted: false);
        }

        private static bool IsCharacterEligibleForPose(
            OCIChar oci,
            PoseGridItem pose,
            IList<OCIChar> studioSelection,
            PoseBrowserCharacterConfig config)
        {
            var pool = BuildOrderedSelectedCharacters(
                studioSelection, config, GetPoseGenderTag(pose), appendUnlisted: false);
            return pool.Contains(oci);
        }

        private static List<OCIChar> BuildOrderedSelectedCharacters(
            IList<OCIChar> studioSelection,
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
        /// Scales one saved relative offset component from saved vs current layout ratios (no fixed world multiplier).
        /// Uses spread ratio when save had distinct anchor/member values; otherwise anchor ratio or average.
        /// </summary>
        internal static float ScaleRelativeOffsetComponent(
            float savedComponent,
            float savedAnchor,
            float savedMember,
            float currentAnchor,
            float currentMember)
        {
            float savedSpread = savedMember - savedAnchor;
            if (Mathf.Abs(savedSpread) >= LayoutSpreadEpsilon)
            {
                float currentSpread = currentMember - currentAnchor;
                return savedComponent * (currentSpread / savedSpread);
            }

            if (Mathf.Abs(savedAnchor) >= LayoutSpreadEpsilon)
                return savedComponent * (currentAnchor / savedAnchor);

            float scaleAnchor = Mathf.Abs(savedAnchor) >= LayoutSpreadEpsilon
                ? currentAnchor / savedAnchor
                : 1f;
            float scaleMember = Mathf.Abs(savedMember) >= LayoutSpreadEpsilon
                ? currentMember / savedMember
                : 1f;
            return savedComponent * 0.5f * (scaleAnchor + scaleMember);
        }

        internal static float ScaleRelativeOffsetY(
            float rySaved,
            float savedAnchorH,
            float savedMemberH,
            float currentAnchorH,
            float currentMemberH) =>
            ScaleRelativeOffsetComponent(rySaved, savedAnchorH, savedMemberH, currentAnchorH, currentMemberH);

        /// <summary>Scales saved anchor-local offset XYZ from Studio object-scale ratios (same rules as body-height Y).</summary>
        internal static Vector3 ScaleRelativeOffset(
            Vector3 savedOffset,
            Vector3 savedAnchorScale,
            Vector3 savedMemberScale,
            Vector3 currentAnchorScale,
            Vector3 currentMemberScale) =>
            new Vector3(
                ScaleRelativeOffsetComponent(
                    savedOffset.x, savedAnchorScale.x, savedMemberScale.x, currentAnchorScale.x, currentMemberScale.x),
                ScaleRelativeOffsetComponent(
                    savedOffset.y, savedAnchorScale.y, savedMemberScale.y, currentAnchorScale.y, currentMemberScale.y),
                ScaleRelativeOffsetComponent(
                    savedOffset.z, savedAnchorScale.z, savedMemberScale.z, currentAnchorScale.z, currentMemberScale.z));

        /// <summary>
        /// Next eligible character in pool order, skipping anyone already posed in this apply batch.
        /// </summary>
        private static OCIChar? PickNextUnposedCharacter(
            IList<OCIChar> pool,
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
