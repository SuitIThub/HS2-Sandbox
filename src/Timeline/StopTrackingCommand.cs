using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Calls Copy Script API to stop tracking (POST /api/tracking/stop).
    /// </summary>
    public class StopTrackingCommand : TimelineCommand
    {
        public override string TypeId => "stop_tracking";

        public override string GetDisplayLabel() => "Stop copy script";

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
            yield return ctx.ApiClient!.StopTrackingAsync(b => success = b);
            if (success == true)
                onComplete();
            else
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}
