using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Jumps to a named checkpoint, but only up to a limited number of times per run.
    /// After the limit is reached, execution continues to the next command instead of jumping.
    /// </summary>
    public class LoopCommand : TimelineCommand
    {
        public override string TypeId => "loop";
        private string _checkpointName = "";
        private int _repeatCount = 1;

        public string CheckpointName
        {
            get => _checkpointName;
            set => _checkpointName = value ?? "";
        }

        public int RepeatCount
        {
            get => _repeatCount;
            set => _repeatCount = Mathf.Max(0, value);
        }

        public override string GetDisplayLabel() => "Loop";

        public override string GetDisplayLabel(TimelineContext? runContext)
        {
            if (runContext == null) return "Loop";
            string key = _checkpointName.Trim();
            if (string.IsNullOrEmpty(key)) return "Loop";
            int current = runContext.LoopCounts.TryGetValue(key, out int n) ? n : 0;
            return $"Loop ({current + 1})";
        }

        private string _repeatCountText = "1";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target:", GUILayout.Width(40));
            _checkpointName = GUILayout.TextField(_checkpointName, GUILayout.ExpandWidth(true));
            GUILayout.Label("×", GUILayout.Width(12));
            _repeatCountText = GUILayout.TextField(_repeatCountText, GUILayout.Width(36));
            if (int.TryParse(_repeatCountText, out int c) && c >= 0)
                _repeatCount = c;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string key = ctx.Variables.Interpolate(_checkpointName ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                onComplete();
                return;
            }
            if (!ctx.Variables.TryResolveIntOperand(_repeatCountText ?? "1", out int repeatCountVal))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }
            int repeatCount = Mathf.Max(0, repeatCountVal);
            int current = ctx.LoopCounts.TryGetValue(key, out int n) ? n : 0;
            if (current < repeatCount - 1)
            {
                ctx.LoopCounts[key] = current + 1;
                ctx.SetJumpTarget(key);
            }
            else
            {
                ctx.LoopCounts[key] = 0;
            }
            onComplete();
        }

        public override string SerializePayload()
        {
            return _checkpointName + "|" + _repeatCountText;
        }

        public override void DeserializePayload(string payload)
        {
            _checkpointName = "";
            _repeatCount = 1;
            _repeatCountText = "1";
            if (string.IsNullOrWhiteSpace(payload)) return;
            int sep = payload.IndexOf('|');
            if (sep >= 0)
            {
                _checkpointName = payload.Substring(0, sep).Trim();
                _repeatCountText = payload.Substring(sep + 1).Trim();
                if (int.TryParse(_repeatCountText, out int c) && c >= 0)
                    _repeatCount = c;
            }
            else
                _checkpointName = payload.Trim();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars == null) return null;
            if (!vars.IsValidInterpolation(_checkpointName ?? "")) return "Unknown variable in target checkpoint";
            if (!string.IsNullOrWhiteSpace(_repeatCountText) && !vars.IsValidIntOperand(_repeatCountText))
                return "Invalid repeat count";
            string resolved = vars.Interpolate(_checkpointName ?? "").Trim();
            if (!string.IsNullOrEmpty(resolved) && !vars.HasCheckpoint(resolved))
                return $"Checkpoint \"{resolved}\" not found";
            return null;
        }
    }
}
