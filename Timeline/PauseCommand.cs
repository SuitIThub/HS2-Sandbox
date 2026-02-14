using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PauseCommand : TimelineCommand
    {
        public override string TypeId => "pause";
        private int _milliseconds = 500;

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
            string s = GUILayout.TextField(_milliseconds.ToString(), GUILayout.Width(60));
            if (int.TryParse(s, out int ms) && ms >= 0)
                _milliseconds = ms;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            ctx.Runner.StartCoroutine(PauseRoutine(onComplete));
        }

        private IEnumerator PauseRoutine(Action onComplete)
        {
            float endTime = Time.realtimeSinceStartup + (_milliseconds / 1000f);
            while (Time.realtimeSinceStartup < endTime)
                yield return null;
            onComplete();
        }

        public override string SerializePayload() => _milliseconds.ToString();

        public override void DeserializePayload(string payload)
        {
            if (int.TryParse(payload?.Trim(), out int ms) && ms >= 0)
                _milliseconds = ms;
        }
    }
}
