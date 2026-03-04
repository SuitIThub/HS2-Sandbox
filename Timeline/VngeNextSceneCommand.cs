using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Vnge scene navigation: next scene. Calls VngePython.NextScene().
    /// </summary>
    public class VngeNextSceneCommand : TimelineCommand
    {
        public override string TypeId => "vnge_next_scene";
        public override string GetDisplayLabel() => "NextScene";

        public override void DrawInlineConfig(InlineDrawContext ctx) =>
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            VngePython.NextScene();
            onComplete();
        }

        public override string SerializePayload() => "";
        public override void DeserializePayload(string payload) { }
    }
}
