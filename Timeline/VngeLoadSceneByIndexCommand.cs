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

        private string _indexText = "0";

        public override string GetDisplayLabel() => "Load scene by index";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Index", GUILayout.Width(48));
            _indexText = GUILayout.TextField(_indexText ?? "0", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ctx.Variables.TryResolveIntOperand(_indexText ?? "0", out int index))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }
            VngePython.LoadSceneByIndex(index);
            onComplete();
        }

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (base.HasInvalidConfiguration(variablesAtThisIndex)) return true;
            if (variablesAtThisIndex == null) return false;
            if (string.IsNullOrWhiteSpace(_indexText)) return true;
            return !variablesAtThisIndex.IsValidIntOperand(_indexText);
        }

        public override string SerializePayload() => _indexText ?? "0";

        public override void DeserializePayload(string payload) => _indexText = payload ?? "0";
    }
}
