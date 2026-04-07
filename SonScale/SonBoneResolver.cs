using System;
using System.Collections.Generic;
using System.Reflection;
using AIChara;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Locates the primary Son (member) bone under the character body when present (male or female body).
    /// HS2 uses Illusion-style names such as <c>cm_J_dan100_00</c>.
    /// </summary>
    internal static class SonBoneResolver
    {
        private static readonly string[] ExactPreferred =
        {
            "cm_J_dan100_00",
            "cm_J_dan_f_top",
            "cm_J_dan_f_L00",
            "cm_J_dan_f_R00",
        };

        /// <summary>
        /// Estimates a point in <paramref name="dan"/> local space on the body-side end of the member mesh.
        /// Uses renderer bounds corners in world space and picks the corner closest to the parent bone (toward the body);
        /// min local Z is unreliable when the shaft axis does not align with dan +Z.
        /// </summary>
        internal static Vector3 EstimateDanBaseLocalPoint(Transform dan)
        {
            const float fallbackZ = -0.035f;
            if (dan == null)
                return new Vector3(0f, 0f, fallbackZ);

            Vector3 refWorld;
            if (dan.parent != null)
            {
                refWorld = dan.parent.position;
                // Zero-length intermediate bones put parent and child at the same place; step up the chain.
                if ((refWorld - dan.position).sqrMagnitude < 1e-8f && dan.parent.parent != null)
                    refWorld = dan.parent.parent.position;
            }
            else
            {
                refWorld = dan.position;
            }

            Vector3 towardBody = refWorld - dan.position;
            bool canHemisphere = towardBody.sqrMagnitude > 1e-8f;
            if (canHemisphere)
                towardBody.Normalize();

            // Prefer corners on the body side of the joint (avoids picking a side face of the AABB).
            if (!TryPickBaseCornerWorld(dan, refWorld, towardBody, canHemisphere, requireBodyHemisphere: true, out Vector3 bestWorld)
                && !TryPickBaseCornerWorld(dan, refWorld, towardBody, canHemisphere, requireBodyHemisphere: false, out bestWorld))
            {
                return new Vector3(0f, 0f, fallbackZ);
            }

            return dan.InverseTransformPoint(bestWorld);
        }

        private static bool TryPickBaseCornerWorld(
            Transform dan,
            Vector3 refWorld,
            Vector3 towardBodyNormalized,
            bool canHemisphere,
            bool requireBodyHemisphere,
            out Vector3 bestWorld)
        {
            bool found = false;
            float bestSq = float.MaxValue;
            Vector3 pick = default;

            void Consider(Vector3 world)
            {
                if (requireBodyHemisphere && canHemisphere)
                {
                    Vector3 fromDan = world - dan.position;
                    if (fromDan.sqrMagnitude < 1e-12f)
                        return;

                    if (Vector3.Dot(fromDan.normalized, towardBodyNormalized) < 0.08f)
                        return;
                }

                float sq = (world - refWorld).sqrMagnitude;
                if (!found || sq < bestSq)
                {
                    found = true;
                    bestSq = sq;
                    pick = world;
                }
            }

            foreach (SkinnedMeshRenderer smr in dan.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Bounds wb = smr.bounds;
                Vector3 c = wb.center;
                Vector3 e = wb.extents;
                for (int ix = -1; ix <= 1; ix += 2)
                {
                    for (int iy = -1; iy <= 1; iy += 2)
                    {
                        for (int iz = -1; iz <= 1; iz += 2)
                            Consider(c + new Vector3(ix * e.x, iy * e.y, iz * e.z));
                    }
                }
            }

            foreach (MeshRenderer mr in dan.GetComponentsInChildren<MeshRenderer>(true))
            {
                Bounds wb = mr.bounds;
                Vector3 c = wb.center;
                Vector3 e = wb.extents;
                for (int ix = -1; ix <= 1; ix += 2)
                {
                    for (int iy = -1; iy <= 1; iy += 2)
                    {
                        for (int iz = -1; iz <= 1; iz += 2)
                            Consider(c + new Vector3(ix * e.x, iy * e.y, iz * e.z));
                    }
                }
            }

            bestWorld = pick;
            return found;
        }

        /// <summary>
        /// Collects dan shaft segment transforms under <paramref name="danRoot"/> (excludes the root).
        /// Length is applied to their <see cref="Transform.localPosition"/> so joint spacing grows with the slider;
        /// scaling only the root stretches skinning while animation keeps segment offsets, which causes overlap.
        /// </summary>
        internal static void CollectSonShaftDescendants(Transform danRoot, List<Transform> dest)
        {
            dest.Clear();
            if (danRoot == null)
                return;

            foreach (Transform t in danRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == danRoot)
                    continue;

                if (IsLikelyShaftSegmentBone(t.name))
                    dest.Add(t);
            }
        }

        /// <summary>Returns true if <paramref name="boneName"/> looks like an Illusion son shaft bone (male numbered chain or female dan_f).</summary>
        internal static bool IsLikelyShaftSegmentBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return false;

            string u = boneName.ToLowerInvariant();
            if (!u.Contains("cm_j_dan"))
                return false;

            if (u.Contains("collider"))
                return false;

            if (u.Contains("dan_f"))
                return true;

            // Male / default: "dan" + two or more digits (dan09, dan100, dan101, dan115, …).
            for (int i = 0; i < u.Length - 4; i++)
            {
                if (u[i] != 'd' || u[i + 1] != 'a' || u[i + 2] != 'n')
                    continue;

                int j = i + 3;
                int digits = 0;
                while (j < u.Length && char.IsDigit(u[j]))
                {
                    digits++;
                    j++;
                }

                if (digits >= 2)
                    return true;
            }

            return false;
        }

        internal static Transform? FindDanTransform(ChaControl cha)
        {
            if (cha == null)
                return null;

            Transform[] roots = GetBodyRoots(cha);

            // Prefer exact names across all roots before heuristic scoring (avoids a weak "dan" hit on objBody
            // hiding a better bone under objBodyBone).
            foreach (Transform root in roots)
            {
                foreach (string name in ExactPreferred)
                {
                    Transform? t = FindChildByName(root, name);
                    if (t != null)
                        return t;
                }
            }

            Transform? best = null;
            int bestScore = -1;
            foreach (Transform root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    int s = ScoreDanName(t.name);
                    if (s > bestScore)
                    {
                        bestScore = s;
                        best = t;
                    }
                }
            }

            return bestScore >= 0 ? best : null;
        }

        private static Transform[] GetBodyRoots(ChaControl cha)
        {
            var list = new List<Transform>();
            if (cha.objBody != null)
                list.Add(cha.objBody.transform);

            GameObject? bodyBone = TryGetObjBodyBone(cha);
            if (bodyBone != null)
                list.Add(bodyBone.transform);

            list.Add(cha.transform);
            return list.ToArray();
        }

        /// <summary>HS2 <see cref="ChaControl"/> may expose <c>objBodyBone</c>; use reflection so builds stay compatible.</summary>
        private static GameObject? TryGetObjBodyBone(ChaControl cha)
        {
            try
            {
                Type t = typeof(ChaControl);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo? field = t.GetField("objBodyBone", flags);
                if (field != null && field.FieldType == typeof(GameObject))
                    return field.GetValue(cha) as GameObject;

                PropertyInfo? prop = t.GetProperty("objBodyBone", flags);
                if (prop != null && prop.PropertyType == typeof(GameObject))
                    return prop.GetValue(cha, null) as GameObject;
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private static Transform? FindChildByName(Transform root, string exactName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, exactName, StringComparison.Ordinal))
                    return t;
            }

            return null;
        }

        private static int ScoreDanName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;

            string u = name.ToLowerInvariant();
            if (!u.Contains("dan"))
                return -1;

            if (u.Contains("dan100"))
                return 100;
            if (u.Contains("dan_f"))
                return 80;
            if (u.Contains("cm_j_dan"))
                return 60;
            return 40;
        }
    }
}
