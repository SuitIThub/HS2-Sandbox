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
            ctx.SetJumpTarget(ctx.Variables.Interpolate(_checkpointName ?? ""));
            onComplete();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars == null) return null;
            if (!vars.IsValidInterpolation(_checkpointName ?? ""))
                return "Unknown variable in checkpoint name";
            string resolved = vars.Interpolate(_checkpointName ?? "").Trim();
            if (!string.IsNullOrEmpty(resolved) && !vars.HasCheckpoint(resolved))
                return $"Checkpoint \"{resolved}\" not found";
            return null;
        }

        public override string SerializePayload() => _checkpointName ?? "";

        public override void DeserializePayload(string payload)
        {
            _checkpointName = payload?.Trim() ?? "";
        }
    }
}
