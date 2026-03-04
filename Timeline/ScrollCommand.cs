using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Scrolls the mouse wheel up or down at the current cursor position (no move).
    /// </summary>
    public class ScrollCommand : TimelineCommand
    {
        public override string TypeId => "scroll";
        private const int WheelDeltaOneNotch = 120;
        private bool _scrollUp = true; // true = up, false = down

        public bool ScrollUp
        {
            get => _scrollUp;
            set => _scrollUp = value;
        }

        public override string GetDisplayLabel() => _scrollUp ? "Scroll Up" : "Scroll Down";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Direction:", GUILayout.Width(55));
            bool up = GUILayout.Toggle(_scrollUp, "Up", GUILayout.Width(40));
            bool down = GUILayout.Toggle(!_scrollUp, "Down", GUILayout.Width(45));
            if (up && !_scrollUp) _scrollUp = true;
            if (down && _scrollUp) _scrollUp = false;
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            int delta = _scrollUp ? WheelDeltaOneNotch : -WheelDeltaOneNotch;
            WindowsInput.SimulateScroll(delta);
            onComplete();
        }

        public override string SerializePayload() => _scrollUp ? "1" : "0";

        public override void DeserializePayload(string payload)
        {
            _scrollUp = true;
            if (payload?.Trim() == "0") _scrollUp = false;
        }
    }
}
