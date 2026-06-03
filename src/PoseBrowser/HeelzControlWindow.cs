#if !KKS
using System;
using System.Collections.Generic;
using System.Linq;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class HeelzControlWindow : SubWindow
    {
        private const float CharListRefreshInterval = 2f;
        private const int TagPickerWindowId = 204901;

        // Cached character list + states (rebuilt on timer, not per frame)
        private readonly List<(OCIChar oci, HeelzCharacterState state)> _cachedChars = new();
        private float _nextCharRefresh;

        // Tag picker state
        private bool _editingOffTags;
        private bool _editingOnTags;
        private string _tagPickerSearch = "";
        private Vector2 _tagPickerScroll;
        private Rect _tagPickerRect = new Rect(0, 0, 320, 400);

        // Cached tag list for the picker (rebuilt when picker opens or search changes)
        private List<string>? _cachedPickerTags;
        private string _cachedPickerSearchKey = "";

        // Pre-built GUIContent instances (avoid per-frame alloc)
        private static readonly GUIContent OnContent = new GUIContent("On", "Force heel hover on");
        private static readonly GUIContent OffContent = new GUIContent("Off", "Force heel hover off");
        private static readonly GUIContent AutoContent = new GUIContent("Auto", "When checked, pose tag rules can change On/Off automatically");
        private static readonly GUIContent EditContent = new GUIContent("Edit", "Open tag picker to edit rule tags");
        private static readonly GUIContent CloseContent = new GUIContent("\u00d7", "Close Heelz Control");
        private static readonly GUIContent NoneLabel = new GUIContent("(none)");
        private static readonly GUIContent NoHeelzLabel = new GUIContent("HS2Heelz not detected. Install HS2Heelz to use heel control.");
        private static readonly GUIContent NoCharsLabel = new GUIContent("No characters in scene.");

        private static readonly GUILayoutOption BtnWidth = GUILayout.Width(32f);
        private static readonly GUILayoutOption RowHeight = GUILayout.Height(22f);
        private static readonly GUILayoutOption EditBtnWidth = GUILayout.Width(36f);

        // Workaround: PoseBrowserWindow provides tag data via this delegate (set during registration)
        internal Func<List<string>>? GetAllTagNames;

        protected override void Start()
        {
            base.Start();
            windowID = 204900;
            windowTitle = "Heelz Control";
            windowRect = new Rect(300, 150, 360, 400);
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                RefreshCharacterList();
                InvalidatePickerCache();
            }
        }

        protected override void DrawWindowContent(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(CloseContent, GUILayout.Width(22f), GUILayout.Height(18f)))
                CloseWindow();
            GUILayout.EndHorizontal();

            if (!HeelzControlService.IsHeelzDetected)
            {
                GUILayout.Label(NoHeelzLabel, GUILayout.MaxWidth(340f));
                FinishChrome();
                return;
            }

            RefreshCharacterListIfDue();

            DrawCharacterList();
            GUILayout.Space(6f);
            DrawTagRulesSection();

            FinishChrome();
        }

        private void CloseWindow()
        {
            _editingOffTags = false;
            _editingOnTags = false;
            var gui = FindObjectOfType<SandboxGUI>();
            if (gui != null)
                gui.SetHeelzControlVisible(false);
            else
                SetVisible(false);
        }

        public override void DrawWindow()
        {
            if (!isVisible) return;
            windowRect = GUILayout.Window(windowID, windowRect, DrawWindowContent, windowTitle);

            if (_editingOffTags || _editingOnTags)
                _tagPickerRect = GUILayout.Window(TagPickerWindowId, _tagPickerRect, DrawTagPickerWindow,
                    _editingOffTags ? "Heels OFF tags" : "Heels ON tags");
        }

        // ------------------------------------------------------------------
        // Character list
        // ------------------------------------------------------------------

        private void RefreshCharacterListIfDue()
        {
            if (Time.realtimeSinceStartup < _nextCharRefresh) return;
            RefreshCharacterList();
        }

        private void RefreshCharacterList()
        {
            _nextCharRefresh = Time.realtimeSinceStartup + CharListRefreshInterval;
            HeelzControlService.CleanupDestroyedCharacters();

            _cachedChars.Clear();
            try
            {
                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar oci)
                        _cachedChars.Add((oci, HeelzControlService.GetCharacterState(oci)));
                }
            }
            catch { }
        }

        private void DrawCharacterList()
        {
            if (_cachedChars.Count == 0)
            {
                GUILayout.Label(NoCharsLabel);
                return;
            }

            for (int i = 0; i < _cachedChars.Count; i++)
            {
                var (oci, state) = _cachedChars[i];
                if (oci == null || oci.charInfo == null) continue;

                // Row 1: name + [On] [Off] + Auto checkbox
                GUILayout.BeginHorizontal();

                GUILayout.Label(state.DisplayName, GUILayout.ExpandWidth(true), RowHeight);

                bool isOn = state.Override == HeelzOverride.ForceOn;
                bool isOff = state.Override == HeelzOverride.ForceOff;

                var prevColor = GUI.color;

                GUI.color = isOn ? Color.green : Color.white;
                if (GUILayout.Button(OnContent, BtnWidth, RowHeight))
                {
                    HeelzControlService.SetOverride(oci.charInfo, HeelzOverride.ForceOn);
                    RefreshSingleCharacter(i);
                }

                GUI.color = isOff ? new Color(1f, 0.5f, 0.5f) : Color.white;
                if (GUILayout.Button(OffContent, BtnWidth, RowHeight))
                {
                    HeelzControlService.SetOverride(oci.charInfo, HeelzOverride.ForceOff);
                    RefreshSingleCharacter(i);
                }

                GUI.color = prevColor;

                bool newAuto = GUILayout.Toggle(state.AutoEnabled, AutoContent, GUILayout.Width(52f));
                if (newAuto != state.AutoEnabled)
                {
                    HeelzControlService.SetAutoEnabled(oci.charInfo, newAuto);
                    RefreshSingleCharacter(i);
                }

                GUILayout.EndHorizontal();

                // Row 2: shoe state + heel height (text pre-built in state, no per-frame alloc)
                GUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                if (Event.current.type == EventType.Repaint)
                {
                    GUILayout.Label(state.ShoeDisplayText, GUILayout.ExpandWidth(false));
                    GUILayout.Space(12f);
                    GUILayout.Label(state.HeelDisplayText, GUILayout.ExpandWidth(false));
                }
                else
                {
                    GUILayout.Label("", GUILayout.ExpandWidth(false));
                    GUILayout.Space(12f);
                    GUILayout.Label("", GUILayout.ExpandWidth(false));
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private void RefreshSingleCharacter(int index)
        {
            if (index >= 0 && index < _cachedChars.Count)
            {
                var (oci, _) = _cachedChars[index];
                if (oci != null)
                    _cachedChars[index] = (oci, HeelzControlService.GetCharacterState(oci));
            }
        }

        // ------------------------------------------------------------------
        // Tag rules section
        // ------------------------------------------------------------------

        private void DrawTagRulesSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Tag Rules", RowHeight);

            // Heels OFF tags
            GUILayout.BeginHorizontal();
            GUILayout.Label("Heels OFF tags:", GUILayout.Width(100f));
            if (GUILayout.Button(EditContent, EditBtnWidth, RowHeight))
            {
                _editingOffTags = !_editingOffTags;
                _editingOnTags = false;
                if (_editingOffTags) OpenTagPicker();
            }
            GUILayout.EndHorizontal();

            string offLabel = HeelzControlService.CachedOffTagLabel;
            if (offLabel.Length > 0)
                GUILayout.Label(offLabel, GUI.skin.label);
            else
                GUILayout.Label(NoneLabel);

            GUILayout.Space(4f);

            // Heels ON tags
            GUILayout.BeginHorizontal();
            GUILayout.Label("Heels ON tags:", GUILayout.Width(100f));
            if (GUILayout.Button(EditContent, EditBtnWidth, RowHeight))
            {
                _editingOnTags = !_editingOnTags;
                _editingOffTags = false;
                if (_editingOnTags) OpenTagPicker();
            }
            GUILayout.EndHorizontal();

            string onLabel = HeelzControlService.CachedOnTagLabel;
            if (onLabel.Length > 0)
                GUILayout.Label(onLabel, GUI.skin.label);
            else
                GUILayout.Label(NoneLabel);

            GUILayout.EndVertical();
        }

        // ------------------------------------------------------------------
        // Tag picker popup
        // ------------------------------------------------------------------

        private void OpenTagPicker()
        {
            _tagPickerSearch = "";
            InvalidatePickerCache();
            _tagPickerScroll = Vector2.zero;
            _tagPickerRect = new Rect(
                windowRect.x + windowRect.width + 4,
                windowRect.y,
                320, 400);
        }

        private void InvalidatePickerCache()
        {
            _cachedPickerTags = null;
            _cachedPickerSearchKey = "\x01invalid";
        }

        private List<string> GetPickerTags(string search)
        {
            if (_cachedPickerTags != null && _cachedPickerSearchKey == search)
                return _cachedPickerTags;

            var allTags = GetAllTagNames?.Invoke() ?? new List<string>();

            // Also include Heelz rule tags that may not be on any pose
            var ruleTags = HeelzControlService.GetAllRuleTags();
            if (ruleTags.Count > 0)
            {
                var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in allTags) merged.Add(t);
                foreach (var t in ruleTags) merged.Add(t);
                allTags = merged.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            }

            if (!string.IsNullOrEmpty(search))
                allTags = allTags.Where(t => t.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            _cachedPickerTags = allTags;
            _cachedPickerSearchKey = search;
            return _cachedPickerTags;
        }

        private void DrawTagPickerWindow(int id)
        {
            HashSet<string> currentSet = _editingOffTags
                ? new HashSet<string>(HeelzControlService.HeelsOffTags, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(HeelzControlService.HeelsOnTags, StringComparer.OrdinalIgnoreCase);

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Search field
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50f));
            string newSearch = GUILayout.TextField(_tagPickerSearch, GUILayout.ExpandWidth(true));
            if (newSearch != _tagPickerSearch)
            {
                _tagPickerSearch = newSearch;
                InvalidatePickerCache();
            }
            GUILayout.EndHorizontal();

            string searchFold = _tagPickerSearch.Trim();

            // "Add new tag" button when search doesn't match existing
            if (searchFold.Length > 0)
            {
                var tags = GetPickerTags(searchFold);
                bool alreadyKnown = tags.Any(t => string.Equals(t, searchFold, StringComparison.OrdinalIgnoreCase));
                if (!alreadyKnown)
                {
                    if (GUILayout.Button($"Add new tag \"{searchFold}\"", GUILayout.Height(26f)))
                    {
                        currentSet.Add(searchFold);
                        CommitTagSet(currentSet);
                        InvalidatePickerCache();
                    }
                }
            }

            // Tag list
            var visibleTags = GetPickerTags(searchFold);
            if (visibleTags.Count == 0)
            {
                GUILayout.Label("No tags match the search.");
            }
            else
            {
                _tagPickerScroll = GUILayout.BeginScrollView(_tagPickerScroll, GUILayout.ExpandHeight(true));
                foreach (var tag in visibleTags)
                {
                    bool on = currentSet.Contains(tag);
                    bool nv = GUILayout.Toggle(on, tag, GUILayout.Height(22f));
                    if (nv != on)
                    {
                        if (nv) currentSet.Add(tag);
                        else currentSet.Remove(tag);
                        CommitTagSet(currentSet);
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Close", GUILayout.Height(24f)))
            {
                _editingOffTags = false;
                _editingOnTags = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            IMGUIUtils.EatInputInRect(_tagPickerRect);
        }

        private void CommitTagSet(HashSet<string> newSet)
        {
            if (_editingOffTags)
                HeelzControlService.SetHeelsOffTags(newSet);
            else if (_editingOnTags)
                HeelzControlService.SetHeelsOnTags(newSet);
        }

        // ------------------------------------------------------------------
        // Chrome
        // ------------------------------------------------------------------

        private void FinishChrome()
        {
            GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
            IMGUIUtils.EatInputInRect(windowRect);
        }
    }
}
#endif
