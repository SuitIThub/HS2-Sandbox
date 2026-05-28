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
        private string _expectedCountText = "0";

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
            _expectedCountText = GUILayout.TextField(_expectedCountText, GUILayout.Width(50));
            if (int.TryParse(_expectedCountText, out int n) && n >= 0)
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
            if (!ctx.Variables.TryResolveIntOperand(_expectedCountText, out int expectedVal))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }
            int expected = Mathf.Max(0, expectedVal);
            ctx.Runner.StartCoroutine(CheckCountThenWaitIfNeeded(ctx, expected, onComplete));
        }

        private IEnumerator CheckCountThenWaitIfNeeded(TimelineContext ctx, int expected, Action onComplete)
        {
            TrackedFilesResponse? result = null;
            yield return ctx.ApiClient!.GetTrackedFilesAsync(r => result = r);
            if (result == null || !result.success)
            {
                onComplete();
                yield break;
            }
            if (result.total_count == expected)
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

        public override string SerializePayload() => _expectedCountText;

        public override void DeserializePayload(string payload)
        {
            _expectedCountText = payload?.Trim() ?? "0";
            if (int.TryParse(_expectedCountText, out int n) && n >= 0)
                _expectedCount = n;
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_expectedCountText)) return "Expected count is empty";
            if (vars != null && !vars.IsValidIntOperand(_expectedCountText))
                return "Invalid expected count";
            return null;
        }
    }
}
