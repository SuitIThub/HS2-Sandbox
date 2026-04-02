using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Stores a relative screenshot folder path in the timeline variable <see cref="ScreenshotPluginInterop.AltPathVariable.Name"/>,
    /// used when the Screenshot command runs with Alt path enabled.
    /// </summary>
    public class ScreenshotAltPathVarCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "screenshot_alt_path_var";
        public override string GetDisplayLabel() => "SS alt path var";

        private string _relativePath = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rel path", GUILayout.Width(48));
            _relativePath = GUILayout.TextField(_relativePath ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Stores in [{ScreenshotPluginInterop.AltPathVariable.Name}]", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_relativePath)) return "Path is empty";
            if (vars != null && !vars.IsValidInterpolation(_relativePath)) return "Unknown variable in path";
            return null;
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolved = ctx.Variables.Interpolate(_relativePath ?? "").Trim();
            ctx.Variables.SetStringExclusive(ScreenshotPluginInterop.AltPathVariable.Name, resolved);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            store.SetStringExclusive(ScreenshotPluginInterop.AltPathVariable.Name, store.Interpolate(_relativePath ?? ""));
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
