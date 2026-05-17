using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using KKAPI.Studio;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow : SubWindow
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
        private const int CharacterConfigWindowId = 2026;
        private const float DockedPaneGap = 4f;
        private const float OptionsPaneDefaultWidth = 340f;
        private const float HelpPaneDefaultWidth = 340f;
        private const float TagPaneDefaultWidth = 288f;
        private const float CharacterPaneDefaultWidth = 300f;
        private const float SortPaneDefaultWidth = 260f;

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
        private readonly HashSet<string> _tagFiltersInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _tagFiltersExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        private enum PendingFolderOperation
        {
            None,
            MovePoses,
            CopyPoses,
            ImportPosePack,
            ImportTreePack
        }

        private PendingFolderOperation _pendingFolderOp;
        private string? _pendingFolderDestPath;
        /// <summary>When move/copy was started from group-entity selection, apply to these groups (not pose checkboxes).</summary>
        private readonly List<string> _pendingFolderOpGroupIds = new List<string>();

        private PosePackExchange.PosePackReadResult? _importReadResult;
        private readonly Dictionary<string, PosePackExchange.PosePackReadEntry> _importEntryById = new Dictionary<string, PosePackExchange.PosePackReadEntry>(StringComparer.Ordinal);
        private bool _importBrowseSnapshotValid;
        private bool _snapBrowseFavoritesOnly;
        private bool _snapViewAllRecursive;
        private string? _snapSelectedNodeFullPath;
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

        // Background index for All poses / Favorites (warmed after Studio loads)
        private readonly PoseLibraryIndexCache _libraryCache = new PoseLibraryIndexCache();
        private Coroutine? _libraryCacheCoroutine;
        private bool _studioLibraryCacheWarmupTriggered;

        // GUIStyles (lazy-init)
        private GUIStyle? _selectedStyle;
        private GUIStyle? _favoriteCardStyle;
        private GUIStyle? _favoriteCardNameStyle;
        private GUIStyle? _favoriteCardTagStyle;
        private GUIStyle? _favoriteStyle;
        private GUIStyle? _treeNodeStyle;
        private GUIStyle? _treeNodeSelectedStyle;
        private GUIStyle? _tagWrapStyle;
        private GUIStyle? _tagWrapStyleRich;
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
            _groupDb = new PoseGroupDatabase(poseRoot);
            _folderTree = new PoseFolderTree(poseRoot);
            _thumbCapture = new PoseThumbnailCapture();

            CreatePlaceholderTexture();
            _folderTree.Refresh();
            _optionsWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 340f, windowRect.height);
            _helpWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 340f, windowRect.height);
            _tagWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 288f, windowRect.height);
            _sortWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, 260f, windowRect.height);
            _characterConfigWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, CharacterPaneDefaultWidth, windowRect.height);
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
            _groupDb?.Update();
            TryStartStudioLibraryCacheWarmup();
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
            _groupDb?.ForceSave();
            SavePersistedOptions();
            if (_libraryCacheCoroutine != null)
            {
                StopCoroutine(_libraryCacheCoroutine);
                _libraryCacheCoroutine = null;
            }
            _libraryCache.Clear();
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

            if (_layoutTier == PoseBrowserLayoutTier.Normal)
            {
                bool anyDockedPane = _showOptionsPane || _showHelpPane ||
                    _tagWindowPurpose != TagWindowPurpose.None || _showCharacterConfigPane || _showSortPane;
                if (anyDockedPane)
                    SyncAllDockedPaneRects();

                if (_showOptionsPane)
                    DrawDockedPaneWindow(OptionsWindowId, ref _optionsWindowRect, DrawOptionsWindowContent, "Pose Browser · Options", OptionsPaneDefaultWidth);

                if (_showHelpPane)
                    DrawDockedPaneWindow(HelpWindowId, ref _helpWindowRect, DrawHelpWindowContent, "Pose Browser · Help", HelpPaneDefaultWidth);

                if (_tagWindowPurpose != TagWindowPurpose.None)
                {
                    string tagTitle = _tagWindowPurpose == TagWindowPurpose.FilterLibrary
                        ? "Pose Browser · Tag filter"
                        : "Pose Browser · Tags on selection";
                    DrawDockedPaneWindow(TagWindowId, ref _tagWindowRect, DrawTagWindowContent, tagTitle, TagPaneDefaultWidth);
                }

                if (_showCharacterConfigPane)
                    DrawDockedPaneWindow(CharacterConfigWindowId, ref _characterConfigWindowRect, DrawCharacterConfigWindowContent, "Pose Browser · Characters", CharacterPaneDefaultWidth);

                if (_showSortPane)
                    DrawDockedPaneWindow(SortWindowId, ref _sortWindowRect, DrawSortWindowContent, "Pose Browser · Sort", SortPaneDefaultWidth);
            }
        }

        /// <summary>
        /// Lay out side panes in a single chain (options → help → tags → characters → sort) before any are drawn.
        /// </summary>
        private void SyncAllDockedPaneRects()
        {
            float x = windowRect.xMax + DockedPaneGap;
            if (_showOptionsPane)
                x = PlaceDockedPane(ref _optionsWindowRect, x, OptionsPaneDefaultWidth);
            if (_showHelpPane)
                x = PlaceDockedPane(ref _helpWindowRect, x, HelpPaneDefaultWidth);
            if (_tagWindowPurpose != TagWindowPurpose.None)
                x = PlaceDockedPane(ref _tagWindowRect, x, TagPaneDefaultWidth);
            if (_showCharacterConfigPane)
                x = PlaceDockedPane(ref _characterConfigWindowRect, x, CharacterPaneDefaultWidth);
            if (_showSortPane)
                PlaceDockedPane(ref _sortWindowRect, x, SortPaneDefaultWidth);

            ShiftOpenDockedPanesLeftToFitScreen();
        }

        private float PlaceDockedPane(ref Rect pane, float x, float defaultWidth)
        {
            float w = pane.width > 1f ? pane.width : defaultWidth;
            pane = new Rect(x, windowRect.y, w, windowRect.height);
            return x + w + DockedPaneGap;
        }

        private void ShiftOpenDockedPanesLeftToFitScreen()
        {
            const float margin = 4f;
            if (!TryGetOpenDockedPaneBounds(out _, out float maxX))
                return;

            float overflow = maxX - (Screen.width - margin);
            if (overflow <= 0f)
                return;

            if (_showOptionsPane)
                ShiftPaneX(ref _optionsWindowRect, -overflow);
            if (_showHelpPane)
                ShiftPaneX(ref _helpWindowRect, -overflow);
            if (_tagWindowPurpose != TagWindowPurpose.None)
                ShiftPaneX(ref _tagWindowRect, -overflow);
            if (_showCharacterConfigPane)
                ShiftPaneX(ref _characterConfigWindowRect, -overflow);
            if (_showSortPane)
                ShiftPaneX(ref _sortWindowRect, -overflow);
        }

        private bool TryGetOpenDockedPaneBounds(out float minX, out float maxX)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            bool any = false;

            if (_showOptionsPane)
            {
                any = true;
                minX = Mathf.Min(minX, _optionsWindowRect.x);
                maxX = Mathf.Max(maxX, _optionsWindowRect.xMax);
            }

            if (_showHelpPane)
            {
                any = true;
                minX = Mathf.Min(minX, _helpWindowRect.x);
                maxX = Mathf.Max(maxX, _helpWindowRect.xMax);
            }

            if (_tagWindowPurpose != TagWindowPurpose.None)
            {
                any = true;
                minX = Mathf.Min(minX, _tagWindowRect.x);
                maxX = Mathf.Max(maxX, _tagWindowRect.xMax);
            }

            if (_showCharacterConfigPane)
            {
                any = true;
                minX = Mathf.Min(minX, _characterConfigWindowRect.x);
                maxX = Mathf.Max(maxX, _characterConfigWindowRect.xMax);
            }

            if (_showSortPane)
            {
                any = true;
                minX = Mathf.Min(minX, _sortWindowRect.x);
                maxX = Mathf.Max(maxX, _sortWindowRect.xMax);
            }

            return any;
        }

        private static void ShiftPaneX(ref Rect pane, float dx)
        {
            pane = new Rect(pane.x + dx, pane.y, pane.width, pane.height);
        }

        private static void DrawDockedPaneWindow(
            int paneId,
            ref Rect paneRect,
            GUI.WindowFunction drawContent,
            string title,
            float minWidth)
        {
            paneRect = GUILayout.Window(
                paneId,
                paneRect,
                drawContent,
                title,
                GUILayout.MinWidth(minWidth),
                GUILayout.MinHeight(120f));
            paneRect.x = Mathf.Clamp(paneRect.x, 4f, Mathf.Max(4f, Screen.width - paneRect.width - 4f));
            paneRect.y = Mathf.Clamp(paneRect.y, 4f, Mathf.Max(4f, Screen.height - paneRect.height - 4f));
            IMGUIUtils.EatInputInRect(paneRect);
        }

        private void CloseTagWindow()
        {
            _tagWindowPurpose = TagWindowPurpose.None;
            _tagWindowForGroup = false;
            _tagWindowGroupId = null;
            _tagWindowGroupIds.Clear();
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

        private bool IsPickingFolderDestination => _pendingFolderOp != PendingFolderOperation.None;

        private bool ImportPreviewActive => _importReadResult != null;

        private void ClearPendingFolderOperation()
        {
            _pendingFolderOp = PendingFolderOperation.None;
            _pendingFolderDestPath = null;
            _pendingFolderOpGroupIds.Clear();
        }

        /// <summary>Clears destination pick state without restoring browse (e.g. replacing an in-progress import pack).</summary>
        private void ClearPendingFolderPickOnly()
        {
            _pendingFolderOp = PendingFolderOperation.None;
            _pendingFolderDestPath = null;
            _pendingFolderOpGroupIds.Clear();
        }

        private void BeginFolderOpForPoses(PendingFolderOperation op)
        {
            _pendingFolderOp = op;
            _pendingFolderDestPath = SaveTargetFolderPath;
            _pendingFolderOpGroupIds.Clear();
        }

        private void BeginFolderOpForGroupEntities(PendingFolderOperation op)
        {
            _pendingFolderOp = op;
            _pendingFolderDestPath = SaveTargetFolderPath;
            _pendingFolderOpGroupIds.Clear();
            foreach (var gid in _selectedGroupIds)
                _pendingFolderOpGroupIds.Add(gid);
        }

        private string SaveTargetFolderPath =>
            _viewAllPosesRecursive || _browseFavoritesOnly
                ? _folderTree.RootPath
                : (_folderTree.SelectedNode?.FullPath ?? _folderTree.RootPath);

        private List<PoseGridItem> GetGridVisibleItems()
        {
            return GetVisibleDisplayEntries().Select(e => e.Item).ToList();
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
            int pages = Mathf.Max(1, Mathf.CeilToInt(CountDisplayPoses() / (float)_itemsPerPage));
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
                if (ImportPreviewActive || _pendingFolderOp == PendingFolderOperation.ImportPosePack || _pendingFolderOp == PendingFolderOperation.ImportTreePack)
                {
                    ClearPendingFolderOperation();
                    RestoreImportBrowseSnapshot();
                }
                else if (_pendingFolderOp == PendingFolderOperation.MovePoses || _pendingFolderOp == PendingFolderOperation.CopyPoses)
                {
                    ClearPendingFolderOperation();
                    ReloadCurrentView();
                }
                else
                    ClearPendingFolderOperation();

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

        private void TryStartStudioLibraryCacheWarmup()
        {
            if (_studioLibraryCacheWarmupTriggered || _dataService == null || _tagDb == null)
                return;
            if (!StudioAPI.StudioLoaded)
                return;

            _studioLibraryCacheWarmupTriggered = true;
            ScheduleLibraryCacheRebuild();
        }

        private void ScheduleLibraryCacheRebuild()
        {
            if (_libraryCacheCoroutine != null)
                return;
            _libraryCache.MarkStale();
            _libraryCacheCoroutine = StartCoroutine(RebuildLibraryCacheCoroutine());
        }

        private IEnumerator RebuildLibraryCacheCoroutine()
        {
            yield return _libraryCache.BuildCoroutine(_dataService, _tagDb);
            _libraryCacheCoroutine = null;
            if (_viewAllPosesRecursive || _browseFavoritesOnly)
                ReloadCurrentView();
        }

        private void NotifyLibraryCacheFavoriteChanged(IEnumerable<PoseGridItem> items)
        {
            foreach (var it in items)
                _libraryCache.SyncMetadata(it);
        }

        private void NotifyLibraryCachePoseMoved(string oldPath, PoseGridItem item)
        {
            _libraryCache.RemovePath(oldPath);
            _libraryCache.AddOrUpdate(item);
        }

        private void NotifyLibraryCachePoseCopied(PoseGridItem? copy)
        {
            if (copy == null) return;
            _tagDb.ApplyToItem(copy);
            _libraryCache.AddOrUpdate(copy);
        }

        private void NotifyLibraryCachePosesDeleted(IEnumerable<PoseGridItem> items)
        {
            _libraryCache.RemovePaths(items.Select(i => i.FilePath));
        }

        private void NotifyLibraryCacheStructureChanged()
        {
            ScheduleLibraryCacheRebuild();
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
            if (CountDisplayPoses() == 0) return;
            ClampCompactPoseIndex();
            if (_compactPoseIndex < 0) _compactPoseIndex = 0;
            _compactPoseIndex = (_compactPoseIndex + delta + _displayEntries.Count) % _displayEntries.Count;
            if (applyToStudio)
                ApplyPoseToSelectedWithUsage(_displayEntries[_compactPoseIndex].Item);
        }

        private void ClampCompactPoseIndex()
        {
            if (CountDisplayPoses() == 0)
            {
                _compactPoseIndex = -1;
                return;
            }
            if (_compactPoseIndex < 0 || _compactPoseIndex >= _displayEntries.Count)
                _compactPoseIndex = 0;
            _compactPoseIndex = Mathf.Clamp(_compactPoseIndex, 0, _displayEntries.Count - 1);
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
            InitGroupStyles();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            GUILayout.Label(
                new GUIContent($"{CountDisplayPoses()} shown", "After search & tag filters."),
                GUILayout.ExpandWidth(false));
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUI.enabled = CountDisplayPoses() > 0;
            if (GUILayout.Button("◀ Prev", GUILayout.Height(26f), GUILayout.MinWidth(72f)))
                AdvanceCompactPose(-1, applyToStudio: true);
            if (GUILayout.Button("Next ▶", GUILayout.Height(26f), GUILayout.MinWidth(72f)))
                AdvanceCompactPose(1, applyToStudio: true);
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            _compactListScroll = GUILayout.BeginScrollView(_compactListScroll, GUILayout.ExpandHeight(true));
            int i = 0;
            while (i < _displayEntries.Count)
            {
                string? gid = _displayEntries[i].Item.GroupId;
                var group = !string.IsNullOrEmpty(gid) ? _groupDb.TryGetGroup(gid) : null;
                if (group == null)
                {
                    DrawCompactPoseRow(i);
                    i++;
                    continue;
                }

                int start = i;
                while (i < _displayEntries.Count && _displayEntries[i].Item.GroupId == gid)
                    i++;
                GUILayout.BeginVertical(_groupCardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.Label("▦ " + group.Name, _groupTitleStyle!);
                for (int j = start; j < i; j++)
                    DrawCompactPoseRow(j);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawCompactPoseRow(int index)
        {
            var entry = _displayEntries[index];
            var item = entry.Item;
            bool rowOn = index == _compactPoseIndex;
            var rowStyle = rowOn ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            string label = (item.IsFavorite ? "★ " : "") + item.DisplayName;
            Color prev = GUI.color;
            if (entry.IsDimmed) GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            if (GUILayout.Button(label, rowStyle, GUILayout.Height(22f), GUILayout.ExpandWidth(true)))
            {
                _compactPoseIndex = index;
                ApplyPoseToSelectedWithUsage(item);
            }
            GUI.color = prev;
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

            bool inGroup = CompactPoseIndexInGroup(out _, out _, out string? miniGroupName);
            if (inGroup && !string.IsNullOrEmpty(miniGroupName))
            {
                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                GUI.enabled = CountDisplayPoses() > 0;
                if (GUILayout.Button("◀ Group", GUILayout.Height(26f)))
                    AdvanceCompactGroup(-1);
                if (GUILayout.Button("Group ▶", GUILayout.Height(26f)))
                    AdvanceCompactGroup(1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Label("▦ " + miniGroupName, _compactWordWrapStyle!, GUILayout.ExpandWidth(true));
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUI.enabled = CountDisplayPoses() > 0;
            if (GUILayout.Button("◀ Pose", GUILayout.Height(26f)))
                AdvanceCompactPose(-1, applyToStudio: true);
            if (GUILayout.Button("Pose ▶", GUILayout.Height(26f)))
                AdvanceCompactPose(1, applyToStudio: true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            string poseLine = CountDisplayPoses() == 0 || _compactPoseIndex < 0 || _compactPoseIndex >= _displayEntries.Count
                ? "—"
                : _displayEntries[_compactPoseIndex].Item.DisplayName;
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
                float gridMaxW = Mathf.Max(120f, windowRect.width - TreePanelWidth - 24f);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MaxWidth(gridMaxW));
                DrawGridPanel();
                DrawBottomBar();
                DrawFolderPoseDialogs();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

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

            bool tagFilterPanelOpen = _tagWindowPurpose == TagWindowPurpose.FilterLibrary;
            if (GUILayout.Button(tagFilterPanelOpen ? "Tags ▶" : TagFilterBarButtonLabel(), GUILayout.Width(112f)))
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

            if (GUILayout.Button("Import…", GUILayout.Width(70f), GUILayout.Height(24f)))
                PromptImportPoseOrTreePack();

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

            bool charPaneOpen = _showCharacterConfigPane;
            if (GUILayout.Button(charPaneOpen ? "Chars ▶" : "Chars", GUILayout.Width(52f), GUILayout.Height(20f)))
                _showCharacterConfigPane = !charPaneOpen;

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
                if (_tagWindowForGroup)
                {
                    if (_tagWindowGroupIds.Count > 1)
                        DrawTagWindowAssignMultiGroupBody(searchNormFold);
                    else
                    {
                        string? gid = _tagWindowGroupIds.Count == 1
                            ? _tagWindowGroupIds[0]
                            : _tagWindowGroupId;
                        if (string.IsNullOrEmpty(gid))
                        {
                            CloseTagWindow();
                        }
                        else
                        {
                            var group = _groupDb.TryGetGroup(gid);
                            if (group == null)
                                CloseTagWindow();
                            else
                                DrawTagWindowAssignGroupBody(group, searchNormFold);
                        }
                    }
                }
                else
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
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Height(26f)))
                CloseTagWindow();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawTagFilterIncludeModeRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Include match", GUILayout.Width(88f), GUILayout.Height(24f));
            string modeHint = _tagFilterAndMode
                ? "Every + tag must match"
                : "Any + tag may match";
            if (GUILayout.Button(new GUIContent(_tagFilterAndMode ? "AND" : "OR", modeHint), GUILayout.Width(48f), GUILayout.Height(24f)))
            {
                _tagFilterAndMode = !_tagFilterAndMode;
                ApplyFilters();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private string TagFilterBarButtonLabel()
        {
            int inc = _tagFiltersInclude.Count;
            int exc = _tagFiltersExclude.Count;
            if (inc == 0 && exc == 0)
                return "Tags";
            if (exc == 0)
                return $"Tags (+{inc})";
            if (inc == 0)
                return $"Tags (−{exc})";
            return $"Tags (+{inc} −{exc})";
        }

        private enum TagFilterRole { Neutral, Include, Exclude }

        private TagFilterRole GetTagFilterRole(string tag)
        {
            if (_tagFiltersInclude.Contains(tag)) return TagFilterRole.Include;
            if (_tagFiltersExclude.Contains(tag)) return TagFilterRole.Exclude;
            return TagFilterRole.Neutral;
        }

        private void CycleTagFilterRole(string tag)
        {
            switch (GetTagFilterRole(tag))
            {
                case TagFilterRole.Neutral:
                    _tagFiltersInclude.Add(tag);
                    break;
                case TagFilterRole.Include:
                    _tagFiltersInclude.Remove(tag);
                    _tagFiltersExclude.Add(tag);
                    break;
                case TagFilterRole.Exclude:
                    _tagFiltersExclude.Remove(tag);
                    break;
            }
        }

        private static string TagFilterRoleButtonLabel(string tag, TagFilterRole role)
        {
            return role switch
            {
                TagFilterRole.Include => "+ " + tag,
                TagFilterRole.Exclude => "− " + tag,
                _ => tag
            };
        }

        private void DrawTagWindowFilterBody(string searchNormFold)
        {
            DrawTagFilterIncludeModeRow();

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

            GUILayout.Label("Click a tag to cycle: neutral → include (+) → exclude (−).");
            GUILayout.Label("Exclude hides ungrouped poses; in a visible group all members stay shown (excluded ones dimmed).");

            GUILayout.Space(4f);
            _tagWindowScroll = GUILayout.BeginScrollView(_tagWindowScroll, GUILayout.ExpandHeight(true));
            foreach (var tag in visible)
            {
                var role = GetTagFilterRole(tag);
                Color prev = GUI.color;
                if (role == TagFilterRole.Include)
                    GUI.color = new Color(0.55f, 1f, 0.65f, 1f);
                else if (role == TagFilterRole.Exclude)
                    GUI.color = new Color(1f, 0.55f, 0.5f, 1f);

                if (GUILayout.Button(TagFilterRoleButtonLabel(tag, role), GUILayout.Height(22f)))
                {
                    CycleTagFilterRole(tag);
                    ApplyFilters();
                }

                GUI.color = prev;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("Clear active filters", GUILayout.Height(24f)))
            {
                _tagFiltersInclude.Clear();
                _tagFiltersExclude.Clear();
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
            NotifyLibraryCacheFavoriteChanged(selected);
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
                    NotifyLibraryCacheFavoriteChanged(selected);
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
            {
                _folderTree.Refresh();
                NotifyLibraryCacheStructureChanged();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            _treeScroll = GUILayout.BeginScrollView(_treeScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            bool allViewSelected = !IsPickingFolderDestination && _viewAllPosesRecursive && !_browseFavoritesOnly;
            var allStyle = allViewSelected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            GUI.enabled = !IsPickingFolderDestination;
            if (GUILayout.Button("All poses", allStyle, GUILayout.Height(22f)))
            {
                _viewAllPosesRecursive = true;
                _browseFavoritesOnly = false;
                _folderTree.SelectedNode = null;
                ClearTreeFolderActionUi();
                LoadAllPosesFromTreeRoot();
            }
            GUI.enabled = true;

            bool favSelected = !IsPickingFolderDestination && _browseFavoritesOnly;
            var favStyle = favSelected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            GUI.enabled = !IsPickingFolderDestination;
            if (GUILayout.Button("★ Favorites", favStyle, GUILayout.Height(22f)))
            {
                _browseFavoritesOnly = true;
                _viewAllPosesRecursive = false;
                _folderTree.SelectedNode = null;
                ClearTreeFolderActionUi();
                LoadFavoritePosesFromTreeRoot();
            }
            GUI.enabled = true;

            bool rootOnlyNormalSelected = !IsPickingFolderDestination && !_viewAllPosesRecursive && !_browseFavoritesOnly && _folderTree.SelectedNode == null;
            bool rootOnlyMoveDest = IsPickingFolderDestination && !string.IsNullOrEmpty(_pendingFolderDestPath) &&
                Path.GetFullPath(_pendingFolderDestPath).Equals(Path.GetFullPath(_folderTree.RootPath), StringComparison.OrdinalIgnoreCase);
            var rootOnlyStyle = (rootOnlyNormalSelected || rootOnlyMoveDest) ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            if (GUILayout.Button($"📁 Root only", rootOnlyStyle, GUILayout.Height(22f)))
            {
                if (IsPickingFolderDestination)
                {
                    _pendingFolderDestPath = _folderTree.RootPath;
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

                bool normalSel = !IsPickingFolderDestination && !_viewAllPosesRecursive && !_browseFavoritesOnly && _folderTree.SelectedNode == node;
                bool moveDestSel = IsPickingFolderDestination && !string.IsNullOrEmpty(_pendingFolderDestPath) &&
                    Path.GetFullPath(node.FullPath).Equals(Path.GetFullPath(_pendingFolderDestPath), StringComparison.OrdinalIgnoreCase);
                var style = (normalSel || moveDestSel) ? _treeNodeSelectedStyle! : _treeNodeStyle!;
                if (GUILayout.Button(node.Name, style, GUILayout.Height(20f), GUILayout.ExpandWidth(true)))
                {
                    if (IsPickingFolderDestination)
                    {
                        _pendingFolderDestPath = node.FullPath;
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

            if (showFolderFooter && ((!_viewAllPosesRecursive && !_browseFavoritesOnly) || IsPickingFolderDestination))
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

            if (IsPickingFolderDestination)
            {
                var capStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    wordWrap = true
                };
                switch (_pendingFolderOp)
                {
                    case PendingFolderOperation.MovePoses:
                        GUILayout.Label("<b>Move to folder</b>", capStyle);
                        break;
                    case PendingFolderOperation.CopyPoses:
                        GUILayout.Label("<b>Copy to folder</b>", capStyle);
                        break;
                    case PendingFolderOperation.ImportPosePack:
                        GUILayout.Label("<b>Import poses</b> — checked items in the grid are written into:", capStyle);
                        break;
                    case PendingFolderOperation.ImportTreePack:
                        string branch = string.IsNullOrEmpty(_importReadResult?.TreeRootFolderName)
                            ? "folder"
                            : _importReadResult.TreeRootFolderName;
                        GUILayout.Label(
                            $"<b>Import tree branch</b> — pack creates subfolder <b>{branch}</b> with checked poses (pick parent below):",
                            capStyle);
                        break;
                }

                string rel = PoseDataService.GetRelativePath(_folderTree.RootPath, _pendingFolderDestPath ?? "");
                if (string.IsNullOrEmpty(rel)) rel = "(pose root)";
                GUILayout.Label($"Into: <b>{rel}</b>", capStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply", GUILayout.Height(24f)))
                    ApplyPendingFolderOperation();
                if (GUILayout.Button("Cancel", GUILayout.Height(24f)))
                    CancelPendingFolderOperation();
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

                if (!IsPickingFolderDestination && _layoutTier == PoseBrowserLayoutTier.Normal && node != null)
                {
                    GUILayout.Space(4f);
                    if (GUILayout.Button("Export branch…", GUILayout.Height(22f)))
                        ExportFolderBranchToDisk(node);
                }
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

                if (!IsPickingFolderDestination && _layoutTier == PoseBrowserLayoutTier.Normal)
                {
                    GUILayout.Space(4f);
                    if (GUILayout.Button("Export library tree…", GUILayout.Height(22f)))
                        ExportLibraryRootTreeToDisk();
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
                        if (!string.IsNullOrEmpty(created))
                        {
                            _folderTree.RefreshAndSelect(created);
                            if (IsPickingFolderDestination)
                                _pendingFolderDestPath = created;
                            else
                                LoadFolder(created!);
                        }
                        else
                            _folderTree.Refresh();
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
                            string? selectAfter = null;
                            if (!string.IsNullOrEmpty(parent))
                            {
                                string parentFull = Path.GetFullPath(parent);
                                if (!parentFull.Equals(Path.GetFullPath(_folderTree.RootPath), StringComparison.OrdinalIgnoreCase))
                                    selectAfter = parentFull;
                            }

                            _folderTree.RefreshAndSelect(selectAfter);
                            _folderActionError = "";
                            ClearPendingFolderOperation();
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
            InitGroupStyles();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            float vsb = GUI.skin.verticalScrollbar != null ? GUI.skin.verticalScrollbar.fixedWidth : 15f;
            if (vsb < 10f) vsb = 18f;
            float marginH = PoseCardHorizontalMarginBudget();
            const float windowChromePad = 20f;
            const float layoutSlack = 8f;
            float availableForGrid = windowRect.width - TreePanelWidth - windowChromePad - layoutSlack;
            float contentWidth = Mathf.Max(80f, availableForGrid - vsb - 8f);

            ComputeGridCellLayout(contentWidth, marginH, out int columns, out float cell);
            cell = Mathf.Clamp(cell, MinCardSize, MaxCardSize);
            float slotW = contentWidth / Mathf.Max(1, columns);

            var visibleEntries = GetVisibleDisplayEntries();
            ClampCurrentPage();

            GUILayout.BeginHorizontal(GUILayout.Height(22f));
            if (GUILayout.Button(new GUIContent("All", "Select all poses in the current folder / library view"), GUILayout.Width(36f)))
                SelectAllInCurrentFolderView();
            if (GUILayout.Button(new GUIContent("None", "Deselect all poses in the current folder / library view"), GUILayout.Width(44f)))
                DeselectAllInCurrentFolderView();
            GUILayout.Space(6f);
            if (GUILayout.Button(new GUIContent("▦ All", "Select all pose groups in the current folder / library view (group headers)"), GUILayout.Width(40f)))
                SelectAllGroupsInCurrentFolderView();
            if (GUILayout.Button(new GUIContent("▦ None", "Deselect all pose groups (group header selection)"), GUILayout.Width(52f)))
                DeselectAllGroupsInCurrentFolderView();
            GUILayout.Label(
                new GUIContent($"{_allItems.Count} in folder", "Total poses in the current tree scope (before search / tag filters)."),
                GUILayout.Width(78f));
            if (_itemsPerPage > 0 && CountDisplayPoses() > 0)
            {
                GUILayout.FlexibleSpace();
                int pages = Mathf.Max(1, Mathf.CeilToInt(CountDisplayPoses() / (float)_itemsPerPage));
                GUILayout.Label($"Page {_currentPage}/{pages} · {CountDisplayPoses()} shown", GUILayout.Width(168f));
                GUI.enabled = _currentPage > 1;
                if (GUILayout.Button("◀", GUILayout.Width(28f))) { _currentPage--; _gridScroll = Vector2.zero; }
                GUI.enabled = _currentPage < pages;
                if (GUILayout.Button("▶", GUILayout.Width(28f))) { _currentPage++; _gridScroll = Vector2.zero; }
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label(new GUIContent($"{CountDisplayPoses()} shown", "After search & tag filters."), GUILayout.Width(72f));
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            _gridScroll = GUILayout.BeginScrollView(
                _gridScroll,
                alwaysShowHorizontal: false,
                alwaysShowVertical: false,
                GUILayout.Width(contentWidth),
                GUILayout.MaxWidth(contentWidth),
                GUILayout.ExpandHeight(true));

            var gridRows = PoseBrowserGridLayout.BuildGridRows(
                visibleEntries,
                _groupDb,
                columns,
                ImportPreviewActive ? _importPreviewGroupsById : null);
            int displayIdx = _itemsPerPage > 0 ? (_currentPage - 1) * _itemsPerPage : 0;
            foreach (var gridRow in gridRows)
            {
                GUILayout.BeginHorizontal(
                    GUILayout.Width(contentWidth),
                    GUILayout.MaxWidth(contentWidth),
                    GUILayout.ExpandWidth(false));
                foreach (var gridCell in gridRow.Cells)
                {
                    if (gridCell.Kind == PoseBrowserGridCellKind.GroupSegment)
                        DrawGroupSegmentCell(gridCell.GroupSegment!, cell, slotW, ref displayIdx);
                    else
                    {
                        DrawGridCell(gridCell.Pose!, displayIdx, cell);
                        displayIdx++;
                    }
                }

                int usedCols = PoseBrowserGridLayout.RowColumnSpan(gridRow);
                for (int p = usedCols; p < columns; p++)
                    DrawGridCellPlaceholder(slotW);

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>Same boxed horizontal footprint as <see cref="DrawGridCell"/> for partial rows.</summary>
        private static void DrawGridCellPlaceholder(float slotW)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(slotW), GUILayout.MaxWidth(slotW), GUILayout.ExpandWidth(false));
            GUILayout.Space(1f);
            GUILayout.EndVertical();
        }

        private void DrawGridCell(PoseBrowserDisplayEntry entry, int displayIndex, float cellW, GUIStyle? cardStyleOverride = null)
        {
            var item = entry.Item;
            GUIStyle cardBox = cardStyleOverride ?? GUI.skin.box;
            if (cardStyleOverride == null)
            {
                if (item.IsSelected)
                    cardBox = _selectedStyle!;
                else if (item.IsFavorite)
                    cardBox = _favoriteCardStyle!;
                else if (entry.IsDimmed)
                    cardBox = _dimmedCardStyle!;
            }
            else
            {
                if (item.IsSelected)
                    cardBox = _selectedStyle!;
                else if (entry.IsDimmed)
                    cardBox = _dimmedCardStyle!;
            }

            const float edge = 2f;
            float innerW = Mathf.Max(40f, cellW - edge * 2f);

            GUILayout.BeginVertical(cardBox, GUILayout.Width(cellW), GUILayout.MaxWidth(cellW), GUILayout.ExpandWidth(false));

            Rect thumbRect = GUILayoutUtility.GetRect(innerW, innerW);
            Texture2D tex = item.Thumbnail ?? _placeholderTex!;

            const float cbSize = 18f;
            var cbRect = new Rect(thumbRect.xMax - cbSize - 3f, thumbRect.y + 3f, cbSize, cbSize);
            Event ev = Event.current;
            if (ev.type == EventType.Repaint)
            {
                Color prev = GUI.color;
                if (entry.IsDimmed)
                    GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit, false);
                GUI.color = prev;
                GUI.Toggle(cbRect, item.IsSelected, "");
            }
            else if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (cbRect.Contains(ev.mousePosition))
                {
                    int g = DisplayIndexToGlobal(displayIndex);
                    if (ev.shift && _lastClickedGlobalIndex >= 0)
                    {
                        ClearGroupSelection();
                        int start = Mathf.Min(_lastClickedGlobalIndex, g);
                        int end = Mathf.Max(_lastClickedGlobalIndex, g);
                        for (int i = start; i <= end && i < _filteredItems.Count; i++)
                            _filteredItems[i].IsSelected = true;
                    }
                    else if (ev.control)
                    {
                        ClearGroupSelection();
                        item.IsSelected = !item.IsSelected;
                        _lastClickedGlobalIndex = g;
                    }
                    else
                    {
                        ClearGroupSelection();
                        item.IsSelected = !item.IsSelected;
                        _lastClickedGlobalIndex = g;
                    }

                    ev.Use();
                }
                else if (thumbRect.Contains(ev.mousePosition))
                {
                    HandleItemClick(item, displayIndex);
                    ev.Use();
                }
            }

            bool favoriteCard = item.IsFavorite && !item.IsSelected;

            GUILayout.BeginHorizontal();
            if (item.IsFavorite)
            {
                var starStyle = favoriteCard ? _favoriteCardNameStyle! : _favoriteStyle!;
                GUILayout.Label("★", starStyle, GUILayout.Width(14f), GUILayout.Height(20f));
            }

            string label = item.DisplayName;
            var nameStyle = favoriteCard ? _favoriteCardNameStyle! : GUI.skin.label;
            GUILayout.Label(label, nameStyle, GUILayout.MaxWidth(innerW - (item.IsFavorite ? 18f : 4f)), GUILayout.Height(20f));
            GUILayout.EndHorizontal();

            if (item.Tags.Count > 0)
            {
                var sortedTags = item.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
                string plainTagStr = string.Join(" · ", sortedTags);
                var tagStyle = favoriteCard ? _favoriteCardTagStyle! : _tagWrapStyle!;
                bool highlightExcludedTags = entry.IsDimmed &&
                    !string.IsNullOrEmpty(item.GroupId) &&
                    sortedTags.Any(t => _tagFiltersExclude.Contains(t));
                string displayTagStr = highlightExcludedTags
                    ? BuildPoseCardTagRichText(sortedTags)
                    : plainTagStr;
                var drawStyle = highlightExcludedTags ? _tagWrapStyleRich! : tagStyle;
                float tagH = MeasureTagBlockHeight(plainTagStr, tagStyle, innerW);
                GUILayout.Label(displayTagStr, drawStyle, GUILayout.Width(innerW), GUILayout.Height(tagH), GUILayout.ExpandHeight(false));
            }

            GUILayout.EndVertical();
        }

        private static float MeasureTagBlockHeight(string tagText, GUIStyle style, float width)
        {
            float h = style.CalcHeight(new GUIContent(tagText), width);
            return Mathf.Max(Mathf.Ceil(h) + 2f, 16f);
        }

        private string BuildPoseCardTagRichText(IReadOnlyList<string> sortedTags)
        {
            const string excludedColor = "#FF5555";
            var parts = new List<string>(sortedTags.Count);
            foreach (var tag in sortedTags)
            {
                string escaped = EscapeRichTextForLabel(tag);
                if (_tagFiltersExclude.Contains(tag))
                    parts.Add($"<color={excludedColor}>{escaped}</color>");
                else
                    parts.Add(escaped);
            }

            return string.Join(" · ", parts);
        }

        private static string EscapeRichTextForLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private void HandleItemClick(PoseGridItem item, int displayIndex)
        {
            Event e = Event.current;
            int globalIdx = DisplayIndexToGlobal(displayIndex);

            if (ImportPreviewActive)
            {
                if (e != null && e.button == 1)
                    return;
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
                    item.IsSelected = !item.IsSelected;
                    _lastClickedGlobalIndex = globalIdx;
                }

                return;
            }

            if (e != null && e.button == 1)
            {
                ApplyPoseToSelectedWithUsage(item);
                return;
            }

            if (e != null && e.control)
            {
                ClearGroupSelection();
                item.IsSelected = !item.IsSelected;
                _lastClickedGlobalIndex = globalIdx;
            }
            else if (e != null && e.shift && _lastClickedGlobalIndex >= 0)
            {
                ClearGroupSelection();
                int start = Mathf.Min(_lastClickedGlobalIndex, globalIdx);
                int end = Mathf.Max(_lastClickedGlobalIndex, globalIdx);
                for (int i = start; i <= end && i < _filteredItems.Count; i++)
                    _filteredItems[i].IsSelected = true;
            }
            else
            {
                ClearGroupSelection();
                foreach (var it in _filteredItems) it.IsSelected = false;
                item.IsSelected = true;
                _lastClickedGlobalIndex = globalIdx;

                ApplyPoseToSelectedWithUsage(item);
            }
        }

        // ── Bottom Bar ──

        private void DrawBottomBar()
        {
            var librarySelected = _filteredItems.Where(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId)).ToList();

            if (ImportPreviewActive)
            {
                int total = _importReadResult?.Entries.Count ?? 0;
                int check = _filteredItems.Count(i => i.IsSelected && !string.IsNullOrEmpty(i.ImportPackEntryId));
                GUILayout.BeginVertical(GUI.skin.box);
                string kind = _pendingFolderOp == PendingFolderOperation.ImportTreePack ? "tree branch" : "pose pack";
                int groupCount = _importPreviewGroupsById.Count;
                string groupHint = groupCount > 0 ? $" · {groupCount} group(s) — click group header to check/uncheck all members" : "";
                GUILayout.Label(
                    $"Import preview ({kind}): {check} of {total} checked{groupHint} — thumbnail or checkbox toggles poses.",
                    GUILayout.Height(36f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Cancel import", GUILayout.Width(110f), GUILayout.Height(24f)))
                    CancelPendingFolderOperation();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label(
                    "Pick the destination folder in the Folders panel (↑), then Apply or Cancel there.",
                    GUILayout.Height(32f));
                GUILayout.EndVertical();
                return;
            }

            bool hasGroupSelection = _selectedGroupIds.Count > 0;
            if (librarySelected.Count == 0 && !hasGroupSelection)
            {
                if (_tagWindowPurpose == TagWindowPurpose.EditSelection)
                    CloseTagWindow();
                if (_pendingUpdateMode != UpdateMode.None) _pendingUpdateMode = UpdateMode.None;
                _showDeletePosesConfirm = false;
                if (_pendingFolderOp == PendingFolderOperation.MovePoses || _pendingFolderOp == PendingFolderOperation.CopyPoses)
                    ClearPendingFolderOperation();
                return;
            }

            const float barBtnH = 26f;
            const float barBtnMinW = 96f;
            bool oneGroupEntity = TryGetSingleSelectedGroup(out var fullGroup);
            var groupMembers = oneGroupEntity && fullGroup != null ? GetGroupMemberItems(fullGroup.Id) : null;
            bool multiGroupEntity = _selectedGroupIds.Count > 1;

            GUILayout.BeginVertical(GUI.skin.box);

            if (oneGroupEntity && fullGroup != null && groupMembers != null)
            {
                DrawGroupEntityActionBar(fullGroup, groupMembers, barBtnH, barBtnMinW);
                DrawActionBarSeparator();
            }
            else if (multiGroupEntity)
            {
                DrawMultiGroupEntityActionBar(barBtnH, barBtnMinW);
                DrawActionBarSeparator();
            }

            if (librarySelected.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Poses: {librarySelected.Count}", GUILayout.Width(100f));
                GUILayout.Space(6f);

                if (librarySelected.Count == 1)
                {
                    if (GUILayout.Button("Update pose", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                        ShowUpdatePoseOptions(librarySelected[0]);

                    if (GUILayout.Button("Rename…", GUILayout.Height(barBtnH), GUILayout.MinWidth(88f)))
                    {
                        _renamePoseText = librarySelected[0].DisplayName;
                        _renamePoseAlsoFile = true;
                        _showRenamePosePopup = true;
                    }
                }

                if (GUILayout.Button("Tag selected", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                {
                    _tagWindowForGroup = false;
                    _tagWindowGroupId = null;
                    OpenTagAssignWindow();
                }

                if (GUILayout.Button("Toggle favorite", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                {
                    foreach (var it in librarySelected)
                        _tagDb.ToggleFavorite(it);
                    NotifyLibraryCacheFavoriteChanged(librarySelected);
                    ResortPoseItemsInPlace();
                    ApplyFilters();
                }

                if (GUILayout.Button("Thumbnails…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                    StartThumbnailCapture(librarySelected);

                DrawMultiCharacterApplyButton(barBtnH, barBtnMinW);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                DrawActionBarSeparator();

                GUILayout.BeginHorizontal();
                DrawPoseGroupingActions(librarySelected, barBtnH, barBtnMinW, hideUngroup: oneGroupEntity);
                DrawActionBarVerticalSeparator(barBtnH);

                bool canMoveCopyGroup = CanMoveCopyAsWholeGroup(librarySelected, out _);
                bool blockedGrouped = SelectionHasGroupedPose(librarySelected) && !canMoveCopyGroup;
                GUI.enabled = !blockedGrouped;
                if (GUILayout.Button("Export…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                    ExportSelectedPosesToDisk();

                if (GUILayout.Button("Move to folder…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                    BeginFolderOpForPoses(PendingFolderOperation.MovePoses);

                if (GUILayout.Button("Copy to folder…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                    BeginFolderOpForPoses(PendingFolderOperation.CopyPoses);

                GUI.enabled = true;

                if (GUILayout.Button("Delete…", GUILayout.Height(barBtnH), GUILayout.MinWidth(88f)))
                    _showDeletePosesConfirm = true;

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (_showDeletePosesConfirm)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(
                        $"Permanently delete {librarySelected.Count} pose file(s)? Each file is copied to !_AutoBackup first.",
                        GUILayout.Height(36f));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Confirm delete", GUILayout.Height(26f), GUILayout.MinWidth(120f)))
                    {
                        foreach (var it in librarySelected)
                        {
                            if (it.Thumbnail != null)
                                Destroy(it.Thumbnail);
                        }
                        foreach (var it in librarySelected)
                            _groupDb.RemoveItem(it);
                        NotifyLibraryCachePosesDeleted(librarySelected);
                        _dataService.DeletePoseFiles(librarySelected, _tagDb);
                        _showDeletePosesConfirm = false;
                        ReloadCurrentView();
                    }
                    if (GUILayout.Button("Cancel", GUILayout.Height(26f), GUILayout.MinWidth(80f)))
                        _showDeletePosesConfirm = false;
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Deselect all", GUILayout.Height(barBtnH), GUILayout.MinWidth(88f)))
            {
                ClearAllSelection();
                _showDeletePosesConfirm = false;
            }
            GUILayout.EndHorizontal();

            if (_pendingFolderOp == PendingFolderOperation.MovePoses || _pendingFolderOp == PendingFolderOperation.CopyPoses)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    "Move/Copy: choose destination in the Folders panel (↑), then Apply or Cancel there.",
                    GUILayout.Height(28f));
            }

            GUILayout.EndVertical();

            if (_pendingUpdateMode != UpdateMode.None)
                DrawUpdatePopup();

            DrawGroupNamePopup();
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
                            ClearPendingFolderOperation();
                            NotifyLibraryCacheStructureChanged();
                            if (!string.IsNullOrEmpty(newFull))
                            {
                                _folderTree.RefreshAndSelect(newFull);
                                LoadFolder(newFull!);
                            }
                            else
                            {
                                _folderTree.Refresh();
                                ReloadCurrentView();
                            }
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
                        string oldPath = sel[0].FilePath;
                        string oldRel = sel[0].RelativePath(_dataService.PoseRootPath);
                        if (_dataService.RenamePoseDisplayNameAndOptionalFile(sel[0], _renamePoseText, _renamePoseAlsoFile, _tagDb))
                        {
                            if (_renamePoseAlsoFile && !string.Equals(oldPath, sel[0].FilePath, StringComparison.OrdinalIgnoreCase))
                            {
                                _groupDb.OnItemPathChanged(oldRel, sel[0]);
                                NotifyLibraryCachePoseMoved(oldPath, sel[0]);
                            }
                            else
                                _libraryCache.SyncMetadata(sel[0]);
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

        private void BeginImportFromDisk(string path)
        {
            if (_layoutTier != PoseBrowserLayoutTier.Normal)
                return;
            if (!PosePackExchange.TryReadPack(path, out var result, out var err) || result == null)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not read pack: {err}");
                return;
            }

            if (ImportPreviewActive)
            {
                DestroyImportPreviewThumbnails();
                ClearImportPreviewGroups();
                _importEntryById.Clear();
                _importReadResult = null;
                ClearPendingFolderPickOnly();
            }
            else
                CaptureImportBrowseSnapshot();

            _importReadResult = result;
            _importEntryById.Clear();
            foreach (var e in result.Entries)
                _importEntryById[e.Id] = e;
            BuildImportPreviewItems(result);
            _pendingFolderOp = result.IsTreePack ? PendingFolderOperation.ImportTreePack : PendingFolderOperation.ImportPosePack;
            _pendingFolderDestPath = SaveTargetFolderPath;
            _folderTree.Refresh();
            NotifyLibraryCacheStructureChanged();
            ApplyFilters();
            _gridScroll = Vector2.zero;
            _lastClickedGlobalIndex = -1;
        }

        private void PromptImportPoseOrTreePack()
        {
            if (_layoutTier != PoseBrowserLayoutTier.Normal)
                return;
            string filter =
                "ZIP archives (*.zip)\0*.zip\0All files (*.*)\0*.*\0";
            string? path = NativeFileDialog.OpenFile("Import pose ZIP", filter, _dataService.PoseRootPath);
            if (string.IsNullOrEmpty(path)) return;
            BeginImportFromDisk(path);
        }

        private void RestoreImportBrowseSnapshot()
        {
            DestroyImportPreviewThumbnails();
            ClearImportPreviewGroups();
            _importEntryById.Clear();
            _importReadResult = null;
            if (!_importBrowseSnapshotValid)
            {
                ReloadCurrentView();
                return;
            }

            _importBrowseSnapshotValid = false;
            _browseFavoritesOnly = _snapBrowseFavoritesOnly;
            _viewAllPosesRecursive = _snapViewAllRecursive;
            if (string.IsNullOrEmpty(_snapSelectedNodeFullPath))
            {
                _folderTree.SelectedNode = null;
                if (_browseFavoritesOnly)
                    LoadFavoritePosesFromTreeRoot();
                else if (_viewAllPosesRecursive)
                    LoadAllPosesFromTreeRoot();
                else
                    LoadFolder(_folderTree.RootPath);
            }
            else
            {
                var node = _folderTree.FindNodeByFullPath(_snapSelectedNodeFullPath);
                if (node != null)
                {
                    _folderTree.EnsureExpandedToShow(node);
                    _folderTree.SelectNode(node);
                    if (_browseFavoritesOnly)
                        LoadFavoritePosesFromTreeRoot();
                    else if (_viewAllPosesRecursive)
                        LoadAllPosesFromTreeRoot();
                    else
                        LoadFolder(node.FullPath);
                }
                else
                    ReloadCurrentView();
            }
        }

        private void CancelPendingFolderOperation()
        {
            var op = _pendingFolderOp;
            ClearPendingFolderOperation();
            if (op == PendingFolderOperation.ImportPosePack || op == PendingFolderOperation.ImportTreePack)
                RestoreImportBrowseSnapshot();
            else if (op == PendingFolderOperation.MovePoses || op == PendingFolderOperation.CopyPoses)
                ReloadCurrentView();
        }

        private void ApplyPendingFolderOperation()
        {
            if (_pendingFolderOp == PendingFolderOperation.None || string.IsNullOrEmpty(_pendingFolderDestPath))
                return;
            if ((_pendingFolderOp == PendingFolderOperation.ImportPosePack || _pendingFolderOp == PendingFolderOperation.ImportTreePack)
                && !_filteredItems.Any(i => i.IsSelected && !string.IsNullOrEmpty(i.ImportPackEntryId)))
            {
                SandboxServices.Log.LogMessage("PoseBrowser: No poses checked for import.");
                return;
            }

            if ((_pendingFolderOp == PendingFolderOperation.MovePoses || _pendingFolderOp == PendingFolderOperation.CopyPoses))
            {
                var sel = _filteredItems.Where(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId)).ToList();
                if (_pendingFolderOpGroupIds.Count == 0 && sel.Count == 0 && !CanMoveCopyAsWholeGroup(sel, out _))
                {
                    CancelPendingFolderOperation();
                    return;
                }
            }

            string dest = _pendingFolderDestPath;
            var op = _pendingFolderOp;
            string? importedTreeBranchRoot = null;
            switch (op)
            {
                case PendingFolderOperation.MovePoses:
                {
                    var sel = _filteredItems.Where(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId)).ToList();
                    if (_pendingFolderOpGroupIds.Count > 0)
                        MoveCopyGroupsById(_pendingFolderOpGroupIds, dest, copy: false);
                    else if (CanMoveCopyAsWholeGroup(sel, out var moveGroup) && moveGroup != null)
                        MoveGroupToFolder(moveGroup, dest);
                    else
                    {
                        foreach (var it in sel)
                        {
                            string oldPath = it.FilePath;
                            string oldRel = it.RelativePath(_dataService.PoseRootPath);
                            if (_dataService.MovePoseFileToFolder(it, dest, _tagDb))
                            {
                                _groupDb.OnItemPathChanged(oldRel, it);
                                NotifyLibraryCachePoseMoved(oldPath, it);
                            }
                        }
                    }

                    break;
                }
                case PendingFolderOperation.CopyPoses:
                {
                    var sel = _filteredItems.Where(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId)).ToList();
                    if (_pendingFolderOpGroupIds.Count > 0)
                        MoveCopyGroupsById(_pendingFolderOpGroupIds, dest, copy: true);
                    else if (CanMoveCopyAsWholeGroup(sel, out var copyGroup) && copyGroup != null)
                        CopyGroupToFolder(copyGroup, dest);
                    else
                    {
                        foreach (var it in sel)
                            NotifyLibraryCachePoseCopied(_dataService.CopyPoseFileToFolder(it, dest, _tagDb));
                    }

                    break;
                }
                case PendingFolderOperation.ImportPosePack:
                    CommitImportPosePack(dest);
                    break;
                case PendingFolderOperation.ImportTreePack:
                    importedTreeBranchRoot = CommitImportTreePack(dest);
                    break;
            }

            if (op == PendingFolderOperation.ImportPosePack || op == PendingFolderOperation.ImportTreePack)
            {
                NotifyLibraryCacheStructureChanged();
                DestroyImportPreviewThumbnails();
                _importBrowseSnapshotValid = false;
                _importEntryById.Clear();
                _importReadResult = null;
            }

            ClearPendingFolderOperation();

            if (!string.IsNullOrEmpty(importedTreeBranchRoot))
                _folderTree.RefreshAndSelect(importedTreeBranchRoot);

            ReloadCurrentView();
        }

        private void DestroyImportPreviewThumbnails()
        {
            foreach (var it in _allItems)
            {
                if (!string.IsNullOrEmpty(it.ImportPackEntryId) && it.Thumbnail != null)
                    Destroy(it.Thumbnail);
                it.Thumbnail = null;
            }

            ClearImportPreviewGroups();
        }

        private void CaptureImportBrowseSnapshot()
        {
            _snapBrowseFavoritesOnly = _browseFavoritesOnly;
            _snapViewAllRecursive = _viewAllPosesRecursive;
            _snapSelectedNodeFullPath = _folderTree.SelectedNode != null ? _folderTree.SelectedNode.FullPath : null;
            _importBrowseSnapshotValid = true;
        }

        private void BuildImportPreviewItems(PosePackExchange.PosePackReadResult result)
        {
            StopThumbnailLoading();
            foreach (var it in _allItems)
            {
                if (it.Thumbnail != null)
                    Destroy(it.Thumbnail);
            }

            _allItems.Clear();
            foreach (var e in result.Entries)
            {
                var item = new PoseGridItem
                {
                    FilePath = "",
                    DisplayName = e.DisplayName,
                    IsPng = e.IsPng,
                    DataPosition = 0,
                    LastWriteTime = e.LastWriteUtc == DateTime.MinValue ? DateTime.MinValue : e.LastWriteUtc.ToLocalTime(),
                    CreationTimeUtc = e.CreationUtc,
                    IsSelected = true,
                    IsFavorite = e.Favorite,
                    Tags = new HashSet<string>(e.Tags, StringComparer.OrdinalIgnoreCase),
                    ImportPackEntryId = e.Id
                };
                if (e.IsPng && e.FileBytes.Length > 0)
                {
                    try
                    {
                        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                        if (tex.LoadImage(e.FileBytes))
                        {
                            tex.wrapMode = TextureWrapMode.Clamp;
                            item.Thumbnail = tex;
                        }
                        else
                        {
                            Destroy(tex);
                        }
                    }
                    catch { /* use placeholder */ }
                }

                _allItems.Add(item);
            }

            AssignImportPackGroups(result);
            ResortPoseItemsInPlace();
        }

        private void CommitImportPosePack(string destFolder)
        {
            if (!Directory.Exists(destFolder)) return;
            var zipToRel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _filteredItems.Where(i => i.IsSelected && !string.IsNullOrEmpty(i.ImportPackEntryId)))
            {
                if (!_importEntryById.TryGetValue(item.ImportPackEntryId!, out var e))
                    continue;
                string baseName = Path.GetFileName(e.SuggestedFileName);
                if (string.IsNullOrEmpty(baseName))
                    baseName = PoseDataService.SanitizeFileName(e.DisplayName) + (e.IsPng ? ".png" : ".dat");
                string ext = Path.GetExtension(baseName);
                string stem = PoseDataService.SanitizeFileName(Path.GetFileNameWithoutExtension(baseName));
                if (string.IsNullOrEmpty(stem)) stem = "pose";
                if (string.IsNullOrEmpty(ext))
                    ext = e.IsPng ? ".png" : ".dat";
                baseName = stem + ext;
                string outPath = PoseDataService.GetUniqueFilePath(Path.Combine(destFolder, baseName));
                File.WriteAllBytes(outPath, e.FileBytes);
                var loaded = _dataService.TryLoadPoseItem(new FileInfo(outPath));
                if (loaded != null)
                {
                    _tagDb.SetTags(loaded, e.Tags);
                    _tagDb.SetFavorite(loaded, e.Favorite);
                    if (!string.IsNullOrEmpty(e.ZipInternalPath))
                    {
                        string zipKey = PoseGroupDatabase.NormalizeMemberPath(e.ZipInternalPath);
                        string rel = PoseGroupDatabase.NormalizeMemberPath(loaded.RelativePath(_dataService.PoseRootPath));
                        if (!string.IsNullOrEmpty(zipKey) && !string.IsNullOrEmpty(rel))
                            zipToRel[zipKey] = rel;
                    }
                }
            }

            if (_importReadResult?.Groups.Count > 0)
                ImportGroupsFromPack(_importReadResult.Groups, zipToRel);
        }

        /// <returns>Full path of the created branch root folder, or null if import did not run.</returns>
        private string? CommitImportTreePack(string parentFolder)
        {
            if (_importReadResult == null || !Directory.Exists(parentFolder)) return null;
            string rootName = PoseDataService.SanitizeFileName(_importReadResult.TreeRootFolderName);
            if (string.IsNullOrEmpty(rootName))
                rootName = "folder";
            string baseRoot = PoseDataService.GetUniqueDirectoryPath(Path.Combine(parentFolder, rootName));
            Directory.CreateDirectory(baseRoot);
            var zipToRel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in _filteredItems.Where(i => i.IsSelected && !string.IsNullOrEmpty(i.ImportPackEntryId)))
            {
                if (!_importEntryById.TryGetValue(item.ImportPackEntryId!, out var e))
                    continue;
                if (!PosePackExchange.TryValidateTreeRelativePath(e.RelPath, out var norm, out _))
                    continue;
                string relFs = norm.Replace('/', Path.DirectorySeparatorChar);
                string outPath = Path.Combine(baseRoot, relFs);
                string? subDir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(subDir))
                    Directory.CreateDirectory(subDir);
                outPath = PoseDataService.GetUniqueFilePath(outPath);
                File.WriteAllBytes(outPath, e.FileBytes);
                var loaded = _dataService.TryLoadPoseItem(new FileInfo(outPath));
                if (loaded != null)
                {
                    _tagDb.SetTags(loaded, e.Tags);
                    _tagDb.SetFavorite(loaded, e.Favorite);
                    if (!string.IsNullOrEmpty(e.ZipInternalPath))
                    {
                        string zipKey = PoseGroupDatabase.NormalizeMemberPath(e.ZipInternalPath);
                        string rel = PoseGroupDatabase.NormalizeMemberPath(loaded.RelativePath(_dataService.PoseRootPath));
                        if (!string.IsNullOrEmpty(zipKey) && !string.IsNullOrEmpty(rel))
                            zipToRel[zipKey] = rel;
                    }
                }
            }

            if (_importReadResult.Groups.Count > 0)
                ImportGroupsFromPack(_importReadResult.Groups, zipToRel);

            return Path.GetFullPath(baseRoot);
        }

        private void ExportSelectedPosesToDisk()
        {
            var sel = _allItems.Where(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId)).ToList();
            ExportItemsToDisk(sel);
        }

        private void ExportItemsToDisk(IReadOnlyList<PoseGridItem> items, string? saveDialogTitle = null)
        {
            if (items.Count == 0)
                return;
            foreach (var it in items)
                _tagDb.ApplyToItem(it);
            string extNoDot = PosePackExchange.ZipExtension.TrimStart('.');
            string filter =
                $"HS2 Sandbox pose export (*.zip)\0*.zip\0All files (*.*)\0*.*\0";
            string? path = NativeFileDialog.SaveFile(
                saveDialogTitle ?? "Export poses (ZIP)",
                extNoDot,
                filter,
                _dataService.PoseRootPath);
            if (string.IsNullOrEmpty(path)) return;
            if (!path.EndsWith(PosePackExchange.ZipExtension, StringComparison.OrdinalIgnoreCase))
                path += PosePackExchange.ZipExtension;
            var list = items as List<PoseGridItem> ?? items.ToList();
            var relToZip = PosePackExchange.MapItemsToFlatZipPaths(_dataService.PoseRootPath, list);
            var groups = BuildExportGroupsForItems(list, relToZip);
            PosePackExchange.TryExportPosePack(path, _dataService.PoseRootPath, list, groups);
        }

        private void ExportFolderBranchToDisk(PoseFolderNode node)
        {
            var items = _dataService.LoadPosesRecursive(node.FullPath);
            if (items.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Nothing to export in this branch (no pose files).");
                return;
            }

            foreach (var it in items)
                _tagDb.ApplyToItem(it);
            string extNoDot = PosePackExchange.ZipExtension.TrimStart('.');
            string filter =
                $"HS2 Sandbox folder export (*.zip)\0*.zip\0All files (*.*)\0*.*\0";
            string? path = NativeFileDialog.SaveFile("Export folder branch (ZIP)", extNoDot, filter, _dataService.PoseRootPath);
            if (string.IsNullOrEmpty(path)) return;
            if (!path.EndsWith(PosePackExchange.ZipExtension, StringComparison.OrdinalIgnoreCase))
                path += PosePackExchange.ZipExtension;
            var relToZip = PosePackExchange.MapItemsToTreeZipPaths(_dataService.PoseRootPath, node.FullPath, node.Name, items);
            var groups = BuildExportGroupsForItems(items, relToZip);
            PosePackExchange.TryExportTreePack(path, _dataService.PoseRootPath, node.FullPath, node.Name, items, groups);
        }

        private void ExportLibraryRootTreeToDisk()
        {
            var items = _dataService.LoadPosesRecursive(_folderTree.RootPath);
            if (items.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Nothing to export in the library (no pose files).");
                return;
            }

            foreach (var it in items)
                _tagDb.ApplyToItem(it);
            string rootLabel = Path.GetFileName(_folderTree.RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(rootLabel))
                rootLabel = "pose";
            string extNoDot = PosePackExchange.ZipExtension.TrimStart('.');
            string filter =
                $"HS2 Sandbox library export (*.zip)\0*.zip\0All files (*.*)\0*.*\0";
            string? path = NativeFileDialog.SaveFile("Export library tree (ZIP)", extNoDot, filter, _dataService.PoseRootPath);
            if (string.IsNullOrEmpty(path)) return;
            if (!path.EndsWith(PosePackExchange.ZipExtension, StringComparison.OrdinalIgnoreCase))
                path += PosePackExchange.ZipExtension;
            var relToZip = PosePackExchange.MapItemsToTreeZipPaths(_dataService.PoseRootPath, _folderTree.RootPath, rootLabel, items);
            var groups = BuildExportGroupsForItems(items, relToZip);
            PosePackExchange.TryExportTreePack(path, _dataService.PoseRootPath, _folderTree.RootPath, rootLabel, items, groups);
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
            _groupDb.ApplyMembershipToItems(_allItems);

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
            if (!_libraryCache.TryGetAllSnapshot(out _allItems))
            {
                _allItems = _dataService.LoadPosesRecursive(_folderTree.RootPath);
                foreach (var item in _allItems)
                    _tagDb.ApplyToItem(item);
                _groupDb.ApplyMembershipToItems(_allItems);
                ScheduleLibraryCacheRebuild();
            }
            else
                _groupDb.ApplyMembershipToItems(_allItems);

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
            if (!_libraryCache.TryGetFavoritesSnapshot(out _allItems))
            {
                _allItems = _dataService.LoadPosesRecursive(_folderTree.RootPath);
                foreach (var item in _allItems)
                    _tagDb.ApplyToItem(item);
                _allItems = _allItems.Where(i => i.IsFavorite).ToList();
                _groupDb.ApplyMembershipToItems(_allItems);
                ScheduleLibraryCacheRebuild();
            }
            else
                _groupDb.ApplyMembershipToItems(_allItems);

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
            RebuildDisplayList();
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
                    string oldPath = item.FilePath;
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

                    if (!string.Equals(oldPath, item.FilePath, StringComparison.OrdinalIgnoreCase))
                        NotifyLibraryCachePoseMoved(oldPath, item);
                    else
                        _libraryCache.SyncMetadata(item);
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

                    NotifyLibraryCacheStructureChanged();
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

                _favoriteCardNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.14f, 0.11f, 0.02f) }
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

                _tagWrapStyleRich = new GUIStyle(_tagWrapStyle) { richText = true };

                _favoriteCardTagStyle = new GUIStyle(_tagWrapStyle)
                {
                    normal = { textColor = new Color(0.26f, 0.20f, 0.04f) }
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
                "• <b>Tags</b> — docked filter window: <b>AND/OR</b> for <b>+ include</b> tags; click each tag to cycle neutral → include → exclude. Groups keep every member (excluded tags dim those cards).\n" +
                "• <b>Sort</b> — opens a docked sort panel: <b>Last used</b> (when poses are applied), <b>Last updated</b> / <b>Last created</b> (file times), <b>Name</b>. First click selects; click again on the same row toggles ↑ / ↓.\n" +
                "• <b>Save Pose</b> — save current character pose into the active folder (selected folder, pose root in <b>All poses</b> or <b>Favorites</b> view).\n" +
                "• <b>Import…</b> — open a pose pack <b>.zip</b> (manifest v2 or v3; preview in the grid, then pick a destination folder and <b>Apply</b> in the folder footer).",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Pose groups (grid)</b>", rich);
            GUILayout.Label(
                "• Select <b>2+ ungrouped</b> poses → bottom bar <b>Group…</b> (name the group). <b>Ungroup</b> removes membership.\n" +
                "• <b>Group header</b> (▦ row) = select the <b>group entity</b> (rename, <b>Group tags…</b>, <b>Export group…</b>, <b>Apply to characters…</b>). Card checkboxes = individual poses.\n" +
                "• <b>Exclude</b> tag filters: hide ungrouped poses; inside a visible group all members stay (excluded pose tags dim cards, red tag text).\n" +
                "• Move/Copy one <b>full group</b> at a time. Data: <b>pose_groups.tsv</b> in Sandbox config.",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Character row & multi-apply</b>", rich);
            GUILayout.Label(
                "• Row shows Studio character count/names (tooltip). Props ignored.\n" +
                "• <b>Chars</b> — male/female <b>priority lists</b> (top = first). <b>Load characters from scene</b>, ↑↓ reorder, ⇄ transfer list, ✕ remove. Saved in <b>pose_browser_character_config.json</b>.\n" +
                "• <b>Apply to characters…</b> when <b>2+ poses</b> selected, or <b>one group header</b> selected. Select characters in Studio first.\n" +
                "• <b>Male</b> / <b>Female</b> <i>pose</i> tags → next free character on that list only. <b>Untagged</b> → interleaved list order; each character gets at most <b>one</b> pose per apply (extras skipped; leftover chars may get a second-pass pose).\n" +
                "• Left-click thumbnail still applies <b>one</b> pose to <b>all</b> selected characters (separate from multi-apply).",
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
                "• <b>★ Favorites</b> — all favorited poses library-wide (not a disk folder; saving while this row is active uses the pose root).\n" +
                "• <b>Root only</b> — files in the pose root only; during Move/Copy, also picks <b>pose root</b> as destination.\n" +
                "• Click a folder name — browse that folder, or during Move/Copy sets <b>destination</b> without changing the grid.\n" +
                "• Footer: <b>New folder</b>, <b>Rename</b> / <b>Delete</b> (empty only); during Move/Copy/import, <b>Apply</b>/<b>Cancel</b> appear at the top of this footer.\n" +
                "• <b>Export branch…</b> / <b>Export library tree…</b> — <b>Full</b> layout only: folder footer when a folder is selected / at library root (v3 ZIP of that branch or the whole tree, includes pose groups when present).",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Import / export (ZIP v3)</b>", rich);
            GUILayout.Label(
                "• After <b>Import…</b>, the grid shows a preview: thumbnail click toggles inclusion (checkbox + Ctrl/Shift work). Use <b>Cancel import</b> in the bottom bar or <b>Cancel</b> in the folder footer to abort. <b>Tree branch</b> packs create a named subfolder under the destination you pick. v2 packs still import.\n" +
                "• <b>Export…</b> in the <b>selection bar</b> saves checked library poses to a v3 <b>.zip</b> (tags/favorites; pose groups when fully selected).\n" +
                "• External tools must build <b>stored</b> (uncompressed) ZIP entries — see <b>Modules/PoseBrowser/POSE_ZIP_FORMAT.md</b> in the repo.",
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
                "• <b>All</b> / <b>None</b> — every pose in the current folder view (not search/tag filtered). <b>▦ All</b> / <b>▦ None</b> — every group header in that scope (import preview: check/uncheck all group members).",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Selection bar (bottom)</b>", rich);
            GUILayout.Label(
                "Shown when something is selected: <b>Update Pose</b> (one), <b>Rename…</b>, <b>Tag Selected</b>, <b>Group…</b> / <b>Ungroup</b> / group tags, <b>Fav Selected</b>, <b>Thumbs…</b>, <b>Export…</b> (v3 pose ZIP), <b>Move…</b> / <b>Copy…</b> (ungrouped poses or one full group), <b>Delete…</b>, <b>Deselect</b>.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Options panel</b>", rich);
            GUILayout.Label(
                "Card width, items per page (0 = all on one scroll), select/deselect all filtered, and a read-only list of <b>keyboard shortcuts</b>. Assign keys in BepInEx <b>Configuration Manager</b> → section <b>Pose Browser · Keyboard shortcuts</b> (next/previous pose; next/previous browse target — same cycle as Mini <b>Folder</b> arrows; only while this window is focused and no text field has keyboard focus).\n" +
                "Card width and page cap are mirrored in BepInEx under <b>Pose Browser</b>. Window positions, layout tier (<b>Full</b>/<b>List</b>/<b>Mini</b>), and sort mode live in <b>pose_browser_options.json</b> next to the other Sandbox config files.",
                rich);

            GUILayout.Space(10f);
            GUILayout.Label("<b>HS2Wiki</b>", rich);
            GUILayout.Label(
                "If <b>HS2Wiki</b> is installed, press <b>F3</b> for the full manual under <b>HS2 Sandbox / Pose Browser</b> (linked pages, images).",
                rich);
            if (GUILayout.Button("Wiki: Overview", GUILayout.Height(24f)))
                PoseBrowserWikiRegistration.TryOpenWikiPage(
                    PoseBrowserWikiRegistration.WikiCategoryRoot,
                    PoseBrowserWikiRegistration.PageOverview);
            if (GUILayout.Button("Wiki: Pose groups", GUILayout.Height(24f)))
                PoseBrowserWikiRegistration.TryOpenWikiPage(
                    PoseBrowserWikiRegistration.WikiCategoryRoot,
                    PoseBrowserWikiRegistration.PagePoseGroups);
            if (GUILayout.Button("Wiki: Multi-character apply", GUILayout.Height(24f)))
                PoseBrowserWikiRegistration.TryOpenWikiPage(
                    PoseBrowserWikiRegistration.WikiCategoryRoot,
                    PoseBrowserWikiRegistration.PageMultiCharacterApply);

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
                        _showCharacterConfigPane = false;
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
            ClearGroupSelection();
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
