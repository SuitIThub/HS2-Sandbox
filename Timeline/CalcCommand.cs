using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Performs an arithmetic operation on two operands (variable or literal) and stores the result in a variable.
    /// Operands can be an integer variable name or a number. Result variable is created if it does not exist.
    /// </summary>
    public class CalcCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        private static readonly string[] OperatorSymbols = { "+", "-", "\u00d7", "/", "%" };

        public override string TypeId => "calc";

        private string _leftOperand = "";
        private int _operatorIndex; // 0=add, 1=subtract, 2=multiply, 3=divide, 4=modulo
        private string _rightOperand = "";
        private string _resultVariable = "";

        public override string GetDisplayLabel() => "Calc";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
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
            GUILayout.Label("Store in", GUILayout.Width(48));
            _resultVariable = GUILayout.TextField(_resultVariable ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resultVar = (_resultVariable ?? "").Trim();
            if (string.IsNullOrEmpty(resultVar)) { onComplete(); return; }

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

            int result = _operatorIndex switch
            {
                0 => left + right,
                1 => left - right,
                2 => left * right,
                3 => right == 0 ? 0 : left / right,
                4 => right == 0 ? 0 : left % right,
                _ => 0
            };

            ctx.Variables.SetInt(resultVar, result);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string resultVar = (_resultVariable ?? "").Trim();
            if (string.IsNullOrEmpty(resultVar)) return;
            int left = store.ResolveIntOperand(_leftOperand ?? "");
            int right = store.ResolveIntOperand(_rightOperand ?? "");
            int result = _operatorIndex switch
            {
                0 => left + right,
                1 => left - right,
                2 => left * right,
                3 => right == 0 ? 0 : left / right,
                4 => right == 0 ? 0 : left % right,
                _ => 0
            };
            store.SetInt(resultVar, result);
        }

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (base.HasInvalidConfiguration(variablesAtThisIndex)) return true;
            if (variablesAtThisIndex == null) return false;
            if (!string.IsNullOrWhiteSpace(_leftOperand) && !variablesAtThisIndex.IsValidIntOperand(_leftOperand)) return true;
            if (!string.IsNullOrWhiteSpace(_rightOperand) && !variablesAtThisIndex.IsValidIntOperand(_rightOperand)) return true;
            return false;
        }

        public override string SerializePayload()
        {
            string Escape(string s) => (s ?? "").Replace("\u0001", "");
            return Escape(_leftOperand) + PayloadSeparator + _operatorIndex + PayloadSeparator + Escape(_rightOperand) + PayloadSeparator + Escape(_resultVariable);
        }

        public override void DeserializePayload(string payload)
        {
            _leftOperand = "";
            _operatorIndex = 0;
            _rightOperand = "";
            _resultVariable = "";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1) _leftOperand = p[0] ?? "";
            if (p.Length >= 2 && int.TryParse(p[1], out int op) && op >= 0 && op < OperatorSymbols.Length) _operatorIndex = op;
            if (p.Length >= 3) _rightOperand = p[2] ?? "";
            if (p.Length >= 4) _resultVariable = p[3] ?? "";
        }
    }
}
