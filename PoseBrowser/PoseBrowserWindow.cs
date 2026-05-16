using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PoseBrowserWindow : SubWindow
    {
        private const float ResizeHandleSize = 18f;
        private const float TreePanelWidth = 200f;
        private const float BottomBarHeight = 36f;
        private const float TopBarHeight = 32f;
        private const float MinCardSize = 96f;
        private const float MaxCardSize = 280f;
        private const int OptionsWindowId = 2021;
        private const int HelpWindowId = 2022;
        private const int TagWindowId = 2024;
        private const int SortWindowId = 2025;

        private enum PoseBrowserLayoutTier
        {
            Normal = 0,
            CompactList = 1,
            CompactMini = 2
        }

        private enum MiniBrowseKind
        {
            RootOnly,
            Folder,
            AllPoses,
            Favorites
        }

        private readonly struct MiniBrowseTarget
        {
            public readonly MiniBrowseKind Kind;
            public readonly PoseFolderNode? Node;

            public MiniBrowseTarget(MiniBrowseKind kind, PoseFolderNode? node = null)
            {
                Kind = kind;
                Node = node;
            }
        }

        private PoseBrowserLayoutTier _layoutTier = PoseBrowserLayoutTier.Normal;
        private int _compactPoseIndex = -1;
        private Vector2 _compactListScroll;

        // Per–layout-tier main window rect (persisted so switching modes restores size/position).
        private float _savedFullW = 900f, _savedFullH = 620f, _savedFullX = 200f, _savedFullY = 80f;
        private float _savedListW = 520f, _savedListH = 400f, _savedListX = 200f, _savedListY = 80f;
        private float _savedMiniW = 280f, _savedMiniH = 240f, _savedMiniX = 200f, _savedMiniY = 80f;

        private static float LayoutMinWidthFor(PoseBrowserLayoutTier tier) => tier switch
        {
            PoseBrowserLayoutTier.Normal => 780f,
            PoseBrowserLayoutTier.CompactList => 300f,
            PoseBrowserLayoutTier.CompactMini => 200f,
            _ => 780f
        };

        private static float LayoutMaxWidthFor(PoseBrowserLayoutTier tier) =>
            tier == PoseBrowserLayoutTier.CompactMini ? 440f : 1400f;

        private static float LayoutMinHeightFor(PoseBrowserLayoutTier tier) => tier switch
        {
            PoseBrowserLayoutTier.Normal => 500f,
            PoseBrowserLayoutTier.CompactList => 278f,
            PoseBrowserLayoutTier.CompactMini => 152f,
            _ => 500f
        };

        private static float LayoutMaxHeightFor(PoseBrowserLayoutTier tier) =>
            tier == PoseBrowserLayoutTier.CompactMini ? 320f : 1000f;

        private float LayoutMinWidth => LayoutMinWidthFor(_layoutTier);
        private float LayoutMaxWidth => LayoutMaxWidthFor(_layoutTier);
        private float LayoutMinHeight => LayoutMinHeightFor(_layoutTier);
        private float LayoutMaxHeight => LayoutMaxHeightFor(_layoutTier);

        private float _cardCellSize = 140f;
        private int _itemsPerPage;
        private int _currentPage = 1;

        private bool _viewAllPosesRecursive;
        private bool _browseFavoritesOnly;

        private enum PoseSortMode
        {
            LastUsed = 0,
            LastUpdated = 1,
            LastCreated = 2,
            Name = 3
        }

        private PoseSortMode _poseSortMode = PoseSortMode.Name;
        private bool _sortAscending = true;

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
        private bool _showFavoritesOnly;

        private enum TagWindowPurpose { None, FilterLibrary, EditSelection }
        private TagWindowPurpose _tagWindowPurpose = TagWindowPurpose.None;
        private Rect _tagWindowRect;
        private Vector2 _tagWindowScroll;
        private string _tagWindowSearch = "";

        // Multi-select (global index into _filteredItems when paginating)
        private int _lastClickedGlobalIndex = -1;

        private bool _showOptionsPane;
        private Rect _optionsWindowRect;

        private bool _showHelpPane;
        private Rect _helpWindowRect;
        private Vector2 _helpScroll;

        private bool _showSortPane;
        private Rect _sortWindowRect;

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
        private GUIStyle? _favoriteCardStyle;
        private GUIStyle? _favoriteStyle;
        private GUIStyle? _treeNodeStyle;
        private GUIStyle? _treeNodeSelectedStyle;
        private GUIStyle? _tagWrapStyle;
        private GUIStyle? _headerSectionCaptionStyle;
        private GUIStyle? _characterHintStyle;
        private GUIStyle? _compactWordWrapStyle;

        // BepInEx config ↔ Options panel (grid settings); hotkeys use KeyboardShortcut in Configuration Manager
        private EventHandler? _cfgGridHandler;

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
            _optionsWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 340f, windowRect.height);
            _helpWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 340f, windowRect.height);
            _tagWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 288f, windowRect.height);
            _sortWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 260f, windowRect.height);
            PoseBrowserConfig.Register(SandboxServices.Config);
            if (PoseBrowserConfig.CardColumnWidth != null)
            {
                _cfgGridHandler = (_, __) => ApplyPoseBrowserConfigToUi();
                PoseBrowserConfig.CardColumnWidth.SettingChanged += _cfgGridHandler;
                PoseBrowserConfig.ItemsPerPage!.SettingChanged += _cfgGridHandler;
            }

            LoadPersistedOptions();
            ApplyPoseBrowserConfigToUi();
            SyncWindowTitleForLayoutTier();
        }

        private void Update()
        {
            _tagDb?.Update();
            if (isVisible)
                MaybeProcessPoseBrowserHotkeys();
        }

        private void OnDestroy()
        {
            if (PoseBrowserConfig.CardColumnWidth != null && _cfgGridHandler != null)
            {
                PoseBrowserConfig.CardColumnWidth.SettingChanged -= _cfgGridHandler;
                PoseBrowserConfig.ItemsPerPage!.SettingChanged -= _cfgGridHandler;
            }

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
            windowRect.width = Mathf.Clamp(windowRect.width, LayoutMinWidth, LayoutMaxWidth);
            windowRect.height = Mathf.Clamp(windowRect.height, LayoutMinHeight, LayoutMaxHeight);
            windowRect.x = Mathf.Clamp(windowRect.x, 4f, Mathf.Max(4f, Screen.width - windowRect.width - 4f));
            windowRect.y = Mathf.Clamp(windowRect.y, 4f, Mathf.Max(4f, Screen.height - windowRect.height - 4f));

            windowRect = GUILayout.Window(windowID, windowRect, DrawWindowContent, windowTitle);

            if (_layoutTier == PoseBrowserLayoutTier.Normal && _showOptionsPane)
            {
                SyncOptionsWindowRect();
                _optionsWindowRect = GUILayout.Window(OptionsWindowId, _optionsWindowRect, DrawOptionsWindowContent, "Pose Browser · Options");
                _optionsWindowRect.x = Mathf.Clamp(_optionsWindowRect.x, 4f, Mathf.Max(4f, Screen.width - _optionsWindowRect.width - 4f));
                _optionsWindowRect.y = Mathf.Clamp(_optionsWindowRect.y, 4f, Mathf.Max(4f, Screen.height - _optionsWindowRect.height - 4f));
                IMGUIUtils.EatInputInRect(_optionsWindowRect);
            }

            if (_layoutTier == PoseBrowserLayoutTier.Normal && _showHelpPane)
            {
                SyncHelpWindowRect();
                _helpWindowRect = GUILayout.Window(HelpWindowId, _helpWindowRect, DrawHelpWindowContent, "Pose Browser · Help");
                _helpWindowRect.x = Mathf.Clamp(_helpWindowRect.x, 4f, Mathf.Max(4f, Screen.width - _helpWindowRect.width - 4f));
                _helpWindowRect.y = Mathf.Clamp(_helpWindowRect.y, 4f, Mathf.Max(4f, Screen.height - _helpWindowRect.height - 4f));
                IMGUIUtils.EatInputInRect(_helpWindowRect);
            }

            if (_layoutTier == PoseBrowserLayoutTier.Normal && _tagWindowPurpose != TagWindowPurpose.None)
            {
                SyncTagWindowRect();
                string tagTitle = _tagWindowPurpose == TagWindowPurpose.FilterLibrary
                    ? "Pose Browser · Tag filter"
                    : "Pose Browser · Tags on selection";
                _tagWindowRect = GUILayout.Window(TagWindowId, _tagWindowRect, DrawTagWindowContent, tagTitle);
                _tagWindowRect.x = Mathf.Clamp(_tagWindowRect.x, 4f, Mathf.Max(4f, Screen.width - _tagWindowRect.width - 4f));
                _tagWindowRect.y = Mathf.Clamp(_tagWindowRect.y, 4f, Mathf.Max(4f, Screen.height - _tagWindowRect.height - 4f));
                IMGUIUtils.EatInputInRect(_tagWindowRect);
            }

            if (_layoutTier == PoseBrowserLayoutTier.Normal && _showSortPane)
            {
                SyncSortWindowRect();
                _sortWindowRect = GUILayout.Window(SortWindowId, _sortWindowRect, DrawSortWindowContent, "Pose Browser · Sort");
                _sortWindowRect.x = Mathf.Clamp(_sortWindowRect.x, 4f, Mathf.Max(4f, Screen.width - _sortWindowRect.width - 4f));
                _sortWindowRect.y = Mathf.Clamp(_sortWindowRect.y, 4f, Mathf.Max(4f, Screen.height - _sortWindowRect.height - 4f));
                IMGUIUtils.EatInputInRect(_sortWindowRect);
            }
        }

        private void SyncOptionsWindowRect()
        {
            _optionsWindowRect = new Rect(windowRect.xMax + 4f, windowRect.y, _optionsWindowRect.width > 0 ? _optionsWindowRect.width : 340f, windowRect.height);
        }

        private void SyncHelpWindowRect()
        {
            float x = windowRect.xMax + 4f;
            if (_showOptionsPane)
                x = _optionsWindowRect.xMax + 4f;
            float w = _helpWindowRect.width > 0 ? _helpWindowRect.width : 340f;
            _helpWindowRect = new Rect(x, windowRect.y, w, windowRect.height);
        }

        private void SyncTagWindowRect()
        {
            float x = windowRect.xMax + 4f;
            if (_showOptionsPane)
                x = _optionsWindowRect.xMax + 4f;
            if (_showHelpPane)
                x = _helpWindowRect.xMax + 4f;
            float w = _tagWindowRect.width > 0 ? _tagWindowRect.width : 288f;
            _tagWindowRect = new Rect(x, windowRect.y, w, windowRect.height);
        }

        private void SyncSortWindowRect()
        {
            float x = windowRect.xMax + 4f;
            if (_showOptionsPane)
                x = _optionsWindowRect.xMax + 4f;
            if (_showHelpPane)
                x = _helpWindowRect.xMax + 4f;
            if (_tagWindowPurpose != TagWindowPurpose.None)
                x = _tagWindowRect.xMax + 4f;
            float w = _sortWindowRect.width > 0 ? _sortWindowRect.width : 260f;
            _sortWindowRect = new Rect(x, windowRect.y, w, windowRect.height);
        }

        private void CloseTagWindow()
        {
            _tagWindowPurpose = TagWindowPurpose.None;
        }

        private void OpenTagFilterWindow()
        {
            _tagWindowPurpose = TagWindowPurpose.FilterLibrary;
            _tagWindowScroll = Vector2.zero;
        }

        private void OpenTagAssignWindow()
        {
            _tagWindowPurpose = TagWindowPurpose.EditSelection;
            _tagWindowSearch = "";
            _tagWindowScroll = Vector2.zero;
        }

        private string SaveTargetFolderPath =>
            _viewAllPosesRecursive || _browseFavoritesOnly
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

        private void ResortPoseItemsInPlace()
        {
            _allItems.Sort((a, b) =>
            {
                int c = _poseSortMode switch
                {
                    PoseSortMode.Name => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
                    PoseSortMode.LastUsed => a.LastUsedUtc.CompareTo(b.LastUsedUtc),
                    PoseSortMode.LastUpdated => a.LastWriteTime.CompareTo(b.LastWriteTime),
                    PoseSortMode.LastCreated => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc),
                    _ => 0
                };
                if (!_sortAscending)
                    c = -c;
                if (c != 0)
                    return c;
                return string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void ApplyPoseToSelectedWithUsage(PoseGridItem item)
        {
            _tagDb.RecordLastUsed(item);
            _dataService.ApplyPoseToSelected(item);
            if (_poseSortMode == PoseSortMode.LastUsed)
            {
                ResortPoseItemsInPlace();
                ApplyFilters();
            }
        }

        private void SelectAllInCurrentFolderView()
        {
            foreach (var it in _allItems)
                it.IsSelected = true;
        }

        private void DeselectAllInCurrentFolderView()
        {
            foreach (var it in _allItems)
                it.IsSelected = false;
        }

        private void CaptureWindowRectForCurrentTier()
        {
            switch (_layoutTier)
            {
                case PoseBrowserLayoutTier.Normal:
                    _savedFullW = windowRect.width;
                    _savedFullH = windowRect.height;
                    _savedFullX = windowRect.x;
                    _savedFullY = windowRect.y;
                    break;
                case PoseBrowserLayoutTier.CompactList:
                    _savedListW = windowRect.width;
                    _savedListH = windowRect.height;
                    _savedListX = windowRect.x;
                    _savedListY = windowRect.y;
                    break;
                case PoseBrowserLayoutTier.CompactMini:
                    _savedMiniW = windowRect.width;
                    _savedMiniH = windowRect.height;
                    _savedMiniX = windowRect.x;
                    _savedMiniY = windowRect.y;
                    break;
            }
        }

        private void RestoreWindowRectForTier(PoseBrowserLayoutTier tier)
        {
            float w, h, x, y;
            switch (tier)
            {
                case PoseBrowserLayoutTier.Normal:
                    w = _savedFullW;
                    h = _savedFullH;
                    x = _savedFullX;
                    y = _savedFullY;
                    break;
                case PoseBrowserLayoutTier.CompactList:
                    w = _savedListW;
                    h = _savedListH;
                    x = _savedListX;
                    y = _savedListY;
                    break;
                default:
                    w = _savedMiniW;
                    h = _savedMiniH;
                    x = _savedMiniX;
                    y = _savedMiniY;
                    break;
            }

            float defW = tier == PoseBrowserLayoutTier.Normal ? 900f : tier == PoseBrowserLayoutTier.CompactList ? 520f : 280f;
            float defH = tier == PoseBrowserLayoutTier.Normal ? 620f : tier == PoseBrowserLayoutTier.CompactList ? 400f : 240f;
            if (w < 50f) w = defW;
            if (h < 50f) h = defH;

            w = Mathf.Clamp(w, LayoutMinWidthFor(tier), LayoutMaxWidthFor(tier));
            h = Mathf.Clamp(h, LayoutMinHeightFor(tier), LayoutMaxHeightFor(tier));
            windowRect = new Rect(x, y, w, h);
        }

        /// <summary>After changing browse target (folder / all / favorites), apply the first filtered pose and sync compact index + grid selection.</summary>
        private void ApplyFirstFilteredPoseAfterBrowseChange()
        {
            if (_filteredItems.Count == 0)
            {
                _compactPoseIndex = -1;
                return;
            }

            var item = _filteredItems[0];
            _compactPoseIndex = 0;
            for (int i = 0; i < _filteredItems.Count; i++)
                _filteredItems[i].IsSelected = false;
            item.IsSelected = true;
            _lastClickedGlobalIndex = 0;
            ApplyPoseToSelectedWithUsage(item);
        }

        private void FinishMainWindowChrome()
        {
            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
                DrawPoseBrowserTooltip(GUI.tooltip, windowRect);

            var resizeHandle = new Rect(windowRect.width - ResizeHandleSize, windowRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            GUI.Box(resizeHandle, "◢");

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - ResizeHandleSize, 20f));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private void SyncWindowTitleForLayoutTier()
        {
            windowTitle = _layoutTier switch
            {
                PoseBrowserLayoutTier.CompactMini => "Pose Browser · Mini",
                PoseBrowserLayoutTier.CompactList => "Pose Browser · Compact",
                _ => "Pose Browser"
            };
        }

        private string LayoutTierShortLabel() => _layoutTier switch
        {
            PoseBrowserLayoutTier.Normal => "Full",
            PoseBrowserLayoutTier.CompactList => "List",
            PoseBrowserLayoutTier.CompactMini => "Mini",
            _ => "Full"
        };

        private void CycleLayoutTier()
        {
            CaptureWindowRectForCurrentTier();
            _layoutTier = (PoseBrowserLayoutTier)(((int)_layoutTier + 1) % 3);

            if (_layoutTier != PoseBrowserLayoutTier.Normal)
            {
                _moveCopyPending = false;
                _moveCopyDestPath = null;
                CloseTagWindow();
                _showOptionsPane = false;
                _showHelpPane = false;
                _showSortPane = false;
                _showSavePopup = false;
                StopThumbnailLoading();
            }
            else
                MaybeStartThumbnailsAfterLoad();

            RestoreWindowRectForTier(_layoutTier);
            SyncWindowTitleForLayoutTier();
            SavePersistedOptions();
        }

        private void MaybeStartThumbnailsAfterLoad()
        {
            if (_layoutTier == PoseBrowserLayoutTier.Normal && _allItems.Count > 0)
                StartThumbnailLoading();
        }

        private void DrawCompactLayoutHeader()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(26f));
            if (GUILayout.Button(new GUIContent($"View ({LayoutTierShortLabel()})", "Cycle: Full → compact list → mini"), GUILayout.Width(110f), GUILayout.Height(24f)))
                CycleLayoutTier();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private List<MiniBrowseTarget> BuildMiniBrowseTargets()
        {
            var list = new List<MiniBrowseTarget> { new MiniBrowseTarget(MiniBrowseKind.RootOnly) };
            foreach (var n in _folderTree.GetAllFoldersDepthFirst())
                list.Add(new MiniBrowseTarget(MiniBrowseKind.Folder, n));
            list.Add(new MiniBrowseTarget(MiniBrowseKind.AllPoses));
            list.Add(new MiniBrowseTarget(MiniBrowseKind.Favorites));
            return list;
        }

        private int GetCurrentMiniBrowseIndex(List<MiniBrowseTarget> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (MiniBrowseTargetMatchesCurrent(targets[i]))
                    return i;
            }
            return 0;
        }

        private bool MiniBrowseTargetMatchesCurrent(MiniBrowseTarget t)
        {
            switch (t.Kind)
            {
                case MiniBrowseKind.RootOnly:
                    return !_viewAllPosesRecursive && !_browseFavoritesOnly && _folderTree.SelectedNode == null;
                case MiniBrowseKind.Folder:
                    return !_viewAllPosesRecursive && !_browseFavoritesOnly &&
                           _folderTree.SelectedNode != null && ReferenceEquals(_folderTree.SelectedNode, t.Node);
                case MiniBrowseKind.AllPoses:
                    return _viewAllPosesRecursive && !_browseFavoritesOnly;
                case MiniBrowseKind.Favorites:
                    return _browseFavoritesOnly;
                default:
                    return false;
            }
        }

        private void ApplyMiniBrowseTarget(MiniBrowseTarget t)
        {
            ClearTreeFolderActionUi();
            _compactPoseIndex = 0;
            switch (t.Kind)
            {
                case MiniBrowseKind.RootOnly:
                    _folderTree.SelectedNode = null;
                    LoadFolder(_folderTree.RootPath);
                    break;
                case MiniBrowseKind.Folder:
                    var node = t.Node!;
                    _folderTree.EnsureExpandedToShow(node);
                    _folderTree.SelectNode(node);
                    LoadFolder(node.FullPath);
                    break;
                case MiniBrowseKind.AllPoses:
                    _folderTree.SelectedNode = null;
                    LoadAllPosesFromTreeRoot();
                    break;
                case MiniBrowseKind.Favorites:
                    LoadFavoritePosesFromTreeRoot();
                    break;
            }
            ApplyFirstFilteredPoseAfterBrowseChange();
        }

        private void AdvanceMiniBrowse(int delta)
        {
            var targets = BuildMiniBrowseTargets();
            if (targets.Count == 0) return;
            int cur = GetCurrentMiniBrowseIndex(targets);
            int n = cur + delta;
            n = ((n % targets.Count) + targets.Count) % targets.Count;
            ApplyMiniBrowseTarget(targets[n]);
        }

        private void AdvanceCompactPose(int delta, bool applyToStudio)
        {
            if (_filteredItems.Count == 0) return;
            ClampCompactPoseIndex();
            if (_compactPoseIndex < 0) _compactPoseIndex = 0;
            _compactPoseIndex = (_compactPoseIndex + delta + _filteredItems.Count) % _filteredItems.Count;
            if (applyToStudio)
                ApplyPoseToSelectedWithUsage(_filteredItems[_compactPoseIndex]);
        }

        private void ClampCompactPoseIndex()
        {
            if (_filteredItems.Count == 0)
            {
                _compactPoseIndex = -1;
                return;
            }
            if (_compactPoseIndex < 0 || _compactPoseIndex >= _filteredItems.Count)
                _compactPoseIndex = 0;
            _compactPoseIndex = Mathf.Clamp(_compactPoseIndex, 0, _filteredItems.Count - 1);
        }

        private string GetCompactMiniFolderCaption()
        {
            if (_browseFavoritesOnly)
                return "★ Favorites · library-wide";
            if (_viewAllPosesRecursive)
                return "All poses · library-wide";
            if (_folderTree.SelectedNode != null)
                return PoseDataService.GetRelativePath(_folderTree.RootPath, _folderTree.SelectedNode.FullPath);
            string rel = PoseDataService.GetRelativePath(_folderTree.RootPath, _folderTree.RootPath);
            if (string.IsNullOrEmpty(rel)) rel = "(pose root)";
            return "Root only · " + rel;
        }

        private void DrawCompactListWindowBody()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawCompactLayoutHeader();
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawTreePanel(showFolderFooter: false);
            DrawCompactPoseListPanel();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawCompactPoseListPanel()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            GUILayout.Label(
                new GUIContent($"{_filteredItems.Count} shown", "After search & tag filters."),
                GUILayout.ExpandWidth(false));
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUI.enabled = _filteredItems.Count > 0;
            if (GUILayout.Button("◀ Prev", GUILayout.Height(26f), GUILayout.MinWidth(72f)))
                AdvanceCompactPose(-1, applyToStudio: true);
            if (GUILayout.Button("Next ▶", GUILayout.Height(26f), GUILayout.MinWidth(72f)))
                AdvanceCompactPose(1, applyToStudio: true);
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            _compactListScroll = GUILayout.BeginScrollView(_compactListScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _filteredItems.Count; i++)
            {
                var item = _filteredItems[i];
                bool rowOn = i == _compactPoseIndex;
                var rowStyle = rowOn ? _treeNodeSelectedStyle! : _treeNodeStyle!;
                string label = (item.IsFavorite ? "★ " : "") + item.DisplayName;
                if (GUILayout.Button(label, rowStyle, GUILayout.Height(22f), GUILayout.ExpandWidth(true)))
                {
                    _compactPoseIndex = i;
                    ApplyPoseToSelectedWithUsage(item);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawCompactMiniWindowBody()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawCompactLayoutHeader();

            GUILayout.BeginHorizontal();
            GUI.enabled = BuildMiniBrowseTargets().Count > 0;
            if (GUILayout.Button("◀ Folder", GUILayout.Height(26f)))
                AdvanceMiniBrowse(-1);
            if (GUILayout.Button("Folder ▶", GUILayout.Height(26f)))
                AdvanceMiniBrowse(1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(GetCompactMiniFolderCaption(), _compactWordWrapStyle!, GUILayout.ExpandWidth(true));

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUI.enabled = _filteredItems.Count > 0;
            if (GUILayout.Button("◀ Pose", GUILayout.Height(26f)))
                AdvanceCompactPose(-1, applyToStudio: true);
            if (GUILayout.Button("Pose ▶", GUILayout.Height(26f)))
                AdvanceCompactPose(1, applyToStudio: true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            string poseLine = _filteredItems.Count == 0 || _compactPoseIndex < 0 || _compactPoseIndex >= _filteredItems.Count
                ? "—"
                : _filteredItems[_compactPoseIndex].DisplayName;
            GUILayout.Label(poseLine, _compactWordWrapStyle!, GUILayout.ExpandWidth(true));

            GUILayout.FlexibleSpace();
            GUI.enabled = _filteredItems.Count > 0 && _compactPoseIndex >= 0 && _compactPoseIndex < _filteredItems.Count;
            if (GUILayout.Button("Reapply", GUILayout.Height(28f)))
                ApplyPoseToSelectedWithUsage(_filteredItems[_compactPoseIndex]);
            GUI.enabled = true;

            GUILayout.EndVertical();
        }

        protected override void DrawWindowContent(int id)
        {
            // Other IMGUI windows may leave GUI.color / GUI.backgroundColor set; those multiply our card tint
            // textures and can wash them out completely.
            Color prevGuiColor = GUI.color;
            Color prevBgColor = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            try
            {
                InitStyles();

                if (_layoutTier == PoseBrowserLayoutTier.CompactMini)
                {
                    DrawCompactMiniWindowBody();
                    FinishMainWindowChrome();
                    return;
                }
                if (_layoutTier == PoseBrowserLayoutTier.CompactList)
                {
                    DrawCompactListWindowBody();
                    FinishMainWindowChrome();
                    return;
                }

                GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                DrawTopBar();

                DrawStudioCharacterSelectionRow();

                GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
                DrawTreePanel(showFolderFooter: true);
                DrawGridPanel();
                GUILayout.EndHorizontal();

                DrawBottomBar();

                DrawFolderPoseDialogs();

                GUILayout.EndVertical();

                FinishMainWindowChrome();
            }
            finally
            {
                GUI.color = prevGuiColor;
                GUI.backgroundColor = prevBgColor;
            }
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

            bool tagFilterPanelOpen = _tagWindowPurpose == TagWindowPurpose.FilterLibrary;
            if (GUILayout.Button(tagFilterPanelOpen ? "Tags ▶" : $"Tags ({_activeTagFilters.Count})", GUILayout.Width(88f)))
            {
                if (tagFilterPanelOpen)
                    CloseTagWindow();
                else
                    OpenTagFilterWindow();
            }

            if (GUILayout.Button(_showSortPane ? "Sort ▶" : "Sort", GUILayout.Width(56f)))
                _showSortPane = !_showSortPane;

            GUILayout.Space(8f);

            if (GUILayout.Button("Save Pose", GUILayout.Width(90f), GUILayout.Height(24f)))
            {
                _showSavePopup = true;
                _savePoseName = "";
            }

            GUILayout.FlexibleSpace();

            DrawTopBarVerticalRule(22f);

            DrawHeaderSectionCaption("Window");

            if (GUILayout.Button(new GUIContent($"View ({LayoutTierShortLabel()})", "Cycle: Full → compact list → mini"), GUILayout.Width(110f), GUILayout.Height(24f)))
                CycleLayoutTier();

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

        private void DrawTagWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("Search tags", GUILayout.Height(18f));
            _tagWindowSearch = GUILayout.TextField(_tagWindowSearch, GUILayout.Height(22f));

            string searchNormFold = _tagWindowSearch.Trim();

            if (_tagWindowPurpose == TagWindowPurpose.FilterLibrary)
                DrawTagWindowFilterBody(searchNormFold);
            else if (_tagWindowPurpose == TagWindowPurpose.EditSelection)
            {
                var selected = _filteredItems.Where(i => i.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    GUILayout.Label("No poses selected — closing.");
                    CloseTagWindow();
                }
                else
                    DrawTagWindowAssignBody(selected, searchNormFold);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Height(26f)))
                CloseTagWindow();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawTagWindowFilterBody(string searchNormFold)
        {
            var allTags = _tagDb.GetAllKnownTags().OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            if (allTags.Count == 0)
            {
                GUILayout.Label("No tags defined yet. Use Tag Selected on the grid.");
                return;
            }

            var visible = string.IsNullOrEmpty(searchNormFold)
                ? allTags
                : allTags.Where(t => t.IndexOf(searchNormFold, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            if (visible.Count == 0)
            {
                GUILayout.Label("No tags match the search.");
                return;
            }

            GUILayout.Space(4f);
            _tagWindowScroll = GUILayout.BeginScrollView(_tagWindowScroll, GUILayout.ExpandHeight(true));
            foreach (var tag in visible)
            {
                bool active = _activeTagFilters.Contains(tag);
                bool newActive = GUILayout.Toggle(active, tag, GUILayout.Height(22f));
                if (newActive != active)
                {
                    if (newActive) _activeTagFilters.Add(tag);
                    else _activeTagFilters.Remove(tag);
                    ApplyFilters();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("Clear active filters", GUILayout.Height(24f)))
            {
                _activeTagFilters.Clear();
                ApplyFilters();
            }
        }

        private enum TagCoverage { None, Some, All }

        private static TagCoverage GetTagCoverage(IReadOnlyList<PoseGridItem> selected, string tag)
        {
            if (selected.Count == 0) return TagCoverage.None;
            int n = selected.Count;
            int c = 0;
            for (int i = 0; i < n; i++)
            {
                if (selected[i].Tags.Contains(tag)) c++;
            }
            if (c == 0) return TagCoverage.None;
            if (c == n) return TagCoverage.All;
            return TagCoverage.Some;
        }

        private void ApplyTagToAllSelected(IReadOnlyList<PoseGridItem> selected, string tag, bool add)
        {
            if (selected.Count == 0) return;
            if (add)
            {
                foreach (var it in selected)
                    _tagDb.AddTags(it, new[] { tag });
            }
            else
            {
                foreach (var it in selected)
                    _tagDb.RemoveTags(it, new[] { tag });
            }
            ApplyFilters();
        }

        private List<string> CollectAssignTagUnion(IReadOnlyList<PoseGridItem> selected)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in _tagDb.GetAllKnownTags())
                set.Add(t);
            foreach (var it in selected)
            {
                foreach (var t in it.Tags)
                    set.Add(t);
            }
            return set.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void DrawTagWindowAssignBody(List<PoseGridItem> selected, string searchNormFold)
        {
            GUILayout.Label($"{selected.Count} pose(s) selected.", GUILayout.Height(20f));

            if (!string.IsNullOrEmpty(searchNormFold))
            {
                bool alreadyKnown = CollectAssignTagUnion(selected).Any(t =>
                    string.Equals(t, searchNormFold, StringComparison.OrdinalIgnoreCase));

                if (!alreadyKnown &&
                    GUILayout.Button($"Add new tag \"{searchNormFold}\" to all selected", GUILayout.Height(26f)))
                {
                    foreach (var it in selected)
                        _tagDb.AddTags(it, new[] { searchNormFold });
                    ApplyFilters();
                }
            }

            var union = CollectAssignTagUnion(selected);
            var visible = string.IsNullOrEmpty(searchNormFold)
                ? union
                : union.Where(t => t.IndexOf(searchNormFold, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            GUILayout.Space(4f);
            GUILayout.Label("Click to set for all selected: ✓ = on all, ☐ = off all, ◪ = mixed (click adds on all).", GUILayout.Height(36f));

            if (visible.Count == 0)
            {
                GUILayout.Label("No tags match the search.");
                return;
            }

            _tagWindowScroll = GUILayout.BeginScrollView(_tagWindowScroll, GUILayout.ExpandHeight(true));
            foreach (var tag in visible)
            {
                var cov = GetTagCoverage(selected, tag);
                if (cov == TagCoverage.Some)
                {
                    var gc = GUI.color;
                    GUI.color = new Color(0.9f, 0.82f, 0.5f);
                    if (GUILayout.Button(
                            new GUIContent($"◪  {tag}", "Mixed: only some selected poses have this tag — click to add on all."),
                            GUILayout.Height(24f)))
                        ApplyTagToAllSelected(selected, tag, add: true);
                    GUI.color = gc;
                }
                else
                {
                    bool on = cov == TagCoverage.All;
                    bool nv = GUILayout.Toggle(on, tag, GUILayout.Height(22f));
                    if (nv != on)
                        ApplyTagToAllSelected(selected, tag, add: nv);
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawSortWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("Click a row to choose the primary sort. Click the same row again to flip ↑ / ↓.", GUILayout.Height(36f));

            void Row(PoseSortMode mode, string label)
            {
                bool on = _poseSortMode == mode;
                string arrow = on ? (_sortAscending ? "↑" : "↓") : "";
                string suffix = on ? $"  {arrow}" : "";
                var st = on ? _treeNodeSelectedStyle! : _treeNodeStyle!;
                if (GUILayout.Button($"{label}{suffix}", st, GUILayout.Height(26f)))
                {
                    if (_poseSortMode == mode)
                        _sortAscending = !_sortAscending;
                    else
                    {
                        _poseSortMode = mode;
                        _sortAscending = true;
                    }
                    ResortPoseItemsInPlace();
                    ApplyFilters();
                    SavePersistedOptions();
                }
            }

            Row(PoseSortMode.LastUsed, "Last used");
            Row(PoseSortMode.LastUpdated, "Last updated (file)");
            Row(PoseSortMode.LastCreated, "Last created (file)");
            Row(PoseSortMode.Name, "Name");

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close sort", GUILayout.Height(26f)))
                _showSortPane = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
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

        private void DrawTreePanel(bool showFolderFooter)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(TreePanelWidth), GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Folders", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("↻", GUILayout.Width(24f), GUILayout.Height(18f)))
                _folderTree.Refresh();
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            _treeScroll = GUILayout.BeginScrollView(_treeScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            bool allViewSelected = !_moveCopyPending && _viewAllPosesRecursive && !_browseFavoritesOnly;
            var allStyle = allViewSelected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            GUI.enabled = !_moveCopyPending;
            if (GUILayout.Button("All poses", allStyle, GUILayout.Height(22f)))
            {
                _viewAllPosesRecursive = true;
                _browseFavoritesOnly = false;
                _folderTree.SelectedNode = null;
                ClearTreeFolderActionUi();
                LoadAllPosesFromTreeRoot();
            }
            GUI.enabled = true;

            bool favSelected = !_moveCopyPending && _browseFavoritesOnly;
            var favStyle = favSelected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            GUI.enabled = !_moveCopyPending;
            if (GUILayout.Button("★ Favorites", favStyle, GUILayout.Height(22f)))
            {
                _browseFavoritesOnly = true;
                _viewAllPosesRecursive = false;
                _folderTree.SelectedNode = null;
                ClearTreeFolderActionUi();
                LoadFavoritePosesFromTreeRoot();
            }
            GUI.enabled = true;

            bool rootOnlyNormalSelected = !_moveCopyPending && !_viewAllPosesRecursive && !_browseFavoritesOnly && _folderTree.SelectedNode == null;
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
                    _browseFavoritesOnly = false;
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

                bool normalSel = !_moveCopyPending && !_viewAllPosesRecursive && !_browseFavoritesOnly && _folderTree.SelectedNode == node;
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
                        _browseFavoritesOnly = false;
                        ClearTreeFolderActionUi();
                        _folderTree.SelectNode(node);
                        LoadFolder(node.FullPath);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            if (showFolderFooter && ((!_viewAllPosesRecursive && !_browseFavoritesOnly) || _moveCopyPending))
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

            GUILayout.BeginHorizontal(GUILayout.Height(22f));
            if (GUILayout.Button(new GUIContent("All", "Select all poses in the current folder / library view"), GUILayout.Width(36f)))
                SelectAllInCurrentFolderView();
            if (GUILayout.Button(new GUIContent("None", "Deselect all poses in the current folder / library view"), GUILayout.Width(44f)))
                DeselectAllInCurrentFolderView();
            GUILayout.Label(
                new GUIContent($"{_allItems.Count} in folder", "Total poses in the current tree scope (before search / tag filters)."),
                GUILayout.Width(78f));
            if (_itemsPerPage > 0 && _filteredItems.Count > 0)
            {
                GUILayout.FlexibleSpace();
                int pages = Mathf.Max(1, Mathf.CeilToInt(_filteredItems.Count / (float)_itemsPerPage));
                GUILayout.Label($"Page {_currentPage}/{pages} · {_filteredItems.Count} shown", GUILayout.Width(168f));
                GUI.enabled = _currentPage > 1;
                if (GUILayout.Button("◀", GUILayout.Width(28f))) { _currentPage--; _gridScroll = Vector2.zero; }
                GUI.enabled = _currentPage < pages;
                if (GUILayout.Button("▶", GUILayout.Width(28f))) { _currentPage++; _gridScroll = Vector2.zero; }
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label(new GUIContent($"{_filteredItems.Count} shown", "After search & tag filters."), GUILayout.Width(72f));
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

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
            GUIStyle cardBox = GUI.skin.box;
            if (item.IsSelected)
                cardBox = _selectedStyle!;
            else if (item.IsFavorite)
                cardBox = _favoriteCardStyle!;

            const float edge = 2f;
            float innerW = Mathf.Max(40f, cellW - edge * 2f);

            GUILayout.BeginVertical(cardBox, GUILayout.Width(cellW), GUILayout.ExpandWidth(false));

            Rect thumbRect = GUILayoutUtility.GetRect(innerW, innerW);
            Texture2D tex = item.Thumbnail ?? _placeholderTex!;

            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit, false);

            const float cbSize = 18f;
            var cbRect = new Rect(thumbRect.xMax - cbSize - 3f, thumbRect.y + 3f, cbSize, cbSize);
            Event evCb = Event.current;
            if (evCb.type == EventType.MouseDown && evCb.button == 0 && cbRect.Contains(evCb.mousePosition))
            {
                int g = DisplayIndexToGlobal(displayIndex);
                if (evCb.shift && _lastClickedGlobalIndex >= 0)
                {
                    int start = Mathf.Min(_lastClickedGlobalIndex, g);
                    int end = Mathf.Max(_lastClickedGlobalIndex, g);
                    for (int i = start; i <= end && i < _filteredItems.Count; i++)
                        _filteredItems[i].IsSelected = true;
                }
                else if (evCb.control)
                {
                    item.IsSelected = !item.IsSelected;
                    _lastClickedGlobalIndex = g;
                }
                else
                {
                    item.IsSelected = !item.IsSelected;
                    _lastClickedGlobalIndex = g;
                }
                evCb.Use();
            }
            GUI.Toggle(cbRect, item.IsSelected, "");

            Event ev = Event.current;
            if (ev.type == EventType.MouseDown && thumbRect.Contains(ev.mousePosition) && !cbRect.Contains(ev.mousePosition))
            {
                HandleItemClick(item, displayIndex);
                ev.Use();
            }

            GUILayout.BeginHorizontal();
            if (item.IsFavorite)
                GUILayout.Label("★", _favoriteStyle!, GUILayout.Width(14f));

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
        }

        private void HandleItemClick(PoseGridItem item, int displayIndex)
        {
            Event e = Event.current;
            int globalIdx = DisplayIndexToGlobal(displayIndex);

            if (e != null && e.button == 1)
            {
                ApplyPoseToSelectedWithUsage(item);
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

                ApplyPoseToSelectedWithUsage(item);
            }
        }

        // ── Bottom Bar ──

        private void DrawBottomBar()
        {
            var selected = _filteredItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                if (_tagWindowPurpose == TagWindowPurpose.EditSelection)
                    CloseTagWindow();
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
                OpenTagAssignWindow();

            if (GUILayout.Button("Fav Selected", GUILayout.Width(100f), GUILayout.Height(24f)))
            {
                foreach (var it in selected)
                    _tagDb.ToggleFavorite(it);
                ResortPoseItemsInPlace();
                ApplyFilters();
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
                            ResortPoseItemsInPlace();
                            ApplyFilters();
                            MaybeStartThumbnailsAfterLoad();
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
            _browseFavoritesOnly = false;
            _allItems = _dataService.LoadPosesFromFolder(path);
            foreach (var item in _allItems)
                _tagDb.ApplyToItem(item);

            ResortPoseItemsInPlace();
            ApplyFilters();
            _gridScroll = Vector2.zero;
            _lastClickedGlobalIndex = -1;

            MaybeStartThumbnailsAfterLoad();
        }

        private void LoadAllPosesFromTreeRoot()
        {
            StopThumbnailLoading();
            _viewAllPosesRecursive = true;
            _browseFavoritesOnly = false;
            _allItems = _dataService.LoadPosesRecursive(_folderTree.RootPath);
            foreach (var item in _allItems)
                _tagDb.ApplyToItem(item);

            ResortPoseItemsInPlace();
            ApplyFilters();
            _gridScroll = Vector2.zero;
            _lastClickedGlobalIndex = -1;

            MaybeStartThumbnailsAfterLoad();
        }

        private void LoadFavoritePosesFromTreeRoot()
        {
            StopThumbnailLoading();
            _browseFavoritesOnly = true;
            _viewAllPosesRecursive = false;
            _folderTree.SelectedNode = null;
            _allItems = _dataService.LoadPosesRecursive(_folderTree.RootPath);
            foreach (var item in _allItems)
                _tagDb.ApplyToItem(item);
            _allItems = _allItems.Where(i => i.IsFavorite).ToList();

            ResortPoseItemsInPlace();
            ApplyFilters();
            _gridScroll = Vector2.zero;
            _lastClickedGlobalIndex = -1;

            MaybeStartThumbnailsAfterLoad();
        }

        private void ReloadCurrentView()
        {
            if (_browseFavoritesOnly)
                LoadFavoritePosesFromTreeRoot();
            else if (_viewAllPosesRecursive)
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
            ClampCompactPoseIndex();
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
                onApplyPose: ApplyPoseToSelectedWithUsage,
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
            _tagDb.RecordLastUsed(item);
            for (int i = 1; i < chars.Count; i++)
                _dataService.ApplyPose(item, chars[i]);
            if (_poseSortMode == PoseSortMode.LastUsed)
            {
                ResortPoseItemsInPlace();
                ApplyFilters();
            }
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
            if (_selectedStyle == null)
            {
                var skinBox = GUI.skin.box;
                // Plain style + zero border: cloning skin.box keeps atlased 9-slice refs on some states and fights our flat tint.
                _selectedStyle = CardTintStyle(skinBox, new Color(0.22f, 0.48f, 0.98f, 0.88f));
                _favoriteCardStyle = CardTintStyle(skinBox, new Color(0.95f, 0.82f, 0.22f, 0.72f));

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

                var treeSelBg = MakeTex(4, 4, new Color(0.22f, 0.48f, 0.98f, 0.88f));
                _treeNodeSelectedStyle = new GUIStyle(_treeNodeStyle);
                ApplyFlatTint(_treeNodeSelectedStyle, treeSelBg, Color.white);

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

            if (_compactWordWrapStyle == null)
            {
                _compactWordWrapStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                };
            }
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

        private static GUIStyle CardTintStyle(GUIStyle skinBox, Color tint)
        {
            var s = new GUIStyle
            {
                margin = skinBox.margin,
                padding = skinBox.padding,
                border = new RectOffset(0, 0, 0, 0),
                clipping = skinBox.clipping
            };
            Texture2D bg = MakeTex(4, 4, tint);
            ApplyFlatBackgroundAllStates(s, bg);
            return s;
        }

        private static void ApplyFlatBackgroundAllStates(GUIStyle s, Texture2D bg)
        {
            s.normal.background = bg;
            s.hover.background = bg;
            s.active.background = bg;
            s.focused.background = bg;
            s.onNormal.background = bg;
            s.onHover.background = bg;
            s.onActive.background = bg;
            s.onFocused.background = bg;
        }

        private static void ApplyFlatTint(GUIStyle s, Texture2D bg, Color textColor)
        {
            void Apply(GUIStyleState st)
            {
                st.background = bg;
                st.textColor = textColor;
            }

            Apply(s.normal);
            Apply(s.hover);
            Apply(s.active);
            Apply(s.focused);
            Apply(s.onNormal);
            Apply(s.onHover);
            Apply(s.onActive);
            Apply(s.onFocused);
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            tex.SetPixels(pix);
            tex.Apply(false, false);
            tex.hideFlags = HideFlags.DontSave;
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
                "• <b>Tags (n)</b> — opens a docked <b>Tag filter</b> window: search, toggle filters (AND/OR stays on the top bar), <b>Clear active filters</b>.\n" +
                "• <b>Sort</b> — opens a docked sort panel: <b>Last used</b> (when poses are applied), <b>Last updated</b> / <b>Last created</b> (file times), <b>Name</b>. First click selects; click again on the same row toggles ↑ / ↓.\n" +
                "• <b>Save Pose</b> — save current character pose into the active folder (selected folder, pose root in <b>All poses</b> or <b>Favorites</b> view).",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Character row</b>", rich);
            GUILayout.Label(
                "Shows how many Studio characters are selected and their names (hover tooltip). Pose apply/save uses characters only (ignores props, etc.). Multi-select: pose applies to every selected character.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Compact views</b>", rich);
            GUILayout.Label(
                "<b>View (…)</b> in the top bar (Window section) cycles <b>Full → List → Mini → Full</b>; choice is saved. Each mode remembers its own window size, position, and resize (stored in <b>pose_browser_options.json</b>).\n" +
                "• <b>List</b> — folder tree + scrollable list of <b>filtered</b> poses (names only; no thumbnails, tags, search bar, or bottom selection bar). <b>Prev / Next</b> applies in list order and wraps. Click a row to apply.\n" +
                "• <b>Mini</b> — <b>Folder</b> arrows walk <b>Root only</b>, every subfolder in depth-first order, <b>All poses</b>, then <b>Favorites</b>, wrapping; the <b>first filtered pose</b> in the new scope is applied immediately. <b>Pose</b> arrows walk the filtered list, apply each step, and wrap. <b>Reapply</b> repeats the current pose.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Folders (left)</b>", rich);
            GUILayout.Label(
                "• <b>↻</b> — refresh tree.\n" +
                "• <b>All poses</b> — every subfolder, recursively (disabled while Move/Copy destination pick is active).\n" +
                "• <b>Favorites</b> — all favorited poses in the library (same recursive scope as All poses).\n" +
                "• <b>Root only</b> — files in the pose root only; during Move/Copy, also picks <b>pose root</b> as destination.\n" +
                "• Click a folder name — browse that folder, or during Move/Copy sets <b>destination</b> without changing the grid.\n" +
                "• Footer: <b>New folder</b>, <b>Rename</b> / <b>Delete</b> (empty only); during Move/Copy, <b>Apply</b>/<b>Cancel</b> appear at the top of this footer.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Grid</b>", rich);
            GUILayout.Label(
                "• Checkbox — select without applying; <b>Ctrl+click</b> and <b>Shift+click</b> work on the checkbox too (range = filtered list).\n" +
                "• <b>Left-click</b> thumbnail — select one + apply pose.\n" +
                "• <b>Ctrl+click</b> — add/remove from selection.\n" +
                "• <b>Shift+click</b> — range select in the filtered list.\n" +
                "• <b>Right-click</b> thumbnail — apply pose only (selection unchanged).\n" +
                "• With pagination (Options), use ◀ ▶; card width slider controls minimum size; extra width fills the row or adds columns.\n" +
                "• <b>All</b> / <b>None</b> above the grid selects or clears every pose in the current view (folder, <b>All poses</b>, or <b>Favorites</b>) — not affected by search/tag filters.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Selection bar (bottom)</b>", rich);
            GUILayout.Label(
                "Shown when something is selected: <b>Update Pose</b> (one), <b>Rename…</b>, <b>Tag Selected</b> (tag editor window: mixed ◪ = partial overlap, click to unify), <b>Fav Selected</b>, <b>Thumbs…</b> (capture overlay), <b>Move…</b> / <b>Copy…</b> (pick destination in the folder tree, then <b>Apply</b> in the left panel), <b>Delete…</b> (backup to <b>!_AutoBackup</b> then remove), <b>Deselect</b>.",
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
            GUILayout.Label("Card / thumbnail width (px). Same value is saved under BepInEx → Pose Browser → Card column width.");
            float newCard = GUILayout.HorizontalSlider(_cardCellSize, MinCardSize, MaxCardSize);
            if (Mathf.Abs(newCard - _cardCellSize) > 0.001f)
                _cardCellSize = newCard;
            GUILayout.Label($"{Mathf.Round(_cardCellSize)} px column");

            GUILayout.Space(10f);
            GUILayout.Label("Pagination: max items per page (0 = show all). Config: Items per page (grid).");
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

            GUILayout.Space(14f);
            GUILayout.Label("Keyboard shortcuts", GUI.skin.label);
            GUILayout.Label(
                "Assigned with the Configuration Manager key picker (BepInEx KeyboardShortcut — same style as Screenshot Manager). Defaults are unassigned (None).",
                GUI.skin.label);
            PoseBrowserConfig.Register(SandboxServices.Config);
            DrawHotkeyReadonlyRow("Next browse (folder step)", PoseBrowserConfig.HotkeyNextBrowse);
            DrawHotkeyReadonlyRow("Previous browse (folder step)", PoseBrowserConfig.HotkeyPrevBrowse);
            DrawHotkeyReadonlyRow("Next pose", PoseBrowserConfig.HotkeyNextPose);
            DrawHotkeyReadonlyRow("Previous pose", PoseBrowserConfig.HotkeyPrevPose);

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
                windowRect.width = Mathf.Clamp(e.mousePosition.x - windowRect.x, LayoutMinWidth, LayoutMaxWidth);
                windowRect.height = Mathf.Clamp(e.mousePosition.y - windowRect.y, LayoutMinHeight, LayoutMaxHeight);
                e.Use();
            }
            else if (_isResizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _isResizing = false;
                CaptureWindowRectForCurrentTier();
                SavePersistedOptions();
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

                if (data.optionsVersion < PoseBrowserConfig.OptionsJsonVersion)
                {
                    PoseBrowserConfig.Register(SandboxServices.Config);
                    if (PoseBrowserConfig.CardColumnWidth != null)
                    {
                        PoseBrowserConfig.CardColumnWidth.Value = Mathf.Clamp(data.cardCellSize, MinCardSize, MaxCardSize);
                        PoseBrowserConfig.ItemsPerPage!.Value = Mathf.Max(0, data.itemsPerPage);
                        SandboxServices.Config.Save();
                    }
                }

                if (data.optionsVersion >= 1)
                {
                    _poseSortMode = (PoseSortMode)Mathf.Clamp(data.poseSortMode, 0, 3);
                    _sortAscending = data.sortAscending;
                }
                else
                {
                    _poseSortMode = PoseSortMode.Name;
                    _sortAscending = true;
                }

                if (data.optionsVersion >= 2)
                {
                    _layoutTier = (PoseBrowserLayoutTier)Mathf.Clamp(data.layoutTier, 0, 2);
                    if (_layoutTier != PoseBrowserLayoutTier.Normal)
                    {
                        CloseTagWindow();
                        _showOptionsPane = false;
                        _showHelpPane = false;
                        _showSortPane = false;
                        StopThumbnailLoading();
                    }
                }
                else
                    _layoutTier = PoseBrowserLayoutTier.Normal;

                if (data.optionsVersion >= 3)
                {
                    if (data.fullWindowW > 10f)
                    {
                        _savedFullW = data.fullWindowW;
                        _savedFullH = data.fullWindowH;
                        _savedFullX = data.fullWindowX;
                        _savedFullY = data.fullWindowY;
                    }
                    if (data.listWindowW > 10f)
                    {
                        _savedListW = data.listWindowW;
                        _savedListH = data.listWindowH;
                        _savedListX = data.listWindowX;
                        _savedListY = data.listWindowY;
                    }
                    if (data.miniWindowW > 10f)
                    {
                        _savedMiniW = data.miniWindowW;
                        _savedMiniH = data.miniWindowH;
                        _savedMiniX = data.miniWindowX;
                        _savedMiniY = data.miniWindowY;
                    }
                }

                ClampCurrentPage();
                RestoreWindowRectForTier(_layoutTier);
                SyncWindowTitleForLayoutTier();
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
                CaptureWindowRectForCurrentTier();
                string path = PersistedOptionsPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new PoseBrowserPersistedOptions
                {
                    optionsVersion = PoseBrowserConfig.OptionsJsonVersion,
                    cardCellSize = _cardCellSize,
                    itemsPerPage = _itemsPerPage,
                    poseSortMode = (int)_poseSortMode,
                    sortAscending = _sortAscending,
                    layoutTier = (int)_layoutTier,
                    fullWindowW = _savedFullW,
                    fullWindowH = _savedFullH,
                    fullWindowX = _savedFullX,
                    fullWindowY = _savedFullY,
                    listWindowW = _savedListW,
                    listWindowH = _savedListH,
                    listWindowX = _savedListX,
                    listWindowY = _savedListY,
                    miniWindowW = _savedMiniW,
                    miniWindowH = _savedMiniH,
                    miniWindowX = _savedMiniX,
                    miniWindowY = _savedMiniY
                };
                File.WriteAllText(path, JsonUtility.ToJson(data, true), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                SyncPoseBrowserConfigFromFile();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not save pose_browser_options.json: {ex.Message}");
            }
        }

        private void ApplyPoseBrowserConfigToUi()
        {
            try
            {
                PoseBrowserConfig.Register(SandboxServices.Config);
                if (PoseBrowserConfig.CardColumnWidth == null) return;
                _cardCellSize = Mathf.Clamp(PoseBrowserConfig.CardColumnWidth.Value, MinCardSize, MaxCardSize);
                _itemsPerPage = Mathf.Max(0, PoseBrowserConfig.ItemsPerPage!.Value);
                _itemsPerPageEdit = _itemsPerPage.ToString();
                ClampCurrentPage();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: ApplyPoseBrowserConfigToUi failed: {ex.Message}");
            }
        }

        private void SyncPoseBrowserConfigFromFile()
        {
            try
            {
                if (PoseBrowserConfig.CardColumnWidth == null) return;
                PoseBrowserConfig.CardColumnWidth.Value = Mathf.Clamp(_cardCellSize, MinCardSize, MaxCardSize);
                PoseBrowserConfig.ItemsPerPage!.Value = Mathf.Max(0, _itemsPerPage);
                SandboxServices.Config.Save();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not write BepInEx Pose Browser config: {ex.Message}");
            }
        }

        private void MaybeProcessPoseBrowserHotkeys()
        {
            if (_thumbCapture.IsActive) return;
            // Avoid firing while typing in any IMGUI field (search, options, etc.).
            if (GUIUtility.keyboardControl != 0) return;

            PoseBrowserConfig.Register(SandboxServices.Config);

            if (PoseBrowserConfig.HotkeyNextBrowse!.Value.IsDown())
            {
                AdvanceMiniBrowse(1);
                return;
            }

            if (PoseBrowserConfig.HotkeyPrevBrowse!.Value.IsDown())
            {
                AdvanceMiniBrowse(-1);
                return;
            }

            if (_filteredItems.Count == 0) return;

            if (PoseBrowserConfig.HotkeyNextPose!.Value.IsDown())
            {
                HotkeyStepPose(1);
                return;
            }

            if (PoseBrowserConfig.HotkeyPrevPose!.Value.IsDown())
                HotkeyStepPose(-1);
        }

        private void HotkeyStepPose(int delta)
        {
            if (_filteredItems.Count == 0) return;
            int idx;
            if (_layoutTier == PoseBrowserLayoutTier.Normal)
                idx = GetPoseStepAnchorIndex();
            else
            {
                ClampCompactPoseIndex();
                idx = _compactPoseIndex;
            }

            idx = (idx + delta + _filteredItems.Count) % _filteredItems.Count;
            _compactPoseIndex = idx;
            var item = _filteredItems[idx];
            ApplyPoseToSelectedWithUsage(item);
            foreach (var it in _filteredItems)
                it.IsSelected = false;
            item.IsSelected = true;
            _lastClickedGlobalIndex = idx;
        }

        private int GetPoseStepAnchorIndex()
        {
            if (_lastClickedGlobalIndex >= 0 && _lastClickedGlobalIndex < _filteredItems.Count)
                return _lastClickedGlobalIndex;
            for (int i = 0; i < _filteredItems.Count; i++)
            {
                if (_filteredItems[i].IsSelected) return i;
            }

            return 0;
        }

        private static void DrawHotkeyReadonlyRow(string label, ConfigEntry<KeyboardShortcut>? entry)
        {
            if (entry == null) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, entry.Description.Description), GUILayout.Width(200f));
            GUILayout.Label(entry.Value.ToString(), GUI.skin.label);
            GUILayout.EndHorizontal();
        }
    }

    [Serializable]
    internal sealed class PoseBrowserPersistedOptions
    {
        public int optionsVersion;
        public float cardCellSize = 140f;
        public int itemsPerPage;
        public int poseSortMode = 3;
        public bool sortAscending = true;
        public int layoutTier;
        public float fullWindowW, fullWindowH, fullWindowX, fullWindowY;
        public float listWindowW, listWindowH, listWindowX, listWindowY;
        public float miniWindowW, miniWindowH, miniWindowX, miniWindowY;
    }
}
