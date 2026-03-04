using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Defines a list variable by name. The list is edited via the list editor. Used by Set list rule and List commands when "use list variable" is checked.
    /// </summary>
    public class SetListCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';
        private const char ValuesSeparator = '\u0002';

        public override string TypeId => "set_list";

        private string _variableName = "";
        private readonly List<string> _values = new List<string>();

        public override string GetDisplayLabel() => "Set List";

        public string[] GetValues()
        {
            return _values.ToArray();
        }

        public void SetValues(string[] values)
        {
            _values.Clear();
            if (values != null)
            {
                foreach (string v in values)
                {
                    string t = (v ?? "").Trim();
                    if (t.Length > 0)
                        _values.Add(t);
                }
            }
        }

        private string GetValuesPreview(int maxLength = 40)
        {
            if (_values.Count == 0) return "(empty)";
            string joined = string.Join("; ", _values);
            if (joined.Length <= maxLength) return joined;
            return joined.Substring(0, maxLength - 3) + "...";
        }

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Variable", GUILayout.Width(48));
            _variableName = GUILayout.TextField(_variableName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(false));
            if (GUILayout.Button("Edit list...", GUILayout.Width(70), GUILayout.Height(18)))
            {
                ctx.OpenListEditor?.Invoke(GetValues, SetValues);
            }
            GUILayout.Label(GetValuesPreview(40), GUILayout.MinWidth(60), GUILayout.ExpandWidth(true), GUILayout.Height(18));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) { onComplete(); return; }
            ctx.Variables.SetList(_variableName.Trim(), _values);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) return;
            store.SetList(_variableName.Trim(), _values);
        }

        public override string SerializePayload()
        {
            string name = (_variableName ?? "").Replace("\u0001", "").Replace("\u0002", "");
            string valuesPayload = string.Join(ValuesSeparator.ToString(), _values);
            return name + PayloadSeparator + valuesPayload;
        }

        public override void DeserializePayload(string payload)
        {
            _variableName = "";
            _values.Clear();
            if (string.IsNullOrEmpty(payload)) return;
            int sep = payload.IndexOf(PayloadSeparator);
            if (sep >= 0)
            {
                _variableName = payload.Substring(0, sep);
                string valuesPart = payload.Substring(sep + 1);
                foreach (string v in valuesPart.Split(ValuesSeparator))
                {
                    string t = (v ?? "").Trim();
                    if (t.Length > 0)
                        _values.Add(t);
                }
            }
            else
                _variableName = payload;
        }

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (variablesAtThisIndex == null) return false;
            return string.IsNullOrWhiteSpace(_variableName);
        }
    }
}
