using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Reads a value from a dictionary variable by key and stores it in an existing variable.
    /// The target type is inferred from the variable: if it is a known integer variable the raw
    /// string is parsed as int (0 on failure); otherwise it is stored as a string.
    /// Dictionary name and key support variable interpolation.
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
                HS2SandboxPlugin.Log.LogWarning("DictGet: dict name is empty.");
                onComplete();
                return;
            }
            if (string.IsNullOrEmpty(targetVar))
            {
                HS2SandboxPlugin.Log.LogWarning("DictGet: target variable name is empty.");
                onComplete();
                return;
            }

            bool isIntTarget = ctx.Variables.HasInt(targetVar);

            if (!ctx.Variables.TryGetDictValue(dictName, key, out string raw))
            {
                HS2SandboxPlugin.Log.LogWarning($"DictGet: key '{key}' not found in dict '{dictName}'.");
                if (isIntTarget) ctx.Variables.SetInt(targetVar, 0);
                else             ctx.Variables.SetString(targetVar, "");
                onComplete();
                return;
            }

            if (isIntTarget)
            {
                if (int.TryParse(raw, out int intVal))
                    ctx.Variables.SetInt(targetVar, intVal);
                else
                {
                    HS2SandboxPlugin.Log.LogWarning($"DictGet: value '{raw}' for key '{key}' in dict '{dictName}' could not be parsed as int. Storing 0.");
                    ctx.Variables.SetInt(targetVar, 0);
                }
            }
            else
            {
                ctx.Variables.SetString(targetVar, raw);
            }

            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string targetVar = (_targetVariable ?? "").Trim();
            if (string.IsNullOrEmpty(targetVar)) return;

            bool isIntTarget = store.HasInt(targetVar);

            store.TryGetDictValue((_dictName ?? "").Trim(), _key ?? "", out string raw);

            if (isIntTarget)
                store.SetInt(targetVar, int.TryParse(raw, out int v) ? v : 0);
            else
                store.SetString(targetVar, raw ?? "");
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
