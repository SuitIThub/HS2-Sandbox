using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Moves the cursor to a recorded screen position (no click).
    /// </summary>
    public class MoveMouseCommand : TimelineCommand
    {
        public override string TypeId => "move_mouse";
        private int _screenX;
        private int _screenY;
        private bool _hasValue;

        public int ScreenX => _screenX;
        public int ScreenY => _screenY;
        public bool HasValue => _hasValue;

        public void SetRecordedPosition(int screenX, int screenY)
        {
            _screenX = screenX;
            _screenY = screenY;
            _hasValue = true;
        }

        public override string GetDisplayLabel() => "Move Mouse";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            string preview = _hasValue ? $"({_screenX}, {_screenY})" : "(not recorded)";
            GUILayout.Label(preview, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60));
            if (ctx.RecordMouse != null && GUILayout.Button("Record", GUILayout.Width(55)))
                ctx.RecordMouse();
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (_hasValue)
                WindowsInput.SetMousePosition(_screenX, _screenY);
            onComplete();
        }

        public override string SerializePayload() => _hasValue ? $"{_screenX},{_screenY}" : "";

        public override void DeserializePayload(string payload)
        {
            _hasValue = false;
            if (string.IsNullOrWhiteSpace(payload)) return;
            string[] p = payload.Split(',');
            if (p.Length >= 2 && int.TryParse(p[0].Trim(), out int x) && int.TryParse(p[1].Trim(), out int y))
            {
                _screenX = x;
                _screenY = y;
                _hasValue = true;
            }
        }
    }
}
