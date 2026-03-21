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
        private int _level; // 0 = highest (black); higher = lower visual weight (darker gray → lighter gray)
        private string _levelFieldText = "0"; // backing text for the level input field

        /// <summary>
        /// Hierarchy level: 0 is the topmost separator (pure black); each step up makes the row progressively more gray.
        /// ActionTimeline uses this to shade the row background.
        /// </summary>
        public int Level => _level;

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
            {
                _labelText = GUILayout.TextField(_labelText ?? "", GUILayout.Width(100), GUILayout.Height(18));
                GUILayout.Label("Lv", GUILayout.Width(16));
                string newLevelText = GUILayout.TextField(_levelFieldText, GUILayout.Width(28), GUILayout.Height(18));
                if (newLevelText != _levelFieldText)
                {
                    _levelFieldText = newLevelText;
                    if (int.TryParse(newLevelText, out int parsed) && parsed >= 0)
                        _level = parsed;
                }
            }
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            onComplete();
        }

        public override string SerializePayload()
        {
            string text = (_labelText ?? "").Replace("\u0001", "");
            return _level == 0 ? text : text + "\u0001" + _level;
        }

        public override void DeserializePayload(string payload)
        {
            _level = 0;
            _levelFieldText = "0";
            if (string.IsNullOrEmpty(payload))
            {
                _labelText = "";
                return;
            }
            int sep = payload.IndexOf('\u0001');
            if (sep < 0)
            {
                _labelText = payload;
                return;
            }
            _labelText = payload.Substring(0, sep);
            if (int.TryParse(payload.Substring(sep + 1), out int lv) && lv >= 0)
            {
                _level = lv;
                _levelFieldText = lv.ToString();
            }
        }
    }
}
