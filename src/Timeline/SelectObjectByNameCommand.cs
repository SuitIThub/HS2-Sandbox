using System;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Selects or deselects a workspace object by name. Resolves objects via
    /// <see cref="StudioObjectTreeResolution"/> (same scroll content as other object-list commands).
    /// Mode Select: OnDeselect then OnClickSelect. Mode Deselect: OnDeselect. Mode Toggle: OnClickSelect only.
    /// </summary>
    public class SelectObjectByNameCommand : TimelineCommand
    {
        private const char Sep = '\u0001';

        // 0 = select, 1 = deselect, 2 = toggle
        private static readonly string[] ModeLabels = { "Select", "Deselect", "Toggle" };

        public override string TypeId => "select_object_by_name";
        public override string GetDisplayLabel() => "Select Object";

        private string _objectName = "";
        private int _mode = 2;

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Object", GUILayout.Width(45));
            _objectName = GUILayout.TextField(_objectName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(45));
            if (GUILayout.Button(ModeLabels[_mode], GUILayout.Width(72)))
                _mode = (_mode + 1) % ModeLabels.Length;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolvedName = ctx.Variables.Interpolate(_objectName ?? "");

            TreeNodeObject? target = StudioObjectTreeResolution.FindTreeNodeByName(resolvedName);

            if (target == null)
            {
                SandboxServices.Log.LogWarning($"SelectObjectByName: No object found with name '{resolvedName}'.");
                onComplete();
                return;
            }

            try
            {
                switch (_mode)
                {
                    case 0: // select: ensure not already selected, then select
                        target.Select(true);
                        break;
                    case 1:
                        target.Select(false);
                        break;
                    default:
                        target.OnClickSelect();
                        break;
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"SelectObjectByName: selection action failed for '{resolvedName}' ({ModeLabels[_mode]}). {ex.Message}");
            }

            onComplete();
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_objectName) + Sep + _mode;
        }

        public override void DeserializePayload(string payload)
        {
            _objectName = "";
            _mode = 2;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length == 1)
            {
                _objectName = p[0];
                return;
            }
            if (p.Length >= 1) _objectName = p[0];
            if (p.Length >= 2 && int.TryParse(p[1], out int m) && m >= 0 && m < ModeLabels.Length) _mode = m;
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars != null && !vars.IsValidInterpolation(_objectName ?? ""))
                return "Unknown variable in object name";
            return null;
        }
    }
}
