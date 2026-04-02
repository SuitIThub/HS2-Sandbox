using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Copies a scalar variable (string, int, or bool — same precedence as interpolation) into a target variable with optional type conversion.
    /// </summary>
    public class GetVariableCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        private static readonly string[] KindLabels = { "str", "int", "bool" };

        private string _sourceVariable = "";
        private string _targetVariable = "";
        private int _targetKindIndex;
        private VariableScalarKind _targetKind = VariableScalarKind.String;

        public override string TypeId => "get";

        public override string GetDisplayLabel() => "Get";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("From", GUILayout.Width(48));
            _sourceVariable = GUILayout.TextField(_sourceVariable ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("To", GUILayout.Width(48));
            _targetVariable = GUILayout.TextField(_targetVariable ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("As", GUILayout.Width(48));
            if (GUILayout.Button(KindLabels[_targetKindIndex], GUILayout.Width(36)))
            {
                _targetKindIndex = (_targetKindIndex + 1) % KindLabels.Length;
                _targetKind = (VariableScalarKind)_targetKindIndex;
            }
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string src = ctx.Variables.Interpolate(_sourceVariable ?? "").Trim();
            string dst = ctx.Variables.Interpolate(_targetVariable ?? "").Trim();
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst))
            {
                SandboxServices.Log.LogWarning("Get: source or target variable name is empty.");
                onComplete();
                return;
            }

            if (!ctx.Variables.TryCopyScalar(src, dst, _targetKind))
            {
                SandboxServices.Log.LogWarning($"Get: scalar variable '{src}' not found.");
                switch (_targetKind)
                {
                    case VariableScalarKind.String:
                        ctx.Variables.SetStringExclusive(dst, "");
                        break;
                    case VariableScalarKind.Int:
                        ctx.Variables.SetIntExclusive(dst, 0);
                        break;
                    case VariableScalarKind.Bool:
                        ctx.Variables.SetBoolExclusive(dst, false);
                        break;
                }
            }
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string src = store.Interpolate(_sourceVariable ?? "").Trim();
            string dst = store.Interpolate(_targetVariable ?? "").Trim();
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) return;
            if (!store.TryCopyScalar(src, dst, _targetKind))
            {
                switch (_targetKind)
                {
                    case VariableScalarKind.String:
                        store.SetStringExclusive(dst, "");
                        break;
                    case VariableScalarKind.Int:
                        store.SetIntExclusive(dst, 0);
                        break;
                    case VariableScalarKind.Bool:
                        store.SetBoolExclusive(dst, false);
                        break;
                }
            }
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_sourceVariable)) return "Source variable is empty";
            if (string.IsNullOrWhiteSpace(_targetVariable)) return "Target variable is empty";
            if (vars != null)
            {
                if (!vars.IsValidInterpolation(_sourceVariable ?? "")) return "Unknown variable in source";
                if (!vars.IsValidInterpolation(_targetVariable ?? "")) return "Unknown variable in target";
            }
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_sourceVariable) + Sep + Esc(_targetVariable) + Sep + _targetKindIndex;
        }

        public override void DeserializePayload(string payload)
        {
            _sourceVariable = "";
            _targetVariable = "";
            _targetKindIndex = 0;
            _targetKind = VariableScalarKind.String;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _sourceVariable = p[0] ?? "";
            if (p.Length >= 2) _targetVariable = p[1] ?? "";
            if (p.Length >= 3 && int.TryParse(p[2], out int ki) && ki >= 0 && ki <= 2)
            {
                _targetKindIndex = ki;
                _targetKind = (VariableScalarKind)ki;
            }
        }
    }
}
