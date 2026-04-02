using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Removes an element from a list variable either by index or by value.
    /// List name and value support variable interpolation. Index supports int variable names and literals.
    /// </summary>
    public class ListRemoveCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        // 0 = by index, 1 = by value
        private static readonly string[] ModeLabels = { "By Index", "By Value" };

        public override string TypeId => "list_remove";
        public override string GetDisplayLabel() => "List Remove";

        private string _listName = "";
        private int _mode; // 0=index, 1=value
        private string _operand = "0"; // index literal/var -or- value string

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("List", GUILayout.Width(32));
            _listName = GUILayout.TextField(_listName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(32));
            if (GUILayout.Button(ModeLabels[_mode], GUILayout.Width(70)))
                _mode = (_mode + 1) % ModeLabels.Length;
            GUILayout.Label(_mode == 0 ? "Index" : "Value", GUILayout.Width(36));
            _operand = GUILayout.TextField(_operand ?? "", GUILayout.MinWidth(50), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string listName = ctx.Variables.Interpolate(_listName ?? "").Trim();

            if (string.IsNullOrEmpty(listName))
            {
                SandboxServices.Log.LogWarning("ListRemove: list name is empty.");
                onComplete();
                return;
            }

            List<string> list = ctx.Variables.GetList(listName);

            if (_mode == 0) // by index
            {
                if (!ctx.Variables.TryResolveIntOperand(_operand ?? "0", out int idx))
                {
                    ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                    return;
                }
                if (idx >= 0 && idx < list.Count)
                    list.RemoveAt(idx);
                else
                    SandboxServices.Log.LogWarning($"ListRemove: index {idx} out of range (list '{listName}' has {list.Count} elements).");
            }
            else // by value
            {
                string value = ctx.Variables.Interpolate(_operand ?? "");
                if (!list.Remove(value))
                    SandboxServices.Log.LogWarning($"ListRemove: value \"{value}\" not found in list '{listName}'.");
            }

            ctx.Variables.SetList(listName, list);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string listName = (_listName ?? "").Trim();
            if (string.IsNullOrEmpty(listName)) return;

            List<string> list = store.GetList(listName);

            if (_mode == 0)
            {
                int idx = store.ResolveIntOperand(_operand ?? "0");
                if (idx >= 0 && idx < list.Count)
                    list.RemoveAt(idx);
            }
            else
            {
                string value = store.Interpolate(_operand ?? "");
                list.Remove(value);
            }

            store.SetList(listName, list);
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_listName)) return "List name is empty";
            if (_mode == 0 && vars != null && !string.IsNullOrWhiteSpace(_operand) && !vars.IsValidIntOperand(_operand))
                return "Invalid index";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_listName) + Sep + _mode + Sep + Esc(_operand);
        }

        public override void DeserializePayload(string payload)
        {
            _listName = "";
            _mode     = 0;
            _operand  = "0";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _listName = p[0];
            if (p.Length >= 2 && int.TryParse(p[1], out int m) && m >= 0 && m < ModeLabels.Length) _mode = m;
            if (p.Length >= 3) _operand  = string.IsNullOrEmpty(p[2]) ? "0" : p[2];
        }
    }
}
