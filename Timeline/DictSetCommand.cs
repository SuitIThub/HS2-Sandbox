using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets a key/value pair inside a dictionary variable. The dictionary is created if it does not exist.
    /// Dictionary name, key, and value all support variable interpolation.
    /// </summary>
    public class DictSetCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "dict_set";
        public override string GetDisplayLabel() => "Dict Set";

        private string _dictName = "";
        private string _key = "";
        private string _value = "";

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
            GUILayout.Label("Value", GUILayout.Width(32));
            _value = GUILayout.TextField(_value ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string dictName = ctx.Variables.Interpolate(_dictName ?? "").Trim();
            string key      = ctx.Variables.Interpolate(_key ?? "");
            string value    = ctx.Variables.Interpolate(_value ?? "");

            if (string.IsNullOrEmpty(dictName))
            {
                SandboxServices.Log.LogWarning("DictSet: dict name is empty.");
                onComplete();
                return;
            }

            ctx.Variables.SetDictValue(dictName, key, value);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string dictName = (_dictName ?? "").Trim();
            if (string.IsNullOrEmpty(dictName)) return;
            store.SetDictValue(dictName, _key ?? "", _value ?? "");
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_dictName)) return "Dict name is empty";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_dictName) + Sep + Esc(_key) + Sep + (_value ?? "").Replace(Sep.ToString(), "");
        }

        public override void DeserializePayload(string payload)
        {
            _dictName = "";
            _key = "";
            _value = "";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _dictName = p[0];
            if (p.Length >= 2) _key      = p[1];
            if (p.Length >= 3) _value    = p[2];
        }
    }
}
