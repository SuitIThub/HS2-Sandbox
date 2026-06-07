using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>StudioNeoV2 simple-color (monocolor) rendering on <see cref="OCIChar"/>.</summary>
    internal static class PoseBrowserCharacterSimpleColor
    {
        public static void SetMonocolor(OCIChar oci, bool enabled)
        {
            if (oci == null) return;
            if (enabled)
            {
                oci.SetSimpleColor(oci.oiCharInfo.simpleColor);
                oci.SetVisibleSimple(true);
            }
            else
            {
                oci.SetVisibleSimple(false);
            }
        }

        public static void RestoreNormal(OCIChar oci)
        {
            if (oci == null) return;
            oci.SetVisibleSimple(false);
        }

        public static void ApplyGroupThumbnailFocus(
            System.Collections.Generic.IReadOnlyList<(PoseGridItem pose, OCIChar character)> assignments,
            int focusIndex)
        {
            for (int i = 0; i < assignments.Count; i++)
                SetMonocolor(assignments[i].character, i != focusIndex);
        }

        public static void RestoreAll(
            System.Collections.Generic.IReadOnlyList<(PoseGridItem pose, OCIChar character)> assignments)
        {
            for (int i = 0; i < assignments.Count; i++)
                RestoreNormal(assignments[i].character);
        }
    }
}
