using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets the CopyScript naming pattern via API. On success refreshes the CopyScript window; on failure shows a Resolve button.
    /// </summary>
    public class SetNamePatternCommand : TimelineCommand
    {
        public override string TypeId => "set_name_pattern";

        private string _pattern = "";

        public override string GetDisplayLabel() => "Set pattern";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Pattern", GUILayout.Width(48));
            _pattern = GUILayout.TextField(_pattern ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (ctx.ApiClient == null)
            {
                onComplete();
                return;
            }
            ctx.Runner.StartCoroutine(Run(ctx, onComplete));
        }

        private IEnumerator Run(TimelineContext ctx, Action onComplete)
        {
            string pattern = ctx.Variables.Interpolate(_pattern ?? "");
            bool success = false;
            yield return ctx.ApiClient!.SetNamePatternAsync(pattern, b => success = b);
            if (success)
            {
                yield return new WaitForSeconds(0.5f);
                UnityEngine.Object.FindObjectOfType<CopyScript>()?.RefreshFromTimeline();
                onComplete();
            }
            else
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
            }
        }

        public override string SerializePayload() => _pattern ?? "";

        public override void DeserializePayload(string payload) => _pattern = payload ?? "";

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars != null && !vars.IsValidInterpolation(_pattern ?? ""))
                return "Unknown variable in pattern";
            return null;
        }
    }
}
