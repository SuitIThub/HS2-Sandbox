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

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target:", GUILayout.Width(40));
            _checkpointName = GUILayout.TextField(_checkpointName, GUILayout.ExpandWidth(true));
            GUILayout.Label("×", GUILayout.Width(12));
            string countStr = GUILayout.TextField(_repeatCount.ToString(), GUILayout.Width(36));
            if (int.TryParse(countStr, out int c) && c >= 0)
                _repeatCount = c;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string key = _checkpointName.Trim();
            if (string.IsNullOrEmpty(key))
            {
                onComplete();
                return;
            }
            int current = ctx.LoopCounts.TryGetValue(key, out int n) ? n : 0;
            if (current < _repeatCount - 1)
            {
                ctx.LoopCounts[key] = current + 1;
                ctx.SetJumpTarget(_checkpointName);
            }
            else
            {
                ctx.LoopCounts[key] = 0;
            }
            onComplete();
        }

        public override string SerializePayload()
        {
            return _checkpointName + "|" + _repeatCount;
        }

        public override void DeserializePayload(string payload)
        {
            _checkpointName = "";
            _repeatCount = 1;
            if (string.IsNullOrWhiteSpace(payload)) return;
            int sep = payload.IndexOf('|');
            if (sep >= 0)
            {
                _checkpointName = payload.Substring(0, sep).Trim();
                if (int.TryParse(payload.Substring(sep + 1).Trim(), out int c) && c >= 0)
                    _repeatCount = c;
            }
            else
                _checkpointName = payload.Trim();
        }
    }
}
