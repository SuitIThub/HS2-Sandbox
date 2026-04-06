using System;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets visibility on a workspace object by name. Uses <see cref="StudioObjectTreeResolution"/>,
    /// then calls <see cref="TreeNodeObject.SetVisible"/>. The visible argument resolves like a bool
    /// operand (literals, variables, <c>[interpolation]</c>).
    /// </summary>
    public class SetObjectVisibleByNameCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        public override string TypeId => "set_object_visible_by_name";
        public override string GetDisplayLabel() => "Object Visible";

        private string _objectName = "";
        private string _visibleText = "True";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Object", GUILayout.Width(45));
            _objectName = GUILayout.TextField(_objectName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Visible", GUILayout.Width(45));
            _visibleText = GUILayout.TextField(_visibleText ?? "True", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolvedName = ctx.Variables.Interpolate(_objectName ?? "");

            TreeNodeObject? target = StudioObjectTreeResolution.FindTreeNodeByName(resolvedName);

            if (target == null)
            {
                SandboxServices.Log.LogWarning($"SetObjectVisibleByName: No object found with name '{resolvedName}'.");
                onComplete();
                return;
            }

            string visOperand = string.IsNullOrWhiteSpace(_visibleText) ? "True" : _visibleText.Trim();
            if (!ctx.Variables.TryResolveBoolOperand(visOperand, out bool visible))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }

            try
            {
                target.SetVisible(visible);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"SetObjectVisibleByName: SetVisible failed for '{resolvedName}'. {ex.Message}");
            }

            onComplete();
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_objectName) + Sep + Esc(_visibleText);
        }

        public override void DeserializePayload(string payload)
        {
            _objectName = "";
            _visibleText = "True";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length == 1)
            {
                _objectName = p[0];
                return;
            }
            if (p.Length >= 1) _objectName = p[0];
            if (p.Length >= 2) _visibleText = string.IsNullOrEmpty(p[1]) ? "True" : p[1];
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars != null && !vars.IsValidInterpolation(_objectName ?? ""))
                return "Unknown variable in object name";
            if (!string.IsNullOrWhiteSpace(_visibleText) && vars != null && !vars.IsValidBoolOperand(_visibleText))
                return "Invalid visible value";
            return null;
        }
    }
}
