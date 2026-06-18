using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Lays out a toolbar of buttons/labels that wraps onto new rows when it exceeds a
    /// given width. Shared helper for module action bars (mirrors the Pose Browser pattern).</summary>
    internal sealed class ActionBarWrapLayout
    {
        private const float ButtonPad = 12f;
        private const float LabelPad = 4f;

        private float _wrapWidth;
        private float _used;
        private bool _rowOpen;
        private float _gap;
        private readonly GUIStyle _buttonStyle;
        private readonly GUIStyle _labelStyle;

        public ActionBarWrapLayout()
        {
            _buttonStyle = GUI.skin.button;
            _labelStyle = GUI.skin.label;
        }

        public void Begin(float wrapWidth, float gap = 6f)
        {
            _wrapWidth = Mathf.Max(80f, wrapWidth);
            _gap = gap;
            _used = 0f;
            _rowOpen = false;
        }

        public float MeasureLabel(string text, float minWidth = 0f) =>
            Mathf.Max(minWidth, _labelStyle.CalcSize(new GUIContent(text)).x + LabelPad);

        public float MeasureButton(string text, float minWidth) =>
            Mathf.Max(minWidth, _buttonStyle.CalcSize(new GUIContent(text)).x + ButtonPad);

        public void AddButton(
            string text,
            float height,
            float minWidth,
            Action onClick,
            bool enabled = true,
            string? tooltip = null)
        {
            float width = MeasureButton(text, minWidth);
            var content = new GUIContent(text, tooltip ?? string.Empty);
            Add(width, () =>
            {
                bool prev = GUI.enabled;
                GUI.enabled = enabled;
                if (GUILayout.Button(content, GUILayout.Height(height), GUILayout.Width(width)))
                    onClick();
                GUI.enabled = prev;
            });
        }

        public void AddLabel(string text, float height, float minWidth = 0f)
        {
            float width = MeasureLabel(text, minWidth);
            Add(width, () => GUILayout.Label(text, GUILayout.Width(width), GUILayout.Height(height)));
        }

        public void Add(float width, Action draw)
        {
            float reserve = (_rowOpen ? _gap : 0f) + width;
            if (_rowOpen && _used + reserve > _wrapWidth + 0.5f)
                EndRow();

            if (!_rowOpen)
            {
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(_wrapWidth), GUILayout.ExpandWidth(false));
                _rowOpen = true;
                _used = 0f;
            }
            else
            {
                GUILayout.Space(_gap);
                _used += _gap;
            }

            draw();
            _used += Mathf.Max(width, GUILayoutUtility.GetLastRect().width);
        }

        public void End() => EndRow();

        private void EndRow()
        {
            if (!_rowOpen)
                return;
            GUILayout.EndHorizontal();
            _rowOpen = false;
            _used = 0f;
        }
    }
}
