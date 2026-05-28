using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Checks GET /api/issues for issues_count. If 0, completes immediately.
    /// If not 0, shows a Resolve button in the timeline list; when the user clicks it,
    /// simulates a click at (0,0) to release focus and continues.
    /// </summary>
    public class ResolveOnIssueCommand : TimelineCommand
    {
        public override string TypeId => "resolve_on_issue";

        public override string GetDisplayLabel() => "Resolve on issue";

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
            ctx.Runner.StartCoroutine(CheckIssuesThenWaitIfNeeded(ctx, onComplete));
        }

        private IEnumerator CheckIssuesThenWaitIfNeeded(TimelineContext ctx, Action onComplete)
        {
            IssuesResponse? result = null;
            yield return ctx.ApiClient!.GetIssuesAsync(r => result = r);
            if (result == null || !result.success)
            {
                onComplete();
                yield break;
            }
            if (result.issues_count == 0)
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

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}
