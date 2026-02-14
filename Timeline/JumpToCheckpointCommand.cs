using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class JumpToCheckpointCommand : TimelineCommand
    {
        public override string TypeId => "jump";
        private string _checkpointName = "";

        public string CheckpointName
        {
            get => _checkpointName;
            set => _checkpointName = value ?? "";
        }

        public override string GetDisplayLabel() => "Jump to checkpoint";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target:", GUILayout.Width(40));
            _checkpointName = GUILayout.TextField(_checkpointName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            ctx.SetJumpTarget(_checkpointName);
            onComplete();
        }

        public override string SerializePayload() => _checkpointName ?? "";

        public override void DeserializePayload(string payload)
        {
            _checkpointName = payload?.Trim() ?? "";
        }
    }
}
