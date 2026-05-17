using System;
using System.Collections.Generic;
using Studio;

namespace HS2SandboxPlugin
{
    internal static class PoseBrowserCharacterApply
    {
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
                    int maleCount = config.Male.Count;
                    int femaleCount = config.Female.Count;
                    int maxRank = Math.Max(maleCount, femaleCount);
                    for (int r = 0; r < maxRank; r++)
                    {
                        if (r < maleCount)
                        {
                            var slot = config.Male[r];
                            if (PoseBrowserCharacterSlot.TryResolveInScene(slot, out var m) &&
                                selectedSet.Contains(m) && used.Add(m))
                                result.Add(m);
                        }

                        if (r < femaleCount)
                        {
                            var slot = config.Female[r];
                            if (PoseBrowserCharacterSlot.TryResolveInScene(slot, out var f) &&
                                selectedSet.Contains(f) && used.Add(f))
                                result.Add(f);
                        }
                    }

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
