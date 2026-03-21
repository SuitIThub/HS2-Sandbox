using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Holds a list of values. Each time the command runs, sets the given variable to the next value in the list (cycling).
    /// List is edited via a separate window opened by the "Edit list" button.
    /// </summary>
    public class ListCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';
        private const char ValuesSeparator = '\u0002';

        public override string TypeId => "list";

        private string _variableName = "";
        private readonly List<string> _values = new List<string>();
        private bool _useListVariable;
        private string _listVariableName = "";

        public override string GetDisplayLabel() => "List";

        /// <summary>For the list editor window. Returns a copy of the values.</summary>
        public string[] GetValues()
        {
            return _values.ToArray();
        }

        /// <summary>For the list editor window. Replaces the list with the given values.</summary>
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

        /// <summary>Preview string for the command row: values joined with "; ", truncated.</summary>
        public string GetValuesPreview(int maxLength = 40)
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
            _useListVariable = GUILayout.Toggle(_useListVariable, "List var", GUILayout.Width(56));
            if (_useListVariable)
            {
                GUILayout.Label("Var", GUILayout.Width(20));
                _listVariableName = GUILayout.TextField(_listVariableName ?? "", GUILayout.Width(100));
            }
            else
            {
                if (GUILayout.Button("Edit list...", GUILayout.Width(70)))
                {
                    ctx.OpenListEditor?.Invoke(GetValues, SetValues);
                }
                GUILayout.Label(GetValuesPreview(40), GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) { onComplete(); return; }
            var list = _useListVariable
                ? ctx.Variables.GetList(ctx.Variables.Interpolate(_listVariableName ?? "").Trim())
                : new List<string>(_values);
            if (list.Count == 0) { onComplete(); return; }

            string key = _variableName.Trim();
            int index = ctx.ListIndices.TryGetValue(key, out int idx) ? idx : 0;
            string value = list[index % list.Count];
            ctx.ListIndices[key] = index + 1;
            ctx.Variables.SetString(key, value);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) return;
            var list = _useListVariable
                ? store.GetList(store.Interpolate(_listVariableName ?? "").Trim())
                : new List<string>(_values);
            if (list.Count == 0) return;
            store.SetString(_variableName.Trim(), list[0]);
        }

        public override string SerializePayload()
        {
            string name = (_variableName ?? "").Replace("\u0001", "").Replace("\u0002", "");
            string valuesPayload = string.Join(ValuesSeparator.ToString(), _values);
            string useListVar = _useListVariable ? "1" : "0";
            string listVarName = (_listVariableName ?? "").Replace("\u0001", "").Replace("\u0002", "");
            return name + PayloadSeparator + valuesPayload + PayloadSeparator + useListVar + PayloadSeparator + listVarName;
        }

        public override void DeserializePayload(string payload)
        {
            _variableName = "";
            _values.Clear();
            _useListVariable = false;
            _listVariableName = "";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1) _variableName = p[0] ?? "";
            if (p.Length >= 2)
            {
                foreach (string v in (p[1] ?? "").Split(ValuesSeparator))
                {
                    string t = (v ?? "").Trim();
                    if (t.Length > 0) _values.Add(t);
                }
            }
            if (p.Length >= 3) _useListVariable = (p[2] ?? "") == "1";
            if (p.Length >= 4) _listVariableName = p[3] ?? "";
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) return "Variable name is empty";
            if (_useListVariable)
            {
                string listVar = (vars?.Interpolate(_listVariableName ?? "") ?? "").Trim();
                if (string.IsNullOrEmpty(listVar)) return "List variable name is empty";
                if (vars != null && !vars.HasList(listVar)) return $"List variable \"{listVar}\" not found";
            }
            else if (_values.Count == 0) return "Values list is empty";
            return null;
        }
    }
}
