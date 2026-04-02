using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Calls the Screenshot plugin <c>SetCaptureAlphaModeByName</c> (transparency mode for rendered screenshots).
    /// </summary>
    public class ScreenshotAlphaModeCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "screenshot_alpha";
        public override string GetDisplayLabel() => "SS alpha mode";

        private string _modeName = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(36));
            _modeName = GUILayout.TextField(_modeName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (!ScreenshotPluginInterop.IsStaticApiAvailable)
                return "Screenshot plugin API not loaded";
            if (string.IsNullOrWhiteSpace(_modeName)) return "Mode is empty";
            if (vars != null && !vars.IsValidInterpolation(_modeName)) return "Unknown variable in mode";
            return null;
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ScreenshotPluginInterop.IsStaticApiAvailable)
            {
                SandboxServices.Log.LogWarning("Screenshot plugin static API not found. Skipping alpha mode.");
                onComplete();
                return;
            }

            string resolved = ctx.Variables.Interpolate(_modeName ?? "");
            if (string.IsNullOrWhiteSpace(resolved))
            {
                onComplete();
                return;
            }

            if (!ScreenshotPluginInterop.TrySetCaptureAlphaModeByName(resolved.Trim()))
                SandboxServices.Log.LogWarning($"SetCaptureAlphaModeByName rejected or failed: \"{resolved}\"");
            onComplete();
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_modeName);
        }

        public override void DeserializePayload(string payload)
        {
            _modeName = "";
            if (string.IsNullOrEmpty(payload)) return;
            _modeName = payload;
        }
    }
}
