#if HS2 || KK || KKS
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
#if HS2
        private string _heelzOffTagsEdit = "";
        private string _heelzOnTagsEdit = "";
        private bool _heelzTagEditsInitialized;

        private void EnsureHeelzTagEditFields()
        {
            if (_heelzTagEditsInitialized) return;
            _heelzTagEditsInitialized = true;
            _heelzOffTagsEdit = HeelzControlService.HeelsOffTagsEntry != null
                ? HeelzControlService.HeelsOffTagsEntry.Value
                : "";
            _heelzOnTagsEdit = HeelzControlService.HeelsOnTagsEntry != null
                ? HeelzControlService.HeelsOnTagsEntry.Value
                : "";
        }
#endif

        private void DrawPluginCompatibilitySection(GUIStyle wrap)
        {
            GUILayout.Space(14f);
            GUILayout.Label("<b>Plugin compatibility</b>", wrap);
            GUILayout.Label(
                "Integrations with other BepInEx plugins. Settings appear only when the plugin is installed.",
                wrap);

#if HS2
            DrawHeelzCompatibilityBlock(wrap);
#endif
            DrawPeCompatibilityBlock(wrap);
        }

#if HS2
        private void DrawHeelzCompatibilityBlock(GUIStyle wrap)
        {
            GUILayout.Space(10f);
            GUILayout.Label("<b>HS2Heelz</b>", wrap);

            if (!HeelzControlService.IsHeelzDetected)
            {
                GUILayout.Label("HS2Heelz is not installed. Heelz Control and tag rules are unavailable.", wrap);
                return;
            }

            GUILayout.Label(
                "Tag rules applied when poses are loaded. Per-character overrides use the Heelz Control window (top bar).",
                wrap);

            EnsureHeelzTagEditFields();

            GUILayout.Label("Heelz OFF tags (comma-separated)", wrap);
            _heelzOffTagsEdit = GUILayout.TextField(_heelzOffTagsEdit);

            GUILayout.Label("Heelz ON tags (comma-separated)", wrap);
            _heelzOnTagsEdit = GUILayout.TextField(_heelzOnTagsEdit);

            if (GUILayout.Button("Apply Heelz tag rules", PoseBrowserScale.H(24f)))
            {
                HeelzControlService.SetHeelsOffTags(ParseTagListForOptions(_heelzOffTagsEdit));
                HeelzControlService.SetHeelsOnTags(ParseTagListForOptions(_heelzOnTagsEdit));
                InvalidatePoseBrowserViewCaches();
            }
        }
#endif

        private void DrawPeCompatibilityBlock(GUIStyle wrap)
        {
            GUILayout.Space(10f);
            GUILayout.Label("<b>" + PePoseCompatService.PluginDisplayName + "</b>", wrap);

            if (!PePoseCompatService.IsPeDetected)
            {
                GUILayout.Label(
                    PePoseCompatService.PluginDisplayName +
                    " is not installed. Breast/butt Advanced Mode data cannot be embedded in pose files.",
                    wrap);
                return;
            }

            GUILayout.Label(
                "Saves " + PePoseCompatService.PluginDisplayName +
                " Advanced Mode breast and butt gravity/force into pose files (ExtendedSave block).",
                wrap);

            PoseBrowserConfig.Register(SandboxServices.Config);
            bool include = PePoseCompatService.IncludeBreastButtInPoses != null
                && PePoseCompatService.IncludeBreastButtInPoses.Value;
            bool newInclude = DrawOptionsToggle(
                include,
                "Embed breast/butt gravity & force when saving or updating poses");
            if (newInclude != include && PePoseCompatService.IncludeBreastButtInPoses != null)
                PePoseCompatService.IncludeBreastButtInPoses.Value = newInclude;
        }

        private static HashSet<string> ParseTagListForOptions(string raw)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (StringEx.IsNullOrWhiteSpace(raw)) return set;
            foreach (var part in raw.Split(','))
            {
                string t = part.Trim();
                if (t.Length > 0) set.Add(t);
            }
            return set;
        }
    }
}
#endif
