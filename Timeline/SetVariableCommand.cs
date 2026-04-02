using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets a scalar variable (string, int, or bool). Type id <c>set</c> supports a kind selector; legacy <c>set_string</c> / <c>set_integer</c> load old timelines.
    /// </summary>
    public class SetVariableCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        private static readonly string[] KindLabels = { "str", "int", "bool" };

        private readonly string? _legacyTypeId;
        private readonly VariableScalarKind? _legacyKind;

        private string _variableName = "";
        private string _value = "";
        private VariableScalarKind _kind = VariableScalarKind.String;
        private int _kindIndex;

        public SetVariableCommand() { }

        protected SetVariableCommand(string legacyTypeId, VariableScalarKind kind)
        {
            _legacyTypeId = legacyTypeId;
            _legacyKind = kind;
            _kind = kind;
            _kindIndex = (int)kind;
        }

        public override string TypeId => _legacyTypeId ?? "set";

        public override string GetDisplayLabel() => _legacyTypeId == null ? "Set" : (_legacyKind == VariableScalarKind.Int ? "Set Int" : "Set Str");

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Variable", GUILayout.Width(48));
            _variableName = GUILayout.TextField(_variableName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (_legacyKind == null)
            {
                GUILayout.Label("Kind", GUILayout.Width(48));
                if (GUILayout.Button(KindLabels[_kindIndex], GUILayout.Width(36)))
                {
                    _kindIndex = (_kindIndex + 1) % KindLabels.Length;
                    _kind = (VariableScalarKind)_kindIndex;
                }
                GUILayout.Space(4);
                GUILayout.Label("Value", GUILayout.Width(40));
            }
            else
                GUILayout.Label("Value", GUILayout.Width(48));
            _value = GUILayout.TextField(_value ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            VariableScalarKind k = _legacyKind ?? _kind;
            string name = (_variableName ?? "").Trim();
            if (string.IsNullOrEmpty(name)) { onComplete(); return; }

            switch (k)
            {
                case VariableScalarKind.String:
                    ctx.Variables.SetStringExclusive(name, ctx.Variables.Interpolate(_value ?? ""));
                    break;
                case VariableScalarKind.Int:
                    if (!ctx.Variables.TryResolveIntOperand(_value ?? "0", out int iv))
                    {
                        ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                        return;
                    }
                    ctx.Variables.SetIntExclusive(name, iv);
                    break;
                case VariableScalarKind.Bool:
                    if (!ctx.Variables.TryResolveBoolOperand(_value ?? "False", out bool bv))
                    {
                        ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                        return;
                    }
                    ctx.Variables.SetBoolExclusive(name, bv);
                    break;
            }
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            VariableScalarKind k = _legacyKind ?? _kind;
            string name = (_variableName ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return;
            switch (k)
            {
                case VariableScalarKind.String:
                    store.SetStringExclusive(name, store.Interpolate(_value ?? ""));
                    break;
                case VariableScalarKind.Int:
                    store.SetIntExclusive(name, store.ResolveIntOperand(_value ?? "0"));
                    break;
                case VariableScalarKind.Bool:
                    store.SetBoolExclusive(name, store.TryResolveBoolOperand(_value ?? "False", out bool b) && b);
                    break;
            }
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            VariableScalarKind k = _legacyKind ?? _kind;
            if (k == VariableScalarKind.String)
            {
                if (vars != null && !vars.IsValidInterpolation(_value ?? ""))
                    return "Unknown variable in value";
                return null;
            }
            if (k == VariableScalarKind.Int)
            {
                if (string.IsNullOrWhiteSpace(_value)) return "Value is empty";
                if (vars != null && !vars.IsValidIntOperand(_value)) return "Invalid value";
                return null;
            }
            if (string.IsNullOrWhiteSpace(_value)) return "Value is empty";
            if (vars != null && !vars.IsValidBoolOperand(_value)) return "Invalid value";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace("\u0001", "");
            if (_legacyTypeId != null)
                return Esc(_variableName) + PayloadSeparator + Esc(_value ?? (_legacyKind == VariableScalarKind.Int ? "0" : ""));
            return Esc(_variableName) + PayloadSeparator + _kindIndex + PayloadSeparator + Esc(_value);
        }

        public override void DeserializePayload(string payload)
        {
            _variableName = "";
            _value = "";
            _kind = VariableScalarKind.String;
            _kindIndex = 0;
            if (string.IsNullOrEmpty(payload)) return;

            if (_legacyTypeId != null)
            {
                _kind = _legacyKind ?? VariableScalarKind.String;
                _kindIndex = (int)_kind;
                int sep = payload.IndexOf(PayloadSeparator);
                if (sep >= 0)
                {
                    _variableName = payload.Substring(0, sep);
                    _value = payload.Substring(sep + 1);
                }
                else
                    _variableName = payload;
                return;
            }

            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 3 && int.TryParse(p[1], out int ki) && ki >= 0 && ki <= 2)
            {
                _variableName = p[0] ?? "";
                _kindIndex = ki;
                _kind = (VariableScalarKind)ki;
                _value = p[2] ?? "";
            }
            else if (p.Length >= 2)
            {
                _variableName = p[0] ?? "";
                _value = p[1] ?? "";
            }
        }
    }

    /// <summary>Legacy type id for older saves.</summary>
    public class SetStringCommand : SetVariableCommand
    {
        public SetStringCommand() : base("set_string", VariableScalarKind.String) { }
    }

    /// <summary>Legacy type id for older saves.</summary>
    public class SetIntegerCommand : SetVariableCommand
    {
        public SetIntegerCommand() : base("set_integer", VariableScalarKind.Int) { }
    }
}
