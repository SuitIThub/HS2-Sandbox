using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Compares two values (string or number, interpolatable). If the comparison is true, jumps to the named checkpoint.
    /// Operands can be a variable name (int or string), a literal number, or interpolated text. Comparison is numeric when both sides are valid int operands, otherwise string.
    /// </summary>
    public class IfCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        // ==, !=, <, <=, >, >=, left is last element of right list, left is first element of right list
        private static readonly string[] OperatorSymbols = { "==", "\u2260", "<", "\u2264", ">", "\u2265", "\u2208L", "\u2208F" };

        public override string TypeId => "if";

        private string _leftOperand = "";
        private int _operatorIndex; // 0==, 1!=, 2<, 3<=, 4>, 5>=, 6 in last, 7 in first
        private string _rightOperand = "";
        private string _checkpointName = "";
        private bool _negate;

        public override string GetDisplayLabel() => "If";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            _negate = GUILayout.Toggle(_negate, "Not", GUILayout.Width(40));
            GUILayout.Space(4);
            GUILayout.Label("Left", GUILayout.Width(32));
            _leftOperand = GUILayout.TextField(_leftOperand ?? "", GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(OperatorSymbols[_operatorIndex], GUILayout.Width(28)))
            {
                _operatorIndex = (_operatorIndex + 1) % OperatorSymbols.Length;
            }
            GUILayout.Label("Right", GUILayout.Width(36));
            _rightOperand = GUILayout.TextField(_rightOperand ?? "", GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Jump to", GUILayout.Width(48));
            _checkpointName = GUILayout.TextField(_checkpointName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string target = ctx.Variables.Interpolate(_checkpointName ?? "").Trim();
            if (string.IsNullOrEmpty(target))
            {
                onComplete();
                return;
            }

            bool numeric = ctx.Variables.IsValidIntOperand(_leftOperand ?? "") && ctx.Variables.IsValidIntOperand(_rightOperand ?? "");
            bool result;
            if (numeric)
            {
                if (!ctx.Variables.TryResolveIntOperand(_leftOperand ?? "", out int left))
                {
                    ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                    return;
                }
                if (!ctx.Variables.TryResolveIntOperand(_rightOperand ?? "", out int right))
                {
                    ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                    return;
                }
                result = EvaluateNumeric(left, right);
            }
            else
            {
                string left = ctx.Variables.Interpolate(_leftOperand ?? "");
                string rightRaw = _rightOperand ?? "";
                if (_operatorIndex == 6 || _operatorIndex == 7)
                    result = EvaluateListPosition(ctx.Variables, left, rightRaw, _operatorIndex == 7);
                else
                {
                    string right = ctx.Variables.Interpolate(rightRaw);
                    result = EvaluateString(left, right);
                }
            }

            if (_negate)
                result = !result;

            if (result)
                ctx.SetJumpTarget(target);
            onComplete();
        }

        private bool EvaluateNumeric(int left, int right)
        {
            return _operatorIndex switch
            {
                0 => left == right,
                1 => left != right,
                2 => left < right,
                3 => left <= right,
                4 => left > right,
                5 => left >= right,
                _ => false
            };
        }

        private bool EvaluateString(string left, string right)
        {
            int cmp = string.Compare(left, right, StringComparison.Ordinal);
            return _operatorIndex switch
            {
                0 => cmp == 0,
                1 => cmp != 0,
                2 => cmp < 0,
                3 => cmp <= 0,
                4 => cmp > 0,
                5 => cmp >= 0,
                _ => false
            };
        }

        /// <summary>
        /// For operators 6/7: checks whether left equals the last or first element of the list named by rightListName.
        /// </summary>
        private bool EvaluateListPosition(TimelineVariableStore variables, string left, string rightListName, bool first)
        {
            if (string.IsNullOrWhiteSpace(rightListName)) return false;
            var list = variables.GetList(rightListName.Trim());
            if (list == null || list.Count == 0) return false;
            string candidate = first ? list[0] : list[list.Count - 1];
            return string.Equals(left, candidate ?? "", StringComparison.Ordinal);
        }

        public override string SerializePayload()
        {
            string Escape(string s) => (s ?? "").Replace("\u0001", "");
            return Escape(_leftOperand)
                + PayloadSeparator + _operatorIndex
                + PayloadSeparator + Escape(_rightOperand)
                + PayloadSeparator + Escape(_checkpointName)
                + PayloadSeparator + (_negate ? "1" : "0");
        }

        public override void DeserializePayload(string payload)
        {
            _leftOperand = "";
            _operatorIndex = 0;
            _rightOperand = "";
            _checkpointName = "";
            _negate = false;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1) _leftOperand = p[0] ?? "";
            if (p.Length >= 2 && int.TryParse(p[1], out int op) && op >= 0 && op < OperatorSymbols.Length) _operatorIndex = op;
            if (p.Length >= 3) _rightOperand = p[2] ?? "";
            if (p.Length >= 4) _checkpointName = p[3] ?? "";
            if (p.Length >= 5) _negate = p[4] == "1";
        }

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (variablesAtThisIndex == null) return false;
            if (!variablesAtThisIndex.IsValidInterpolation(_leftOperand ?? "")) return true;
            if (!variablesAtThisIndex.IsValidInterpolation(_rightOperand ?? "")) return true;
            if (!variablesAtThisIndex.IsValidInterpolation(_checkpointName ?? "")) return true;
            return false;
        }
    }
}
