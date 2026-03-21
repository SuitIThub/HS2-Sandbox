using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// For each element in a source list variable, looks it up as a key in a dictionary variable
    /// and collects the resulting values into a target list variable.
    /// Elements whose keys are absent in the dict produce an empty string entry.
    /// All name fields support variable interpolation.
    /// </summary>
    public class ListApplyDictCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "list_apply_dict";
        public override string GetDisplayLabel() => "List Apply Dict";

        private string _sourceList = "";
        private string _dictName = "";
        private string _targetList = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Source list", GUILayout.Width(64));
            _sourceList = GUILayout.TextField(_sourceList ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Dict", GUILayout.Width(64));
            _dictName = GUILayout.TextField(_dictName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target list", GUILayout.Width(64));
            _targetList = GUILayout.TextField(_targetList ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string sourceList = ctx.Variables.Interpolate(_sourceList ?? "").Trim();
            string dictName   = ctx.Variables.Interpolate(_dictName ?? "").Trim();
            string targetList = (_targetList ?? "").Trim();

            if (string.IsNullOrEmpty(sourceList))
            {
                HS2SandboxPlugin.Log.LogWarning("ListApplyDict: source list name is empty.");
                onComplete();
                return;
            }
            if (string.IsNullOrEmpty(dictName))
            {
                HS2SandboxPlugin.Log.LogWarning("ListApplyDict: dict name is empty.");
                onComplete();
                return;
            }
            if (string.IsNullOrEmpty(targetList))
            {
                HS2SandboxPlugin.Log.LogWarning("ListApplyDict: target list name is empty.");
                onComplete();
                return;
            }

            List<string> keys = ctx.Variables.GetList(sourceList);
            var result = new List<string>(keys.Count);
            foreach (string key in keys)
            {
                ctx.Variables.TryGetDictValue(dictName, key, out string value);
                result.Add(value ?? key);
            }

            ctx.Variables.SetList(targetList, result);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string targetList = (_targetList ?? "").Trim();
            if (string.IsNullOrEmpty(targetList)) return;

            string sourceList = (_sourceList ?? "").Trim();
            string dictName   = (_dictName ?? "").Trim();

            List<string> keys = string.IsNullOrEmpty(sourceList) ? new List<string>() : store.GetList(sourceList);
            var result = new List<string>(keys.Count);
            foreach (string key in keys)
            {
                store.TryGetDictValue(dictName, key, out string value);
                result.Add(value ?? key);
            }

            store.SetList(targetList, result);
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_sourceList)) return "Source list name is empty";
            if (string.IsNullOrWhiteSpace(_dictName)) return "Dict name is empty";
            if (string.IsNullOrWhiteSpace(_targetList)) return "Target list name is empty";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_sourceList) + Sep + Esc(_dictName) + Sep + Esc(_targetList);
        }

        public override void DeserializePayload(string payload)
        {
            _sourceList = "";
            _dictName   = "";
            _targetList = "";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _sourceList = p[0];
            if (p.Length >= 2) _dictName   = p[1];
            if (p.Length >= 3) _targetList = p[2];
        }
    }
}
