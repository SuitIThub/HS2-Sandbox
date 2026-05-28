using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Exits the current subtimeline and returns to the parent, or stops the run if at root.
    /// </summary>
    public class ReturnCommand : TimelineCommand
    {
        public override string TypeId => "return";
        public override string GetDisplayLabel() => "Return";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(ctx.IsInSubTimeline ? "Exit Subtimeline" : "Stop Execution", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            ctx.ReturnRequested = true;
            onComplete();
        }

        public override string SerializePayload() => "";
        public override void DeserializePayload(string payload) { }
    }
}
