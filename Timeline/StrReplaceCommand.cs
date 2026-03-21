using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Replaces occurrences of a substring inside a string variable with another substring.
    /// Variable name, find pattern, and replacement all support variable interpolation.
    /// Supports replacing the first occurrence only or all occurrences.
    /// </summary>
    public class StrReplaceCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "str_replace";
        public override string GetDisplayLabel() => "Str Replace";

        private string _variableName = "";
        private string _find = "";
        private string _replace = "";
        private bool _replaceAll = true;

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Variable", GUILayout.Width(52));
            _variableName = GUILayout.TextField(_variableName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Find", GUILayout.Width(52));
            _find = GUILayout.TextField(_find ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Replace", GUILayout.Width(52));
            _replace = GUILayout.TextField(_replace ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(_replaceAll ? "All" : "First", GUILayout.Width(40)))
                _replaceAll = !_replaceAll;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string varName = (_variableName ?? "").Trim();
            if (string.IsNullOrEmpty(varName))
            {
                HS2SandboxPlugin.Log.LogWarning("StrReplace: variable name is empty.");
                onComplete();
                return;
            }

            string find    = ctx.Variables.Interpolate(_find ?? "");
            string replace = ctx.Variables.Interpolate(_replace ?? "");
            string current = ctx.Variables.GetString(varName);

            string result;
            if (_replaceAll)
                result = current.Replace(find, replace);
            else
                ReplaceFirst(current, find, replace, out result);

            ctx.Variables.SetString(varName, result);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string varName = (_variableName ?? "").Trim();
            if (string.IsNullOrEmpty(varName)) return;

            string find    = store.Interpolate(_find ?? "");
            string replace = store.Interpolate(_replace ?? "");
            string current = store.GetString(varName);

            string result;
            if (_replaceAll)
                result = current.Replace(find, replace);
            else
                ReplaceFirst(current, find, replace, out result);

            store.SetString(varName, result);
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) return "Variable name is empty";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_variableName) + Sep + Esc(_find) + Sep + Esc(_replace) + Sep + (_replaceAll ? "1" : "0");
        }

        public override void DeserializePayload(string payload)
        {
            _variableName = "";
            _find         = "";
            _replace      = "";
            _replaceAll   = true;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _variableName = p[0];
            if (p.Length >= 2) _find         = p[1];
            if (p.Length >= 3) _replace      = p[2];
            if (p.Length >= 4) _replaceAll   = p[3] != "0";
        }

        private static void ReplaceFirst(string source, string find, string replacement, out string result)
        {
            if (string.IsNullOrEmpty(find)) { result = source; return; }
            int pos = source.IndexOf(find, StringComparison.Ordinal);
            if (pos < 0) { result = source; return; }
            result = source.Substring(0, pos) + replacement + source.Substring(pos + find.Length);
        }
    }
}
