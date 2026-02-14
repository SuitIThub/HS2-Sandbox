using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// GET /api/tracking (no count) and compares total_count to the user-set expected count.
    /// If they match, completes immediately. If not, shows a Resolve button; when the user
    /// clicks it, simulates a click at (0,0) and continues.
    /// </summary>
    public class ResolveOnCountCommand : TimelineCommand
    {
        public override string TypeId => "resolve_on_count";
        private int _expectedCount;

        public int ExpectedCount
        {
            get => _expectedCount;
            set => _expectedCount = Mathf.Max(0, value);
        }

        public override string GetDisplayLabel() => "Resolve on count";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Expected count:", GUILayout.Width(95));
            string s = GUILayout.TextField(_expectedCount.ToString(), GUILayout.Width(50));
            if (int.TryParse(s, out int n) && n >= 0)
                _expectedCount = n;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (ctx.ApiClient == null)
            {
                onComplete();
                return;
            }
            ctx.Runner.StartCoroutine(CheckCountThenWaitIfNeeded(ctx, onComplete));
        }

        private IEnumerator CheckCountThenWaitIfNeeded(TimelineContext ctx, Action onComplete)
        {
            TrackedFilesResponse? result = null;
            yield return ctx.ApiClient!.GetTrackedFilesAsync(r => result = r);
            if (result == null || !result.success)
            {
                onComplete();
                yield break;
            }
            if (result.total_count == _expectedCount)
            {
                onComplete();
                yield break;
            }
            ctx.PendingResolveCallback = () =>
            {
                WindowsInput.SimulateMouseClickAt(0, 0, 0);
                onComplete();
            };
        }

        public override string SerializePayload() => _expectedCount.ToString();

        public override void DeserializePayload(string payload)
        {
            if (int.TryParse(payload?.Trim(), out int n) && n >= 0)
                _expectedCount = n;
        }
    }
}
