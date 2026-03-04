using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets a CopyScript list rule by tag name via API. UI matches CopyScript rule row: tag + values (comma-separated), step. On success refreshes CopyScript window; on failure shows Resolve.
    /// </summary>
    public class SetListRuleCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';
        private const char ValuesSeparator = '\u0002';

        public override string TypeId => "set_rule_list";

        private string _tagName = "";
        private string _valuesStr = ""; // comma-separated in UI
        private string _stepText = "1";
        private int _step = 1;
        private bool _useListVariable;
        private string _listVariableName = "";

        public override string GetDisplayLabel() => "Rule (list)";

        /// <summary>For the list editor window. Returns values (comma-split).</summary>
        public string[] GetValues()
        {
            return (_valuesStr ?? "").Split(',').Select(x => x.Trim()).ToArray();
        }

        /// <summary>For the list editor window. Sets values from array (joined with comma).</summary>
        public void SetValues(string[] values)
        {
            _valuesStr = values != null ? string.Join(", ", values) : "";
        }

        private string GetValuesPreview(int maxLength = 40)
        {
            string[] parts = GetValues();
            if (parts.Length == 0) return "(empty)";
            string joined = string.Join("; ", parts);
            if (joined.Length <= maxLength) return joined;
            return joined.Substring(0, maxLength - 3) + "...";
        }

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tag", GUILayout.Width(24));
            _tagName = GUILayout.TextField(_tagName ?? "", GUILayout.Width(100), GUILayout.Height(18));
            _useListVariable = GUILayout.Toggle(_useListVariable, "", GUILayout.Width(18), GUILayout.Height(18));
            if (_useListVariable)
            {
                GUILayout.Label("Var", GUILayout.Width(20), GUILayout.Height(18));
                _listVariableName = GUILayout.TextField(_listVariableName ?? "", GUILayout.Width(100), GUILayout.Height(18));
            }
            else
            {
                if (GUILayout.Button("Edit list...", GUILayout.Width(70), GUILayout.Height(18)))
                {
                    ctx.OpenListEditor?.Invoke(GetValues, SetValues);
                }
                GUILayout.Label(GetValuesPreview(40), GUILayout.MinWidth(60), GUILayout.ExpandWidth(true), GUILayout.Height(18));
            }
            GUILayout.Label("Step", GUILayout.Width(28), GUILayout.Height(18));
            _stepText = GUILayout.TextField(_stepText ?? "1", GUILayout.Width(28), GUILayout.Height(18));
            if (int.TryParse(_stepText, out int stepVal) && stepVal >= 0) _step = stepVal;
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
            string[] values;
            if (_useListVariable)
            {
                var list = ctx.Variables.GetList(ctx.Variables.Interpolate(_listVariableName ?? "").Trim());
                values = list.ToArray();
            }
            else
            {
                string valuesResolved = ctx.Variables.Interpolate(_valuesStr ?? "");
                values = valuesResolved.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            }
            if (!ctx.Variables.TryResolveIntOperand(_stepText ?? "1", out int stepValRaw))
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
                yield break;
            }
            int stepVal = Mathf.Max(0, stepValRaw);
            var rule = new CopyScriptRule
            {
                type = "list",
                tag_name = tag,
                values = values,
                step = stepVal
            };
            bool success = false;
            yield return ctx.ApiClient!.UpdateRuleAsync(tag, rule, b => success = b);
            if (success)
            {
                yield return new WaitForSeconds(0.5f);
                UnityEngine.Object.FindObjectOfType<CopyScript>()?.RefreshFromTimeline();
                onComplete();
            }
            else
            {
                ctx.PendingResolveCallback = () => ctx.Runner.StartCoroutine(Run(ctx, onComplete));
            }
        }

        public override string SerializePayload()
        {
            var parts = (_valuesStr ?? "").Split(',').Select(x => x.Trim()).ToArray();
            string valuesPayload = string.Join(ValuesSeparator.ToString(), parts);
            string useListVar = _useListVariable ? "1" : "0";
            return (_tagName ?? "") + PayloadSeparator + valuesPayload + PayloadSeparator + (_stepText ?? "1")
                + PayloadSeparator + useListVar + PayloadSeparator + (_listVariableName ?? "");
        }

        public override void DeserializePayload(string payload)
        {
            _tagName = "";
            _valuesStr = "";
            _step = 1;
            _stepText = "1";
            _useListVariable = false;
            _listVariableName = "";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1) _tagName = p[0] ?? "";
            if (p.Length >= 2) _valuesStr = string.Join(", ", (p[1] ?? "").Split(ValuesSeparator));
            if (p.Length >= 3) { _stepText = p[2] ?? "1"; if (int.TryParse(_stepText, out int st) && st >= 0) _step = st; }
            if (p.Length >= 4) _useListVariable = (p[3] ?? "") == "1";
            if (p.Length >= 5) _listVariableName = p[4] ?? "";
        }

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (variablesAtThisIndex == null) return false;
            if (!variablesAtThisIndex.IsValidInterpolation(_tagName ?? "")) return true;
            if (_useListVariable)
            {
                string listVar = (variablesAtThisIndex.Interpolate(_listVariableName ?? "") ?? "").Trim();
                if (string.IsNullOrEmpty(listVar) || !variablesAtThisIndex.HasList(listVar)) return true;
            }
            else
            {
                if (!variablesAtThisIndex.IsValidInterpolation(_valuesStr ?? "")) return true;
            }
            if (!string.IsNullOrWhiteSpace(_stepText) && !variablesAtThisIndex.IsValidIntOperand(_stepText)) return true;
            return false;
        }
    }
}
