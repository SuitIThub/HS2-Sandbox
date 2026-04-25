using System;
using System.IO;
using BepInEx;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class NotebookWindow : SubWindow
    {
        [Serializable]
        private class NotebookNote
        {
            public string title = "New Note";
            public string content = "";
        }

        [Serializable]
        private class NotebookSaveData
        {
            public NotebookNote[] notes = Array.Empty<NotebookNote>();
            public int selectedIndex = 0;
        }

        private const float MinWidth = 520f;
        private const float MinHeight = 360f;
        private const float MaxWidth = 1100f;
        private const float MaxHeight = 900f;
        private const float ResizeHandleSize = 18f;
        private const int MaxTitleLength = 120;

        private string _savePath = string.Empty;
        private NotebookNote[] _notes = Array.Empty<NotebookNote>();
        private int _selectedIndex;
        private bool _isResizing;
        private bool _dirty;
        private float _nextSaveAt;
        private Vector2 _contentScroll;
        private Vector2 _tabScroll;

        protected override void Start()
        {
            base.Start();
            windowID = 2012;
            windowTitle = "Notebook";
            windowRect = new Rect(420f, 120f, 680f, 500f);
            _savePath = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "notebook.json");
            LoadFromDisk();
        }

        private void Update()
        {
            if (_dirty && Time.unscaledTime >= _nextSaveAt)
            {
                SaveToDisk();
            }
        }

        public override void DrawWindow()
        {
            base.DrawWindow();
            if (!isVisible)
                return;

            HandleResize();
            windowRect.width = Mathf.Clamp(windowRect.width, MinWidth, MaxWidth);
            windowRect.height = Mathf.Clamp(windowRect.height, MinHeight, MaxHeight);
            windowRect.x = Mathf.Clamp(windowRect.x, 4f, Mathf.Max(4f, Screen.width - windowRect.width - 4f));
            windowRect.y = Mathf.Clamp(windowRect.y, 4f, Mathf.Max(4f, Screen.height - windowRect.height - 4f));
        }

        protected override void DrawWindowContent(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            DrawTabBar();
            DrawTitleRow();
            DrawContentEditor();
            DrawBottomRow();

            GUILayout.EndVertical();

            var resizeHandle = new Rect(windowRect.width - ResizeHandleSize, windowRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            GUI.Box(resizeHandle, "◢");

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - ResizeHandleSize, 20f));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            _tabScroll = GUILayout.BeginScrollView(_tabScroll, false, false, GUILayout.Height(34f), GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _notes.Length; i++)
            {
                bool selected = i == _selectedIndex;
                var oldColor = GUI.color;
                if (selected)
                    GUI.color = new Color(0.70f, 0.90f, 1f, 1f);

                string title = string.IsNullOrWhiteSpace(_notes[i].title) ? $"Note {i + 1}" : _notes[i].title;
                if (title.Length > 18)
                    title = title.Substring(0, 15) + "...";

                if (GUILayout.Button(title, GUILayout.Height(24f), GUILayout.MinWidth(95f)))
                    _selectedIndex = i;

                GUI.color = oldColor;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            if (GUILayout.Button("+", GUILayout.Width(28f), GUILayout.Height(24f)))
                AddNote();
            GUILayout.EndHorizontal();
        }

        private void DrawTitleRow()
        {
            var note = GetSelectedNote();
            if (note == null)
                return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Title", GUILayout.Width(38f));
            string newTitle = GUILayout.TextField(note.title ?? "", MaxTitleLength, GUILayout.ExpandWidth(true));
            if (!string.Equals(newTitle, note.title, StringComparison.Ordinal))
            {
                note.title = newTitle;
                MarkDirty();
            }

            GUI.enabled = _notes.Length > 1;
            if (GUILayout.Button("Delete", GUILayout.Width(70f), GUILayout.Height(22f)))
                DeleteSelectedNote();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawContentEditor()
        {
            var note = GetSelectedNote();
            if (note == null)
                return;

            _contentScroll = GUILayout.BeginScrollView(_contentScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            string newContent = GUILayout.TextArea(note.content ?? "", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (!string.Equals(newContent, note.content, StringComparison.Ordinal))
            {
                note.content = newContent;
                MarkDirty();
            }
            GUILayout.EndScrollView();
        }

        private void DrawBottomRow()
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(24f)))
            {
                var gui = FindObjectOfType<SandboxGUI>();
                if (gui != null)
                    gui.SetWindowVisible(SandboxWindowKeys.Notebook, false);
                else
                    SetVisible(false);
            }
            GUILayout.EndHorizontal();
        }

        private NotebookNote? GetSelectedNote()
        {
            if (_notes.Length == 0)
                return null;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _notes.Length - 1);
            return _notes[_selectedIndex];
        }

        private void AddNote()
        {
            var next = new NotebookNote[_notes.Length + 1];
            Array.Copy(_notes, next, _notes.Length);
            next[next.Length - 1] = new NotebookNote { title = $"Note {next.Length}" };
            _notes = next;
            _selectedIndex = _notes.Length - 1;
            MarkDirty();
        }

        private void DeleteSelectedNote()
        {
            if (_notes.Length <= 1)
                return;

            var next = new NotebookNote[_notes.Length - 1];
            int writeIndex = 0;
            for (int i = 0; i < _notes.Length; i++)
            {
                if (i == _selectedIndex)
                    continue;
                next[writeIndex++] = _notes[i];
            }

            _notes = next;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _notes.Length - 1);
            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
            _nextSaveAt = Time.unscaledTime + 0.5f;
        }

        private void SaveToDisk()
        {
            try
            {
                string dir = Path.GetDirectoryName(_savePath) ?? string.Empty;
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new NotebookSaveData
                {
                    notes = _notes,
                    selectedIndex = _selectedIndex
                };

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(_savePath, json);
                _dirty = false;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Notebook save failed: {ex.Message}");
            }
        }

        private void LoadFromDisk()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    string json = File.ReadAllText(_savePath);
                    var data = JsonUtility.FromJson<NotebookSaveData>(json);
                    if (data != null && data.notes != null && data.notes.Length > 0)
                    {
                        _notes = data.notes;
                        _selectedIndex = Mathf.Clamp(data.selectedIndex, 0, _notes.Length - 1);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Notebook load failed: {ex.Message}");
            }

            _notes = new[] { new NotebookNote { title = "Note 1", content = "" } };
            _selectedIndex = 0;
            MarkDirty();
        }

        private void HandleResize()
        {
            Event e = Event.current;
            if (e == null)
                return;

            var handleRect = new Rect(
                windowRect.x + windowRect.width - ResizeHandleSize,
                windowRect.y + windowRect.height - ResizeHandleSize,
                ResizeHandleSize,
                ResizeHandleSize);

            if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
            {
                _isResizing = true;
                e.Use();
            }
            else if (_isResizing && e.type == EventType.MouseDrag)
            {
                float newWidth = e.mousePosition.x - windowRect.x;
                float newHeight = e.mousePosition.y - windowRect.y;
                windowRect.width = Mathf.Clamp(newWidth, MinWidth, MaxWidth);
                windowRect.height = Mathf.Clamp(newHeight, MinHeight, MaxHeight);
                e.Use();
            }
            else if (_isResizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _isResizing = false;
                e.Use();
            }
        }

        private void OnDestroy()
        {
            if (_dirty)
                SaveToDisk();
        }
    }
}
