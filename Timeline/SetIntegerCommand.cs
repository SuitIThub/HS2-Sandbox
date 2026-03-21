using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets an integer variable by name. Variable is created if it does not exist.
    /// </summary>
    public class SetIntegerCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        public override string TypeId => "set_integer";

        private string _variableName = "";
        private string _valueText = "0";

        public override string GetDisplayLabel() => "Set Integer";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Variable", GUILayout.Width(48));
            _variableName = GUILayout.TextField(_variableName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Value", GUILayout.Width(48));
            _valueText = GUILayout.TextField(_valueText ?? "0", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) { onComplete(); return; }
            if (!ctx.Variables.TryResolveIntOperand(_valueText ?? "0", out int value))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }
            ctx.Variables.SetInt(_variableName.Trim(), value);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) return;
            int value = int.TryParse(_valueText?.Trim(), out int v) ? v : 0;
            store.SetInt(_variableName.Trim(), value);
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_valueText)) return "Value is empty";
            if (vars != null && !vars.IsValidIntOperand(_valueText)) return "Invalid value";
            return null;
        }

        public override string SerializePayload()
        {
            return (_variableName ?? "").Replace("\u0001", "") + PayloadSeparator + (_valueText ?? "0").Replace("\u0001", "");
        }

        public override void DeserializePayload(string payload)
        {
            _variableName = "";
            _valueText = "0";
            if (string.IsNullOrEmpty(payload)) return;
            int sep = payload.IndexOf(PayloadSeparator);
            if (sep >= 0)
            {
                _variableName = payload.Substring(0, sep);
                _valueText = payload.Substring(sep + 1);
            }
            else
                _variableName = payload;
        }
    }
}
