using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Baked path-faithful body skeleton (chains feeding the 19 stick joints) captured from a
    /// real character via the diagnostic dump, per game. Names/parents are identical for both
    /// sexes; local TRS differs. Built as GameObjects so AnimationClip.SampleAnimation binds
    /// generic clips by transform path (root = the animator object, i.e. the skeleton root bone
    /// attaches at depth 0). HS2 uses the cf_J_* rig under p_cf_anim; KK/KKS use the lowercase
    /// cf_j_* rig under p_cf_body_bone. See [[animbrowser-clip-sampling-runtime]].
    /// </summary>
    internal static class AnimPreviewSkeletonData
    {
#if KK || KKS
        // Koikatsu / Koikatsu Sunshine rig (lowercase cf_j_* body bones, animator root p_cf_body_bone).
        // Baked from a KKS character diagnostic dump (clean T-pose; both sexes share bind positions,
        // only cf_n_height uniform scale differs: F 0.9066, M 0.9360). 26 bones feed the 19 stick joints.
        public const int BoneCount = 26;

        /// <summary>Bone names, parents-before-children. cf_j_root (index 0) attaches to the sampling root.</summary>
        public static readonly string[] BoneNames =
        {
            "cf_j_root", "cf_n_height", "cf_j_hips", "cf_j_spine01", "cf_j_waist01", "cf_j_spine02", "cf_j_waist02", "cf_j_spine03", "cf_j_thigh00_L", "cf_j_thigh00_R", "cf_d_shoulder_L", "cf_d_shoulder_R", "cf_j_neck", "cf_j_leg01_L", "cf_j_leg01_R", "cf_j_shoulder_L", "cf_j_shoulder_R", "cf_j_head", "cf_j_leg03_L", "cf_j_leg03_R", "cf_j_arm00_L", "cf_j_arm00_R", "cf_j_forearm01_L", "cf_j_forearm01_R", "cf_j_hand_L", "cf_j_hand_R"
        };

        /// <summary>Parent bone index per bone (-1 = sampling root / animator object).</summary>
        public static readonly int[] BoneParents =
        {
            -1, 0, 1, 2, 2, 3, 4, 5, 6, 6, 7, 7, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 20, 21, 22, 23
        };

        /// <summary>Stick joint index (0..18) -> bone index in this table.</summary>
        public static readonly int[] StickJointBone =
        {
            0, 3, 7, 12, 17, 15, 20, 22, 24, 16, 21, 23, 25, 8, 13, 18, 9, 14, 19
        };

        public static readonly Vector3[] FemaleLocalPosition =
        {
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 1.1435f, 0.0000f),
            new Vector3(0.0000f, 0.0050f, 0.0000f),
            new Vector3(0.0000f, -0.0050f, 0.0000f),
            new Vector3(0.0000f, 0.0900f, -0.0066f),
            new Vector3(0.0000f, -0.1353f, -0.0166f),
            new Vector3(0.0000f, 0.0900f, -0.0102f),
            new Vector3(-0.0830f, -0.0250f, 0.0000f),
            new Vector3(0.0830f, -0.0250f, 0.0000f),
            new Vector3(-0.0156f, 0.0847f, -0.0070f),
            new Vector3(0.0156f, 0.0847f, -0.0070f),
            new Vector3(0.0000f, 0.1219f, 0.0000f),
            new Vector3(0.0000f, -0.4600f, 0.0000f),
            new Vector3(0.0000f, -0.4600f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0650f, 0.0000f),
            new Vector3(0.0000f, -0.4350f, 0.0000f),
            new Vector3(0.0000f, -0.4350f, 0.0000f),
            new Vector3(-0.0940f, 0.0000f, 0.0000f),
            new Vector3(0.0940f, 0.0000f, 0.0000f),
            new Vector3(-0.2530f, 0.0000f, 0.0000f),
            new Vector3(0.2531f, 0.0000f, 0.0000f),
            new Vector3(-0.2500f, 0.0000f, 0.0000f),
            new Vector3(0.2500f, 0.0000f, 0.0000f)
        };

        public static readonly Quaternion[] FemaleLocalRotation =
        {
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00873f, 0.00001f, 0.00000f, 0.99996f),
            new Quaternion(0.00873f, 0.00000f, 0.00000f, 0.99996f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00873f, 0.00000f, 0.00000f, 0.99996f),
            new Quaternion(0.00873f, 0.00000f, 0.00000f, 0.99996f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.00001f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.02199f, 0.00000f, 0.99976f),
            new Quaternion(0.00000f, 0.02117f, 0.00000f, 0.99978f),
            new Quaternion(0.00000f, 0.02198f, 0.00000f, 0.99976f),
            new Quaternion(0.00005f, -0.02119f, 0.00000f, 0.99978f),
            new Quaternion(0.00000f, 0.00001f, 0.00000f, 1.00000f),
            new Quaternion(-0.00005f, 0.00002f, 0.00001f, 1.00000f)
        };

        public static readonly Vector3[] FemaleLocalScale =
        {
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(0.9066f, 0.9066f, 0.9066f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f)
        };

        public static readonly Vector3[] MaleLocalPosition =
        {
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 1.1435f, 0.0000f),
            new Vector3(0.0000f, 0.0050f, 0.0000f),
            new Vector3(0.0000f, -0.0050f, 0.0000f),
            new Vector3(0.0000f, 0.0900f, -0.0066f),
            new Vector3(0.0000f, -0.1353f, -0.0166f),
            new Vector3(0.0000f, 0.0900f, -0.0102f),
            new Vector3(-0.0830f, -0.0250f, 0.0000f),
            new Vector3(0.0830f, -0.0250f, 0.0000f),
            new Vector3(-0.0156f, 0.0847f, -0.0070f),
            new Vector3(0.0156f, 0.0847f, -0.0070f),
            new Vector3(0.0000f, 0.1219f, 0.0000f),
            new Vector3(0.0000f, -0.4600f, 0.0000f),
            new Vector3(0.0000f, -0.4600f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0650f, 0.0000f),
            new Vector3(0.0000f, -0.4350f, 0.0000f),
            new Vector3(0.0000f, -0.4350f, 0.0000f),
            new Vector3(-0.0940f, 0.0000f, 0.0000f),
            new Vector3(0.0940f, 0.0000f, 0.0000f),
            new Vector3(-0.2530f, 0.0000f, 0.0000f),
            new Vector3(0.2531f, 0.0000f, 0.0000f),
            new Vector3(-0.2500f, 0.0000f, 0.0000f),
            new Vector3(0.2500f, 0.0000f, 0.0000f)
        };

        public static readonly Quaternion[] MaleLocalRotation =
        {
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00873f, 0.00001f, 0.00000f, 0.99996f),
            new Quaternion(0.00873f, 0.00000f, 0.00000f, 0.99996f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00873f, 0.00000f, 0.00000f, 0.99996f),
            new Quaternion(0.00873f, 0.00000f, 0.00000f, 0.99996f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.00001f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.02199f, 0.00000f, 0.99976f),
            new Quaternion(0.00000f, 0.02117f, 0.00000f, 0.99978f),
            new Quaternion(0.00000f, 0.02198f, 0.00000f, 0.99976f),
            new Quaternion(0.00005f, -0.02119f, 0.00000f, 0.99978f),
            new Quaternion(0.00000f, 0.00001f, 0.00000f, 1.00000f),
            new Quaternion(-0.00005f, 0.00002f, 0.00001f, 1.00000f)
        };

        public static readonly Vector3[] MaleLocalScale =
        {
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(0.9360f, 0.9360f, 0.9360f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f)
        };
