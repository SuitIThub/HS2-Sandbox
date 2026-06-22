using System;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Joint selection and stick-figure bone pairs for preview rendering.</summary>
    internal static class AnimPreviewBoneSet
    {
        public const int JointCount = 19;

        // Torso from spine01; legs branch from spine01 (pelvis). Root (0) is not drawn.
        private static readonly int[] PairA = { 1, 2, 3, 2, 5, 6, 7, 2, 9, 10, 11, 1, 13, 14, 1, 16, 17 };
        private static readonly int[] PairB = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };

        public static int PairCount => PairA.Length;

        public static string GetJointName(int index) =>
            index >= 0 && index < JointNames.Length ? JointNames[index] : string.Empty;

        private static readonly string[] JointNames =
#if KK || KKS
        // Koikatsu / KKS rig: lowercase cf_j_* body bones (no _s deform variants). Order must match
        // AnimPreviewSkeletonData.StickJointBone (root, spine01, spine03/upper, neck, head, L arm, R arm, L leg, R leg).
        {
            "cf_j_root",
            "cf_j_spine01",
            "cf_j_spine03",
            "cf_j_neck",
            "cf_j_head",
            "cf_j_shoulder_L",
            "cf_j_arm00_L",
            "cf_j_forearm01_L",
            "cf_j_hand_L",
            "cf_j_shoulder_R",
            "cf_j_arm00_R",
            "cf_j_forearm01_R",
            "cf_j_hand_R",
            "cf_j_thigh00_L",
            "cf_j_leg01_L",
            "cf_j_leg03_L",
            "cf_j_thigh00_R",
            "cf_j_leg01_R",
            "cf_j_leg03_R",
        };
#else
        {
            "cf_J_Root_s",
            "cf_J_Spine01_s",
            "cf_J_Spine02_s",
            "cf_J_Neck_s",
            "cf_J_Head_s",
            "cf_J_Shoulder02_s_L",
            "cf_J_ArmUp01_s_L",
            "cf_J_ArmLow01_s_L",
            "cf_J_Hand_s_L",
            "cf_J_Shoulder02_s_R",
            "cf_J_ArmUp01_s_R",
            "cf_J_ArmLow01_s_R",
            "cf_J_Hand_s_R",
            "cf_J_LegUp01_s_L",
            "cf_J_LegLow01_s_L",
            "cf_J_LegLow03_s_L",
            "cf_J_LegUp01_s_R",
            "cf_J_LegLow01_s_R",
            "cf_J_LegLow03_s_R",
        };
#endif

        public static void GetPair(int pairIndex, out int jointA, out int jointB)
        {
            jointA = PairA[pairIndex];
            jointB = PairB[pairIndex];
        }

        /// <summary>Root-relative joint offsets (not world space).</summary>
        public static bool TryReadJoints(OCIChar oci, Vector3[] buffer, bool[] valid)
        {
            if (oci == null || buffer == null || buffer.Length < JointCount || valid == null || valid.Length < JointCount)
                return false;

            int found = 0;
            for (int i = 0; i < JointCount; i++)
            {
                if (TryGetJointOffset(oci, JointNames[i], out Vector3 offset))
                {
                    buffer[i] = offset;
                    valid[i] = true;
                    found++;
                }
                else
                {
                    buffer[i] = Vector3.zero;
                    valid[i] = false;
                }
            }

            return found >= 6;
        }

        private static bool TryGetJointOffset(OCIChar oci, string boneName, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (!TryFindBoneTransform(oci, boneName, out Transform? bone) || bone == null)
                return false;

            Transform root = oci.charInfo != null ? oci.charInfo.transform : bone;
            offset = bone.position - root.position;
            return true;
        }

        private static bool TryFindBoneTransform(OCIChar oci, string boneName, out Transform? bone)
        {
            bone = null;
            if (oci.charInfo == null || string.IsNullOrEmpty(boneName))
                return false;

            if (TryFindBoneViaListBones(oci, boneName, out bone))
                return true;

            if (TryFindChildTransformByName(oci.charInfo.transform, boneName, out bone))
                return true;

            if (boneName.EndsWith("_s", StringComparison.Ordinal))
            {
                string withoutS = boneName.Substring(0, boneName.Length - 2);
                if (TryFindBoneViaListBones(oci, withoutS, out bone))
                    return true;
                if (TryFindChildTransformByName(oci.charInfo.transform, withoutS, out bone))
                    return true;
            }

            if (string.Equals(boneName, "cf_J_Root_s", StringComparison.Ordinal))
            {
                if (TryFindBoneViaListBones(oci, "cf_J_Root", out bone))
                    return true;
                if (TryFindChildTransformByName(oci.charInfo.transform, "cf_J_Root", out bone))
                    return true;
            }

            string male = boneName.Replace("cf_J_", "cm_J_");
            if (!string.Equals(male, boneName, StringComparison.Ordinal))
            {
                if (TryFindBoneViaListBones(oci, male, out bone))
                    return true;
                if (TryFindChildTransformByName(oci.charInfo.transform, male, out bone))
                    return true;
            }

            return false;
        }

        private static bool TryFindBoneViaListBones(OCIChar oci, string boneName, out Transform? bone)
        {
            bone = null;
            if (oci.listBones == null)
                return false;

            try
            {
                foreach (OCIChar.BoneInfo info in oci.listBones)
                {
                    Transform? t = info.guideObject?.transformTarget;
                    if (t == null)
                        continue;
                    if (!string.Equals(t.name, boneName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    bone = t;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private static bool TryFindChildTransformByName(Transform root, string exactName, out Transform? found)
        {
            found = null;
            if (root == null)
                return false;

            if (string.Equals(root.name, exactName, StringComparison.OrdinalIgnoreCase))
            {
                found = root;
                return true;
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(t.name, exactName, StringComparison.OrdinalIgnoreCase))
                    continue;
                found = t;
                return true;
            }

            return false;
        }
    }
}
