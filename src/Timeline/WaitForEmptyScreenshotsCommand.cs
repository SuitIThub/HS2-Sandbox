using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Waits until the tracking API returns no screenshots: GET /api/tracking?count=1 every second
    /// until returned_count is 0, then completes.
    /// </summary>
    public class WaitForEmptyScreenshotsCommand : TimelineCommand
    {
        public override string TypeId => "wait_empty_screenshots";
        private const float PollIntervalSeconds = 1f;

        public override string GetDisplayLabel() => "Wait for screenshots";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label("to be emptied to 0", GUILayout.ExpandWidth(true));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (ctx.ApiClient == null)
            {
                onComplete();
                return;
            }
            ctx.Runner.StartCoroutine(PollUntilEmpty(ctx, onComplete));
        }

        private IEnumerator PollUntilEmpty(TimelineContext ctx, Action onComplete)
        {
            while (true)
            {
                TrackedFilesResponse? result = null;
                yield return ctx.ApiClient!.GetTrackedFilesAsync(1, r => result = r);
                if (result == null || !result.success)
                {
                    yield return WaitRealtime(PollIntervalSeconds);
                    continue;
                }
                if (result.returned_count == 0)
                {
                    onComplete();
                    yield break;
                }
                yield return WaitRealtime(PollIntervalSeconds);
            }
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float endTime = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < endTime)
                yield return null;
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}