#else
        public const int BoneCount = 48;

        /// <summary>Bone names, parents-before-children. cf_J_Root (index 0) attaches to the sampling root.</summary>
        public static readonly string[] BoneNames =
        {
            "cf_J_Root", "cf_N_height", "cf_J_Hips", "cf_J_Kosi01", "cf_J_Spine01", "cf_J_Kosi02", "cf_J_Spine01_s", "cf_J_Spine02", "cf_J_LegUp00_L", "cf_J_LegUp00_R", "cf_J_Spine02_s", "cf_J_Spine03", "cf_J_LegLow01_L", "cf_J_LegUp01_L", "cf_J_LegLow01_R", "cf_J_LegUp01_R", "cf_J_Neck", "cf_J_ShoulderIK_L", "cf_J_ShoulderIK_R", "cf_J_LegLow01_s_L", "cf_J_LegLow03_L", "cf_J_LegUp01_s_L", "cf_J_LegLow01_s_R", "cf_J_LegLow03_R", "cf_J_LegUp01_s_R", "cf_J_Head", "cf_J_Neck_s", "cf_J_Shoulder_L", "cf_J_Shoulder_R", "cf_J_LegLow03_s_L", "cf_J_LegLow03_s_R", "cf_J_Head_s", "cf_J_ArmUp00_L", "cf_J_Shoulder02_s_L", "cf_J_ArmUp00_R", "cf_J_Shoulder02_s_R", "cf_J_ArmLow01_L", "cf_J_ArmUp01_dam_L", "cf_J_ArmLow01_R", "cf_J_ArmUp01_dam_R", "cf_J_ArmLow01_s_L", "cf_J_Hand_L", "cf_J_ArmUp01_s_L", "cf_J_ArmLow01_s_R", "cf_J_Hand_R", "cf_J_ArmUp01_s_R", "cf_J_Hand_s_L", "cf_J_Hand_s_R"
        };

        /// <summary>Parent bone index per bone (-1 = sampling root / animator object).</summary>
        public static readonly int[] BoneParents =
        {
            -1, 0, 1, 2, 2, 3, 4, 4, 5, 5, 7, 7, 8, 8, 9, 9, 11, 11, 11, 12, 12, 13, 14, 14, 15, 16, 16, 17, 18, 20, 23, 25, 27, 27, 28, 28, 32, 32, 34, 34, 36, 36, 37, 38, 38, 39, 41, 44
        };

        /// <summary>Stick joint index (0..18) -> bone index in this table.</summary>
        public static readonly int[] StickJointBone =
        {
            0, 6, 10, 26, 31, 33, 42, 40, 46, 35, 45, 43, 47, 21, 19, 29, 24, 22, 30
        };

        public static readonly Vector3[] FemaleLocalPosition =
        {
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 11.4350f, 0.0000f),
            new Vector3(0.0000f, -0.1000f, 0.0000f),
            new Vector3(0.0000f, 0.1000f, 0.0000f),
            new Vector3(0.0000f, -1.0000f, -0.2000f),
            new Vector3(0.0000f, -0.1252f, 0.0510f),
            new Vector3(0.0000f, 1.1000f, 0.0000f),
            new Vector3(-0.9527f, -0.5413f, -0.1083f),
            new Vector3(0.9527f, -0.5413f, -0.1083f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 1.1000f, -0.1000f),
            new Vector3(0.0000f, -4.2000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, -4.2000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 1.3800f, -0.2300f),
            new Vector3(-0.3000f, 1.0500f, -0.2200f),
            new Vector3(0.3000f, 1.0500f, -0.2200f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, -4.0000f, 0.0000f),
            new Vector3(-0.0004f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, -4.0000f, 0.0000f),
            new Vector3(0.0004f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.8200f, 0.1000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(-0.8265f, 0.0000f, 0.0000f),
            new Vector3(-0.7081f, 0.0000f, 0.0000f),
            new Vector3(0.8265f, 0.0000f, 0.0000f),
            new Vector3(0.7081f, 0.0000f, 0.0000f),
            new Vector3(-2.6500f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(2.6500f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(-2.2800f, 0.0000f, 0.0000f),
            new Vector3(0.0300f, -0.0365f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(2.2802f, 0.0000f, 0.0000f),
            new Vector3(-0.0300f, -0.0365f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f)
        };

        public static readonly Quaternion[] FemaleLocalRotation =
        {
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(-0.00977f, 0.00000f, 0.00000f, 0.99995f),
            new Quaternion(-0.00977f, 0.00000f, 0.00000f, 0.99995f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.03054f, 0.00000f, 0.00000f, 0.99953f),
            new Quaternion(0.00000f, 0.00021f, 0.00000f, 1.00000f),
            new Quaternion(0.03054f, 0.00000f, 0.00000f, 0.99953f),
            new Quaternion(-0.00175f, 0.00140f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(-0.00003f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00009f, 1.00000f),
            new Quaternion(-0.00003f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, -0.00009f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.01309f, 0.00000f, 0.99991f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.01309f, 0.00000f, 0.99991f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.02443f, 0.00000f, 0.99970f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.02443f, 0.00000f, 0.99970f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.01134f, 0.00000f, 0.99994f),
            new Quaternion(-0.00009f, 0.00521f, 0.01798f, -0.99982f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.01134f, 0.00000f, 0.99994f),
            new Quaternion(0.00000f, 0.00000f, 0.01798f, 0.99984f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f)
        };

        public static readonly Vector3[] FemaleLocalScale =
        {
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(0.7699f, 0.7699f, 0.7699f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(0.8055f, 1.0000f, 0.8828f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(0.8817f, 1.0000f, 1.0110f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.1178f, 1.0000f, 1.1178f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.1893f, 1.0000f, 1.1981f),
            new Vector3(1.1178f, 1.0000f, 1.1178f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.1893f, 1.0000f, 1.1981f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.1084f, 1.0000f, 1.0103f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(0.9534f, 0.9534f, 0.9534f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.1150f, 1.0952f, 1.0806f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.1150f, 1.0952f, 1.0806f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.1645f, 1.1645f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0753f, 1.0646f),
            new Vector3(1.0000f, 1.1617f, 1.1617f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0753f, 1.0646f),
            new Vector3(1.1841f, 1.1841f, 1.1841f),
            new Vector3(1.1841f, 1.1841f, 1.1841f)
        };

        public static readonly Vector3[] MaleLocalPosition =
        {
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 11.4350f, 0.0000f),
            new Vector3(0.0000f, -0.1000f, 0.0000f),
            new Vector3(0.0000f, 0.1000f, 0.0000f),
            new Vector3(0.0000f, -1.0000f, -0.2000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 1.1000f, 0.0000f),
            new Vector3(-0.8800f, -0.5000f, -0.1000f),
            new Vector3(0.8800f, -0.5000f, -0.1000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 1.1000f, -0.1000f),
            new Vector3(0.0000f, -4.2000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, -4.2000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 1.3800f, -0.2300f),
            new Vector3(-0.3000f, 1.0500f, -0.2200f),
            new Vector3(0.3000f, 1.0500f, -0.2200f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, -4.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, -0.0154f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, -4.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, -0.0154f),
            new Vector3(0.0000f, 0.8200f, 0.1000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0005f, 0.0000f, 0.0006f),
            new Vector3(-0.0005f, 0.0000f, 0.0006f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(-1.0000f, 0.0000f, 0.0000f),
            new Vector3(-0.7635f, 0.0000f, 0.0000f),
            new Vector3(1.0000f, 0.0000f, 0.0000f),
            new Vector3(0.7635f, 0.0000f, 0.0000f),
            new Vector3(-2.6500f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(2.6500f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(-2.2800f, 0.0000f, 0.0000f),
            new Vector3(-0.0060f, 0.0085f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(2.2802f, 0.0000f, 0.0000f),
            new Vector3(0.0060f, 0.0085f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f),
            new Vector3(0.0000f, 0.0000f, 0.0000f)
        };

        public static readonly Quaternion[] MaleLocalRotation =
        {
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(-0.00977f, 0.00000f, 0.00000f, 0.99995f),
            new Quaternion(-0.00977f, 0.00000f, 0.00000f, 0.99995f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.03054f, 0.00000f, 0.00000f, 0.99953f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.03054f, 0.00000f, 0.00000f, 0.99953f),
            new Quaternion(-0.00175f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(-0.00020f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(-0.00020f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00042f, 0.00000f, -0.00042f, 1.00000f),
            new Quaternion(0.00042f, 0.00000f, 0.00042f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.01309f, 0.00000f, 0.99991f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.01309f, 0.00000f, 0.99991f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.02443f, 0.00000f, 0.99970f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.02443f, 0.00000f, 0.99970f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, -0.01134f, 0.00000f, 0.99994f),
            new Quaternion(0.00004f, 0.00521f, -0.00747f, -0.99996f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.01134f, 0.00000f, 0.99994f),
            new Quaternion(0.00000f, 0.00000f, -0.00747f, 0.99997f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f),
            new Quaternion(0.00000f, 0.00000f, 0.00000f, 1.00000f)
        };

        public static readonly Vector3[] MaleLocalScale =
        {
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(0.9500f, 0.9500f, 0.9500f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0250f, 1.0000f, 1.0250f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0250f, 1.0000f, 1.0250f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0434f, 1.0000f, 1.0434f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0647f, 1.0000f, 1.0647f),
            new Vector3(1.0434f, 1.0000f, 1.0434f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0647f, 1.0000f, 1.0647f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.1679f, 1.0000f, 1.0697f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0067f, 1.0000f, 1.0067f),
            new Vector3(1.0067f, 1.0000f, 1.0067f),
            new Vector3(1.0400f, 1.0400f, 1.0400f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0250f, 1.0687f, 1.0941f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0250f, 1.0687f, 1.0941f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.0843f, 1.0843f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.1123f, 1.1488f),
            new Vector3(1.0000f, 1.0812f, 1.0812f),
            new Vector3(1.0000f, 1.0000f, 1.0000f),
            new Vector3(1.0000f, 1.1123f, 1.1488f),
            new Vector3(1.0400f, 1.0400f, 1.0400f),
            new Vector3(1.0400f, 1.0400f, 1.0400f)
        };

#endif

        // sex: 0 = male, anything else = female (matches OCIChar.sex / preview convention).
        public static Vector3[] LocalPositions(int sex) => sex == 0 ? MaleLocalPosition : FemaleLocalPosition;
        public static Quaternion[] LocalRotations(int sex) => sex == 0 ? MaleLocalRotation : FemaleLocalRotation;
        public static Vector3[] LocalScales(int sex) => sex == 0 ? MaleLocalScale : FemaleLocalScale;
    }
}
