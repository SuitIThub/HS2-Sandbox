using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets a CopyScript counter rule by tag name via API. UI matches CopyScript rule row: tag + start, increment, step, max. On success refreshes CopyScript window; on failure shows Resolve.
    /// </summary>
    public class SetCounterRuleCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        public override string TypeId => "set_rule_counter";

        private string _tagName = "";
        private string _startText = "0";
        private string _incrementText = "1";
        private string _stepText = "1";
        private string _maxText = "0";
        private int _startValue;
        private int _increment = 1;
        private int _step = 1;
        private bool _useMaxValue;
        private int _maxValue;

        public override string GetDisplayLabel() => "Rule (counter)";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tag", GUILayout.Width(24));
            _tagName = GUILayout.TextField(_tagName ?? "", GUILayout.Width(100), GUILayout.Height(18));
            GUILayout.Label("Start", GUILayout.Width(32), GUILayout.Height(18));
            _startText = GUILayout.TextField(_startText ?? "0", GUILayout.Width(40), GUILayout.Height(18));
            if (int.TryParse(_startText, out int startVal)) _startValue = startVal;
            GUILayout.Label("Inc", GUILayout.Width(22), GUILayout.Height(18));
            _incrementText = GUILayout.TextField(_incrementText ?? "1", GUILayout.Width(32), GUILayout.Height(18));
            if (int.TryParse(_incrementText, out int incVal)) _increment = incVal;
            GUILayout.Label("Step", GUILayout.Width(28), GUILayout.Height(18));
            _stepText = GUILayout.TextField(_stepText ?? "1", GUILayout.Width(28), GUILayout.Height(18));
            if (int.TryParse(_stepText, out int stepVal) && stepVal >= 0) _step = stepVal;
            _useMaxValue = GUILayout.Toggle(_useMaxValue, "Max", GUILayout.Width(62), GUILayout.Height(18));
            if (_useMaxValue)
            {
                _maxText = GUILayout.TextField(_maxText ?? "0", GUILayout.Width(40), GUILayout.Height(18));
                if (int.TryParse(_maxText, out int maxVal)) _maxValue = maxVal;
            }
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
            string tag = ctx.Variables.Interpolate(_tagName ?? "").Trim();
            if (string.IsNullOrEmpty(tag))
            {
                onComplete();
                yield break;
            }
            if (!ctx.Variables.TryResolveIntOperand(_startText ?? "0", out int startVal))
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
                yield break;
            }
            if (!ctx.Variables.TryResolveIntOperand(_incrementText ?? "1", out int incVal))
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
                yield break;
            }
            if (!ctx.Variables.TryResolveIntOperand(_stepText ?? "1", out int stepValRaw))
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
                yield break;
            }
            int stepVal = Mathf.Max(0, stepValRaw);
            if (!ctx.Variables.TryResolveIntOperand(_maxText ?? "0", out int maxVal))
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
                yield break;
            }
            var rule = new CopyScriptRule
            {
                type = "counter",
                tag_name = tag,
                start_value = startVal,
                increment = incVal,
                step = stepVal,
                use_max_value = _useMaxValue,
                max_value = maxVal
            };
            bool success = false;
            yield return ctx.ApiClient!.UpdateRuleAsync(tag, rule, b => success = b);
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

        public override string SerializePayload()
        {
            return (_tagName ?? "") + PayloadSeparator + (_startText ?? "0") + PayloadSeparator + (_incrementText ?? "1") + PayloadSeparator + (_stepText ?? "1")
                + PayloadSeparator + (_useMaxValue ? "1" : "0") + PayloadSeparator + (_maxText ?? "0");
        }

        public override void DeserializePayload(string payload)
        {
            _tagName = "";
            _startText = "0";
            _incrementText = "1";
            _stepText = "1";
            _maxText = "0";
            _startValue = 0;
            _increment = 1;
            _step = 1;
            _useMaxValue = false;
            _maxValue = 0;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1) _tagName = p[0] ?? "";
            if (p.Length >= 2) { _startText = p[1] ?? "0"; int.TryParse(_startText, out _startValue); }
            if (p.Length >= 3) { _incrementText = p[2] ?? "1"; int.TryParse(_incrementText, out _increment); }
            if (p.Length >= 4) { _stepText = p[3] ?? "1"; if (int.TryParse(_stepText, out int st) && st >= 0) _step = st; }
            if (p.Length >= 5) _useMaxValue = p[4] == "1";
            if (p.Length >= 6) { _maxText = p[5] ?? "0"; int.TryParse(_maxText, out _maxValue); }
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars == null) return null;
            if (!vars.IsValidInterpolation(_tagName ?? "")) return "Unknown variable in tag name";
            if (!string.IsNullOrWhiteSpace(_startText) && !vars.IsValidIntOperand(_startText)) return "Invalid start value";
            if (!string.IsNullOrWhiteSpace(_incrementText) && !vars.IsValidIntOperand(_incrementText)) return "Invalid increment value";
            if (!string.IsNullOrWhiteSpace(_stepText) && !vars.IsValidIntOperand(_stepText)) return "Invalid step value";
            if (_useMaxValue && !string.IsNullOrWhiteSpace(_maxText) && !vars.IsValidIntOperand(_maxText)) return "Invalid max value";
            return null;
        }
    }
}
