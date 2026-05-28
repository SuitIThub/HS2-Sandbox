using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Waits until the user presses a Confirm button in the timeline list, then optionally clicks (0,0) to release focus and continues.
    /// </summary>
    public class ConfirmCommand : TimelineCommand
    {
        private bool _refocus = true;

        public override string TypeId => "confirm";

        public override string GetDisplayLabel() => "Confirm";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label("Press Confirm in list when running", GUILayout.ExpandWidth(true));
            _refocus = GUILayout.Toggle(_refocus, "Refocus", GUILayout.Width(80));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            ctx.PendingConfirmCallback = () =>
            {
                if (_refocus)
                {
                    WindowsInput.SimulateMouseClickAt(0, 0, 0);
                }
                onComplete();
            };
        }

        public override string SerializePayload() => _refocus ? "refocus" : "";

        public override void DeserializePayload(string payload)
        {
            _refocus = string.IsNullOrEmpty(payload) || payload == "refocus";
        }
    }
}
