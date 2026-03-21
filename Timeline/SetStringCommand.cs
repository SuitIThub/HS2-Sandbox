using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets a string variable by name. Variable is created if it does not exist.
    /// </summary>
    public class SetStringCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        public override string TypeId => "set_string";

        private string _variableName = "";
        private string _value = "";

        public override string GetDisplayLabel() => "Set String";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Variable", GUILayout.Width(48));
            _variableName = GUILayout.TextField(_variableName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Value", GUILayout.Width(48));
            _value = GUILayout.TextField(_value ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!string.IsNullOrWhiteSpace(_variableName))
                ctx.Variables.SetString(_variableName.Trim(), ctx.Variables.Interpolate(_value ?? ""));
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) return;
            store.SetString(_variableName.Trim(), _value ?? "");
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars != null && !vars.IsValidInterpolation(_value ?? ""))
                return "Unknown variable in value";
            return null;
        }

        public override string SerializePayload()
        {
            return (_variableName ?? "").Replace("\u0001", "") + PayloadSeparator + (_value ?? "").Replace("\u0001", "");
        }

        public override void DeserializePayload(string payload)
        {
            _variableName = "";
            _value = "";
            if (string.IsNullOrEmpty(payload)) return;
            int sep = payload.IndexOf(PayloadSeparator);
            if (sep >= 0)
            {
                _variableName = payload.Substring(0, sep);
                _value = payload.Substring(sep + 1);
            }
            else
                _variableName = payload;
        }
    }
}
