using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Pooled plugin-owned embedded skeleton rigs (no scene characters).</summary>
    internal static class AnimPreviewRigPool
    {
        public static readonly Vector3 OffScreenPosition = new Vector3(0f, -5000f, 0f);

        private static readonly List<AnimPreviewRig> OwnedRigs = new List<AnimPreviewRig>();

        public static string DiagnosticGameRoot
        {
            get
            {
                try
                {
                    return BepInEx.Paths.GameRootPath ?? "(null)";
                }
                catch
                {
                    return "(unknown)";
                }
            }
        }

        public static IEnumerator EnsureFigureCountCoroutine(int count, IList<AnimPreviewFigureSlot> slots, AnimPreviewRig[] output)
        {
            if (output == null || count <= 0 || count > output.Length)
                yield break;

            for (int i = 0; i < count; i++)
            {
                int sex = slots != null && i < slots.Count ? slots[i].PreferredSex : 1;
                output[i] = AcquireRig(sex, i, output, i);
            }

            yield return null;
        }

        public static void DisposeOwned()
        {
            for (int i = 0; i < OwnedRigs.Count; i++)
                OwnedRigs[i].Detach();
            OwnedRigs.Clear();
        }

        internal static int DiagnosticOwnedRigCount => OwnedRigs.Count;

        internal static bool DiagnosticTryBuildEmbedded(int sex, out string detail)
        {
            detail = string.Empty;
            try
            {
                var rig = new AnimPreviewRig();
                rig.EnsureSkeleton(sex);
                if (!rig.TrySampleJoints(out Vector3[] joints, out bool[] valid))
                {
                    detail = "TrySampleJoints failed";
                    return false;
                }

                int found = 0;
                for (int i = 0; i < valid.Length; i++)
                {
                    if (valid[i])
                        found++;
                }

                detail = found + "/" + AnimPreviewBoneSet.JointCount + " joints";
                rig.Detach();
                return found >= 6;
            }
            catch (System.Exception ex)
            {
                detail = ex.Message;
                return false;
            }
        }

        private static AnimPreviewRig AcquireRig(int sex, int figureIndex, AnimPreviewRig[] assigned, int assignedCount)
        {
            for (int i = 0; i < OwnedRigs.Count; i++)
            {
                AnimPreviewRig rig = OwnedRigs[i];
                if (IsAlreadyAssigned(rig, assigned, assignedCount))
                    continue;
                if (sex < 0 || rig.PreferredSex == sex)
                {
                    rig.StageAnchor = BuildStageAnchor(figureIndex);
                    rig.EnsureSkeleton(sex);
                    return rig;
                }
            }

            for (int i = 0; i < OwnedRigs.Count; i++)
            {
                AnimPreviewRig rig = OwnedRigs[i];
                if (!IsAlreadyAssigned(rig, assigned, assignedCount))
                {
                    rig.StageAnchor = BuildStageAnchor(figureIndex);
                    rig.EnsureSkeleton(rig.PreferredSex);
                    return rig;
                }
            }

            var created = new AnimPreviewRig
            {
                PreferredSex = sex >= 0 ? sex : 1,
                StageAnchor = BuildStageAnchor(figureIndex),
            };
            created.EnsureSkeleton(created.PreferredSex);
            OwnedRigs.Add(created);
            return created;
        }

        private static Vector3 BuildStageAnchor(int figureIndex)
        {
            // All figures share one root position. Grouped (co-authored) clips position each
            // character relative to a common root via their own root offset, so a shared anchor
            // makes them line up the way the scene intends (no per-figure spacing).
            return OffScreenPosition + new Vector3(0f, 1f, 0f);
        }

        private static bool IsAlreadyAssigned(AnimPreviewRig rig, AnimPreviewRig[] assigned, int assignedCount)
        {
            for (int i = 0; i < assignedCount; i++)
            {
                if (ReferenceEquals(assigned[i], rig))
                    return true;
            }

            return false;
        }
    }
}
