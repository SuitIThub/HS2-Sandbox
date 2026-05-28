using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Calls the Screenshot plugin <c>SetScreenshotResolution</c> (F11 render size).
    /// </summary>
    public class ScreenshotResolutionCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "screenshot_resolution";
        public override string GetDisplayLabel() => "SS resolution";

        private string _widthText = "";
        private string _heightText = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("W", GUILayout.Width(18));
            _widthText = GUILayout.TextField(_widthText ?? "", GUILayout.MinWidth(50), GUILayout.ExpandWidth(true));
            GUILayout.Label("H", GUILayout.Width(18));
            _heightText = GUILayout.TextField(_heightText ?? "", GUILayout.MinWidth(50), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (!ScreenshotPluginInterop.IsStaticApiAvailable)
                return "Screenshot plugin API not loaded";
            if (vars == null) return null;
            if (!string.IsNullOrWhiteSpace(_widthText) && !vars.IsValidIntOperand(_widthText))
                return "Invalid width";
            if (!string.IsNullOrWhiteSpace(_heightText) && !vars.IsValidIntOperand(_heightText))
                return "Invalid height";
            return null;
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ScreenshotPluginInterop.IsStaticApiAvailable)
            {
                SandboxServices.Log.LogWarning("Screenshot plugin static API not found. Skipping resolution.");
                onComplete();
                return;
            }

            if (!ctx.Variables.TryResolveIntOperand(_widthText ?? "0", out int w))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }

            if (!ctx.Variables.TryResolveIntOperand(_heightText ?? "0", out int h))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }

            if (!ScreenshotPluginInterop.TrySetScreenshotResolution(w, h))
                SandboxServices.Log.LogWarning($"SetScreenshotResolution failed for {w}x{h}");
            onComplete();
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_widthText) + Sep + Esc(_heightText);
        }

        public override void DeserializePayload(string payload)
        {
            _widthText = "";
            _heightText = "";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _widthText = p[0] ?? "";
            if (p.Length >= 2) _heightText = p[1] ?? "";
        }
    }
}
