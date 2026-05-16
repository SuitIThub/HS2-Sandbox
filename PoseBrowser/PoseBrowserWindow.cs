using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PoseBrowserWindow : SubWindow
    {
        private const float MinWidth = 780f;
        private const float MinHeight = 500f;
        private const float MaxWidth = 1400f;
        private const float MaxHeight = 1000f;
        private const float ResizeHandleSize = 18f;
        private const float TreePanelWidth = 200f;
        private const float BottomBarHeight = 36f;
        private const float TopBarHeight = 32f;
        private const float MinCardSize = 96f;
        private const float MaxCardSize = 280f;
        private const int OptionsWindowId = 2021;
        private const int HelpWindowId = 2022;

        private float _cardCellSize = 140f;
        private int _itemsPerPage;
        private int _currentPage = 1;

        private bool _viewAllPosesRecursive;

        private PoseDataService _dataService = null!;
        private PoseTagDatabase _tagDb = null!;
        private PoseFolderTree _folderTree = null!;
        private PoseThumbnailCapture _thumbCapture = null!;

        private List<PoseGridItem> _allItems = new List<PoseGridItem>();
        private List<PoseGridItem> _filteredItems = new List<PoseGridItem>();
        private Texture2D? _placeholderTex;

        private Vector2 _treeScroll;
        private Vector2 _gridScroll;
        private bool _isResizing;

        // Search & filter state
        private string _searchText = "";
        private bool _searchUseRegex;
        private string _searchRegexError = "";
        private HashSet<string> _activeTagFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _tagFilterAndMode = true;
        private bool _showTagFilterDropdown;
        private Vector2 _tagDropdownScroll;
        private bool _showFavoritesOnly;

        // Multi-select (global index into _filteredItems when paginating)
        private int _lastClickedGlobalIndex = -1;

        private bool _showOptionsPane;
        private Rect _optionsWindowRect;

        private bool _showHelpPane;
        private Rect _helpWindowRect;
        private Vector2 _helpScroll;

        // Folder / pose rename & move-copy
        private bool _showRenameFolderPopup;
        private PoseFolderNode? _renameFolderTarget;
        private string _renameFolderText = "";

        private bool _showRenamePosePopup;
        private string _renamePoseText = "";
        private bool _renamePoseAlsoFile = true;

        private bool _moveCopyPending;
        private bool _moveCopyIsCopy = true;
        private string? _moveCopyDestPath;
        private string _itemsPerPageEdit = "0";
        private bool _didAutoLoadBrowse;

        private bool _showNewChildFolderPopup;
        private string _newChildFolderName = "";
        private bool _showDeleteFolderConfirm;
        private bool _showDeletePosesConfirm;
        private string _folderActionError = "";

        // Mass tagging popup
        private bool _showTagPopup;
        private string _tagPopupText = "";

        // Save pose
        private bool _showSavePopup;
        private string _savePoseName = "";

        // Update pose
        private enum UpdateMode { None, KeepThumb, NewThumb }
        private UpdateMode _pendingUpdateMode = UpdateMode.None;
        private PoseGridItem? _pendingUpdateItem;

        // Thumbnail loading coroutine
        private Coroutine? _thumbnailLoadCoroutine;
        private int _thumbnailLoadIndex;

        // GUIStyles (lazy-init)
        private GUIStyle? _selectedStyle;
        private GUIStyle? _favoriteStyle;
        private GUIStyle? _treeNodeStyle;
        private GUIStyle? _treeNodeSelectedStyle;
        private GUIStyle? _tagWrapStyle;
        private GUIStyle? _headerSectionCaptionStyle;
        private GUIStyle? _characterHintStyle;

        protected override void Start()
        {
            base.Start();
            windowID = 2020;
            windowTitle = "Pose Browser";
            windowRect = new Rect(200f, 80f, 900f, 620f);

            string poseRoot = Path.Combine(UserData.Path, "studio", "pose");
            _dataService = new PoseDataService(poseRoot);
            _tagDb = new PoseTagDatabase(poseRoot);
            _folderTree = new PoseFolderTree(poseRoot);
            _thumbCapture = new PoseThumbnailCapture();

            CreatePlaceholderTexture();
            _folderTree.Refresh();
            _optionsWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 268f, windowRect.height);
            _helpWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 340f, windowRect.height);
            LoadPersistedOptions();
        }

        private void Update()
        {
            _tagDb?.Update();
        }

        private void OnDestroy()
        {
            _tagDb?.ForceSave();
            SavePersistedOptions();
            if (_placeholderTex != null)
                Destroy(_placeholderTex);
        }

        private void CreatePlaceholderTexture()
        {
            _placeholderTex = new Texture2D(64, 64, TextureFormat.ARGB32, false);
            var pixels = new Color32[64 * 64];
            var bg = new Color32(60, 60, 60, 255);
            var fg = new Color32(120, 120, 120, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
            for (int x = 20; x < 44; x++)
                for (int y = 20; y < 44; y++)
                    pixels[y * 64 + x] = fg;
            _placeholderTex.SetPixels32(pixels);
            _placeholderTex.Apply();
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (visible && _folderTree.RootNodes.Count == 0)
                _folderTree.Refresh();
            if (visible && !_didAutoLoadBrowse)
            {
                _didAutoLoadBrowse = true;
                LoadFolder(_folderTree.RootPath);
            }
        }

        public override void DrawWindow()
        {
            if (!isVisible) return;

            if (_thumbCapture.IsActive)
                _thumbCapture.DrawOverlay();

            HandleResize();
            windowRect.width = Mathf.Clamp(windowRect.width, MinWidth, MaxWidth);
            windowRect.height = Mathf.Clamp(windowRect.height, MinHeight, MaxHeight);
            windowRect.x = Mathf.Clamp(windowRect.x, 4f, Mathf.Max(4f, Screen.width - windowRect.width - 4f));
            windowRect.y = Mathf.Clamp(windowRect.y, 4f, Mathf.Max(4f, Screen.height - windowRect.height - 4f));

            windowRect = GUILayout.Window(windowID, windowRect, DrawWindowContent, windowTitle);

            if (_showOptionsPane)
            {
                SyncOptionsWindowRect();
                _optionsWindowRect = GUILayout.Window(OptionsWindowId, _optionsWindowRect, DrawOptionsWindowContent, "Pose Browser · Options");
                _optionsWindowRect.x = Mathf.Clamp(_optionsWindowRect.x, 4f, Mathf.Max(4f, Screen.width - _optionsWindowRect.width - 4f));
                _optionsWindowRect.y = Mathf.Clamp(_optionsWindowRect.y, 4f, Mathf.Max(4f, Screen.height - _optionsWindowRect.height - 4f));
                IMGUIUtils.EatInputInRect(_optionsWindowRect);
            }

            if (_showHelpPane)
            {
                SyncHelpWindowRect();
                _helpWindowRect = GUILayout.Window(HelpWindowId, _helpWindowRect, DrawHelpWindowContent, "Pose Browser · Help");
                _helpWindowRect.x = Mathf.Clamp(_helpWindowRect.x, 4f, Mathf.Max(4f, Screen.width - _helpWindowRect.width - 4f));
                _helpWindowRect.y = Mathf.Clamp(_helpWindowRect.y, 4f, Mathf.Max(4f, Screen.height - _helpWindowRect.height - 4f));
                IMGUIUtils.EatInputInRect(_helpWindowRect);
            }
        }

        private void SyncOptionsWindowRect()
        {
            _optionsWindowRect = new Rect(windowRect.xMax + 4f, windowRect.y, _optionsWindowRect.width > 0 ? _optionsWindowRect.width : 268f, windowRect.height);
        }

        private void SyncHelpWindowRect()
        {
            float x = windowRect.xMax + 4f;
            if (_showOptionsPane)
                x = _optionsWindowRect.xMax + 4f;
            float w = _helpWindowRect.width > 0 ? _helpWindowRect.width : 340f;
            _helpWindowRect = new Rect(x, windowRect.y, w, windowRect.height);
        }

        private string SaveTargetFolderPath =>
            _viewAllPosesRecursive
                ? _folderTree.RootPath
                : (_folderTree.SelectedNode?.FullPath ?? _folderTree.RootPath);

        private List<PoseGridItem> GetGridVisibleItems()
        {
            if (_itemsPerPage <= 0)
                return _filteredItems;
            int skip = (_currentPage - 1) * _itemsPerPage;
            if (skip >= _filteredItems.Count)
                return new List<PoseGridItem>();
            int take = Mathf.Min(_itemsPerPage, _filteredItems.Count - skip);
            return _filteredItems.GetRange(skip, take);
        }

        private int DisplayIndexToGlobal(int displayIndex)
        {
            if (_itemsPerPage <= 0) return displayIndex;
            return (_currentPage - 1) * _itemsPerPage + displayIndex;
        }

        private void ClampCurrentPage()
        {
            if (_itemsPerPage <= 0)
            {
                _currentPage = 1;
                return;
            }
            int pages = Mathf.Max(1, Mathf.CeilToInt(_filteredItems.Count / (float)_itemsPerPage));
            _currentPage = Mathf.Clamp(_currentPage, 1, pages);
        }

        private void ClearTreeFolderActionUi()
        {
            _showDeleteFolderConfirm = false;
            _showNewChildFolderPopup = false;
            _folderActionError = "";
        }

        protected override void DrawWindowContent(int id)
        {
            InitStyles();

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            DrawTopBar();

            DrawStudioCharacterSelectionRow();

            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawTreePanel();
            DrawGridPanel();
            GUILayout.EndHorizontal();

            DrawBottomBar();

            DrawFolderPoseDialogs();

            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
                DrawPoseBrowserTooltip(GUI.tooltip, windowRect);

            var resizeHandle = new Rect(windowRect.width - ResizeHandleSize, windowRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            GUI.Box(resizeHandle, "◢");

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - ResizeHandleSize, 20f));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        // ── Top Bar ──

        private void DrawTopBar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(TopBarHeight));

            DrawHeaderSectionCaption("Poses");

            GUILayout.Label("Search:", GUILayout.Width(46f));
            string newSearch = GUILayout.TextField(_searchText, GUILayout.MinWidth(160f), GUILayout.ExpandWidth(true));
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                ApplyFilters();
            }

            GUILayout.Space(6f);
            bool newRegex = GUILayout.Toggle(_searchUseRegex, ".*", GUILayout.Width(36f));
            if (newRegex != _searchUseRegex)
            {
                _searchUseRegex = newRegex;
                ApplyFilters();
            }

            GUILayout.Space(6f);

            bool newFavOnly = GUILayout.Toggle(_showFavoritesOnly, "★", GUILayout.Width(28f));
            if (newFavOnly != _showFavoritesOnly)
            {
                _showFavoritesOnly = newFavOnly;
                ApplyFilters();
            }

            GUILayout.Space(4f);

            if (GUILayout.Button(_tagFilterAndMode ? "AND" : "OR", GUILayout.Width(40f)))
            {
                _tagFilterAndMode = !_tagFilterAndMode;
                ApplyFilters();
            }

            if (GUILayout.Button($"Tags ({_activeTagFilters.Count})", GUILayout.Width(80f)))
                _showTagFilterDropdown = !_showTagFilterDropdown;

            GUILayout.Space(8f);

            if (GUILayout.Button("Save Pose", GUILayout.Width(90f), GUILayout.Height(24f)))
            {
                _showSavePopup = true;
                _savePoseName = "";
            }

            GUILayout.FlexibleSpace();

            DrawTopBarVerticalRule(22f);

            DrawHeaderSectionCaption("Window");

            if (GUILayout.Button(_showHelpPane ? "Help ▶" : "Help", GUILayout.Width(56f), GUILayout.Height(24f)))
                _showHelpPane = !_showHelpPane;

            if (GUILayout.Button(_showOptionsPane ? "Options ▶" : "Options", GUILayout.Width(78f), GUILayout.Height(24f)))
                _showOptionsPane = !_showOptionsPane;

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_searchRegexError))
            {
                var c = GUI.color;
                GUI.color = new Color(1f, 0.5f, 0.45f);
                GUILayout.Label(_searchRegexError);
                GUI.color = c;
            }

            if (_showTagFilterDropdown)
                DrawTagFilterDropdown();

            if (_showSavePopup)
                DrawSavePopup();
        }

        private void DrawHeaderSectionCaption(string text)
        {
            GUILayout.Label(text, _headerSectionCaptionStyle!, GUILayout.MinWidth(36f), GUILayout.MaxWidth(80f), GUILayout.Height(TopBarHeight));
        }

        private static void DrawTopBarVerticalRule(float height)
        {
            GUILayout.Space(4f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.28f);
            var r = GUILayoutUtility.GetRect(1f, height, GUILayout.Width(1f), GUILayout.Height(height));
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUILayout.Space(8f);
        }

        private void DrawStudioCharacterSelectionRow()
        {
            var names = _dataService.GetSelectedCharacterDisplayNames();
            GUILayout.BeginHorizontal(GUILayout.Height(20f));
            GUILayout.Space(6f);

            var style = _characterHintStyle!;
            if (names.Count == 0)
            {
                GUILayout.Label(
                    new GUIContent(
                        "Character: none",
                        "Select one or more characters in Studio. Extra selected items (props, accessories, etc.) are ignored."),
                    style,
                    GUILayout.ExpandWidth(false));
            }
            else if (names.Count == 1)
            {
                GUILayout.Label(new GUIContent($"Character: {names[0]}", names[0]), style, GUILayout.ExpandWidth(false));
            }
            else
            {
                GUILayout.Label(
                    new GUIContent($"Character: {names.Count} selected", string.Join("\n", names)),
                    style,
                    GUILayout.ExpandWidth(false));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawTagFilterDropdown()
        {
            var allTags = _tagDb.GetAllKnownTags().OrderBy(t => t).ToList();
            if (allTags.Count == 0)
            {
                GUILayout.Label("  No tags defined yet.");
                return;
            }

            float dropH = Mathf.Min(allTags.Count * 22f + 30f, 200f);
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(dropH));
            _tagDropdownScroll = GUILayout.BeginScrollView(_tagDropdownScroll);

            foreach (var tag in allTags)
            {
                bool active = _activeTagFilters.Contains(tag);
                bool newActive = GUILayout.Toggle(active, tag);
                if (newActive != active)
                {
                    if (newActive) _activeTagFilters.Add(tag);
                    else _activeTagFilters.Remove(tag);
                    ApplyFilters();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All", GUILayout.Height(20f)))
            {
                _activeTagFilters.Clear();
                ApplyFilters();
            }
            if (GUILayout.Button("Close", GUILayout.Height(20f)))
                _showTagFilterDropdown = false;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawSavePopup()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Save current character pose:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(45f));
            _savePoseName = GUILayout.TextField(_savePoseName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(24f)))
            {
                DoSavePose();
                _showSavePopup = false;
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                _showSavePopup = false;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ── Tree Panel ──

        private void DrawTreePanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(TreePanelWidth), GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Folders", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("↻", GUILayout.Width(24f), GUILayout.Height(18f)))
                _folderTree.Refresh();
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            _treeScroll = GUILayout.BeginScrollView(_treeScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            bool allViewSelected = _viewAllPosesRecursive;
            var allStyle = allViewSelected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            GUI.enabled = !_moveCopyPending;
            if (GUILayout.Button("All poses", allStyle, GUILayout.Height(22f)))
            {
                _viewAllPosesRecursive = true;
                _folderTree.SelectedNode = null;
                ClearTreeFolderActionUi();
                LoadAllPosesFromTreeRoot();
            }
            GUI.enabled = true;

            bool rootOnlyNormalSelected = !_moveCopyPending && !_viewAllPosesRecursive && _folderTree.SelectedNode == null;
            bool rootOnlyMoveDest = _moveCopyPending && !string.IsNullOrEmpty(_moveCopyDestPath) &&
                Path.GetFullPath(_moveCopyDestPath).Equals(Path.GetFullPath(_folderTree.RootPath), StringComparison.OrdinalIgnoreCase);
            var rootOnlyStyle = (rootOnlyNormalSelected || rootOnlyMoveDest) ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            if (GUILayout.Button($"📁 Root only", rootOnlyStyle, GUILayout.Height(22f)))
            {
                if (_moveCopyPending)
                {
                    _moveCopyDestPath = _folderTree.RootPath;
                    _folderTree.SelectedNode = null;
                    ClearTreeFolderActionUi();
                }
                else
                {
                    _viewAllPosesRecursive = false;
                    _folderTree.SelectedNode = null;
                    ClearTreeFolderActionUi();
                    LoadFolder(_folderTree.RootPath);
                }
            }

            foreach (var node in _folderTree.GetVisibleNodes())
            {
                GUILayout.BeginHorizontal();

                GUILayout.Space(node.Depth * 16f);

                if (node.HasChildren)
                {
                    string arrow = node.IsExpanded ? "▼" : "►";
                    if (GUILayout.Button(arrow, GUILayout.Width(20f), GUILayout.Height(20f)))
                        _folderTree.ToggleExpand(node);
                }
                else
                {
                    GUILayout.Space(24f);
                }

                bool normalSel = !_moveCopyPending && !_viewAllPosesRecursive && _folderTree.SelectedNode == node;
                bool moveDestSel = _moveCopyPending && !string.IsNullOrEmpty(_moveCopyDestPath) &&
                    Path.GetFullPath(node.FullPath).Equals(Path.GetFullPath(_moveCopyDestPath), StringComparison.OrdinalIgnoreCase);
                var style = (normalSel || moveDestSel) ? _treeNodeSelectedStyle! : _treeNodeStyle!;
                if (GUILayout.Button(node.Name, style, GUILayout.Height(20f), GUILayout.ExpandWidth(true)))
                {
                    if (_moveCopyPending)
                    {
                        _moveCopyDestPath = node.FullPath;
                        _folderTree.SelectNode(node);
                        ClearTreeFolderActionUi();
                    }
                    else
                    {
                        _viewAllPosesRecursive = false;
                        ClearTreeFolderActionUi();
                        _folderTree.SelectNode(node);
                        LoadFolder(node.FullPath);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            if (!_viewAllPosesRecursive || _moveCopyPending)
            {
                GUILayout.Space(6f);
                DrawTreeFolderActions();
            }

            GUILayout.EndVertical();
        }

        private void DrawTreeFolderActions()
        {
            bool isLibraryRootScope = _folderTree.SelectedNode == null;
            var node = _folderTree.SelectedNode;
            string parentPathForChildren = isLibraryRootScope ? _folderTree.RootPath : node!.FullPath;
            bool empty = !isLibraryRootScope && PoseDataService.IsPoseFolderEmpty(node!.FullPath);

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));

            if (_moveCopyPending)
            {
                var capStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = true
                };
                GUILayout.Label(_moveCopyIsCopy ? "<b>Copy to folder</b>" : "<b>Move to folder</b>", capStyle);
                string rel = PoseDataService.GetRelativePath(_folderTree.RootPath, _moveCopyDestPath ?? "");
                if (string.IsNullOrEmpty(rel)) rel = "(pose root)";
                GUILayout.Label($"Into: <b>{rel}</b>", capStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply", GUILayout.Height(24f)))
                    ApplyMoveCopyOperation();
                if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                    CancelMoveCopyOperation();
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }

            GUILayout.Label(isLibraryRootScope ? "Library root" : "Selected folder", GUILayout.MinHeight(20f));
            if (isLibraryRootScope)
                GUILayout.Label("New folders go here.", GUILayout.MinHeight(16f));

            if (!string.IsNullOrEmpty(_folderActionError))
            {
                var c = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.4f);
                GUILayout.Label(_folderActionError, GUILayout.MaxHeight(40f));
                GUI.color = c;
            }

            if (!isLibraryRootScope)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Rename…", GUILayout.Height(22f)))
                {
                    _renameFolderTarget = node!;
                    _renameFolderText = node!.Name;
                    _showRenameFolderPopup = true;
                    _showDeleteFolderConfirm = false;
                    _folderActionError = "";
                }
                if (GUILayout.Button("New folder…", GUILayout.Height(22f)))
                {
                    _showNewChildFolderPopup = true;
                    _newChildFolderName = "";
                    _showDeleteFolderConfirm = false;
                    _folderActionError = "";
                }
                GUILayout.EndHorizontal();

                GUI.enabled = empty;
                if (GUILayout.Button("Delete folder…", GUILayout.Height(22f)))
                {
                    _showDeleteFolderConfirm = true;
                    _folderActionError = "";
                }
                GUI.enabled = true;

                if (!empty && !_showDeleteFolderConfirm)
                    GUILayout.Label("(Delete: empty only)", GUILayout.MinHeight(18f));
            }
            else
            {
                if (GUILayout.Button("New folder…", GUILayout.Height(22f)))
                {
                    _showNewChildFolderPopup = true;
                    _newChildFolderName = "";
                    _showDeleteFolderConfirm = false;
                    _folderActionError = "";
                }
            }

            if (_showNewChildFolderPopup)
            {
                GUILayout.Label("New subfolder name:");
                _newChildFolderName = GUILayout.TextField(_newChildFolderName);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create", GUILayout.Height(22f)))
                {
                    if (_dataService.TryCreateChildFolder(parentPathForChildren, _newChildFolderName, out var created, out var err))
                    {
                        _folderTree.Refresh();
                        if (!string.IsNullOrEmpty(created))
                        {
                            var nn = _folderTree.FindNodeByFullPath(created!);
                            if (nn != null)
                            {
                                _folderTree.EnsureExpandedToShow(nn);
                                _folderTree.SelectNode(nn);
                                if (_moveCopyPending)
                                    _moveCopyDestPath = created;
                                else
                                    LoadFolder(created!);
                            }
                            else
                            {
                                if (_moveCopyPending && !string.IsNullOrEmpty(created))
                                    _moveCopyDestPath = created;
                                else if (!_moveCopyPending)
                                    ReloadCurrentView();
                            }
                        }
                        _showNewChildFolderPopup = false;
                        _folderActionError = "";
                    }
                    else
                    {
                        _folderActionError = err ?? "Could not create folder.";
                    }
                }
                if (GUILayout.Button("Cancel", GUILayout.Height(22f)))
                {
                    _showNewChildFolderPopup = false;
                    _folderActionError = "";
                }
                GUILayout.EndHorizontal();
            }

            if (!isLibraryRootScope && _showDeleteFolderConfirm && empty && node != null)
            {
                GUILayout.Label($"Delete empty folder \"{node.Name}\"?", GUILayout.Height(18f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm delete", GUILayout.Height(24f)))
                {
                    if (!PoseDataService.IsPoseFolderEmpty(node.FullPath))
                        _folderActionError = "Folder is no longer empty.";
                    else
                    {
                        string? parent = Path.GetDirectoryName(node.FullPath);
                        if (_dataService.TryDeleteEmptyFolder(node.FullPath, _tagDb, out var err))
                        {
                            _folderTree.Refresh();
                            _folderTree.SelectedNode = null;
                            _folderActionError = "";
                            _moveCopyPending = false;
                            _moveCopyDestPath = null;
                            LoadFolder(string.IsNullOrEmpty(parent) ? _folderTree.RootPath : parent);
                        }
                        else
                        {
                            _folderActionError = err ?? "Delete failed.";
                        }
                    }
                    _showDeleteFolderConfirm = false;
                }
                if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                {
                    _showDeleteFolderConfirm = false;
                    _folderActionError = "";
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        // ── Grid Panel ──

        /// <summary>
        /// Vertical scrollbar steals width from the scroll view client area; reserve it up-front so
        /// column × cell width never exceeds the inner width (no horizontal scrollbar).
        /// </summary>
        private static float PoseCardHorizontalMarginBudget()
        {
            var box = GUI.skin.box;
            int m = box.margin.left + box.margin.right;
            int b = box.border.left + box.border.right;
            // Boxed cells consume more width than GUILayout.Width (margins, borders; some skins under-report).
            return Mathf.Max(8f, m + b + 6f);
        }

        private void ComputeGridCellLayout(float contentWidth, float marginH, out int columns, out float cellDrawW)
        {
            contentWidth = Mathf.Max(80f, contentWidth);
            float target = Mathf.Clamp(_cardCellSize, MinCardSize, MaxCardSize);

            // Fewest columns that respect the slider as a minimum card width (floor slots).
            columns = Mathf.Max(1, Mathf.FloorToInt(contentWidth / (target + marginH)));

            // Add columns while each cell can still be at least as wide as the slider (uses leftover width).
            while (true)
            {
                int next = columns + 1;
                float cellIfNext = contentWidth / next - marginH;
                if (cellIfNext + 0.02f < target) break;
                if (cellIfNext < MinCardSize) break;
                columns = next;
            }

            float slot = contentWidth / columns;
            cellDrawW = Mathf.Floor((slot - marginH) * 100f) / 100f;
            // Fill the row up to MaxCardSize; slider is a minimum target, not a hard ceiling.
            cellDrawW = Mathf.Clamp(cellDrawW, MinCardSize, MaxCardSize);

            const float slack = 1f;
            while (columns > 1 && columns * (cellDrawW + marginH) > contentWidth + slack)
            {
                columns--;
                slot = contentWidth / columns;
                cellDrawW = Mathf.Floor((slot - marginH) * 100f) / 100f;
                cellDrawW = Mathf.Clamp(cellDrawW, MinCardSize, MaxCardSize);
            }
        }

        private void DrawGridPanel()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            float vsb = GUI.skin.verticalScrollbar != null ? GUI.skin.verticalScrollbar.fixedWidth : 15f;
            if (vsb < 10f) vsb = 18f;
            float marginH = PoseCardHorizontalMarginBudget();
            // Window client width is slightly less than windowRect; scrollbar + skin margins eat more than fixedWidth.
            const float windowChromePad = 20f;
            const float layoutSlack = 8f;
            float availableForGrid = windowRect.width - TreePanelWidth - windowChromePad - layoutSlack;
            float contentWidth = Mathf.Max(80f, availableForGrid - vsb - 8f);

            ComputeGridCellLayout(contentWidth, marginH, out int columns, out float cell);
            cell = Mathf.Clamp(cell, MinCardSize, MaxCardSize);

            var visible = GetGridVisibleItems();
            ClampCurrentPage();

            if (_itemsPerPage > 0 && _filteredItems.Count > 0)
            {
                int pages = Mathf.Max(1, Mathf.CeilToInt(_filteredItems.Count / (float)_itemsPerPage));
                GUILayout.BeginHorizontal(GUILayout.Height(22f));
                GUILayout.Label($"Page {_currentPage}/{pages} · {_filteredItems.Count} poses", GUILayout.Width(200f));
                GUI.enabled = _currentPage > 1;
                if (GUILayout.Button("◀", GUILayout.Width(28f))) { _currentPage--; _gridScroll = Vector2.zero; }
                GUI.enabled = _currentPage < pages;
                if (GUILayout.Button("▶", GUILayout.Width(28f))) { _currentPage++; _gridScroll = Vector2.zero; }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            _gridScroll = GUILayout.BeginScrollView(_gridScroll, alwaysShowHorizontal: false, alwaysShowVertical: false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            int count = visible.Count;
            int rows = Mathf.CeilToInt((float)count / columns);

            float tagBlockH = 44f;

            for (int row = 0; row < rows; row++)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                for (int col = 0; col < columns; col++)
                {
                    int idx = row * columns + col;
                    if (idx < count)
                        DrawGridCell(visible[idx], idx, cell, tagBlockH);
                    else
                        DrawGridCellPlaceholder(cell);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>Same boxed horizontal footprint as <see cref="DrawGridCell"/> for partial rows.</summary>
        private static void DrawGridCellPlaceholder(float cellW)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(cellW), GUILayout.ExpandWidth(false));
            GUILayout.Space(1f);
            GUILayout.EndVertical();
        }

        private void DrawGridCell(PoseGridItem item, int displayIndex, float cellW, float tagBlockH)
        {
            var oldBg = GUI.backgroundColor;
            if (item.IsSelected)
                GUI.backgroundColor = new Color(0.3f, 0.6f, 1f, 1f);

            const float edge = 2f;
            float innerW = Mathf.Max(40f, cellW - edge * 2f);

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(cellW), GUILayout.ExpandWidth(false));

            Rect thumbRect = GUILayoutUtility.GetRect(innerW, innerW);
            Texture2D tex = item.Thumbnail ?? _placeholderTex!;

            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit, false);

            const float cbSize = 18f;
            var cbRect = new Rect(thumbRect.xMax - cbSize - 3f, thumbRect.y + 3f, cbSize, cbSize);
            bool newSel = GUI.Toggle(cbRect, item.IsSelected, "");
            if (newSel != item.IsSelected)
            {
                item.IsSelected = newSel;
                _lastClickedGlobalIndex = DisplayIndexToGlobal(displayIndex);
            }

            Event ev = Event.current;
            if (ev.type == EventType.MouseDown && thumbRect.Contains(ev.mousePosition) && !cbRect.Contains(ev.mousePosition))
            {
                HandleItemClick(item, displayIndex);
                ev.Use();
            }

            GUILayout.BeginHorizontal();
            if (item.IsFavorite)
                GUILayout.Label("★", GUILayout.Width(14f));

            string label = item.DisplayName;
            var nameStyle = GUI.skin.label;
            GUILayout.Label(label, nameStyle, GUILayout.MaxWidth(innerW - (item.IsFavorite ? 18f : 4f)), GUILayout.Height(18f));
            GUILayout.EndHorizontal();

            if (item.Tags.Count > 0)
            {
                string tagStr = string.Join(" · ", item.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
                GUILayout.Label(tagStr, _tagWrapStyle!, GUILayout.Width(innerW), GUILayout.Height(tagBlockH), GUILayout.ExpandHeight(false));
            }

            GUILayout.EndVertical();
            GUI.backgroundColor = oldBg;
        }

        private void HandleItemClick(PoseGridItem item, int displayIndex)
        {
            Event e = Event.current;
            int globalIdx = DisplayIndexToGlobal(displayIndex);

            if (e != null && e.button == 1)
            {
                _dataService.ApplyPoseToSelected(item);
                return;
            }

            if (e != null && e.control)
            {
                item.IsSelected = !item.IsSelected;
                _lastClickedGlobalIndex = globalIdx;
            }
            else if (e != null && e.shift && _lastClickedGlobalIndex >= 0)
            {
                int start = Mathf.Min(_lastClickedGlobalIndex, globalIdx);
                int end = Mathf.Max(_lastClickedGlobalIndex, globalIdx);
                for (int i = start; i <= end && i < _filteredItems.Count; i++)
                    _filteredItems[i].IsSelected = true;
            }
            else
            {
                foreach (var it in _filteredItems) it.IsSelected = false;
                item.IsSelected = true;
                _lastClickedGlobalIndex = globalIdx;

                _dataService.ApplyPoseToSelected(item);
            }
        }

        // ── Bottom Bar ──

        private void DrawBottomBar()
        {
            var selected = _filteredItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                if (_showTagPopup) _showTagPopup = false;
                if (_pendingUpdateMode != UpdateMode.None) _pendingUpdateMode = UpdateMode.None;
                _showDeletePosesConfirm = false;
                if (_moveCopyPending)
                {
                    _moveCopyPending = false;
                    _moveCopyDestPath = null;
                }
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();

            GUILayout.Label($"Selection: {selected.Count}", GUILayout.Width(92f));

            if (selected.Count == 1)
            {
                if (GUILayout.Button("Update Pose", GUILayout.Width(100f), GUILayout.Height(24f)))
                    ShowUpdatePoseOptions(selected[0]);

                if (GUILayout.Button("Rename…", GUILayout.Width(72f), GUILayout.Height(24f)))
                {
                    _renamePoseText = selected[0].DisplayName;
                    _renamePoseAlsoFile = true;
                    _showRenamePosePopup = true;
                }
            }

            if (GUILayout.Button("Tag Selected", GUILayout.Width(100f), GUILayout.Height(24f)))
            {
                _showTagPopup = true;
                _tagPopupText = "";
            }

            if (GUILayout.Button("Fav Selected", GUILayout.Width(100f), GUILayout.Height(24f)))
            {
                foreach (var it in selected)
                    _tagDb.ToggleFavorite(it);
            }

            if (GUILayout.Button("Thumbs…", GUILayout.Width(72f), GUILayout.Height(24f)))
                StartThumbnailCapture(selected);

            if (GUILayout.Button("Move…", GUILayout.Width(60f), GUILayout.Height(24f)))
            {
                _moveCopyIsCopy = false;
                _moveCopyPending = true;
                _moveCopyDestPath = SaveTargetFolderPath;
            }

            if (GUILayout.Button("Copy…", GUILayout.Width(60f), GUILayout.Height(24f)))
            {
                _moveCopyIsCopy = true;
                _moveCopyPending = true;
                _moveCopyDestPath = SaveTargetFolderPath;
            }

            if (GUILayout.Button("Delete…", GUILayout.Width(62f), GUILayout.Height(24f)))
                _showDeletePosesConfirm = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Deselect", GUILayout.Width(70f), GUILayout.Height(24f)))
            {
                foreach (var it in _filteredItems)
                    it.IsSelected = false;
                _showDeletePosesConfirm = false;
            }

            GUILayout.EndHorizontal();

            if (_moveCopyPending)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    "Move/Copy: choose destination in the Folders panel (↑), then Apply or Cancel there.",
                    GUILayout.Height(28f));
            }

            if (_showDeletePosesConfirm)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    $"Permanently delete {selected.Count} pose file(s)? Each file is copied to !_AutoBackup first.",
                    GUILayout.Height(36f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm delete", GUILayout.Height(26f), GUILayout.MinWidth(120f)))
                {
                    foreach (var it in selected)
                    {
                        if (it.Thumbnail != null)
                            Destroy(it.Thumbnail);
                    }
                    _dataService.DeletePoseFiles(selected, _tagDb);
                    _showDeletePosesConfirm = false;
                    ReloadCurrentView();
                }
                if (GUILayout.Button("Cancel", GUILayout.Height(26f), GUILayout.MinWidth(80f)))
                    _showDeletePosesConfirm = false;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            if (_showTagPopup)
                DrawTagPopup(selected);

            if (_pendingUpdateMode != UpdateMode.None)
                DrawUpdatePopup();
        }

        private void DrawFolderPoseDialogs()
        {
            if (_showRenameFolderPopup && _renameFolderTarget != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Rename folder:");
                _renameFolderText = GUILayout.TextField(_renameFolderText);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("OK", GUILayout.Height(22f)))
                {
                    var target = _renameFolderTarget;
                    if (target != null)
                    {
                        string oldFull = Path.GetFullPath(target.FullPath);
                        if (_dataService.RenameFolder(oldFull, _renameFolderText, _tagDb, out var newFull))
                        {
                            _moveCopyPending = false;
                            _moveCopyDestPath = null;
                            _folderTree.Refresh();
                            if (!string.IsNullOrEmpty(newFull))
                            {
                                var nn = _folderTree.FindNodeByFullPath(newFull!);
                                if (nn != null)
                                {
                                    _folderTree.EnsureExpandedToShow(nn);
                                    _folderTree.SelectNode(nn);
                                    LoadFolder(newFull!);
                                }
                                else
                                    ReloadCurrentView();
                            }
                            else
                                ReloadCurrentView();
                        }
                    }
                    _showRenameFolderPopup = false;
                    _renameFolderTarget = null;
                }
                if (GUILayout.Button("Cancel", GUILayout.Height(22f)))
                {
                    _showRenameFolderPopup = false;
                    _renameFolderTarget = null;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            if (_showRenamePosePopup)
            {
                var sel = _filteredItems.Where(i => i.IsSelected).ToList();
                if (sel.Count != 1) _showRenamePosePopup = false;
                else
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label("Rename pose:");
                    _renamePoseText = GUILayout.TextField(_renamePoseText);
                    _renamePoseAlsoFile = GUILayout.Toggle(_renamePoseAlsoFile, "Rename file to match (safe name)");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("OK", GUILayout.Height(22f)))
                    {
                        if (_dataService.RenamePoseDisplayNameAndOptionalFile(sel[0], _renamePoseText, _renamePoseAlsoFile, _tagDb))
                        {
                            _allItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
                            ApplyFilters();
                            StartThumbnailLoading();
                        }
                        _showRenamePosePopup = false;
                    }
                    if (GUILayout.Button("Cancel", GUILayout.Height(22f)))
                        _showRenamePosePopup = false;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
            }

        }

        private void CancelMoveCopyOperation()
        {
            _moveCopyPending = false;
            _moveCopyDestPath = null;
            ReloadCurrentView();
        }

        private void ApplyMoveCopyOperation()
        {
            if (!_moveCopyPending || string.IsNullOrEmpty(_moveCopyDestPath))
                return;
            var sel = _filteredItems.Where(i => i.IsSelected).ToList();
            if (sel.Count == 0)
            {
                CancelMoveCopyOperation();
                return;
            }

            string dest = _moveCopyDestPath;
            if (_moveCopyIsCopy)
            {
                foreach (var it in sel)
                    _dataService.CopyPoseFileToFolder(it, dest, _tagDb);
            }
            else
            {
                foreach (var it in sel)
                    _dataService.MovePoseFileToFolder(it, dest, _tagDb);
            }

            _moveCopyPending = false;
            _moveCopyDestPath = null;
            ReloadCurrentView();
        }

        private void DrawTagPopup(List<PoseGridItem> selected)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Add tags to {selected.Count} pose(s):");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tags:", GUILayout.Width(40f));
            _tagPopupText = GUILayout.TextField(_tagPopupText, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Label("(comma-separated)", GUILayout.Height(16f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", GUILayout.Height(24f)))
            {
                var tags = _tagPopupText.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0);
                foreach (var it in selected)
                    _tagDb.AddTags(it, tags);
                _showTagPopup = false;
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                _showTagPopup = false;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void ShowUpdatePoseOptions(PoseGridItem item)
        {
            _pendingUpdateItem = item;
            _pendingUpdateMode = UpdateMode.KeepThumb;
        }

        private void DrawUpdatePopup()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Update pose: {_pendingUpdateItem?.DisplayName}");
            GUILayout.Label("Overwrite pose data with current character pose.");
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            if (_pendingUpdateItem?.IsPng == true)
            {
                if (GUILayout.Button("Keep Thumbnail", GUILayout.Height(24f)))
                {
                    DoUpdatePose(_pendingUpdateItem!, null);
                    _pendingUpdateMode = UpdateMode.None;
                }
            }
            if (GUILayout.Button("New Thumbnail", GUILayout.Height(24f)))
            {
                _pendingUpdateMode = UpdateMode.NewThumb;
                StartUpdateCapture(_pendingUpdateItem!);
            }
            if (_pendingUpdateItem?.IsPng != true)
            {
                if (GUILayout.Button("No Thumbnail", GUILayout.Height(24f)))
                {
                    DoUpdatePose(_pendingUpdateItem!, null);
                    _pendingUpdateMode = UpdateMode.None;
                }
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                _pendingUpdateMode = UpdateMode.None;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // ── Actions ──

        private void LoadFolder(string path)
        {
            StopThumbnailLoading();
            _viewAllPosesRecursive = false;
            _allItems = _dataService.LoadPosesFromFolder(path);
            foreach (var item in _allItems)
                _tagDb.ApplyToItem(item);

            _allItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            ApplyFilters();
            _gridScroll = Vector2.zero;
            _lastClickedGlobalIndex = -1;

            StartThumbnailLoading();
        }

        private void LoadAllPosesFromTreeRoot()
        {
            StopThumbnailLoading();
            _viewAllPosesRecursive = true;
            _allItems = _dataService.LoadPosesRecursive(_folderTree.RootPath);
            foreach (var item in _allItems)
                _tagDb.ApplyToItem(item);

            _allItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            ApplyFilters();
            _gridScroll = Vector2.zero;
            _lastClickedGlobalIndex = -1;

            StartThumbnailLoading();
        }

        private void ReloadCurrentView()
        {
            if (_viewAllPosesRecursive)
                LoadAllPosesFromTreeRoot();
            else if (_folderTree.SelectedNode != null)
                LoadFolder(_folderTree.SelectedNode.FullPath);
            else
                LoadFolder(_folderTree.RootPath);
        }

        private void ApplyFilters()
        {
            _searchRegexError = "";
            Regex? searchRx = null;
            if (_searchUseRegex && !string.IsNullOrWhiteSpace(_searchText))
            {
                try
                {
                    searchRx = new Regex(_searchText, RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    _searchRegexError = "Regex: " + ex.Message;
                }
            }

            _filteredItems = _allItems.Where(item =>
            {
                if (_showFavoritesOnly && !item.IsFavorite)
                    return false;

                if (!string.IsNullOrEmpty(_searchText))
                {
                    if (_searchUseRegex)
                    {
                        if (searchRx == null)
                            return true;
                        try
                        {
                            if (!searchRx.IsMatch(item.DisplayName))
                                return false;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    else if (item.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }
                }

                if (_activeTagFilters.Count > 0)
                {
                    if (_tagFilterAndMode)
                    {
                        if (!_activeTagFilters.All(t => item.Tags.Contains(t)))
                            return false;
                    }
                    else
                    {
                        if (!_activeTagFilters.Any(t => item.Tags.Contains(t)))
                            return false;
                    }
                }

                return true;
            }).ToList();

            _currentPage = 1;
            ClampCurrentPage();
        }

        private void StartThumbnailLoading()
        {
            _thumbnailLoadIndex = 0;
            _thumbnailLoadCoroutine = StartCoroutine(LoadThumbnailsCoroutine());
        }

        private void StopThumbnailLoading()
        {
            if (_thumbnailLoadCoroutine != null)
            {
                StopCoroutine(_thumbnailLoadCoroutine);
                _thumbnailLoadCoroutine = null;
            }
        }

        private IEnumerator LoadThumbnailsCoroutine()
        {
            const int batchSize = 5;
            while (_thumbnailLoadIndex < _allItems.Count)
            {
                int end = Mathf.Min(_thumbnailLoadIndex + batchSize, _allItems.Count);
                for (int i = _thumbnailLoadIndex; i < end; i++)
                {
                    var item = _allItems[i];
                    if (item.Thumbnail == null && item.IsPng)
                        item.Thumbnail = _dataService.LoadThumbnailTexture(item);
                }
                _thumbnailLoadIndex = end;
                yield return null;
            }
            _thumbnailLoadCoroutine = null;
        }

        private void StartThumbnailCapture(List<PoseGridItem> items)
        {
            _thumbCapture.StartCapture(
                this,
                items,
                onApplyPose: item => _dataService.ApplyPoseToSelected(item),
                onCaptured: (item, pngBytes) =>
                {
                    if (item.IsPng)
                    {
                        string oldRel = item.RelativePath(_dataService.PoseRootPath);
                        _dataService.BackupFile(item.FilePath);

                        byte[] poseData = _dataService.ReadPoseDataBytes(item);
                        using var fs = new FileStream(item.FilePath, FileMode.Create, FileAccess.Write);
                        using var bw = new BinaryWriter(fs);
                        bw.Write(pngBytes);
                        bw.Write(poseData);
                        item.DataPosition = pngBytes.Length;
                    }
                    else
                    {
                        string oldRel = item.RelativePath(_dataService.PoseRootPath);
                        _dataService.ConvertDatToPng(item, pngBytes);
                        _tagDb.OnItemPathChanged(oldRel, item);
                    }

                    var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    tex.LoadImage(pngBytes);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    if (item.Thumbnail != null) Destroy(item.Thumbnail);
                    item.Thumbnail = tex;
                },
                onComplete: () => { }
            );
        }

        private void DoSavePose()
        {
            if (string.IsNullOrWhiteSpace(_savePoseName)) return;
            var chars = _dataService.GetSelectedCharacters().ToList();
            if (chars.Count == 0)
            {
                SandboxServices.Log.LogWarning("PoseBrowser: No character selected for saving.");
                return;
            }

            string folder = SaveTargetFolderPath;
            string baseName = _savePoseName.Trim();

            _thumbCapture.StartCapture(
                this,
                new List<PoseGridItem> { new PoseGridItem { FilePath = Path.Combine(folder, baseName + ".png"), DisplayName = baseName } },
                onApplyPose: _ => { },
                onCaptured: (_, pngBytes) =>
                {
                    if (chars.Count == 1)
                    {
                        string filePath = Path.Combine(folder, PoseDataService.SanitizeFileName(baseName) + ".png");
                        filePath = PoseDataService.GetUniqueFilePath(filePath);
                        _dataService.SavePose(filePath, baseName, pngBytes, chars[0]);
                    }
                    else
                    {
                        foreach (var oci in chars)
                        {
                            string stub = PoseDataService.SanitizeFileName($"{baseName} — {PoseDataService.GetOCICharDisplayName(oci)}");
                            if (string.IsNullOrEmpty(stub))
                                stub = PoseDataService.SanitizeFileName(baseName);
                            string filePath = Path.Combine(folder, stub + ".png");
                            filePath = PoseDataService.GetUniqueFilePath(filePath);
                            _dataService.SavePose(filePath, baseName, pngBytes, oci);
                        }
                    }

                    ReloadCurrentView();
                },
                onComplete: () => { }
            );
        }

        private void DoUpdatePose(PoseGridItem item, byte[]? newPngBytes)
        {
            var chars = _dataService.GetSelectedCharacters().ToList();
            if (chars.Count == 0)
            {
                SandboxServices.Log.LogWarning("PoseBrowser: No character selected for update.");
                return;
            }

            _dataService.UpdatePose(item, chars[0], newPngBytes);
            for (int i = 1; i < chars.Count; i++)
                _dataService.ApplyPose(item, chars[i]);
            if (newPngBytes != null)
            {
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(newPngBytes);
                tex.wrapMode = TextureWrapMode.Clamp;
                if (item.Thumbnail != null) Destroy(item.Thumbnail);
                item.Thumbnail = tex;
            }
        }

        private void StartUpdateCapture(PoseGridItem item)
        {
            _thumbCapture.StartCapture(
                this,
                new List<PoseGridItem> { item },
                onApplyPose: _ => { },
                onCaptured: (capturedItem, pngBytes) =>
                {
                    DoUpdatePose(capturedItem, pngBytes);
                    _pendingUpdateMode = UpdateMode.None;
                },
                onComplete: () => { _pendingUpdateMode = UpdateMode.None; }
            );
        }

        // ── Styles ──

        private void InitStyles()
        {
            if (_selectedStyle != null) return;

            _selectedStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.3f, 0.6f, 1f, 0.4f)) }
            };

            _favoriteStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0f) }
            };

            _treeNodeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 0, 0)
            };

            _treeNodeSelectedStyle = new GUIStyle(_treeNodeStyle)
            {
                normal = { background = MakeTex(2, 2, new Color(0.3f, 0.6f, 1f, 0.4f)),
                           textColor = Color.white }
            };

            _tagWrapStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = Mathf.Max(10, GUI.skin.label.fontSize - 1),
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.72f, 0.72f, 0.72f) }
            };

            _headerSectionCaptionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, GUI.skin.label.fontSize - 1),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 6, 0, 0),
                normal = { textColor = new Color(0.52f, 0.55f, 0.6f) }
            };

            _characterHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, GUI.skin.label.fontSize - 1),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.62f, 0.66f, 0.7f) }
            };
        }

        private static GUIStyle? _poseBrowserTooltipStyle;

        private static void DrawPoseBrowserTooltip(string text, Rect windowRect)
        {
            if (_poseBrowserTooltipStyle == null)
            {
                var bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.12f, 0.94f));
                bg.Apply();
                _poseBrowserTooltipStyle = new GUIStyle(GuiSkinHelper.SafeLabelStyle())
                {
                    normal = { background = bg, textColor = new Color(0.92f, 0.92f, 0.94f) },
                    padding = new RectOffset(6, 6, 4, 4),
                    wordWrap = true,
                    fontSize = 11,
                    richText = false
                };
            }

            float boxW = Mathf.Min(_poseBrowserTooltipStyle.CalcSize(new GUIContent(text)).x + 12f, 280f);
            float boxH = _poseBrowserTooltipStyle.CalcHeight(new GUIContent(text), boxW) + 8f;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            float x = Mathf.Clamp(mouse.x + 14f, 0f, windowRect.width - boxW);
            float y = Mathf.Clamp(mouse.y + 20f, 0f, windowRect.height - boxH);
            GUI.Label(new Rect(x, y, boxW, boxH), text, _poseBrowserTooltipStyle);
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void DrawHelpWindowContent(int id)
        {
            var rich = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                wordWrap = true
            };

            _helpScroll = GUILayout.BeginScrollView(_helpScroll);
            GUILayout.Label("<b>Pose Browser — quick manual</b>", rich);
            GUILayout.Space(4f);
            GUILayout.Label(
                "Library: <b>UserData/studio/pose</b>. Open from the Sandbox toolbar (pose icon). Resize the main window from the bottom-right grip.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Poses — top bar</b>", rich);
            GUILayout.Label(
                "• <b>Search</b> — filter by name/path. <b>.*</b> = case-insensitive regex (bad patterns show in red).\n" +
                "• <b>★</b> — only favorites.\n" +
                "• <b>AND / OR</b> — combine tag filters.\n" +
                "• <b>Tags (n)</b> — pick tags from the list; Clear All clears filters only.\n" +
                "• <b>Save Pose</b> — save current character pose into the active folder (selected folder, pose root in <b>All poses</b> view).",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Character row</b>", rich);
            GUILayout.Label(
                "Shows how many Studio characters are selected and their names (hover tooltip). Pose apply/save uses characters only (ignores props, etc.). Multi-select: pose applies to every selected character.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Folders (left)</b>", rich);
            GUILayout.Label(
                "• <b>↻</b> — refresh tree.\n" +
                "• <b>All poses</b> — every subfolder, recursively (disabled while Move/Copy destination pick is active).\n" +
                "• <b>Root only</b> — files in the pose root only; during Move/Copy, also picks <b>pose root</b> as destination.\n" +
                "• Click a folder name — browse that folder, or during Move/Copy sets <b>destination</b> without changing the grid.\n" +
                "• Footer: <b>New folder</b>, <b>Rename</b> / <b>Delete</b> (empty only); during Move/Copy, <b>Apply</b>/<b>Cancel</b> appear at the top of this footer.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Grid</b>", rich);
            GUILayout.Label(
                "• Checkbox — select without applying.\n" +
                "• <b>Left-click</b> thumbnail — select one + apply pose.\n" +
                "• <b>Ctrl+click</b> — add/remove from selection.\n" +
                "• <b>Shift+click</b> — range select in the filtered list.\n" +
                "• <b>Right-click</b> thumbnail — apply pose only (selection unchanged).\n" +
                "• With pagination (Options), use ◀ ▶; card width slider controls minimum size; extra width fills the row or adds columns.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Selection bar (bottom)</b>", rich);
            GUILayout.Label(
                "Shown when something is selected: <b>Update Pose</b> (one), <b>Rename…</b>, <b>Tag Selected</b>, <b>Fav Selected</b>, <b>Thumbs…</b> (capture overlay), <b>Move…</b> / <b>Copy…</b> (pick destination in the folder tree, then <b>Apply</b> in the left panel), <b>Delete…</b> (backup to <b>!_AutoBackup</b> then remove), <b>Deselect</b>.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Options panel</b>", rich);
            GUILayout.Label(
                "Card width, items per page (0 = all on one scroll), select/deselect all filtered. Settings persist in BepInEx config under <b>com.hs2.sandbox</b>.",
                rich);

            GUILayout.Space(10f);
            GUILayout.Label("<b>HS2Wiki</b>", rich);
            GUILayout.Label(
                "If the <b>HS2Wiki</b> plugin is installed, press <b>F3</b> for the full in-game manual: category <b>HS2 Sandbox / Pose Browser</b> with linked pages, images, and image viewer.",
                rich);
            if (GUILayout.Button("Open wiki: Overview", GUILayout.Height(24f)))
                PoseBrowserWikiRegistration.TryOpenWikiPage(
                    PoseBrowserWikiRegistration.WikiCategoryRoot,
                    PoseBrowserWikiRegistration.PageOverview);

            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("Close help", GUILayout.Height(26f)))
                _showHelpPane = false;

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawOptionsWindowContent(int id)
        {
            GUILayout.Label("Card / thumbnail width (px)");
            _cardCellSize = GUILayout.HorizontalSlider(_cardCellSize, MinCardSize, MaxCardSize);
            GUILayout.Label($"{Mathf.Round(_cardCellSize)} px column");

            GUILayout.Space(10f);
            GUILayout.Label("Pagination: max items per page (0 = show all)");
            GUILayout.BeginHorizontal();
            _itemsPerPageEdit = GUILayout.TextField(_itemsPerPageEdit, GUILayout.Width(56f));
            if (GUILayout.Button("Apply", GUILayout.Width(52f), GUILayout.Height(22f)))
            {
                if (int.TryParse(_itemsPerPageEdit.Trim(), out int v) && v >= 0)
                {
                    _itemsPerPage = v;
                    ClampCurrentPage();
                    _gridScroll = Vector2.zero;
                    SavePersistedOptions();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select all filtered", GUILayout.Height(24f)))
            {
                foreach (var it in _filteredItems)
                    it.IsSelected = true;
            }
            if (GUILayout.Button("Deselect all", GUILayout.Height(24f)))
            {
                foreach (var it in _filteredItems)
                    it.IsSelected = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close panel", GUILayout.Height(26f)))
            {
                SavePersistedOptions();
                _showOptionsPane = false;
            }

            if (GUI.changed)
                SavePersistedOptions();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        // ── Resize ──

        private void HandleResize()
        {
            Event e = Event.current;
            if (e == null) return;

            var handleRect = new Rect(
                windowRect.x + windowRect.width - ResizeHandleSize,
                windowRect.y + windowRect.height - ResizeHandleSize,
                ResizeHandleSize, ResizeHandleSize);

            if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
            {
                _isResizing = true;
                e.Use();
            }
            else if (_isResizing && e.type == EventType.MouseDrag)
            {
                windowRect.width = Mathf.Clamp(e.mousePosition.x - windowRect.x, MinWidth, MaxWidth);
                windowRect.height = Mathf.Clamp(e.mousePosition.y - windowRect.y, MinHeight, MaxHeight);
                e.Use();
            }
            else if (_isResizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _isResizing = false;
                e.Use();
            }
        }

        private static string PersistedOptionsPath =>
            Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_browser_options.json");

        private void LoadPersistedOptions()
        {
            try
            {
                string path = PersistedOptionsPath;
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonUtility.FromJson<PoseBrowserPersistedOptions>(json);
                if (data == null) return;

                _cardCellSize = Mathf.Clamp(data.cardCellSize, MinCardSize, MaxCardSize);
                _itemsPerPage = Mathf.Max(0, data.itemsPerPage);
                _itemsPerPageEdit = _itemsPerPage.ToString();
                ClampCurrentPage();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not load pose_browser_options.json: {ex.Message}");
            }
        }

        private void SavePersistedOptions()
        {
            try
            {
                string path = PersistedOptionsPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new PoseBrowserPersistedOptions
                {
                    cardCellSize = _cardCellSize,
                    itemsPerPage = _itemsPerPage
                };
                File.WriteAllText(path, JsonUtility.ToJson(data, true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not save pose_browser_options.json: {ex.Message}");
            }
        }
    }

    [Serializable]
    internal sealed class PoseBrowserPersistedOptions
    {
        public float cardCellSize = 140f;
        public int itemsPerPage;
    }
}
