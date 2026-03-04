using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Vnge scene navigation: previous scene. Calls VngePython.PrevScene().
    /// </summary>
    public class VngePrevSceneCommand : TimelineCommand
    {
        public override string TypeId => "vnge_prev_scene";
        public override string GetDisplayLabel() => "PrevScene";

        public override void DrawInlineConfig(InlineDrawContext ctx) =>
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            VngePython.PrevScene();
            onComplete();
        }

        public override string SerializePayload() => "";
        public override void DeserializePayload(string payload) { }
    }
}
