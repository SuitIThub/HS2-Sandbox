using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class SimulateMouseCommand : TimelineCommand
    {
        /// <summary>Delay in seconds between moving the cursor and performing the click (avoids click firing before move is applied).</summary>
        private const float MoveToClickDelaySeconds = 0.05f;
        public override string TypeId => "simulate_mouse";
        private int _button; // 0 left, 1 right, 2 middle
        private int _screenX;
        private int _screenY;
        private bool _hasValue;

        public int Button => _button;
        public int ScreenX => _screenX;
        public int ScreenY => _screenY;
        public bool HasValue => _hasValue;

        public void SetRecorded(int button, int screenX, int screenY)
        {
            _button = button;
            _screenX = screenX;
            _screenY = screenY;
            _hasValue = true;
        }

        public override string GetDisplayLabel() => "Simulate Mouse";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            string preview = _hasValue ? $"Btn {_button} @ ({_screenX}, {_screenY})" : "(not recorded)";
            GUILayout.Label(preview, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
            if (ctx.RecordMouse != null && GUILayout.Button("Record", GUILayout.Width(55)))
                ctx.RecordMouse();
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!_hasValue)
            {
                onComplete();
                return;
            }
            ctx.Runner.StartCoroutine(MoveThenClickAfterDelay(onComplete));
        }

        private IEnumerator MoveThenClickAfterDelay(Action onComplete)
        {
            WindowsInput.SetMousePosition(_screenX, _screenY);
            float endTime = Time.realtimeSinceStartup + MoveToClickDelaySeconds;
            while (Time.realtimeSinceStartup < endTime)
                yield return null;
            WindowsInput.SimulateMouseButton(_button, false);
            WindowsInput.SimulateMouseButton(_button, true);
            onComplete();
        }

        public override string SerializePayload()
        {
            return _hasValue ? $"{_button},{_screenX},{_screenY}" : "";
        }

        public override void DeserializePayload(string payload)
        {
            _hasValue = false;
            if (string.IsNullOrWhiteSpace(payload)) return;
            string[] p = payload.Split(',');
            if (p.Length >= 3 && int.TryParse(p[0].Trim(), out int b) && int.TryParse(p[1].Trim(), out int x) && int.TryParse(p[2].Trim(), out int y))
            {
                _button = b;
                _screenX = x;
                _screenY = y;
                _hasValue = true;
            }
        }
    }
}
