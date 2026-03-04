using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PauseCommand : TimelineCommand
    {
        public override string TypeId => "pause";
        private int _milliseconds = 500;
        private string _millisecondsText = "500";

        public int Milliseconds
        {
            get => _milliseconds;
            set => _milliseconds = Mathf.Max(0, value);
        }

        public override string GetDisplayLabel() => "Pause";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("ms", GUILayout.Width(20));
            _millisecondsText = GUILayout.TextField(_millisecondsText, GUILayout.Width(60));
            if (int.TryParse(_millisecondsText, out int ms) && ms >= 0)
                _milliseconds = ms;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ctx.Variables.TryResolveIntOperand(_millisecondsText, out int msVal))
            {
                ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
                return;
            }
            int ms = Mathf.Max(0, msVal);
            ctx.Runner.StartCoroutine(PauseRoutine(ms, onComplete));
        }

        private IEnumerator PauseRoutine(int milliseconds, Action onComplete)
        {
            float endTime = Time.realtimeSinceStartup + (milliseconds / 1000f);
            while (Time.realtimeSinceStartup < endTime)
                yield return null;
            onComplete();
        }

        public override string SerializePayload() => _millisecondsText;

        public override void DeserializePayload(string payload)
        {
            _millisecondsText = "500";
            if (int.TryParse(payload?.Trim(), out int ms) && ms >= 0)
            {
                _milliseconds = ms;
                _millisecondsText = payload!.Trim();
            }
        }

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (variablesAtThisIndex == null) return false;
            if (string.IsNullOrWhiteSpace(_millisecondsText)) return true;
            return !variablesAtThisIndex.IsValidIntOperand(_millisecondsText);
        }
    }
}
