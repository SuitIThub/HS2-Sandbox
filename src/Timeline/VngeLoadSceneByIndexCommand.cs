using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// VNGE: load scene by index. Calls VngePython.LoadSceneByIndex(index).
    /// </summary>
    public class VngeLoadSceneByIndexCommand : TimelineCommand
    {
        public override string TypeId => "vnge_load_scene";

        private string _indexText = "1";

        public override string GetDisplayLabel() => "Load scene by index";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Index", GUILayout.Width(48));
            _indexText = GUILayout.TextField(_indexText ?? "1", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ctx.Variables.TryResolveIntOperand(_indexText ?? "1", out int index))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }
            VngePython.LoadSceneByIndex(index - 1);
            onComplete();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_indexText)) return "Index is empty";
            if (vars == null) return null;
            if (!vars.IsValidIntOperand(_indexText)) return "Invalid scene index";
            if (!vars.TryResolveIntOperand(_indexText, out int index)) return "Invalid scene index";
            int max = VngePython.GetMaxSceneIndex() + 1;
            if (index < 1 || index > max) return $"Scene index {index} out of range (1–{max})";
            return null;
        }

        public override string SerializePayload() => _indexText ?? "1";

        public override void DeserializePayload(string payload) => _indexText = payload ?? "1";
    }
}
