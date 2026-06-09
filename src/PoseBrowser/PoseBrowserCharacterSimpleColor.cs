using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Studio simple-color (monocolor) rendering on <see cref="OCIChar"/>.</summary>
    internal static class PoseBrowserCharacterSimpleColor
    {
        public static void SetMonocolor(OCIChar oci, bool enabled)
        {
            if (oci == null) return;
            if (enabled)
            {
                Color color = GetSimpleColor(oci);
                oci.SetSimpleColor(color);
                oci.SetVisibleSimple(true);
#if KKS || KK
                ApplyChaControlSimpleDraw(oci, color, drawSimple: true);
#endif
            }
            else
            {
                oci.SetVisibleSimple(false);
#if KKS || KK
                ApplyChaControlSimpleDraw(oci, color: default, drawSimple: false);
#endif
            }
        }

        public static void RestoreNormal(OCIChar oci)
        {
            if (oci == null) return;
            oci.SetVisibleSimple(false);
#if KKS || KK
            ApplyChaControlSimpleDraw(oci, color: default, drawSimple: false);
#endif
        }

        private static Color GetSimpleColor(OCIChar oci)
        {
            try
            {
                if (oci.oiCharInfo != null)
                    return oci.oiCharInfo.simpleColor;
            }
            catch
            {
                // ignore
            }

            return new Color(0.55f, 0.55f, 0.55f, 1f);
        }

#if KKS || KK
        /// <summary>KK/KKS female bodies need <see cref="ChaControl.ChangeSimpleBodyDraw"/>; OCIChar alone is not enough.</summary>
        private static void ApplyChaControlSimpleDraw(OCIChar oci, Color color, bool drawSimple)
        {
            try
            {
                ChaControl? cha = oci.charInfo;
                if (cha == null)
                    return;

                if (drawSimple)
                    cha.ChangeSimpleBodyColor(color);
                cha.ChangeSimpleBodyDraw(drawSimple);
            }
            catch
            {
                // ignore — monocolor is best-effort for thumbnails
            }
        }
#endif

        public static void ApplyGroupThumbnailFocus(
            System.Collections.Generic.IList<PoseOciPair> assignments,
            int focusIndex)
        {
            for (int i = 0; i < assignments.Count; i++)
                SetMonocolor(assignments[i].Character, i != focusIndex);
        }

        public static void RestoreAll(
            System.Collections.Generic.IList<PoseOciPair> assignments)
        {
            for (int i = 0; i < assignments.Count; i++)
                RestoreNormal(assignments[i].Character);
        }
    }
}
