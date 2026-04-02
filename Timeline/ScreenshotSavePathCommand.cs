using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Calls the Screenshot plugin <c>SetScreenshotSaveRelativePath</c> (folder under game root; persists to config).
    /// </summary>
    public class ScreenshotSavePathCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "screenshot_save_path";
        public override string GetDisplayLabel() => "SS save path";

        private string _relativePath = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rel path", GUILayout.Width(48));
            _relativePath = GUILayout.TextField(_relativePath ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (!ScreenshotPluginInterop.IsStaticApiAvailable)
                return "Screenshot plugin API not loaded";
            if (string.IsNullOrWhiteSpace(_relativePath)) return "Path is empty";
            if (vars != null && !vars.IsValidInterpolation(_relativePath)) return "Unknown variable in path";
            return null;
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ScreenshotPluginInterop.IsStaticApiAvailable)
            {
                SandboxServices.Log.LogWarning("Screenshot plugin static API not found. Skipping save path.");
                onComplete();
                return;
            }

            string resolved = ctx.Variables.Interpolate(_relativePath ?? "").Trim();
            if (string.IsNullOrEmpty(resolved))
            {
                onComplete();
                return;
            }

            try
            {
                if (!ScreenshotPluginInterop.TrySetScreenshotSaveRelativePath(resolved))
                    SandboxServices.Log.LogWarning($"SetScreenshotSaveRelativePath failed for \"{resolved}\"");
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"SetScreenshotSaveRelativePath: {ex.Message}");
            }

            onComplete();
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_relativePath);
        }

        public override void DeserializePayload(string payload)
        {
            _relativePath = "";
            if (string.IsNullOrEmpty(payload)) return;
            _relativePath = payload;
        }
    }
}
