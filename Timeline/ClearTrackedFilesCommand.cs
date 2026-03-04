using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Calls Copy Script API to clear tracked files / clean (DELETE /api/tracking).
    /// </summary>
    public class ClearTrackedFilesCommand : TimelineCommand
    {
        public override string TypeId => "clear_tracked";

        public override string GetDisplayLabel() => "Clean";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));
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

        private static IEnumerator Run(TimelineContext ctx, Action onComplete)
        {
            bool? success = null;
            yield return ctx.ApiClient!.ClearTrackedFilesAsync(b => success = b);
            if (success == true)
                onComplete();
            else
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}
