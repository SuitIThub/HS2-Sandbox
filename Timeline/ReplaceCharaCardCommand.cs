using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Replaces workspace-selected characters with a character card (.png) from UserData storage.
    /// Path is interpolated; may be absolute or relative to the game UserData folder.
    /// </summary>
    public class ReplaceCharaCardCommand : TimelineCommand
    {
        public override string TypeId => "replace_chara_card";
        public override string GetDisplayLabel() => "Replace Chara";

        private string _cardPath = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Card PNG", GUILayout.Width(52));
            _cardPath = GUILayout.TextField(_cardPath ?? "", GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Browse…", GUILayout.Width(58)))
            {
                string charaRoot = Path.Combine(Paths.GameRootPath, "UserData", "chara");
                if (!Directory.Exists(charaRoot))
                    charaRoot = Path.Combine(Paths.GameRootPath, "UserData");
                string? picked = NativeFileDialog.OpenFile(
                    "Character card (.png)",
                    "PNG images (*.png)\0*.png\0All files (*.*)\0*.*\0",
                    charaRoot);
                if (picked is { Length: > 0 })
                    _cardPath = picked;
            }
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolved = ctx.Variables.Interpolate(_cardPath ?? "");
            string fullPath = ResolveCardPath(resolved);

            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                SandboxServices.Log.LogWarning($"ReplaceCharaCard: card file not found: '{resolved}'");
                onComplete();
                return;
            }

            if (!StudioAPI.StudioLoaded)
            {
                SandboxServices.Log.LogWarning("ReplaceCharaCard: Studio is not loaded yet.");
                onComplete();
                return;
            }

            IEnumerable<OCIChar>? selected;
            try
            {
                selected = StudioAPI.GetSelectedCharacters();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"ReplaceCharaCard: GetSelectedCharacters failed. {ex.Message}");
                onComplete();
                return;
            }

            if (selected == null)
            {
                SandboxServices.Log.LogWarning("ReplaceCharaCard: no character selected in the workspace.");
                onComplete();
                return;
            }

            bool any = false;
            foreach (OCIChar oci in selected)
            {
                if (oci != null) { any = true; break; }
            }
            if (!any)
            {
                SandboxServices.Log.LogWarning("ReplaceCharaCard: no character selected in the workspace.");
                onComplete();
                return;
            }

            fullPath = Path.GetFullPath(fullPath);
            foreach (OCIChar oci in selected)
            {
                if (oci == null) continue;
                try
                {
                    oci.ChangeChara(fullPath);
                }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogWarning($"ReplaceCharaCard: ChangeChara failed. {ex.Message}");
                }
            }

            onComplete();
        }

        /// <summary>
        /// Absolute path, or relative path under &lt;GameRoot&gt;/UserData (forward slashes ok).
        /// </summary>
        private static string ResolveCardPath(string raw)
        {
            string t = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(t)) return "";

            if (Path.IsPathRooted(t) && File.Exists(t))
                return Path.GetFullPath(t);

            string userData = Path.Combine(Paths.GameRootPath, "UserData");
            string combined = Path.GetFullPath(Path.Combine(userData, t.TrimStart('/', '\\')));
            if (File.Exists(combined))
                return combined;

            if (File.Exists(t))
                return Path.GetFullPath(t);

            return combined;
        }

        public override string SerializePayload()
        {
            return _cardPath ?? "";
        }

        public override void DeserializePayload(string payload)
        {
            _cardPath = payload ?? "";
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_cardPath)) return "Card path is empty";
            if (vars != null && !vars.IsValidInterpolation(_cardPath ?? ""))
                return "Unknown variable in card path";
            return null;
        }
    }
}
