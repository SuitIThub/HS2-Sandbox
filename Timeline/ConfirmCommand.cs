using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Waits until the user presses a Confirm button in the timeline list, then clicks (0,0) to release focus and continues.
    /// </summary>
    public class ConfirmCommand : TimelineCommand
    {
        public override string TypeId => "confirm";

        public override string GetDisplayLabel() => "Confirm";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label("Press Confirm in list when running", GUILayout.ExpandWidth(true));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            ctx.PendingConfirmCallback = () =>
            {
                WindowsInput.SimulateMouseClickAt(0, 0, 0);
                onComplete();
            };
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}
