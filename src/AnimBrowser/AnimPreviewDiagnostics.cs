using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Writes preview/skeleton diagnostics for offline stick-figure tuning.</summary>
    internal static class AnimPreviewDiagnostics
    {
        public static string DumpFilePath =>
            PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "anim_preview_diagnostic.txt");

        public static string WriteDumpFile(bool includeEmbeddedTest)
        {
            var sb = new StringBuilder(8192);
            WriteDump(sb, includeEmbeddedTest);

            string path = DumpFilePath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            SandboxServices.Log.LogInfo("Anim preview diagnostic written to: " + path);
            return path;
        }

        private static void WriteDump(StringBuilder sb, bool includeEmbeddedTest)
        {
            sb.AppendLine("=== Anim Browser — preview / skeleton diagnostic ===");
            sb.AppendLine("Written (UTC): " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Game root: " + AnimPreviewRigPool.DiagnosticGameRoot);
            sb.AppendLine("Diagnostic file: " + DumpFilePath);
            sb.AppendLine();

            bool studioLoaded;
            try
            {
                studioLoaded = Singleton<Studio.Studio>.Instance != null;
            }
            catch
            {
                studioLoaded = false;
            }

            sb.AppendLine("Studio loaded: " + (studioLoaded ? "yes" : "no"));
            sb.AppendLine("Preview mode: embedded skeleton (no scene characters required)");
            sb.AppendLine("Preview owned rigs (pool): " + AnimPreviewRigPool.DiagnosticOwnedRigCount);
            sb.AppendLine();

            if (includeEmbeddedTest)
            {
                sb.AppendLine("=== Embedded rig test (plugin-owned skeleton) ===");
                WriteEmbeddedRigTestSection(sb);
                sb.AppendLine();
            }

            var sceneChars = CollectSceneCharacters();
            sb.AppendLine("=== Scene OCIChar instances (" + sceneChars.Count + ") — optional reference only ===");
            if (sceneChars.Count == 0)
            {
                sb.AppendLine("(none — preview still works from embedded T-pose data)");
            }
            else
            {
                for (int i = 0; i < sceneChars.Count; i++)
                    WriteCharacterSection(sb, sceneChars[i], i);
            }

            sb.AppendLine("=== Stick-figure joint map (configured names) ===");
            WriteConfiguredJointsSection(sb, sceneChars.Count > 0 ? sceneChars[0] : null);
            sb.AppendLine();

            OCIChar? skeletonSource = sceneChars.Count > 0 ? sceneChars[0] : null;
            if (skeletonSource != null)
            {
                sb.AppendLine("=== Full skeleton name list (first scene character) ===");
                WriteSkeletonNameList(sb, skeletonSource);
                sb.AppendLine();
            }

            // Per-character clip-type probe + full animator-subtree transform table
            // (path relative to animator root, parent, localPos, localEuler, localScale).
            // Dumps every scene character so a male + female run captures both rigs.
            for (int i = 0; i < sceneChars.Count; i++)
            {
                OCIChar oci = sceneChars[i];
                string sexLabel = oci.sex == 0 ? "male" : "female";

                sb.AppendLine("=== Clip type probe — scene #" + i + " (" + sexLabel + ") ===");
                WriteClipProbe(sb, oci);
                sb.AppendLine();

                sb.AppendLine("=== Skeleton transform table — scene #" + i + " (" + sexLabel + ") (path | parent | localPos | localEuler | localScale) ===");
                WriteSkeletonTransformTable(sb, oci);
                sb.AppendLine();
            }

            sb.AppendLine("=== Notes ===");
            sb.AppendLine("- Preview uses embedded female/male T-pose data; scene characters are never copied or modified.");
            sb.AppendLine("- Share this file to tune stick-figure bone names and rest offsets.");
            sb.AppendLine("- Look for stick-figure joints with found=no — those names need updating.");
        }

        private static void WriteEmbeddedRigTestSection(StringBuilder sb)
        {
            for (int sex = 0; sex <= 1; sex++)
            {
                string label = sex == 0 ? "male" : "female";
                bool ok = AnimPreviewRigPool.DiagnosticTryBuildEmbedded(sex, out string detail);
                sb.AppendLine(label + " embedded rig: " + (ok ? "ok" : "failed") + " (" + detail + ")");
            }
        }

        private static void WriteCharacterSection(StringBuilder sb, OCIChar oci, int index)
        {
            sb.AppendLine("--- scene #" + index + " ---");
            sb.AppendLine("dicKey: " + SafeDicKey(oci));
            sb.AppendLine("sex: " + oci.sex + (oci.sex == 0 ? " (male)" : " (female)"));

            if (oci.charInfo != null)
            {
                sb.AppendLine("charInfo.worldPos: " + Fmt(oci.charInfo.transform.position));
                sb.AppendLine("charInfo.localScale: " + Fmt(oci.charInfo.transform.lossyScale));
            }

            if (oci.oiCharInfo?.changeAmount != null)
                sb.AppendLine("changeAmount.pos: " + Fmt(oci.oiCharInfo.changeAmount.pos));

            int listBoneCount = 0;
            try
            {
                listBoneCount = oci.listBones?.Count ?? 0;
            }
            catch
            {
                // ignored
            }

            sb.AppendLine("listBones count: " + listBoneCount);

            var buffer = new Vector3[AnimPreviewBoneSet.JointCount];
            var valid = new bool[AnimPreviewBoneSet.JointCount];
            bool jointsOk = AnimPreviewBoneSet.TryReadJoints(oci, buffer, valid);
            int found = CountTrue(valid);
            sb.AppendLine("stickFigure TryReadJoints: " + (jointsOk ? "ok" : "fail") + " (" + found + "/" + AnimPreviewBoneSet.JointCount + " joints)");
            for (int j = 0; j < AnimPreviewBoneSet.JointCount; j++)
            {
                string name = AnimPreviewBoneSet.GetJointName(j);
                if (valid[j])
                    sb.AppendLine("  [" + j + "] " + name + " found offset=" + Fmt(buffer[j]));
                else
                    sb.AppendLine("  [" + j + "] " + name + " found=no");
            }

            sb.AppendLine(" drawable stick pairs: " + CountDrawablePairs(valid));
            sb.AppendLine();
        }

        private static void WriteConfiguredJointsSection(StringBuilder sb, OCIChar? sample)
        {
            sb.AppendLine("pairIndex\tjointA\tjointB\tnameA\tnameB");
            for (int p = 0; p < AnimPreviewBoneSet.PairCount; p++)
            {
                AnimPreviewBoneSet.GetPair(p, out int a, out int b);
                sb.AppendLine(
                    p + "\t" + a + "\t" + b + "\t" +
                    AnimPreviewBoneSet.GetJointName(a) + "\t" +
                    AnimPreviewBoneSet.GetJointName(b));
            }

            if (sample == null)
                return;

            sb.AppendLine();
            sb.AppendLine("(resolution on sample character '" + SafeDicKey(sample) + "')");
            var buffer = new Vector3[AnimPreviewBoneSet.JointCount];
            var valid = new bool[AnimPreviewBoneSet.JointCount];
            AnimPreviewBoneSet.TryReadJoints(sample, buffer, valid);
            for (int p = 0; p < AnimPreviewBoneSet.PairCount; p++)
            {
                AnimPreviewBoneSet.GetPair(p, out int a, out int b);
                bool drawable = a < valid.Length && b < valid.Length && valid[a] && valid[b];
                sb.AppendLine(
                    "pair " + p + " drawable=" + (drawable ? "yes" : "no") +
                    " len=" + (drawable ? (buffer[a] - buffer[b]).magnitude.ToString("F4", CultureInfo.InvariantCulture) : "-"));
            }
        }

        private static void WriteSkeletonNameList(StringBuilder sb, OCIChar oci)
        {
            if (oci.charInfo == null)
            {
                sb.AppendLine("(no charInfo)");
                return;
            }

            var names = new List<string>();
            foreach (Transform t in oci.charInfo.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.IndexOf("_J_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.StartsWith("cf_J", StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("cm_J", StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(n);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            sb.AppendLine("matching transform count: " + names.Count);
            for (int i = 0; i < names.Count; i++)
                sb.AppendLine("  " + names[i]);
        }

        /// <summary>
        /// Reports clip flags for the clips bound to the character's body Animator.
        /// humanMotion=True means muscle-space (needs Animator+Avatar to retarget — a bare
        /// transform clone won't bind via SampleAnimation); False means generic transform curves.
        /// </summary>
        private static void WriteClipProbe(StringBuilder sb, OCIChar oci)
        {
            if (oci.charInfo == null)
            {
                sb.AppendLine("(no charInfo)");
                return;
            }

            Animator[] animators;
            try
            {
                animators = oci.charInfo.GetComponentsInChildren<Animator>(true);
            }
            catch (Exception ex)
            {
                sb.AppendLine("(animator lookup failed: " + ex.Message + ")");
                return;
            }

            if (animators == null || animators.Length == 0)
            {
                sb.AppendLine("(no Animator found under charInfo)");
                return;
            }

            int reported = 0;
            for (int a = 0; a < animators.Length; a++)
            {
                Animator animator = animators[a];
                RuntimeAnimatorController? rac = null;
                try
                {
                    rac = animator.runtimeAnimatorController;
                }
                catch
                {
                    // ignored
                }

                sb.AppendLine("animator[" + a + "] on '" + animator.name + "' isHuman=" + animator.isHuman +
                    " hasController=" + (rac != null));

                if (rac == null)
                    continue;

                AnimationClip[] clips;
                try
                {
                    clips = rac.animationClips;
                }
                catch (Exception ex)
                {
                    sb.AppendLine("  (animationClips failed: " + ex.Message + ")");
                    continue;
                }

                if (clips == null)
                    continue;

                for (int c = 0; c < clips.Length && reported < 8; c++)
                {
                    AnimationClip clip = clips[c];
                    if (clip == null)
                        continue;

                    sb.AppendLine("  clip '" + clip.name + "' " + FormatClipProbeFlags(clip));
                    reported++;
                }
            }

            if (reported == 0)
                sb.AppendLine("(no clips reachable via animationClips — they may be assigned per-state at runtime)");
        }

        private static string FormatClipProbeFlags(AnimationClip clip)
        {
            var sb = new StringBuilder(128);
            sb.Append("humanMotion=").Append(clip.humanMotion);
            sb.Append(" legacy=").Append(clip.legacy);
            // hasMotionCurves/hasRootCurves: Unity 2018.3+ (HS2/KKS). Absent on KK (5.6) and AI (2018.2).
#if !KK && !AI
            sb.Append(" hasMotionCurves=").Append(clip.hasMotionCurves);
            sb.Append(" hasRootCurves=").Append(clip.hasRootCurves);
#endif
            sb.Append(" len=").Append(clip.length.ToString("F2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>
        /// Dumps the ENTIRE transform subtree of the character with everything that could matter
        /// for rebuilding a path-faithful sampling skeleton: path (relative to charInfo root),
        /// parent, localPosition, localEulerAngles, localRotation quaternion, localScale,
        /// world position, active state and attached component types. The animator root is marked
        /// because clip binding paths are relative to it. Intentionally exhaustive — more data is
        /// better here; offline tooling prunes what it needs.
        /// </summary>
        private static void WriteSkeletonTransformTable(StringBuilder sb, OCIChar oci)
        {
            if (oci.charInfo == null)
            {
                sb.AppendLine("(no charInfo)");
                return;
            }

            Transform charRoot = oci.charInfo.transform;

            Transform? animRoot = null;
            try
            {
                Animator[] animators = oci.charInfo.GetComponentsInChildren<Animator>(true);
                if (animators != null && animators.Length > 0)
                    animRoot = animators[0].transform;
            }
            catch
            {
                // ignored
            }

            sb.AppendLine("char root: " + charRoot.name);
            sb.AppendLine("animator root path: " + (animRoot != null ? BuildRelativePath(charRoot, animRoot) : "(no Animator)") +
                "  (clip binding paths are relative to the animator root)");
            sb.AppendLine("columns: path | parent | localPos | localEuler | localQuat(x,y,z,w) | localScale | worldPos | active | components");

            var rows = new List<string>();
            foreach (Transform t in charRoot.GetComponentsInChildren<Transform>(true))
            {
                string path = BuildRelativePath(charRoot, t);
                string parentName = t.parent != null ? t.parent.name : "(none)";
                Quaternion q = t.localRotation;
                string quat = string.Format(CultureInfo.InvariantCulture, "({0:F5}, {1:F5}, {2:F5}, {3:F5})", q.x, q.y, q.z, q.w);
                rows.Add(
                    path + "\t" + parentName + "\t" +
                    Fmt(t.localPosition) + "\t" + Fmt(t.localEulerAngles) + "\t" + quat + "\t" +
                    Fmt(t.localScale) + "\t" + Fmt(t.position) + "\t" +
                    t.gameObject.activeSelf + "\t" + DescribeComponents(t));
            }

            rows.Sort(StringComparer.OrdinalIgnoreCase);
            sb.AppendLine("transform count: " + rows.Count);
            for (int i = 0; i < rows.Count; i++)
                sb.AppendLine("  " + rows[i]);
        }

        private static string DescribeComponents(Transform t)
        {
            Component[] comps;
            try
            {
                comps = t.GetComponents<Component>();
            }
            catch
            {
                return "(?)";
            }

            var names = new List<string>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null)
                {
                    names.Add("<missing>");
                    continue;
                }

                if (c is Transform)
                    continue;
                names.Add(c.GetType().Name);
            }

            return names.Count == 0 ? "-" : string.Join(",", names.ToArray());
        }

        private static string BuildRelativePath(Transform root, Transform t)
        {
            if (t == root)
                return t.name;

            var stack = new List<string>();
            Transform? cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }

            stack.Reverse();
            return string.Join("/", stack.ToArray());
        }

        private static List<OCIChar> CollectSceneCharacters()
        {
            var list = new List<OCIChar>();
            try
            {
                var studio = Singleton<Studio.Studio>.Instance;
                if (studio?.dicObjectCtrl == null)
                    return list;

                foreach (KeyValuePair<int, ObjectCtrlInfo> kvp in studio.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar oci)
                        list.Add(oci);
                }
            }
            catch
            {
                // ignored
            }

            return list;
        }

        private static int CountTrue(bool[] flags)
        {
            int n = 0;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i])
                    n++;
            }

            return n;
        }

        private static int CountDrawablePairs(bool[] valid)
        {
            int n = 0;
            for (int p = 0; p < AnimPreviewBoneSet.PairCount; p++)
            {
                AnimPreviewBoneSet.GetPair(p, out int a, out int b);
                if (a < valid.Length && b < valid.Length && valid[a] && valid[b])
                    n++;
            }

            return n;
        }

        private static int SafeDicKey(OCIChar oci)
        {
            try
            {
                return oci.oiCharInfo?.dicKey ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        private static string Fmt(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:F4}, {1:F4}, {2:F4})", v.x, v.y, v.z);
    }
}
