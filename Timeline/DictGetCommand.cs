using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Reads a value from a dictionary variable by key and stores it in an existing variable.
    /// The target type is inferred from the variable: int, bool, or string (default). Raw strings
    /// parse to int/bool with the same rules as scalar conversion. Dictionary name and key support interpolation.
    /// </summary>
    public class DictGetCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "dict_get";
        public override string GetDisplayLabel() => "Dict Get";

        private string _dictName = "";
        private string _key = "";
        private string _targetVariable = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Dict", GUILayout.Width(32));
            _dictName = GUILayout.TextField(_dictName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Key", GUILayout.Width(32));
            _key = GUILayout.TextField(_key ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Store in", GUILayout.Width(48));
            _targetVariable = GUILayout.TextField(_targetVariable ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string dictName  = ctx.Variables.Interpolate(_dictName ?? "").Trim();
            string key       = ctx.Variables.Interpolate(_key ?? "");
            string targetVar = (_targetVariable ?? "").Trim();

            if (string.IsNullOrEmpty(dictName))
            {
                SandboxServices.Log.LogWarning("DictGet: dict name is empty.");
                onComplete();
                return;
            }
            if (string.IsNullOrEmpty(targetVar))
            {
                SandboxServices.Log.LogWarning("DictGet: target variable name is empty.");
                onComplete();
                return;
            }

            bool isIntTarget = ctx.Variables.HasInt(targetVar);
            bool isBoolTarget = ctx.Variables.HasBool(targetVar);

            if (!ctx.Variables.TryGetDictValue(dictName, key, out string raw))
            {
                SandboxServices.Log.LogWarning($"DictGet: key '{key}' not found in dict '{dictName}'.");
                if (isIntTarget) ctx.Variables.SetIntExclusive(targetVar, 0);
                else if (isBoolTarget) ctx.Variables.SetBoolExclusive(targetVar, false);
                else ctx.Variables.SetStringExclusive(targetVar, "");
                onComplete();
                return;
            }

            if (isIntTarget)
            {
                if (int.TryParse(raw, out int intVal))
                    ctx.Variables.SetIntExclusive(targetVar, intVal);
                else if (TimelineVariableStore.TryParseBoolText(raw, out bool bv))
                    ctx.Variables.SetIntExclusive(targetVar, bv ? 1 : 0);
                else
                {
                    SandboxServices.Log.LogWarning($"DictGet: value '{raw}' for key '{key}' in dict '{dictName}' could not be parsed as int. Storing 0.");
                    ctx.Variables.SetIntExclusive(targetVar, 0);
                }
            }
            else if (isBoolTarget)
            {
                if (TimelineVariableStore.TryParseBoolText(raw, out bool boolVal))
                    ctx.Variables.SetBoolExclusive(targetVar, boolVal);
                else if (int.TryParse(raw, out int n))
                    ctx.Variables.SetBoolExclusive(targetVar, n != 0);
                else
                {
                    SandboxServices.Log.LogWarning($"DictGet: value '{raw}' for key '{key}' in dict '{dictName}' could not be parsed as bool. Storing False.");
                    ctx.Variables.SetBoolExclusive(targetVar, false);
                }
            }
            else
            {
                ctx.Variables.SetStringExclusive(targetVar, raw);
            }

            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string targetVar = (_targetVariable ?? "").Trim();
            if (string.IsNullOrEmpty(targetVar)) return;

            bool isIntTarget = store.HasInt(targetVar);
            bool isBoolTarget = store.HasBool(targetVar);

            store.TryGetDictValue((_dictName ?? "").Trim(), _key ?? "", out string raw);

            if (isIntTarget)
            {
                if (int.TryParse(raw, out int v)) store.SetIntExclusive(targetVar, v);
                else if (TimelineVariableStore.TryParseBoolText(raw, out bool bv)) store.SetIntExclusive(targetVar, bv ? 1 : 0);
                else store.SetIntExclusive(targetVar, 0);
            }
            else if (isBoolTarget)
            {
                if (TimelineVariableStore.TryParseBoolText(raw, out bool b)) store.SetBoolExclusive(targetVar, b);
                else if (int.TryParse(raw, out int n)) store.SetBoolExclusive(targetVar, n != 0);
                else store.SetBoolExclusive(targetVar, false);
            }
            else
                store.SetStringExclusive(targetVar, raw ?? "");
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_dictName)) return "Dict name is empty";
            if (string.IsNullOrWhiteSpace(_targetVariable)) return "Target variable name is empty";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_dictName) + Sep + Esc(_key) + Sep + Esc(_targetVariable);
        }

        public override void DeserializePayload(string payload)
        {
            _dictName = "";
            _key = "";
            _targetVariable = "";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _dictName       = p[0];
            if (p.Length >= 2) _key            = p[1];
            if (p.Length >= 3) _targetVariable = p[2];
        }
    }
}
