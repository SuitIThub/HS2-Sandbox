using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Calls Copy Script API to start tracking (POST /api/tracking/start).
    /// </summary>
    public class StartTrackingCommand : TimelineCommand
    {
        public override string TypeId => "start_tracking";

        public override string GetDisplayLabel() => "Start copy script";

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
            yield return ctx.ApiClient!.StartTrackingAsync(b => success = b);
            if (success == true)
                onComplete();
            else
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}
