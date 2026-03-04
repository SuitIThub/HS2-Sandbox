using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Visual separator: draws a long line and optional label text. Does nothing when executed.
    /// Has a tiny button to toggle a text field for renaming/organization. Row background is pitch black.
    /// </summary>
    public class LabelCommand : TimelineCommand
    {
        public override string TypeId => "label";

        private string _labelText = "";
        private bool _showRenameField;

        /// <summary>Empty so the timeline shows nothing in the label column; the actual text is centered in the row body.</summary>
        public override string GetDisplayLabel() => "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            Color prevColor = GUI.color;
            GUI.color = Color.white;
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUI.color = prevColor;
            string displayText = string.IsNullOrWhiteSpace(_labelText) ? "" : _labelText.Trim();
            GUILayout.Label(displayText, GUILayout.ExpandWidth(false));
            GUI.color = Color.white;
            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            GUI.color = prevColor;
            if (GUILayout.Button("\u270e", GUILayout.Width(16), GUILayout.Height(16)))
                _showRenameField = !_showRenameField;
            if (_showRenameField)
                _labelText = GUILayout.TextField(_labelText ?? "", GUILayout.Width(100), GUILayout.Height(18));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            onComplete();
        }

        public override string SerializePayload() => _labelText ?? "";

        public override void DeserializePayload(string payload) => _labelText = payload ?? "";
    }
}
