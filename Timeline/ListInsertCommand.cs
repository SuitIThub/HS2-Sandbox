using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Inserts a value into a list variable at a given index, or appends/prepends it.
    /// List name and value support variable interpolation. Index supports int variable names and literals.
    /// </summary>
    public class ListInsertCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        // 0 = insert at index, 1 = append, 2 = prepend
        private static readonly string[] ModeLabels = { "At index", "Append", "Prepend" };

        public override string TypeId => "list_insert";
        public override string GetDisplayLabel() => "List Insert";

        private string _listName = "";
        private string _value = "";
        private int _mode; // 0=index, 1=append, 2=prepend
        private string _index = "0";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("List", GUILayout.Width(32));
            _listName = GUILayout.TextField(_listName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Value", GUILayout.Width(36));
            _value = GUILayout.TextField(_value ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(32));
            if (GUILayout.Button(ModeLabels[_mode], GUILayout.Width(70)))
                _mode = (_mode + 1) % ModeLabels.Length;
            if (_mode == 0)
            {
                GUILayout.Label("Index", GUILayout.Width(36));
                _index = GUILayout.TextField(_index ?? "0", GUILayout.MinWidth(50), GUILayout.ExpandWidth(true));
            }
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string listName = ctx.Variables.Interpolate(_listName ?? "").Trim();
            string value    = ctx.Variables.Interpolate(_value ?? "");

            if (string.IsNullOrEmpty(listName))
            {
                SandboxServices.Log.LogWarning("ListInsert: list name is empty.");
                onComplete();
                return;
            }

            List<string> list = ctx.Variables.GetList(listName);

            switch (_mode)
            {
                case 1: // append
                    list.Add(value);
                    break;
                case 2: // prepend
                    list.Insert(0, value);
                    break;
                default: // at index
                    if (!ctx.Variables.TryResolveIntOperand(_index ?? "0", out int idx))
                    {
                        ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                        return;
                    }
                    idx = Math.Max(0, Math.Min(idx, list.Count));
                    list.Insert(idx, value);
                    break;
            }

            ctx.Variables.SetList(listName, list);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            string listName = (_listName ?? "").Trim();
            if (string.IsNullOrEmpty(listName)) return;

            List<string> list = store.GetList(listName);
            string value = store.Interpolate(_value ?? "");

            switch (_mode)
            {
                case 1:
                    list.Add(value);
                    break;
                case 2:
                    list.Insert(0, value);
                    break;
                default:
                    int idx = store.ResolveIntOperand(_index ?? "0");
                    idx = Math.Max(0, Math.Min(idx, list.Count));
                    list.Insert(idx, value);
                    break;
            }

            store.SetList(listName, list);
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_listName)) return "List name is empty";
            if (_mode == 0 && vars != null && !string.IsNullOrWhiteSpace(_index) && !vars.IsValidIntOperand(_index))
                return "Invalid index";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_listName) + Sep + Esc(_value) + Sep + _mode + Sep + Esc(_index);
        }

        public override void DeserializePayload(string payload)
        {
            _listName = "";
            _value    = "";
            _mode     = 0;
            _index    = "0";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _listName = p[0];
            if (p.Length >= 2) _value    = p[1];
            if (p.Length >= 3 && int.TryParse(p[2], out int m) && m >= 0 && m < ModeLabels.Length) _mode = m;
            if (p.Length >= 4) _index    = string.IsNullOrEmpty(p[3]) ? "0" : p[3];
        }
    }
}
