using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Waits until the API returns a new screenshot: GET /api/tracking?count=1 every second.
    /// If returned_count == 0, keeps waiting. If returned_count == 1 and files[0].original_name
    /// differs from the timeline's LastScreenshotName, updates it and completes.
    /// LastScreenshotName persists for the entire timeline run.
    /// </summary>
    public class WaitForScreenshotCommand : TimelineCommand
    {
        public override string TypeId => "wait_screenshot";
        private const float PollIntervalSeconds = 1f;

        public override string GetDisplayLabel() => "Wait for screenshot";

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
            ctx.Runner.StartCoroutine(PollUntilNewScreenshot(ctx, onComplete));
        }

        private IEnumerator PollUntilNewScreenshot(TimelineContext ctx, Action onComplete)
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
                    yield return WaitRealtime(PollIntervalSeconds);
                    continue;
                }
                if (result.files != null && result.files.Length >= 1)
                {
                    string originalName = result.files[0].original_name ?? "";
                    if (originalName != ctx.LastScreenshotName)
                    {
                        ctx.LastScreenshotName = originalName;
                        onComplete();
                        yield break;
                    }
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
