using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets the CopyScript destination path via API. On success refreshes the CopyScript window; on failure shows a Resolve button.
    /// </summary>
    public class SetDestinationPathCommand : TimelineCommand
    {
        public override string TypeId => "set_destination_path";

        private string _path = "";

        public override string GetDisplayLabel() => "Set destination";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Path", GUILayout.Width(32));
            _path = GUILayout.TextField(_path ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
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
            string path = ctx.Variables.Interpolate(_path ?? "").Trim();
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                try { Directory.CreateDirectory(path); }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogWarning($"Set destination path: could not create directory: {ex.Message}");
                }
            }
            bool success = false;
            yield return ctx.ApiClient!.SetDestinationPathAsync(path, b => success = b);
            if (success)
            {
                yield return new WaitForSeconds(0.5f);
                CopyScriptInterop.TryRefreshCopyScriptWindow();
                onComplete();
            }
            else
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
            }
        }

        public override string SerializePayload() => _path ?? "";

        public override void DeserializePayload(string payload) => _path = payload ?? "";

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars != null && !vars.IsValidInterpolation(_path ?? ""))
                return "Unknown variable in path";
            return null;
        }
    }
}
