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
        // Structural pixel dimensions: raw base values (suffixed *Base) plus a UI-scaled accessor that keeps
        // the original name, so existing call sites automatically follow PoseBrowserScale.Factor.
        private const float ResizeHandleSizeBase = 18f;
        private float ResizeHandleSize => PoseBrowserScale.Px(ResizeHandleSizeBase);
        private const float WindowChromeButtonWidthBase = 22f;
        private float WindowChromeButtonWidth => PoseBrowserScale.Px(WindowChromeButtonWidthBase);
        private const float MinimizedChipSizeBase = 28f;
        private float MinimizedChipSize => PoseBrowserScale.Px(MinimizedChipSizeBase);
        private const float MinimizedChipClickDragThreshold = 4f;
        private const float TreePanelWidthBase = 205f;
        private float TreePanelWidth => PoseBrowserScale.Px(TreePanelWidthBase);
        /// <summary>Padding between tree panel and grid (window inner chrome).</summary>
        private const float GridPanelChromePadBase = 24f;
        private float GridPanelChromePad => PoseBrowserScale.Px(GridPanelChromePadBase);
        /// <summary>Sum of fixed top-bar control widths (must stay in sync with <see cref="DrawTopBar"/>).</summary>
        private const float NormalTopBarMinWidthBase = 780f;
        private float NormalTopBarMinWidth => PoseBrowserScale.Px(NormalTopBarMinWidthBase);
        private const float BottomBarHeightBase = 36f;
        private float BottomBarHeight => PoseBrowserScale.Px(BottomBarHeightBase);
        private const float TopBarHeightBase = 32f;
        private float TopBarHeight => PoseBrowserScale.Px(TopBarHeightBase);
        // MinCardSize/MaxCardSize stay raw: they define the card-width slider range and clamp space.
        // The UI scale is applied to card metrics inside ComputeGridCellLayout instead.
        private const float MinCardSize = 96f;
        private const float MaxCardSize = 280f;
        private const float GridCardSizeBoost = 1.12f;
        private const float PoseCardNameRowHBase = 20f;
        private float PoseCardNameRowH => PoseBrowserScale.Px(PoseCardNameRowHBase);
        private const float PoseCardTextPadH = 4f;
        private const float PoseCardNameStarWBase = 14f;
        private float PoseCardNameStarW => PoseBrowserScale.Px(PoseCardNameStarWBase);
        private const float GridCellGapBase = 3f;
        private float GridCellGap => PoseBrowserScale.Px(GridCellGapBase);
        private const int OptionsWindowId = SandboxImguiWindowIds.PoseBrowser.Options;
        private const int HelpWindowId = SandboxImguiWindowIds.PoseBrowser.Help;
        private const int TagWindowId = SandboxImguiWindowIds.PoseBrowser.Tag;
        private const int SortWindowId = SandboxImguiWindowIds.PoseBrowser.Sort;
        private const int CharacterConfigWindowId = SandboxImguiWindowIds.PoseBrowser.Characters;
        private const int ItemAssociationWindowId = SandboxImguiWindowIds.PoseBrowser.ItemAssociation;
        private const float DockedPaneGapBase = 4f;
        private float DockedPaneGap => PoseBrowserScale.Px(DockedPaneGapBase);
        private const float OptionsPaneDefaultWidthBase = 420f;
        private float OptionsPaneDefaultWidth => PoseBrowserScale.Px(OptionsPaneDefaultWidthBase);
        private const float HelpPaneDefaultWidthBase = 340f;
        private float HelpPaneDefaultWidth => PoseBrowserScale.Px(HelpPaneDefaultWidthBase);
        private const float TagPaneDefaultWidthBase = 288f;
        private float TagPaneDefaultWidth => PoseBrowserScale.Px(TagPaneDefaultWidthBase);
        private const float CharacterPaneDefaultWidthBase = 300f;
        private float CharacterPaneDefaultWidth => PoseBrowserScale.Px(CharacterPaneDefaultWidthBase);
        private const float SortPaneDefaultWidthBase = 260f;
        private float SortPaneDefaultWidth => PoseBrowserScale.Px(SortPaneDefaultWidthBase);

        // Static GUIContent for constant UI elements (avoids per-frame allocations)
        private static readonly GUIContent _gcBtnSoloDefault = new GUIContent("Solo", "Select all ungrouped poses in the current view");
        private static readonly GUIContent _gcBtnSoloFiltered = new GUIContent("Solo", "Select ungrouped poses that match search / tag filters");
        private static readonly GUIContent _gcBtnGroup = new GUIContent("▦ Group", "Select all group headers (▦ entities) shown in the current results — for rename, group tags, export, apply to characters…");
        private static readonly GUIContent _gcBtnGroupPoseDefault = new GUIContent("▦ Pose", "Select all pose checkboxes in groups in the current view");
        private static readonly GUIContent _gcBtnGroupPoseFiltered = new GUIContent("▦ Pose", "Select pose checkboxes in groups that match search / tag filters (skips dimmed non-matching members)");
        private static readonly GUIContent _gcBtnAllDefault = new GUIContent("All", "Select all poses in the current folder / library view");
        private static readonly GUIContent _gcBtnAllFiltered = new GUIContent("All", "Select all poses matching search / tag filters (skips dimmed group members that do not match)");
        private static readonly GUIContent _gcBtnInvertGroup = new GUIContent("Invert", "Invert group header (▦) selection among groups in the current results");
        private static readonly GUIContent _gcBtnInvertPose = new GUIContent("Invert", "Invert pose checkbox selection among poses in the current results (skips dimmed)");
        private static readonly GUIContent _gcBtnNone = new GUIContent("None", "Clear all pose checkboxes and group header (▦) selection");
        private static readonly GUIContent _gcPagePrev = new GUIContent("◀");
        private static readonly GUIContent _gcPageNext = new GUIContent("▶");
        private static readonly GUIContent _gcThumbnailLoading = new GUIContent("Loading…", "Thumbnail image is still being loaded from disk.");

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
        private bool _isMinimized;
        private Rect _minimizedChipRect;
        private Vector2 _minimizeBtnOffsetFromWindow;
        private bool _chipDragging;
        private Vector2 _chipDragOffset;
        private Vector2 _chipMouseDownPos;
        private int _compactPoseIndex = -1;
        private string? _compactSelectedGroupId;

        // Compact-list virtualization: only the rows inside the scroll viewport are drawn.
        // Blocks group a run of poses (ungrouped pose = 1 block, a group = 1 block) so each block
        // is drawn atomically and the group card box stays intact.
        private struct CompactListBlock { public int Start; public int Count; public string? GroupId; }
        private List<CompactListBlock>? _compactBlocks;
        private float _compactListViewportH;
        private float _compactRowPitch = 24f;
        private float _compactMeasureLastRowY = -1f;
        /// <summary>Last group whose poses were applied to characters; cleared when another pose is applied.</summary>
        private string? _lastAppliedGroupId;
        private bool _anyPoseAppliedSinceLastGroupApply;
        private bool _applyGroupRelativePositions = true;
        private bool _applyGroupRelativeHeights;
        private bool _applyGroupRelativeObjectScales;
        private Vector2 _compactListScroll;
        private bool _compactListShowTree = true;
        private const string CompactHoverTooltipPrefix = "\x01pb:";
        private float _compactHoverThumbnailWidth = 200f;
        private int _compactHoverIndex = -1;
        private int _compactHoverTexIndex = -1;
        private Texture2D? _compactHoverTex;
        private float _compactHoverRowScreenY;

        // Per–layout-tier main window rect (persisted so switching modes restores size/position).
        private float _savedFullW = 900f, _savedFullH = 620f, _savedFullX = 200f, _savedFullY = 80f;
        private float _savedListW = 520f, _savedListH = 400f, _savedListX = 200f, _savedListY = 80f;
        private float _savedListNoTreeW = 260f, _savedListNoTreeH = 400f;
        private float _savedMiniW = 280f, _savedMiniH = 240f, _savedMiniX = 200f, _savedMiniY = 80f;

        /// <summary>Floor for compact list min width; actual minimum is computed (see <see cref="ComputeCompactListMinimumWindowWidth"/>).</summary>
        private const float CompactListMinWidthWithTree = 400f;
        private const float CompactListMinWidthNoTree = 160f;
        private const float CompactListDefaultWidthWithTree = 520f;
        private const float CompactListDefaultWidthNoTree = 260f;
        private const float CompactListDefaultHeight = 400f;

        private static float LayoutMinWidthFor(PoseBrowserLayoutTier tier) => PoseBrowserScale.Px(tier switch
        {
            // Matches toolbar + tree/grid column minimum (see ComputeContentMinimumWindowWidth).
            PoseBrowserLayoutTier.Normal => 980f,
            PoseBrowserLayoutTier.CompactList => CompactListMinWidthWithTree,
            PoseBrowserLayoutTier.CompactMini => 200f,
            _ => 980f
        });

        private float EffectiveLayoutMinWidthFor(PoseBrowserLayoutTier tier)
        {
            if (tier == PoseBrowserLayoutTier.CompactList)
                return ComputeCompactListMinimumWindowWidth();
            return LayoutMinWidthFor(tier);
        }

        /// <summary>Minimum window width so compact header rows and tree + pose list columns fit without clipping.</summary>
        private float ComputeCompactListMinimumWindowWidth()
        {
            if (!_compactListShowTree)
                return PoseBrowserScale.Px(CompactListMinWidthNoTree);

            // Computed from raw base dimensions, then scaled once at the end (single-scale, no double counting).
            const float compactCharacterLabelMaxW = 130f;
            float headerRow1 = WindowChromeButtonWidthBase * 2f + 6f + 110f + 52f;
            float headerRow2 = 44f + 44f + 64f;
            float headerRow3 = 52f + compactCharacterLabelMaxW;
            if (_poseBrowserUpdateCheck.State == PoseBrowserUpdateCheck.Status.UpdateAvailable)
                headerRow3 += 108f;
            float headerMin = Mathf.Max(headerRow1, headerRow2, headerRow3);

            const float poseListColumnMin = 72f + 72f + 16f;
            float bodyMin = TreePanelWidthBase + poseListColumnMin;
            float contentMin = Mathf.Max(headerMin, bodyMin) + WindowChromeHorizontalPadding() + 8f;
            return PoseBrowserScale.Px(Mathf.Max(contentMin, CompactListMinWidthWithTree));
        }

        private static float LayoutMaxWidthFor(PoseBrowserLayoutTier tier) =>
            PoseBrowserScale.Px(tier == PoseBrowserLayoutTier.CompactMini ? 440f : 1400f);

        private static float LayoutMinHeightFor(PoseBrowserLayoutTier tier) => PoseBrowserScale.Px(tier switch
        {
            PoseBrowserLayoutTier.Normal => 500f,
            PoseBrowserLayoutTier.CompactList => 278f,
            PoseBrowserLayoutTier.CompactMini => 152f,
            _ => 500f
        });

        private static float LayoutMaxHeightFor(PoseBrowserLayoutTier tier) =>
            PoseBrowserScale.Px(tier == PoseBrowserLayoutTier.CompactMini ? 320f : 1000f);

        private float LayoutMinWidth => EffectiveLayoutMinWidthFor(_layoutTier);
        private float LayoutMaxWidth => LayoutMaxWidthFor(_layoutTier);
        private float LayoutMinHeight => LayoutMinHeightFor(_layoutTier);
        private float LayoutMaxHeight => LayoutMaxHeightFor(_layoutTier);

        private float _cardCellSize = 152f;
        private int _itemsPerPage;

        /// <summary>Grid column metrics computed on the Layout pass and reused on Repaint (avoids resize flicker / IMGUI mismatches).</summary>
        private struct PoseGridLayoutMetrics
        {
            public int Frame;
            public float GridAvailW;
            public float ContentWidth;
            public int Columns;
            public float ColumnFootprintW;
            public float CellInnerW;
            public float UniformPoseCardOuterH;
            public float UniformTagBlockH;
            public float UniformGroupTagBlockH;
        }

        private PoseGridLayoutMetrics _poseGridLayout;
        private readonly List<float> _gridRowOuterHeights = new List<float>();
        private int _gridUniformLayoutFrame = -1;
        private int _gridUniformLayoutColumns = -1;
        private float _gridUniformLayoutCellInnerW = -1f;
        private float _gridUniformLayoutColumnFootprintW = -1f;
        private float _gridUniformLayoutGridAvailW = -1f;
        private int _gridUniformLayoutRowCount = -1;
        private int _gridUniformLayoutDisplayCount = -1;
        private bool _gridUniformLayoutImportPreview;
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
        private float _gridScrollViewportH;
        private bool _isResizing;

        // Search & filter state
        private string _searchText = "";
        private bool _searchUseRegex;
        private string _searchRegexError = "";
        private readonly HashSet<string> _tagFiltersInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _tagFiltersExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _tagFilterAndMode = true;
        private PoseDisplayFilterMode _tagFilterGroupsMode;
        private PoseDisplayFilterMode _tagFilterThumbnailMode;
        private bool _showFavoritesOnly;

        private readonly List<PoseBrowserFilterPreset> _filterPresets = new List<PoseBrowserFilterPreset>();
        private string? _activeFilterPresetName;
        private int _selectedFilterPresetIndex = -1;
        private string _filterPresetSaveName = "";
        private Vector2 _filterPresetConfigScroll;
        private static string FilterPresetsPath => PoseBrowserFilterPresets.GetDefaultPath();

        private const string TagFilterTipsTooltip =
            "Click a tag to cycle: neutral → include (+) → exclude (−).\n\n" +
            "Grouped poses inherit their group's tags for include/exclude. Exclude hides ungrouped poses; " +
            "in a visible group, members with an excluded tag (on the pose or group) are dimmed.";

        private enum TagWindowPurpose { None, FilterLibrary, EditSelection }
        private TagWindowPurpose _tagWindowPurpose = TagWindowPurpose.None;
        private Rect _tagWindowRect;
        private Vector2 _tagWindowScroll;
        private string _tagWindowSearch = "";
        /// <summary>Tracks assign-pane target so selection changes refresh the pane without clearing search.</summary>
        private string _tagWindowTargetKey = "";

        // Multi-select (global index into _filteredItems when paginating)
        private int _lastClickedGlobalIndex = -1;

        private bool _showOptionsPane;
        private Rect _optionsWindowRect;

        private bool _showHelpPane;
        private Rect _helpWindowRect;
        private Vector2 _helpScroll;
        private Vector2 _optionsScroll;

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
        private string _lastDatRepairMessage = "";
        private Coroutine? _poseFileRepairCoroutine;
        private readonly PoseFileRepair.RepairProgress _poseFileRepairProgress = new PoseFileRepair.RepairProgress();
        private readonly PoseFileRepair.RepairResult _poseFileRepairResult = new PoseFileRepair.RepairResult();
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

        // Thumbnail loading coroutine (on-demand / viewport-bound + LRU eviction).
        // Only cards that actually get drawn (i.e. the virtualized visible rows) request their
        // thumbnail, so the queue stays tiny regardless of how many poses the page lists.
        private Coroutine? _thumbnailLoadCoroutine;
        private readonly HashSet<PoseGridItem> _thumbnailsPendingLoad = new HashSet<PoseGridItem>();
        private readonly List<PoseGridItem> _thumbnailLoadBatch = new List<PoseGridItem>(8);
        private const int ThumbnailLruMaxLoaded = 350;
        private const int ThumbnailLruEvictBatch = 80;
        private int _thumbnailEvictionFrame;
        private bool _thumbnailLoadNeeded;

        // Background index for All poses / Favorites (warmed after Studio loads)
        private readonly PoseLibraryIndexCache _libraryCache = new PoseLibraryIndexCache();
        private Coroutine? _libraryCacheCoroutine;
        private bool _studioLibraryCacheWarmupTriggered;

        // GUIStyles (lazy-init)
        private GUIStyle? _poseCardBaseStyle;
        private GUIStyle? _selectedStyle;
        private GUIStyle? _favoriteCardStyle;
        private GUIStyle? _dimmedCardStyle;
        private GUIStyle? _poseCardNameStyle;
        private GUIStyle? _favoriteCardNameStyle;
        private GUIStyle? _favoriteCardTagStyle;
        private GUIStyle? _favoriteStyle;
        private GUIStyle? _treeNodeStyle;
        private GUIStyle? _treeNodeSelectedStyle;
        private GUIStyle? _tagWrapStyle;
        private GUIStyle? _tagWrapStyleRich;
        private GUIStyle? _characterHintStyle;
        private GUIStyle? _compactWordWrapStyle;

        private readonly PoseBrowserUpdateCheck _poseBrowserUpdateCheck = new PoseBrowserUpdateCheck();
        private Coroutine? _poseBrowserUpdateCheckCoroutine;

        // BepInEx config ↔ Options panel (grid settings); hotkeys use KeyboardShortcut in Configuration Manager
        private EventHandler? _cfgGridHandler;

        protected override void Start()
        {
            base.Start();
            windowID = SandboxImguiWindowIds.PoseBrowser.Main;
            windowTitle = $"Pose Browser v{PoseBrowserVersionInfo.Version}";
            windowRect = new Rect(200f, 80f, PoseBrowserScale.Px(900f), PoseBrowserScale.Px(620f));

            string poseRoot = PathEx.Combine(UserData.Path, "studio", "pose");
            _dataService = new PoseDataService(poseRoot);
            _tagDb = new PoseTagDatabase(poseRoot);
            _groupDb = new PoseGroupDatabase(poseRoot);
            _itemDb = new PoseItemDatabase(poseRoot);
            _folderTree = new PoseFolderTree(poseRoot);
            _thumbCapture = new PoseThumbnailCapture();

            CreatePlaceholderTexture();
            _folderTree.Refresh();
            _optionsWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, OptionsPaneDefaultWidth, windowRect.height);
            _helpWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, HelpPaneDefaultWidth, windowRect.height);
            _tagWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, TagPaneDefaultWidth, windowRect.height);
            _sortWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, SortPaneDefaultWidth, windowRect.height);
            _characterConfigWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, CharacterPaneDefaultWidth, windowRect.height);
            PoseBrowserConfig.Register(SandboxServices.Config);
            if (PoseBrowserConfig.CardColumnWidth != null)
            {
                _cfgGridHandler = (_, __) => ApplyPoseBrowserConfigToUi();
                PoseBrowserConfig.CardColumnWidth.SettingChanged += _cfgGridHandler;
                PoseBrowserConfig.ItemsPerPage!.SettingChanged += _cfgGridHandler;
            }

            LoadPersistedOptions();
            LoadFilterPresets();
            InitPoseHistory();
            InitPoseStash();
            ApplyPoseBrowserConfigToUi();
            SyncWindowTitleForLayoutTier();
            _poseBrowserUpdateCheckCoroutine = StartCoroutine(_poseBrowserUpdateCheck.RunCheck());
        }

        private void Update()
        {
            _tagDb?.Update();
            _groupDb?.Update();
            TryStartStudioLibraryCacheWarmup();
            MaybeProcessPoseBrowserWindowHotkeys();
            if (isVisible && !_isMinimized)
            {
                RefreshStudioSelectionCacheIfDue(force: false);
                MaybeProcessPoseBrowserHotkeys();
                EvictLruThumbnailsIfNeeded();
                if (_thumbnailLoadNeeded && _thumbnailLoadCoroutine == null && _thumbnailsPendingLoad.Count > 0)
                {
                    _thumbnailLoadNeeded = false;
                    _thumbnailLoadCoroutine = StartCoroutine(LoadThumbnailsCoroutine());
                }
            }
        }

        private void OnDestroy()
        {
            if (_poseBrowserUpdateCheckCoroutine != null)
            {
                StopCoroutine(_poseBrowserUpdateCheckCoroutine);
                _poseBrowserUpdateCheckCoroutine = null;
            }

            if (PoseBrowserConfig.CardColumnWidth != null && _cfgGridHandler != null)
            {
                PoseBrowserConfig.CardColumnWidth.SettingChanged -= _cfgGridHandler;
                PoseBrowserConfig.ItemsPerPage!.SettingChanged -= _cfgGridHandler;
            }

            _tagDb?.ForceSave();
            _groupDb?.ForceSave();
            _itemDb?.ForceSave();
            SavePoseHistory();
            SavePoseStash();
            SavePersistedOptions();
            if (_libraryCacheCoroutine != null)
            {
                StopCoroutine(_libraryCacheCoroutine);
                _libraryCacheCoroutine = null;
            }

            if (_poseFileRepairCoroutine != null)
            {
                StopCoroutine(_poseFileRepairCoroutine);
                _poseFileRepairCoroutine = null;
                _poseFileRepairProgress.IsRunning = false;
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
            if (!visible)
            {
                _isMinimized = false;
                OnMainPoseBrowserHidden();
            }
            if (visible && _folderTree.RootNodes.Count == 0)
                _folderTree.Refresh();
            if (visible && !_didAutoLoadBrowse)
            {
                _didAutoLoadBrowse = true;
                LoadFolder(_folderTree.RootPath);
            }
            else if (visible && _allItems.Count > 0)
            {
                MaybeStartThumbnailsAfterLoad();
            }
        }

        public override void DrawWindow()
        {
            // Swap in a font-scaled clone of the active skin so all skin-driven controls (buttons, labels,
            // boxes, toggles, text fields, window chrome, scrollbars) follow the UI scale, then restore it so
            // other Sandbox windows drawn in the same OnGUI pass are unaffected.
            var prevSkin = GUI.skin;
            GUI.skin = GetScaledPoseBrowserSkin(prevSkin);
            try
            {
                DrawWindowScaled();
            }
            finally
            {
                GUI.skin = prevSkin;
            }
        }

        private static GUISkin? _scaledSkin;
        private static GUISkin? _scaledSkinBase;
        private static float _scaledSkinFactor = -1f;

        private static GUISkin GetScaledPoseBrowserSkin(GUISkin baseSkin)
        {
            PoseBrowserScale.CaptureBaseFont(baseSkin);
            float f = PoseBrowserScale.Factor;

            // At ~1× there is nothing to scale; use the base skin directly (no clone, no side effects).
            if (Mathf.Approximately(f, 1f))
                return baseSkin;

            if (_scaledSkin != null && _scaledSkinBase == baseSkin && Mathf.Approximately(_scaledSkinFactor, f))
                return _scaledSkin;

            var skin = UnityEngine.Object.Instantiate(baseSkin);
            int scaledBase = PoseBrowserScale.Font(PoseBrowserScale.BaseFontSize);

            void ScaleFont(GUIStyle? s)
            {
                if (s == null) return;
                s.fontSize = s.fontSize > 0 ? Mathf.Max(1, Mathf.RoundToInt(s.fontSize * f)) : scaledBase;
                // Some game skins pin a fixed line height/width on label/button styles. If left unscaled the
                // row stays at the old height and the now-larger glyphs clip (seen on the tree footer labels).
                if (s.fixedHeight > 0f) s.fixedHeight *= f;
                if (s.fixedWidth > 0f) s.fixedWidth *= f;
            }

            ScaleFont(skin.box);
            ScaleFont(skin.label);
            ScaleFont(skin.button);
            ScaleFont(skin.toggle);
            ScaleFont(skin.textField);
            ScaleFont(skin.textArea);
            ScaleFont(skin.window);
            ScaleFont(skin.horizontalSlider);
            ScaleFont(skin.horizontalSliderThumb);
            ScaleFont(skin.verticalSlider);
            ScaleFont(skin.verticalSliderThumb);
            ScaleFont(skin.scrollView);
            if (skin.customStyles != null)
                foreach (var cs in skin.customStyles)
                    ScaleFont(cs);

            // Widen scrollbars so they stay grabbable at higher scales (grid width math reads these via GUI.skin).
            if (skin.verticalScrollbar != null && skin.verticalScrollbar.fixedWidth > 0f)
                skin.verticalScrollbar.fixedWidth *= f;
            if (skin.horizontalScrollbar != null && skin.horizontalScrollbar.fixedHeight > 0f)
                skin.horizontalScrollbar.fixedHeight *= f;

            skin.font = baseSkin.font;
            skin.hideFlags = HideFlags.HideAndDontSave;

            _scaledSkin = skin;
            _scaledSkinBase = baseSkin;
            _scaledSkinFactor = f;
            return skin;
        }

        private float _cachedStyleScaleFactor = float.NaN;

        /// <summary>
        /// Cached <see cref="GUIStyle"/>s capture their font size at first build. When the UI scale changes we
        /// must drop every cached style (and the grid metrics that depend on measured text) so they rebuild
        /// against the new scaled skin.
        /// </summary>
        private void EnsureStyleCachesMatchScale()
        {
            float f = PoseBrowserScale.Factor;
            if (_cachedStyleScaleFactor.Equals(f))
                return;
            _cachedStyleScaleFactor = f;

            _poseCardBaseStyle = null; _selectedStyle = null; _favoriteCardStyle = null; _dimmedCardStyle = null;
            _poseCardNameStyle = null; _favoriteCardNameStyle = null; _favoriteCardTagStyle = null; _favoriteStyle = null;
            _treeNodeStyle = null; _treeNodeSelectedStyle = null; _tagWrapStyle = null; _tagWrapStyleRich = null;
            _characterHintStyle = null; _compactWordWrapStyle = null; _optionsWrapStyle = null;
            _hotkeySectionBoxStyle = null; _hotkeyHeaderStyle = null; _hotkeyRowBoxStyle = null;
            _hotkeyActionStyle = null; _hotkeyBindingBadgeStyle = null; _hotkeyUnassignedBadgeStyle = null;
            _historyRichLabelStyle = null; _historyEntryBoxStyle = null; _historyEntryCurrentBoxStyle = null; _historyEntryButtonStyle = null;
            _groupCardStyle = null; _groupCardSelectedStyle = null; _groupTitleStyle = null; _groupInnerCardChromeBase = null;
            _groupInnerCardStyle = null; _groupInnerCardSelectedStyle = null; _groupInnerCardFavoriteStyle = null; _groupInnerCardDimmedStyle = null;
            _actionBarSeparatorStyle = null;
            _itemPaneWarnLabelStyle = null; _itemPaneSectionLabelStyle = null; _itemPaneStoredNameButtonStyle = null;
            _itemPaneStoredNameSelectedButtonStyle = null; _itemPaneIconButtonStyle = null;
            _stashEntryBoxStyle = null; _stashEntryButtonStyle = null;
            _poseBrowserTooltipStyle = null;

            // Grid row/cell metrics depend on measured (scaled) text — force a full recompute next layout pass.
            _poseGridLayout = new PoseGridLayoutMetrics();
            _gridUniformLayoutGridAvailW = -1f;
        }

        private void DrawWindowScaled()
        {
            EnsureStyleCachesMatchScale();

            if (_showUndockedStash)
                DrawUndockedStashWindow();

            if (!isVisible)
                return;

            if (_thumbCapture.IsActive)
                _thumbCapture.DrawOverlay();

            if (_isMinimized)
            {
                DrawMinimizedRestoreChip();
                return;
            }

            HandleResize();
            float minW = EffectiveResizeMinWidth();
            float minH = LayoutMinHeight;
            windowRect.width = Mathf.Clamp(windowRect.width, minW, LayoutMaxWidth);
            windowRect.height = Mathf.Clamp(windowRect.height, minH, LayoutMaxHeight);
            windowRect.x = Mathf.Clamp(windowRect.x, 4f, Mathf.Max(4f, Screen.width - windowRect.width - 4f));
            windowRect.y = Mathf.Clamp(windowRect.y, 4f, Mathf.Max(4f, Screen.height - windowRect.height - 4f));

            // GUILayout.Window grows to content min-size; that fights shrinking while dragging.
            float lockedW = windowRect.width;
            float lockedH = windowRect.height;
            var windowIn = new Rect(windowRect.x, windowRect.y, lockedW, lockedH);

            windowRect = GUILayout.Window(windowID, windowIn, DrawWindowContent, windowTitle);

            // GUILayout.Window grows the returned rect to fit its content min-size, and at some UI scales that
            // growth is a sub-pixel amount that would slip under a tolerance and accumulate frame after frame
            // (the window "creeps" larger). Pin the size back to the explicit locked value unconditionally —
            // only the drag-updated x/y from the returned rect are kept.
            windowRect.width = lockedW;
            windowRect.height = lockedH;
            if (!_isResizing)
            {
                windowRect.width = Mathf.Clamp(windowRect.width, minW, LayoutMaxWidth);
                windowRect.height = Mathf.Clamp(windowRect.height, minH, LayoutMaxHeight);
            }

            if (_layoutTier == PoseBrowserLayoutTier.Normal)
            {
                bool anyDockedPane = _showOptionsPane || _showHelpPane || _showHistoryPane || IsStashDockedVisible ||
                    _tagWindowPurpose != TagWindowPurpose.None || _showCharacterConfigPane || _showSortPane ||
                    _showItemAssociationPane;
                bool layoutPass = Event.current.type == EventType.Layout;
                if (anyDockedPane && layoutPass)
                    SyncAllDockedPaneRects();

                if (_showOptionsPane)
                    DrawDockedPaneWindow(OptionsWindowId, ref _optionsWindowRect, DrawOptionsWindowContent, "Pose Browser · Options", OptionsPaneDefaultWidth);

                if (_showHelpPane)
                    DrawDockedPaneWindow(HelpWindowId, ref _helpWindowRect, DrawHelpWindowContent, "Pose Browser · Help", HelpPaneDefaultWidth);

                if (_showHistoryPane)
                    DrawDockedPaneWindow(HistoryWindowId, ref _historyWindowRect, DrawHistoryWindowContent, "Pose Browser · History", HistoryPaneDefaultWidth);

                if (IsStashDockedVisible)
                    DrawDockedPaneWindow(StashWindowId, ref _stashWindowRect, DrawStashWindowContent, "Pose Browser · Stash", StashPaneDefaultWidth);

                if (_tagWindowPurpose != TagWindowPurpose.None)
                {
                    if (_tagWindowPurpose == TagWindowPurpose.EditSelection && layoutPass)
                        SyncTagAssignWindowToSelection();
                    string tagTitle = _tagWindowPurpose == TagWindowPurpose.FilterLibrary
                        ? "Pose Browser · Tag filter"
                        : _tagWindowForGroup
                            ? "Pose Browser · Group tags"
                            : "Pose Browser · Tags on selection";
                    DrawDockedPaneWindow(TagWindowId, ref _tagWindowRect, DrawTagWindowContent, tagTitle, TagPaneDefaultWidth);
                }

                if (_showCharacterConfigPane)
                    DrawCharacterConfigDockedPane();

                if (_showSortPane)
                    DrawDockedPaneWindow(SortWindowId, ref _sortWindowRect, DrawSortWindowContent, "Pose Browser · Sort", SortPaneDefaultWidth);

                if (_showItemAssociationPane)
                    DrawItemAssociationDockedPane();
            }
            else if ((_layoutTier == PoseBrowserLayoutTier.CompactList ||
                      _layoutTier == PoseBrowserLayoutTier.CompactMini) &&
                     (_showCharacterConfigPane || _showHistoryPane || IsStashDockedVisible))
            {
                SyncCompactListDockedPanes();
                if (_showHistoryPane)
                    DrawDockedPaneWindow(HistoryWindowId, ref _historyWindowRect, DrawHistoryWindowContent, "Pose Browser · History", HistoryPaneDefaultWidth);
                if (IsStashDockedVisible)
                    DrawDockedPaneWindow(StashWindowId, ref _stashWindowRect, DrawStashWindowContent, "Pose Browser · Stash", StashPaneDefaultWidth);
                if (_showCharacterConfigPane)
                    DrawCharacterConfigDockedPane();
            }

            DrawCompactHoverThumbnail();
        }

        private void SyncCompactListDockedPanes()
        {
            float x = windowRect.xMax + DockedPaneGap;
            if (_showHistoryPane)
                x = PlaceDockedPane(ref _historyWindowRect, x, HistoryPaneDefaultWidth);
            if (IsStashDockedVisible)
                x = PlaceDockedPane(ref _stashWindowRect, x, StashPaneDefaultWidth);
            if (_showCharacterConfigPane)
                PlaceDockedPane(ref _characterConfigWindowRect, x, CharacterPaneDefaultWidth);
            ShiftOpenDockedPanesLeftToFitScreen();
        }

        private void SyncCharacterConfigPaneRect()
        {
            PlaceDockedPane(ref _characterConfigWindowRect, windowRect.xMax + DockedPaneGap, CharacterPaneDefaultWidth);
            const float margin = 4f;
            float overflow = _characterConfigWindowRect.xMax - (Screen.width - margin);
            if (overflow > 0f)
                ShiftPaneX(ref _characterConfigWindowRect, -overflow);
        }

        private void DrawCharacterConfigDockedPane()
        {
            DrawDockedPaneWindow(
                CharacterConfigWindowId,
                ref _characterConfigWindowRect,
                DrawCharacterConfigWindowContent,
                "Pose Browser · Characters",
                CharacterPaneDefaultWidth);
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
            if (_showHistoryPane)
                x = PlaceDockedPane(ref _historyWindowRect, x, HistoryPaneDefaultWidth);
            if (IsStashDockedVisible)
                x = PlaceDockedPane(ref _stashWindowRect, x, StashPaneDefaultWidth);
            if (_tagWindowPurpose != TagWindowPurpose.None)
                x = PlaceDockedPane(ref _tagWindowRect, x, TagPaneDefaultWidth);
            if (_showCharacterConfigPane)
                x = PlaceDockedPane(ref _characterConfigWindowRect, x, CharacterPaneDefaultWidth);
            if (_showSortPane)
                x = PlaceDockedPane(ref _sortWindowRect, x, SortPaneDefaultWidth);
            if (_showItemAssociationPane)
                PlaceDockedPane(ref _itemAssociationWindowRect, x, ItemAssociationPaneDefaultWidth);

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
            if (_showHistoryPane)
                ShiftPaneX(ref _historyWindowRect, -overflow);
            if (IsStashDockedVisible)
                ShiftPaneX(ref _stashWindowRect, -overflow);
            if (_tagWindowPurpose != TagWindowPurpose.None)
                ShiftPaneX(ref _tagWindowRect, -overflow);
            if (_showCharacterConfigPane)
                ShiftPaneX(ref _characterConfigWindowRect, -overflow);
            if (_showSortPane)
                ShiftPaneX(ref _sortWindowRect, -overflow);
            if (_showItemAssociationPane)
                ShiftPaneX(ref _itemAssociationWindowRect, -overflow);
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

            if (_showHistoryPane)
            {
                any = true;
                minX = Mathf.Min(minX, _historyWindowRect.x);
                maxX = Mathf.Max(maxX, _historyWindowRect.xMax);
            }

            if (IsStashDockedVisible)
            {
                any = true;
                minX = Mathf.Min(minX, _stashWindowRect.x);
                maxX = Mathf.Max(maxX, _stashWindowRect.xMax);
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

            if (_showItemAssociationPane)
            {
                any = true;
                minX = Mathf.Min(minX, _itemAssociationWindowRect.x);
                maxX = Mathf.Max(maxX, _itemAssociationWindowRect.xMax);
            }

            return any;
        }

        private static void ShiftPaneX(ref Rect pane, float dx)
        {
            pane = new Rect(pane.x + dx, pane.y, pane.width, pane.height);
        }

        private void DrawDockedPaneWindow(
            int paneId,
            ref Rect paneRect,
            GUI.WindowFunction drawContent,
            string title,
            float minWidth)
        {
            float tooltipW = Mathf.Max(paneRect.width, minWidth);
            float tooltipH = Mathf.Max(paneRect.height, 120f);
            void Wrapped(int winId)
            {
                drawContent(winId);
                if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
                    DrawPoseBrowserTooltip(GUI.tooltip, new Rect(0f, 0f, tooltipW, tooltipH));
            }

            paneRect = GUILayout.Window(
                paneId,
                paneRect,
                Wrapped,
                title,
                GUILayout.MinWidth(minWidth),
                PoseBrowserScale.MinH(120f));
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
            _tagWindowTargetKey = "";
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
            _tagWindowTargetKey = "";
        }

        /// <summary>Keeps the assign tag pane aligned with group vs pose selection while it stays open.</summary>
        private void SyncTagAssignWindowToSelection()
        {
            if (_tagWindowPurpose != TagWindowPurpose.EditSelection)
                return;

            if (_selectedGroupIds.Count > 0)
            {
                PruneSelectedGroups();
                var ids = _selectedGroupIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
                string newKey = "g:" + string.Join("|", ids.ToArray());
                if (newKey == _tagWindowTargetKey)
                    return;

                _tagWindowTargetKey = newKey;
                _tagWindowForGroup = true;
                _tagWindowGroupIds.Clear();
                foreach (var gid in ids)
                    _tagWindowGroupIds.Add(gid);
                _tagWindowGroupId = _tagWindowGroupIds.Count == 1 ? _tagWindowGroupIds[0] : null;
                _tagWindowScroll = Vector2.zero;
                return;
            }

            var selected = _filteredItems
                .Where(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId))
                .ToList();
            if (selected.Count > 0)
            {
                string pathsKey = string.Join("|", selected.Select(i => i.FilePath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray());
                string newKey = "p:" + pathsKey;
                if (newKey == _tagWindowTargetKey)
                    return;

                _tagWindowTargetKey = newKey;
                _tagWindowForGroup = false;
                _tagWindowGroupId = null;
                _tagWindowGroupIds.Clear();
                _tagWindowScroll = Vector2.zero;
                return;
            }

            CloseTagWindow();
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
            RecordPoseHistoryBeforeSingleApply(item);
            RecordNonGroupPoseApply();
            _tagDb.RecordLastUsed(item);
            _dataService.ApplyPoseToSelected(item);
            RecordPoseHistoryAfterSingleApply(item);
#if HS2
            HeelzControlService.ApplyTagRulesForSelectedCharacters(
                _dataService.GetSelectedCharacters(), item.Tags);
#endif
            if (_poseSortMode == PoseSortMode.LastUsed)
            {
                ResortPoseItemsInPlace();
                ApplyFilters();
            }
        }

        private bool HasActivePoseContentFilters() =>
            _showFavoritesOnly
            || !StringEx.IsNullOrWhiteSpace(_searchText)
            || _tagFiltersInclude.Count > 0
            || _tagFiltersExclude.Count > 0;

        private void SelectAllMatchingDisplayResults()
        {
            ClearGroupSelection();
            foreach (var it in _allItems)
                it.IsSelected = false;
            foreach (var e in _displayEntries)
            {
                if (!e.IsDimmed)
                    e.Item.IsSelected = true;
            }
        }

        private void SelectAllInCurrentFolderView()
        {
            if (HasActivePoseContentFilters())
            {
                SelectAllMatchingDisplayResults();
                return;
            }

            ClearGroupSelection();
            foreach (var it in _allItems)
                it.IsSelected = true;
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
                    CaptureCompactListWindowSize();
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
                    GetCompactListSavedSize(out w, out h, out x, out y);
                    break;
                default:
                    w = _savedMiniW;
                    h = _savedMiniH;
                    x = _savedMiniX;
                    y = _savedMiniY;
                    break;
            }

            float defW = tier switch
            {
                PoseBrowserLayoutTier.Normal => 900f,
                PoseBrowserLayoutTier.CompactList => _compactListShowTree
                    ? CompactListDefaultWidthWithTree
                    : CompactListDefaultWidthNoTree,
                _ => 280f
            };
            float defH = tier == PoseBrowserLayoutTier.Normal ? 620f
                : tier == PoseBrowserLayoutTier.CompactList ? CompactListDefaultHeight
                : 240f;
            if (w < 50f) w = defW;
            if (h < 50f) h = defH;

            w = Mathf.Clamp(w, EffectiveLayoutMinWidthFor(tier), LayoutMaxWidthFor(tier));
            h = Mathf.Clamp(h, LayoutMinHeightFor(tier), LayoutMaxHeightFor(tier));
            windowRect = new Rect(x, y, w, h);
        }

        private void CaptureCompactListWindowSize()
        {
            _savedListX = windowRect.x;
            _savedListY = windowRect.y;
            if (_compactListShowTree)
            {
                _savedListW = windowRect.width;
                _savedListH = windowRect.height;
            }
            else
            {
                _savedListNoTreeW = windowRect.width;
                _savedListNoTreeH = windowRect.height;
            }
        }

        private void GetCompactListSavedSize(out float w, out float h, out float x, out float y)
        {
            x = _savedListX;
            y = _savedListY;
            if (_compactListShowTree)
            {
                w = _savedListW;
                h = _savedListH;
            }
            else
            {
                w = _savedListNoTreeW;
                h = _savedListNoTreeH;
            }
        }

        private void ApplyCompactListWindowSizeFromSaved()
        {
            GetCompactListSavedSize(out float w, out float h, out float x, out float y);
            float defW = _compactListShowTree ? CompactListDefaultWidthWithTree : CompactListDefaultWidthNoTree;
            if (w < 50f) w = defW;
            if (h < 50f) h = CompactListDefaultHeight;
            w = Mathf.Clamp(w, EffectiveLayoutMinWidthFor(PoseBrowserLayoutTier.CompactList), LayoutMaxWidth);
            h = Mathf.Clamp(h, LayoutMinHeight, LayoutMaxHeight);
            windowRect = new Rect(x, y, w, h);
        }

        private void ToggleCompactListTree()
        {
            if (_layoutTier != PoseBrowserLayoutTier.CompactList)
                return;

            CaptureCompactListWindowSize();
            _compactListShowTree = !_compactListShowTree;
            ApplyCompactListWindowSizeFromSaved();
            SavePersistedOptions();
        }

        /// <summary>After changing browse target (folder / all / favorites), apply the first filtered pose and sync compact index + grid selection.</summary>
        private void ApplyFirstFilteredPoseAfterBrowseChange()
        {
            if (_filteredItems.Count == 0)
            {
                ClearCompactListSelection();
                return;
            }

            var item = _filteredItems[0];
            SelectCompactPose(0);
            for (int i = 0; i < _filteredItems.Count; i++)
                _filteredItems[i].IsSelected = false;
            item.IsSelected = true;
            _lastClickedGlobalIndex = 0;
            ApplyPoseToSelectedWithUsage(item);
        }

        private void FinishMainWindowChrome()
        {
            if (_layoutTier == PoseBrowserLayoutTier.CompactList && Event.current.type == EventType.Repaint)
                TryCaptureCompactHoverFromTooltip();

            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
                DrawPoseBrowserTooltip(GUI.tooltip, windowRect);

            var resizeHandle = new Rect(windowRect.width - ResizeHandleSize, windowRect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
            GUI.Box(resizeHandle, "◢");

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - ResizeHandleSize, 20f));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private void DrawWindowChromeButtons(float buttonHeight)
        {
            if (GUILayout.Button(new GUIContent("−", "Minimize Pose Browser"), GUILayout.Width(WindowChromeButtonWidth), PoseBrowserScale.H(buttonHeight)))
            {
                var btnRect = GUILayoutUtility.GetLastRect();
                Vector2 btnScreen = GUIUtility.GUIToScreenPoint(new Vector2(btnRect.x, btnRect.y));
                MinimizePoseBrowser(btnScreen);
            }

            if (GUILayout.Button(new GUIContent("×", "Close Pose Browser"), GUILayout.Width(WindowChromeButtonWidth), PoseBrowserScale.H(buttonHeight)))
                ClosePoseBrowser();
        }

        private void MinimizePoseBrowser(Vector2 minimizeButtonScreen)
        {
            CaptureWindowRectForCurrentTier();
            _minimizeBtnOffsetFromWindow = minimizeButtonScreen - new Vector2(windowRect.x, windowRect.y);
            _minimizedChipRect = new Rect(minimizeButtonScreen.x, minimizeButtonScreen.y, MinimizedChipSize, MinimizedChipSize);
            _chipDragging = false;
            _isMinimized = true;
        }

        private void RestoreFromMinimize()
        {
            _isMinimized = false;
            RestoreWindowRectForTier(_layoutTier);

            windowRect.x = _minimizedChipRect.x - _minimizeBtnOffsetFromWindow.x;
            windowRect.y = _minimizedChipRect.y - _minimizeBtnOffsetFromWindow.y;
            windowRect.x = Mathf.Clamp(windowRect.x, 4f, Mathf.Max(4f, Screen.width - windowRect.width - 4f));
            windowRect.y = Mathf.Clamp(windowRect.y, 4f, Mathf.Max(4f, Screen.height - windowRect.height - 4f));
        }

        private void ClosePoseBrowser()
        {
            _isMinimized = false;
            var gui = FindObjectOfType<SandboxGUI>();
            if (gui != null)
                gui.SetPoseBrowserVisible(false);
            else
                SetVisible(false);
        }

#if HS2
        private void ToggleHeelzControlWindow()
        {
            var gui = FindObjectOfType<SandboxGUI>();
            if (gui != null)
                gui.SetHeelzControlVisible(!gui.IsHeelzControlVisible);
        }
#endif

        private void DrawMinimizedRestoreChip()
        {
            Event e = Event.current;
            if (_minimizedChipRect.width < 1f)
                _minimizedChipRect = new Rect(_minimizedChipRect.x, _minimizedChipRect.y, MinimizedChipSize, MinimizedChipSize);

            var chip = _minimizedChipRect;
            GUI.Box(chip, new GUIContent("PB", "Restore Pose Browser"));

            if (e.type == EventType.MouseDown && e.button == 0 && chip.Contains(e.mousePosition))
            {
                _chipDragging = true;
                _chipDragOffset = e.mousePosition - new Vector2(chip.x, chip.y);
                _chipMouseDownPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _chipDragging)
            {
                float nx = e.mousePosition.x - _chipDragOffset.x;
                float ny = e.mousePosition.y - _chipDragOffset.y;
                nx = Mathf.Clamp(nx, 0f, Screen.width - chip.width);
                ny = Mathf.Clamp(ny, 0f, Screen.height - chip.height);
                _minimizedChipRect = new Rect(nx, ny, chip.width, chip.height);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && _chipDragging)
            {
                bool clicked = (e.mousePosition - _chipMouseDownPos).sqrMagnitude <=
                    MinimizedChipClickDragThreshold * MinimizedChipClickDragThreshold;
                _chipDragging = false;
                if (clicked && chip.Contains(e.mousePosition))
                    RestoreFromMinimize();
                e.Use();
            }

            IMGUIUtils.EatInputInRect(chip);
        }

        private void SyncWindowTitleForLayoutTier()
        {
            string layout = _layoutTier switch
            {
                PoseBrowserLayoutTier.CompactMini => "Pose Browser · Mini",
                PoseBrowserLayoutTier.CompactList => "Pose Browser · Compact",
                _ => "Pose Browser"
            };
            windowTitle = $"{layout} v{PoseBrowserVersionInfo.Version}";
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
            // No-op: thumbnails are now loaded on demand from the draw path (RequestThumbnail),
            // so only the visible cards trigger a load. Kept for the existing call sites after
            // a folder/view load; the next DrawGridCell pass queues whatever is on screen.
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
            bool isMini = _layoutTier == PoseBrowserLayoutTier.CompactMini;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal(PoseBrowserScale.H(26f));
            DrawWindowChromeButtons(24f);
            GUILayout.Space(6f);
            if (GUILayout.Button(new GUIContent($"View ({LayoutTierShortLabel()})", "Cycle: Full → compact list → mini"), PoseBrowserScale.W(110f), PoseBrowserScale.H(24f)))
                CycleLayoutTier();
            if (_layoutTier == PoseBrowserLayoutTier.CompactList)
            {
                if (GUILayout.Button(
                        new GUIContent(_compactListShowTree ? "Tree ▶" : "Tree", "Show or hide the folder tree panel. Window size is remembered separately for each layout."),
                        PoseBrowserScale.W(52f),
                        PoseBrowserScale.H(24f)))
                    ToggleCompactListTree();
            }

            GUILayout.FlexibleSpace();
            if (isMini)
                DrawPoseBrowserUpdateNotice(24f);
            GUILayout.EndHorizontal();

            if (_layoutTier == PoseBrowserLayoutTier.CompactList)
            {
                GUILayout.BeginHorizontal(PoseBrowserScale.H(24f));
                DrawHistoryCompactListHeaderButtons();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(PoseBrowserScale.H(24f));
                DrawTopBarCharacterSection(24f, compact: true);
                GUILayout.FlexibleSpace();
                DrawPoseBrowserUpdateNotice(24f);
                GUILayout.EndHorizontal();
            }
            else if (isMini)
            {
                GUILayout.BeginHorizontal(PoseBrowserScale.H(24f));
                DrawHistoryMiniHeaderButtons();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(PoseBrowserScale.H(24f));
                DrawTopBarCharacterSection(24f, compact: true);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
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
            ClearCompactListSelection();
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

        private void SelectCompactPose(int index)
        {
            _compactSelectedGroupId = null;
            _compactPoseIndex = index;
        }

        private void SelectCompactGroup(string groupId)
        {
            _compactSelectedGroupId = groupId;
            _compactPoseIndex = -1;
        }

        private void ClearCompactListSelection()
        {
            _compactSelectedGroupId = null;
            _compactPoseIndex = -1;
        }

        private bool IsCompactGroupSelected(string groupId) =>
            !string.IsNullOrEmpty(_compactSelectedGroupId) &&
            string.Equals(_compactSelectedGroupId, groupId, StringComparison.Ordinal);

        private bool CompactSelectedGroupStillInList()
        {
            if (string.IsNullOrEmpty(_compactSelectedGroupId))
                return false;
            foreach (var e in _displayEntries)
            {
                if (e.Item.GroupId == _compactSelectedGroupId)
                    return true;
            }

            return false;
        }

        private void AdvanceCompactPose(int delta, bool applyToStudio)
        {
            if (CountDisplayPoses() == 0) return;
            _compactSelectedGroupId = null;
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
                ClearCompactListSelection();
                return;
            }

            if (!string.IsNullOrEmpty(_compactSelectedGroupId))
            {
                if (!CompactSelectedGroupStillInList())
                {
                    _compactSelectedGroupId = null;
                    _compactPoseIndex = 0;
                }
                else
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
            if (_compactListShowTree)
                DrawTreePanel(showFolderFooter: false);
            DrawCompactPoseListPanel();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawCompactPoseListPanel()
        {
            InitGroupStyles();
            bool studioHasCharacters = GetCachedStudioHasSelectedCharacters();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            GUILayout.Label(
                new GUIContent($"{CountDisplayPoses()} shown", "After search & tag filters."),
                GUILayout.ExpandWidth(false));
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUI.enabled = CountDisplayPoses() > 0;
            if (GUILayout.Button("◀ Prev", PoseBrowserScale.H(26f), PoseBrowserScale.MinW(72f)))
                AdvanceCompactPose(-1, applyToStudio: true);
            if (GUILayout.Button("Next ▶", PoseBrowserScale.H(26f), PoseBrowserScale.MinW(72f)))
                AdvanceCompactPose(1, applyToStudio: true);
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            _compactListScroll = GUILayout.BeginScrollView(_compactListScroll, GUILayout.ExpandHeight(true));

            // Virtualized: only blocks intersecting the scroll viewport are drawn; spacers stand
            // in for the rest so the scrollbar stays correct regardless of list size.
            var blocks = GetCompactBlocks();
            float pitch = _compactRowPitch;
            float viewportH = _compactListViewportH > 1f ? _compactListViewportH : 600f;
            viewportH = Mathf.Min(viewportH, Mathf.Max(200f, windowRect.height));
            float scrollY = _compactListScroll.y;

            int firstBlock = 0;
            int lastBlock = blocks.Count - 1;
            if (blocks.Count > 0)
            {
                float y = 0f;
                firstBlock = blocks.Count;
                lastBlock = -1;
                for (int b = 0; b < blocks.Count; b++)
                {
                    float h = CompactBlockHeight(blocks[b], pitch);
                    float top = y;
                    float bot = y + h;
                    y = bot;
                    if (bot >= scrollY && top <= scrollY + viewportH)
                    {
                        if (firstBlock > b) firstBlock = b;
                        lastBlock = b;
                    }
                }
                if (firstBlock > lastBlock) { firstBlock = 0; lastBlock = blocks.Count - 1; }
                else { firstBlock = Mathf.Max(0, firstBlock - 1); lastBlock = Mathf.Min(blocks.Count - 1, lastBlock + 1); }
            }

            float spaceAbove = 0f;
            for (int b = 0; b < firstBlock; b++) spaceAbove += CompactBlockHeight(blocks[b], pitch);
            if (spaceAbove > 0f) GUILayout.Space(spaceAbove);

            _compactMeasureLastRowY = -1f;
            for (int b = firstBlock; b <= lastBlock && b < blocks.Count; b++)
            {
                var block = blocks[b];
                if (block.GroupId == null)
                {
                    DrawCompactPoseRow(block.Start);
                }
                else
                {
                    _compactMeasureLastRowY = -1f;
                    DrawCompactGroupBlock(block, studioHasCharacters);
                    _compactMeasureLastRowY = -1f;
                }
            }

            float spaceBelow = 0f;
            for (int b = lastBlock + 1; b < blocks.Count; b++) spaceBelow += CompactBlockHeight(blocks[b], pitch);
            if (spaceBelow > 0f) GUILayout.Space(spaceBelow);

            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
            {
                Rect svRect = GUILayoutUtility.GetLastRect();
                if (svRect.height > 1f) _compactListViewportH = svRect.height;
            }

            GUILayout.EndVertical();
        }

        private List<CompactListBlock> GetCompactBlocks()
        {
            if (_compactBlocks != null)
                return _compactBlocks;

            var blocks = new List<CompactListBlock>();
            int i = 0;
            while (i < _displayEntries.Count)
            {
                string? gid = _displayEntries[i].Item.GroupId;
                var group = !string.IsNullOrEmpty(gid) ? _groupDb.TryGetGroup(gid) : null;
                if (group == null)
                {
                    blocks.Add(new CompactListBlock { Start = i, Count = 1, GroupId = null });
                    i++;
                    continue;
                }

                int start = i;
                while (i < _displayEntries.Count && _displayEntries[i].Item.GroupId == gid)
                    i++;
                blocks.Add(new CompactListBlock { Start = start, Count = i - start, GroupId = gid });
            }

            _compactBlocks = blocks;
            return _compactBlocks;
        }

        private float CompactBlockHeight(in CompactListBlock block, float pitch)
        {
            if (block.GroupId == null)
                return pitch;
            float cardV = _groupCardStyle!.padding.vertical + _groupCardStyle.margin.vertical;
            return cardV + (block.Count + 1) * pitch; // group header row + member rows
        }

        private void DrawCompactGroupBlock(in CompactListBlock block, bool studioHasCharacters)
        {
            string gid = block.GroupId!;
            var group = _groupDb.TryGetGroup(gid);
            bool groupSelected = IsCompactGroupSelected(gid);
            var cardStyle = groupSelected ? _groupCardSelectedStyle! : _groupCardStyle!;
            GUILayout.BeginVertical(cardStyle, GUILayout.ExpandWidth(true));

            string groupLabel = "▦ " + (group != null ? group.Name : "Group");
            var groupHeaderStyle = groupSelected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            bool canGroupApply = studioHasCharacters && block.Count > 0;

            // Manual rect so we can hover-test before building the (expensive) assignment-plan
            // tooltip — only the group actually under the mouse pays for it.
            var headerRect = GUILayoutUtility.GetRect(
                new GUIContent(groupLabel), groupHeaderStyle, PoseBrowserScale.H(22f), GUILayout.ExpandWidth(true));
            string headerTip = "Apply all poses in this group to selected Studio characters (Chars priority list).";
            if (Event.current.type == EventType.Repaint && headerRect.Contains(Event.current.mousePosition))
                headerTip = BuildGroupApplyAssignmentTooltip(gid) ?? headerTip;

            GUI.enabled = canGroupApply;
            if (GUI.Button(headerRect, new GUIContent(groupLabel, headerTip), groupHeaderStyle))
            {
                SelectCompactGroup(gid);
                ApplyGroupMembersToSelectedCharacters(gid);
            }
            GUI.enabled = true;

            int end = block.Start + block.Count;
            for (int j = block.Start; j < end; j++)
                DrawCompactPoseRow(j);

            GUILayout.EndVertical();
        }

        private void TryCaptureCompactHoverFromTooltip()
        {
            _compactHoverIndex = -1;
            string tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip) || !tip.StartsWith(CompactHoverTooltipPrefix, StringComparison.Ordinal))
                return;

            string indexText = tip.Substring(CompactHoverTooltipPrefix.Length);
            if (!int.TryParse(indexText, out int index) || index < 0 || index >= _displayEntries.Count)
                return;

            _compactHoverIndex = index;
            _compactHoverRowScreenY = GUIUtility.GUIToScreenPoint(new Vector2(0f, Event.current.mousePosition.y)).y;
            GUI.tooltip = string.Empty;
        }

        private void DrawCompactPoseRow(int index)
        {
            var entry = _displayEntries[index];
            var item = entry.Item;
            bool rowOn = string.IsNullOrEmpty(_compactSelectedGroupId) && index == _compactPoseIndex;
            var rowStyle = rowOn ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            string label = (item.IsFavorite ? "★ " : "") + item.DisplayName;
            string hoverTip = CompactHoverTooltipPrefix + index;
            Color prev = GUI.color;
            if (entry.IsDimmed) GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            if (GUILayout.Button(new GUIContent(label, hoverTip), rowStyle, PoseBrowserScale.H(22f), GUILayout.ExpandWidth(true)))
            {
                SelectCompactPose(index);
                ApplyPoseToSelectedWithUsage(item);
            }
            GUI.color = prev;

            // Measure the real row pitch (height + inter-row margin) from two consecutive rows so
            // the virtualization spacers match exactly and the scroll position never drifts.
            if (Event.current.type == EventType.Repaint)
            {
                Rect rr = GUILayoutUtility.GetLastRect();
                if (_compactMeasureLastRowY >= 0f)
                {
                    float p = rr.y - _compactMeasureLastRowY;
                    if (p > 1f && p < 200f)
                        _compactRowPitch = p;
                }
                _compactMeasureLastRowY = rr.y;
            }
        }

        private void DrawCompactHoverThumbnail()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (_layoutTier != PoseBrowserLayoutTier.CompactList)
            {
                _compactHoverIndex = -1;
                return;
            }

            if (_compactHoverIndex < 0 || _compactHoverIndex >= _displayEntries.Count)
                return;

            Texture2D? tex = ResolveCompactHoverTexture(_compactHoverIndex);
            if (tex == null)
                return;

            float thumbW = _compactHoverThumbnailWidth;
            float aspect = tex.width > 0 ? (float)tex.height / tex.width : 1f;
            float thumbH = thumbW * aspect;

            float x = windowRect.xMax + DockedPaneGap;
            float y = _compactHoverRowScreenY;

            if (x + thumbW > Screen.width - 4f)
                x = windowRect.x - thumbW - DockedPaneGap;
            if (x < 4f) x = 4f;

            if (y + thumbH > Screen.height - 4f)
                y = Screen.height - 4f - thumbH;
            if (y < 4f) y = 4f;

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevGui = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, thumbW, thumbH), tex, ScaleMode.ScaleToFit, false);
            GUI.color = prevGui;
            GUI.matrix = prevMatrix;
        }

        private Texture2D? ResolveCompactHoverTexture(int index)
        {
            var item = _displayEntries[index].Item;

            if (_compactHoverTexIndex != index)
            {
                _compactHoverTexIndex = index;
                _compactHoverTex = null;
            }

            if (_compactHoverTex != null)
                return _compactHoverTex;

            Texture2D? tex = item.Thumbnail;
            if (tex == null && item.IsPng)
            {
                tex = _dataService.LoadThumbnailTexture(item);
                if (tex != null)
                    item.Thumbnail = tex;
            }

            _compactHoverTex = tex ?? _placeholderTex;
            return _compactHoverTex;
        }

        private void DrawCompactMiniWindowBody()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawCompactLayoutHeader();

            GUILayout.BeginHorizontal();
            GUI.enabled = BuildMiniBrowseTargets().Count > 0;
            if (GUILayout.Button("◀ Folder", PoseBrowserScale.H(26f)))
                AdvanceMiniBrowse(-1);
            if (GUILayout.Button("Folder ▶", PoseBrowserScale.H(26f)))
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
                if (GUILayout.Button("◀ Group", PoseBrowserScale.H(26f)))
                    AdvanceCompactGroup(-1);
                if (GUILayout.Button("Group ▶", PoseBrowserScale.H(26f)))
                    AdvanceCompactGroup(1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Label("▦ " + miniGroupName, _compactWordWrapStyle!, GUILayout.ExpandWidth(true));
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUI.enabled = CountDisplayPoses() > 0;
            if (GUILayout.Button("◀ Pose", PoseBrowserScale.H(26f)))
                AdvanceCompactPose(-1, applyToStudio: true);
            if (GUILayout.Button("Pose ▶", PoseBrowserScale.H(26f)))
                AdvanceCompactPose(1, applyToStudio: true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            string poseLine = CountDisplayPoses() == 0 || _compactPoseIndex < 0 || _compactPoseIndex >= _displayEntries.Count
                ? "—"
                : _displayEntries[_compactPoseIndex].Item.DisplayName;
            GUILayout.Label(poseLine, _compactWordWrapStyle!, GUILayout.ExpandWidth(true));

            GUILayout.FlexibleSpace();
            GUI.enabled = _filteredItems.Count > 0 && _compactPoseIndex >= 0 && _compactPoseIndex < _filteredItems.Count;
            if (GUILayout.Button("Reapply", PoseBrowserScale.H(28f)))
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

                GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
                DrawTreePanel(showFolderFooter: true);
                float gridAvailW = Mathf.Max(120f, windowRect.width - TreePanelWidth - GridPanelChromePad);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MaxWidth(gridAvailW));
                DrawGridPanel(gridAvailW);
                DrawBottomBar(gridAvailW);
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

        private static bool DrawSearchFieldWithClear(ref string text, float height, params GUILayoutOption[] textFieldOptions)
        {
            GUILayout.BeginHorizontal();
            var fieldOpts = new List<GUILayoutOption> { PoseBrowserScale.H(height) };
            if (textFieldOptions != null && textFieldOptions.Length > 0)
                fieldOpts.AddRange(textFieldOptions);
            string newText = GUILayout.TextField(text, fieldOpts.ToArray());
            bool changed = !string.Equals(newText, text, StringComparison.Ordinal);
            text = newText;
            GUI.enabled = text.Length > 0;
            if (GUILayout.Button(new GUIContent("✕", "Clear search"), PoseBrowserScale.W(24f), PoseBrowserScale.H(height)))
            {
                if (text.Length > 0)
                {
                    text = "";
                    changed = true;
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            return changed;
        }

        private void DrawPoseFilterBarRow()
        {
            GUILayout.BeginHorizontal(PoseBrowserScale.H(24f));
            GUILayout.Label("Search:", PoseBrowserScale.W(46f));
            if (DrawSearchFieldWithClear(ref _searchText, 22f, PoseBrowserScale.MinW(120f), GUILayout.ExpandWidth(true)))
                ApplyFilters();

            GUILayout.Space(6f);
            bool newRegex = GUILayout.Toggle(_searchUseRegex, ".*", PoseBrowserScale.W(36f));
            if (newRegex != _searchUseRegex)
            {
                _searchUseRegex = newRegex;
                ApplyFilters();
            }

            GUILayout.Space(6f);

            bool newFavOnly = GUILayout.Toggle(_showFavoritesOnly, "★", PoseBrowserScale.W(28f));
            if (newFavOnly != _showFavoritesOnly)
            {
                _showFavoritesOnly = newFavOnly;
                ApplyFilters();
            }

            GUILayout.Space(4f);

            bool tagFilterPanelOpen = _tagWindowPurpose == TagWindowPurpose.FilterLibrary;
            bool filterWindowActive = HasActiveFilterWindowSettings();
            string filterBtnTip = filterWindowActive
                ? FilterBarButtonTooltip()
                : "Tag filters and display options for the pose list.";
            if (GUILayout.Button(
                    new GUIContent(FilterBarButtonLabel(tagFilterPanelOpen), filterBtnTip),
                    PoseBrowserScale.W(68f),
                    PoseBrowserScale.H(22f)))
            {
                if (tagFilterPanelOpen)
                    CloseTagWindow();
                else
                    OpenTagFilterWindow();
            }

            if (GUILayout.Button(_showSortPane ? "Sort ▶" : "Sort", PoseBrowserScale.W(56f), PoseBrowserScale.H(22f)))
                _showSortPane = !_showSortPane;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_searchRegexError))
            {
                var c = GUI.color;
                GUI.color = new Color(1f, 0.5f, 0.45f);
                GUILayout.Label(_searchRegexError);
                GUI.color = c;
            }
        }

        private void DrawTopBar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(TopBarHeight));

            DrawWindowChromeButtons(24f);
            GUILayout.Space(4f);

            DrawTopBarCharacterSection(24f);

            GUILayout.Space(8f);

            if (GUILayout.Button("Save Pose", PoseBrowserScale.W(90f), PoseBrowserScale.H(24f)))
            {
                _showSavePopup = true;
                _savePoseName = "";
            }

            if (GUILayout.Button("Import…", PoseBrowserScale.W(70f), PoseBrowserScale.H(24f)))
                PromptImportPoseOrTreePack();

            GUILayout.Space(6f);
            DrawHistoryTopBarButtons();

            GUILayout.FlexibleSpace();

            DrawTopBarVerticalRule(22f);

            if (GUILayout.Button(new GUIContent($"View ({LayoutTierShortLabel()})", "Cycle: Full → compact list → mini"), PoseBrowserScale.W(110f), PoseBrowserScale.H(24f)))
                CycleLayoutTier();

#if HS2
            if (GUILayout.Button("Heelz", PoseBrowserScale.W(52f), PoseBrowserScale.H(24f)))
                ToggleHeelzControlWindow();
#endif

            if (GUILayout.Button(_showHelpPane ? "Help ▶" : "Help", PoseBrowserScale.W(56f), PoseBrowserScale.H(24f)))
                _showHelpPane = !_showHelpPane;

            if (GUILayout.Button(_showOptionsPane ? "Options ▶" : "Options", PoseBrowserScale.W(78f), PoseBrowserScale.H(24f)))
                _showOptionsPane = !_showOptionsPane;

            GUILayout.EndHorizontal();

            if (_showSavePopup)
                DrawSavePopup();
        }

        private static void DrawTopBarVerticalRule(float height)
        {
            GUILayout.Space(4f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.28f);
            var r = GUILayoutUtility.GetRect(1f, PoseBrowserScale.Px(height), PoseBrowserScale.W(1f), PoseBrowserScale.H(height));
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUILayout.Space(8f);
        }

        private void DrawTopBarCharacterSection(float controlHeight, bool compact = false)
        {
            var names = GetCachedStudioCharacterDisplayNames();

            bool charPaneOpen = _showCharacterConfigPane;
            if (GUILayout.Button(charPaneOpen ? "Chars ▶" : "Chars", PoseBrowserScale.W(52f), PoseBrowserScale.H(controlHeight)))
            {
                _showCharacterConfigPane = !charPaneOpen;
                if (_showCharacterConfigPane)
                    _characterConfig.ReloadFromDisk();
            }

            var style = _characterHintStyle!;
            // MinWidth(0) keeps the (content-sized) character label from forcing the top bar — and thus the
            // window min-width — wider when a long character name is selected; it shrinks and clips instead.
            GUILayoutOption[] labelOpts = compact
                ? new[] { PoseBrowserScale.MaxW(130f), GUILayout.MinWidth(0f) }
                : new[] { GUILayout.ExpandWidth(true), GUILayout.MinWidth(0f) };
            if (names.Count == 0)
            {
                GUILayout.Label(
                    new GUIContent(
                        "Character: none",
                        "Select one or more characters in Studio. Extra selected items (props, accessories, etc.) are ignored."),
                    style,
                    labelOpts);
            }
            else if (names.Count == 1)
            {
                GUILayout.Label(new GUIContent($"Character: {names[0]}", names[0]), style, labelOpts);
            }
            else
            {
                GUILayout.Label(
                    new GUIContent($"Character: {names.Count} selected", string.Join("\n", names.ToArray())),
                    style,
                    labelOpts);
            }

            if (compact)
                DrawStashPaneToggleButton(controlHeight, 52f);

            if (!compact)
                DrawPoseBrowserUpdateNotice(controlHeight);
        }

        private void DrawPoseBrowserUpdateNotice(float controlHeight = 24f)
        {
            if (_poseBrowserUpdateCheck.State != PoseBrowserUpdateCheck.Status.UpdateAvailable)
                return;

            string remote = _poseBrowserUpdateCheck.RemoteVersion ?? "?";
            string url = _poseBrowserUpdateCheck.DownloadUrl ?? PoseBrowserVersionInfo.LatestReleasePageUrl;
            bool directDll = url.IndexOf(".dll", StringComparison.OrdinalIgnoreCase) >= 0;
            string tip = directDll
                ? $"Pose Browser v{remote} is available.\nClick to download the DLL.\n{url}"
                : $"Pose Browser v{remote} is available.\nClick to open the latest GitHub release page.\n{url}";

            var label = new GUIContent($"Update v{remote}", tip);
            if (GUILayout.Button(label, GUI.skin.button, PoseBrowserScale.H(controlHeight), PoseBrowserScale.MinW(108f)))
                Application.OpenURL(url);
        }

        private void DrawTagWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal(PoseBrowserScale.H(24f));
            GUILayout.Label("Search tags", PoseBrowserScale.W(82f), PoseBrowserScale.H(22f));
            DrawSearchFieldWithClear(ref _tagWindowSearch, 22f, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);

            string searchNormFold = _tagWindowSearch.Trim();

            if (_tagWindowPurpose == TagWindowPurpose.FilterLibrary)
                DrawTagWindowFilterBody(searchNormFold);
            else if (_tagWindowPurpose == TagWindowPurpose.EditSelection)
            {
                SyncTagAssignWindowToSelection();
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
            if (GUILayout.Button("Close", PoseBrowserScale.H(26f)))
                CloseTagWindow();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawTagFilterIncludeModeRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                new GUIContent("Include", "How multiple + include tags combine: AND = every tag required, OR = any one tag."),
                PoseBrowserScale.W(52f),
                PoseBrowserScale.H(24f));
            string modeHint = _tagFilterAndMode
                ? "Every + tag must match"
                : "Any + tag may match";
            if (GUILayout.Button(new GUIContent(_tagFilterAndMode ? "AND" : "OR", modeHint), PoseBrowserScale.W(48f), PoseBrowserScale.H(24f)))
            {
                _tagFilterAndMode = !_tagFilterAndMode;
                ApplyFilters();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    new GUIContent("ⓘ", TagFilterTipsTooltip),
                    GUI.skin.label,
                    PoseBrowserScale.W(18f),
                    PoseBrowserScale.H(24f)))
            {
                // Display-only; tooltip is shown on hover.
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private bool HasSaveableFilterState() =>
            !StringEx.IsNullOrWhiteSpace(_searchText)
            || _tagFiltersInclude.Count > 0
            || _tagFiltersExclude.Count > 0;

        private PoseBrowserFilterPreset CaptureCurrentFilterPreset(string name) =>
            new PoseBrowserFilterPreset
            {
                name = name,
                searchText = _searchText ?? "",
                searchUseRegex = _searchUseRegex,
                tagFilterAndMode = _tagFilterAndMode,
                tagFilterGroupsMode = (int)_tagFilterGroupsMode,
                tagFilterThumbnailMode = (int)_tagFilterThumbnailMode,
                includeTags = _tagFiltersInclude.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray(),
                excludeTags = _tagFiltersExclude.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray()
            };

        private bool TryGetActiveFilterPreset(out PoseBrowserFilterPreset? preset)
        {
            preset = null;
            if (string.IsNullOrEmpty(_activeFilterPresetName))
                return false;
            for (int i = 0; i < _filterPresets.Count; i++)
            {
                if (string.Equals(_filterPresets[i].name, _activeFilterPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    preset = _filterPresets[i];
                    return true;
                }
            }

            _activeFilterPresetName = null;
            return false;
        }

        private bool IsActiveFilterPresetDirty()
        {
            if (!TryGetActiveFilterPreset(out var preset) || preset == null)
                return false;
            return !preset.MatchesState(
                _searchText ?? "",
                _searchUseRegex,
                _tagFilterAndMode,
                (int)_tagFilterGroupsMode,
                (int)_tagFilterThumbnailMode,
                _tagFiltersInclude,
                _tagFiltersExclude);
        }

        private bool SelectedFilterPresetIsActive()
        {
            if (_selectedFilterPresetIndex < 0 || _selectedFilterPresetIndex >= _filterPresets.Count)
                return false;
            return string.Equals(
                _filterPresets[_selectedFilterPresetIndex].name,
                _activeFilterPresetName,
                StringComparison.OrdinalIgnoreCase);
        }

        private void LoadFilterPresets()
        {
            _filterPresets.Clear();
            if (PoseBrowserFilterPresets.TryLoad(FilterPresetsPath, out var loaded))
                _filterPresets.AddRange(loaded);
            _selectedFilterPresetIndex = -1;
            _activeFilterPresetName = null;
        }

        private void PersistFilterPresets() =>
            PoseBrowserFilterPresets.Save(FilterPresetsPath, _filterPresets);

        private void ApplyFilterPreset(PoseBrowserFilterPreset preset)
        {
            _searchText = preset.searchText ?? "";
            _searchUseRegex = preset.searchUseRegex;
            _tagFilterAndMode = preset.tagFilterAndMode;
            _tagFilterGroupsMode = ClampDisplayFilterMode(preset.tagFilterGroupsMode);
            _tagFilterThumbnailMode = ClampDisplayFilterMode(preset.tagFilterThumbnailMode);
            _tagFiltersInclude.Clear();
            _tagFiltersExclude.Clear();
            if (preset.includeTags != null)
            {
                foreach (var t in preset.includeTags)
                {
                    if (!StringEx.IsNullOrWhiteSpace(t))
                        _tagFiltersInclude.Add(t);
                }
            }

            if (preset.excludeTags != null)
            {
                foreach (var t in preset.excludeTags)
                {
                    if (!StringEx.IsNullOrWhiteSpace(t))
                        _tagFiltersExclude.Add(t);
                }
            }

            _activeFilterPresetName = preset.name;
            ApplyFilters();
        }

        private void TrySaveFilterPreset()
        {
            string name = (_filterPresetSaveName ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                return;

            var snapshot = CaptureCurrentFilterPreset(name);
            int existing = -1;
            for (int i = 0; i < _filterPresets.Count; i++)
            {
                if (string.Equals(_filterPresets[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    existing = i;
                    break;
                }
            }

            if (existing >= 0)
                _filterPresets[existing] = snapshot;
            else
                _filterPresets.Add(snapshot);

            _activeFilterPresetName = name;
            _selectedFilterPresetIndex = existing >= 0 ? existing : _filterPresets.Count - 1;
            _filterPresetSaveName = "";
            PersistFilterPresets();
        }

        private void UpdateActiveFilterPreset()
        {
            if (!TryGetActiveFilterPreset(out var preset) || preset == null)
                return;

            string name = preset.name;
            var snapshot = CaptureCurrentFilterPreset(name);
            int idx = _filterPresets.FindIndex(p =>
                string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                return;

            _filterPresets[idx] = snapshot;
            PersistFilterPresets();
        }

        private void DeleteSelectedFilterPreset()
        {
            if (_selectedFilterPresetIndex < 0 || _selectedFilterPresetIndex >= _filterPresets.Count)
                return;

            string removedName = _filterPresets[_selectedFilterPresetIndex].name;
            _filterPresets.RemoveAt(_selectedFilterPresetIndex);
            if (string.Equals(_activeFilterPresetName, removedName, StringComparison.OrdinalIgnoreCase))
                _activeFilterPresetName = null;
            _selectedFilterPresetIndex = -1;
            PersistFilterPresets();
        }

        private static void DrawTagFilterSeparator()
        {
            GUILayout.Space(6f);
            Rect lineRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                PoseBrowserScale.H(1f));
            if (Event.current.type == EventType.Repaint)
            {
                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.22f);
                GUI.DrawTexture(lineRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = prev;
            }

            GUILayout.Space(6f);
        }

        private void DrawFilterPresetsSection()
        {
            if (HasSaveableFilterState())
            {
                GUILayout.BeginHorizontal();
                _filterPresetSaveName = GUILayout.TextField(
                    _filterPresetSaveName,
                    PoseBrowserScale.MinW(80f),
                    GUILayout.ExpandWidth(true));
                if (GUILayout.Button(new GUIContent("Save", "Save current search and tag filters under the name above."), PoseBrowserScale.W(52f)))
                    TrySaveFilterPreset();
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }

            if (_filterPresets.Count == 0)
                return;

            GUILayout.Label(new GUIContent("Saved filters", "Click a name to apply that search and tag filter setup."));

            for (int i = 0; i < _filterPresets.Count; i++)
            {
                var preset = _filterPresets[i];
                bool isSelected = _selectedFilterPresetIndex == i;
                bool isActive = string.Equals(
                    _activeFilterPresetName,
                    preset.name,
                    StringComparison.OrdinalIgnoreCase);
                Color prev = GUI.color;
                if (isActive)
                    GUI.color = new Color(0.65f, 0.85f, 1f, 1f);
                else if (isSelected)
                    GUI.color = new Color(0.85f, 0.85f, 0.85f, 1f);

                if (GUILayout.Button(preset.name, PoseBrowserScale.H(22f)))
                {
                    _selectedFilterPresetIndex = i;
                    ApplyFilterPreset(preset);
                }

                GUI.color = prev;
            }

            if (_selectedFilterPresetIndex >= 0)
            {
                GUILayout.Space(2f);
                GUILayout.BeginHorizontal();
                if (IsActiveFilterPresetDirty()
                    && SelectedFilterPresetIsActive()
                    && GUILayout.Button(
                        new GUIContent("Update", "Overwrite the active saved filter with the current search and tag settings."),
                        PoseBrowserScale.W(64f)))
                {
                    UpdateActiveFilterPreset();
                }

                if (GUILayout.Button(
                        new GUIContent("Delete", "Remove the selected saved filter."),
                        PoseBrowserScale.W(56f)))
                {
                    DeleteSelectedFilterPreset();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private bool HasActiveFilterWindowSettings() =>
            _tagFiltersInclude.Count > 0
            || _tagFiltersExclude.Count > 0
            || _tagFilterGroupsMode != PoseDisplayFilterMode.Off
            || _tagFilterThumbnailMode != PoseDisplayFilterMode.Off;

        private const string FilterActiveIcon = "●";

        private string FilterBarButtonLabel(bool panelOpen)
        {
            bool active = HasActiveFilterWindowSettings();
            if (panelOpen)
                return active ? $"Filter {FilterActiveIcon} ▶" : "Filter ▶";
            return active ? $"Filter {FilterActiveIcon}" : "Filter";
        }

        private string FilterBarButtonTooltip()
        {
            var lines = new List<string> { "Active filter window settings:" };
            if (_tagFiltersInclude.Count > 0)
                lines.Add("Include: " + string.Join(", ", _tagFiltersInclude.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray()));
            if (_tagFiltersExclude.Count > 0)
                lines.Add("Exclude: " + string.Join(", ", _tagFiltersExclude.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray()));
            if (_tagFilterGroupsMode != PoseDisplayFilterMode.Off)
                lines.Add(DisplayFilterModeSummary("Grouped poses", _tagFilterGroupsMode));
            if (_tagFilterThumbnailMode != PoseDisplayFilterMode.Off)
                lines.Add(DisplayFilterModeSummary("Thumbnails", _tagFilterThumbnailMode));
            if (_tagFiltersInclude.Count > 0 || _tagFiltersExclude.Count > 0)
                lines.Add("Include match: " + (_tagFilterAndMode ? "AND" : "OR"));
            return string.Join("\n", lines.ToArray());
        }

        private enum TagFilterRole { Neutral, Include, Exclude }

        private static PoseDisplayFilterMode ClampDisplayFilterMode(int mode) =>
            mode switch
            {
                (int)PoseDisplayFilterMode.Exclude => PoseDisplayFilterMode.Exclude,
                (int)PoseDisplayFilterMode.IncludeOnly => PoseDisplayFilterMode.IncludeOnly,
                _ => PoseDisplayFilterMode.Off
            };

        private static void CycleDisplayFilterMode(ref PoseDisplayFilterMode mode) =>
            mode = (PoseDisplayFilterMode)(((int)mode + 1) % 3);

        private static string DisplayFilterModeSummary(string label, PoseDisplayFilterMode mode) =>
            mode switch
            {
                PoseDisplayFilterMode.Exclude => $"{label}: hide",
                PoseDisplayFilterMode.IncludeOnly => $"{label}: only",
                _ => label
            };

        private void DrawDisplayFilterModeButton(
            string neutralLabel,
            string excludeLabel,
            string includeLabel,
            string neutralTip,
            string excludeTip,
            string includeTip,
            ref PoseDisplayFilterMode mode)
        {
            string label = mode switch
            {
                PoseDisplayFilterMode.Exclude => excludeLabel,
                PoseDisplayFilterMode.IncludeOnly => includeLabel,
                _ => neutralLabel
            };
            string tip = mode switch
            {
                PoseDisplayFilterMode.Exclude => excludeTip,
                PoseDisplayFilterMode.IncludeOnly => includeTip,
                _ => neutralTip
            };

            Color prev = GUI.color;
            if (mode == PoseDisplayFilterMode.IncludeOnly)
                GUI.color = new Color(0.55f, 1f, 0.65f, 1f);
            else if (mode == PoseDisplayFilterMode.Exclude)
                GUI.color = new Color(1f, 0.55f, 0.5f, 1f);

            var prevMode = mode;
            if (GUILayout.Button(new GUIContent(label, tip + " Click to cycle."), PoseBrowserScale.H(24f)))
                CycleDisplayFilterMode(ref mode);

            GUI.color = prev;
            if (prevMode != mode)
            {
                ApplyFilters();
                SavePersistedOptions();
            }
        }

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

            DrawDisplayFilterModeButton(
                "Grouped poses",
                "− Hide grouped poses",
                "+ Only grouped poses",
                "Show all poses regardless of group membership.",
                "Omit poses that belong to a group (ungrouped only).",
                "Show only poses that belong to a group.",
                ref _tagFilterGroupsMode);

            DrawDisplayFilterModeButton(
                "Thumbnails",
                "− Hide without thumbnail",
                "+ Only without thumbnail",
                "Show all poses (.png and .dat).",
                "Omit .dat-only poses (no .png preview file).",
                "Show only .dat-only poses (no .png preview file).",
                ref _tagFilterThumbnailMode);

            bool hasFilterConfigUi = HasSaveableFilterState() || _filterPresets.Count > 0;
            if (hasFilterConfigUi)
            {
                DrawTagFilterSeparator();
                _filterPresetConfigScroll = GUILayout.BeginScrollView(
                    _filterPresetConfigScroll,
                    PoseBrowserScale.MaxH(160f),
                    PoseBrowserScale.MinH(52f));
                DrawFilterPresetsSection();
                GUILayout.EndScrollView();
            }

            DrawTagFilterSeparator();
            GUILayout.Label(new GUIContent("Tags:", "Click a tag to cycle neutral → include (+) → exclude (−)."));

            var allTags = GetAllLibraryTagNames();
            if (allTags.Count == 0)
            {
                GUILayout.Label("No tags defined yet. Add tags on poses or groups.");
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

            GUILayout.Space(2f);
            _tagWindowScroll = GUILayout.BeginScrollView(_tagWindowScroll, GUILayout.ExpandHeight(true));
            foreach (var tag in visible)
            {
                var role = GetTagFilterRole(tag);
                Color prev = GUI.color;
                if (role == TagFilterRole.Include)
                    GUI.color = new Color(0.55f, 1f, 0.65f, 1f);
                else if (role == TagFilterRole.Exclude)
                    GUI.color = new Color(1f, 0.55f, 0.5f, 1f);

                if (GUILayout.Button(TagFilterRoleButtonLabel(tag, role), PoseBrowserScale.H(22f)))
                {
                    CycleTagFilterRole(tag);
                    ApplyFilters();
                }

                GUI.color = prev;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("Clear active filters", PoseBrowserScale.H(24f)))
            {
                _tagFiltersInclude.Clear();
                _tagFiltersExclude.Clear();
                _tagFilterGroupsMode = PoseDisplayFilterMode.Off;
                _tagFilterThumbnailMode = PoseDisplayFilterMode.Off;
                _activeFilterPresetName = null;
                ApplyFilters();
                SavePersistedOptions();
            }
        }

        private enum TagCoverage { None, Some, All }

        private static TagCoverage GetTagCoverage(IList<PoseGridItem> selected, string tag)
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

        private void ApplyTagToAllSelected(IList<PoseGridItem> selected, string tag, bool add)
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

        /// <summary>Every tag used on any pose (tag DB), any pose group, or Heelz Control rules — shared by all tag windows.</summary>
        internal List<string> GetAllLibraryTagNames()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in _tagDb.GetAllKnownTags())
                set.Add(t);
            foreach (var group in _groupDb.GetAllGroups())
            {
                foreach (var t in group.Tags)
                    set.Add(t);
            }
#if HS2
            foreach (var t in HeelzControlService.GetAllRuleTags())
                set.Add(t);
#endif

            return set.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void DrawTagWindowAssignBody(List<PoseGridItem> selected, string searchNormFold)
        {
            GUILayout.Label($"{selected.Count} pose(s) selected.", PoseBrowserScale.H(20f));

            if (!string.IsNullOrEmpty(searchNormFold))
            {
                bool alreadyKnown = GetAllLibraryTagNames().Any(t =>
                    string.Equals(t, searchNormFold, StringComparison.OrdinalIgnoreCase));

                if (!alreadyKnown &&
                    GUILayout.Button($"Add new tag \"{searchNormFold}\" to all selected", PoseBrowserScale.H(26f)))
                {
                    foreach (var it in selected)
                        _tagDb.AddTags(it, new[] { searchNormFold });
                    NotifyLibraryCacheFavoriteChanged(selected);
                    ApplyFilters();
                }
            }

            var union = GetAllLibraryTagNames();
            var visible = string.IsNullOrEmpty(searchNormFold)
                ? union
                : union.Where(t => t.IndexOf(searchNormFold, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            GUILayout.Space(4f);
            GUILayout.Label("Click to set for all selected: ✓ = on all, ☐ = off all, ◪ = mixed (click adds on all).", PoseBrowserScale.H(36f));

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
                            PoseBrowserScale.H(24f)))
                        ApplyTagToAllSelected(selected, tag, add: true);
                    GUI.color = gc;
                }
                else
                {
                    bool on = cov == TagCoverage.All;
                    bool nv = GUILayout.Toggle(on, tag, PoseBrowserScale.H(22f));
                    if (nv != on)
                        ApplyTagToAllSelected(selected, tag, add: nv);
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawSortWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("Click a row to choose the primary sort. Click the same row again to flip ↑ / ↓.", PoseBrowserScale.H(36f));

            void Row(PoseSortMode mode, string label)
            {
                bool on = _poseSortMode == mode;
                string arrow = on ? (_sortAscending ? "↑" : "↓") : "";
                string suffix = on ? $"  {arrow}" : "";
                var st = on ? _treeNodeSelectedStyle! : _treeNodeStyle!;
                if (GUILayout.Button($"{label}{suffix}", st, PoseBrowserScale.H(26f)))
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
            if (GUILayout.Button("Close sort", PoseBrowserScale.H(26f)))
                _showSortPane = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawSavePopup()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Save current character pose:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", PoseBrowserScale.W(45f));
            _savePoseName = GUILayout.TextField(_savePoseName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", PoseBrowserScale.H(24f)))
            {
                DoSavePose();
                _showSavePopup = false;
            }
            if (GUILayout.Button("Cancel", PoseBrowserScale.H(24f)))
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
            if (GUILayout.Button("↻", PoseBrowserScale.W(24f), PoseBrowserScale.H(18f)))
            {
                _folderTree.Refresh();
                NotifyLibraryCacheStructureChanged();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            _treeScroll = GUILayout.BeginScrollView(
                _treeScroll,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            bool allViewSelected = !IsPickingFolderDestination && _viewAllPosesRecursive && !_browseFavoritesOnly;
            var allStyle = allViewSelected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            GUI.enabled = !IsPickingFolderDestination;
            if (GUILayout.Button("All poses", allStyle, PoseBrowserScale.H(22f)))
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
            if (GUILayout.Button("★ Favorites", favStyle, PoseBrowserScale.H(22f)))
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
            if (GUILayout.Button($"📁 Root only", rootOnlyStyle, PoseBrowserScale.H(22f)))
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
                    if (GUILayout.Button(arrow, PoseBrowserScale.W(20f), PoseBrowserScale.H(20f)))
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
                float treeLabelW = TreeNodeLabelMaxWidth(node.Depth);
                if (node.CachedTruncatedName == null || node.CachedTruncatedWidth != treeLabelW)
                {
                    node.CachedTruncatedName = TruncateWithEllipsis(node.Name, style, treeLabelW);
                    node.CachedTruncatedWidth = treeLabelW;
                }
                string shownName = node.CachedTruncatedName;
                if (GUILayout.Button(new GUIContent(shownName, node.Name), style, PoseBrowserScale.H(20f), GUILayout.ExpandWidth(true)))
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
                if (GUILayout.Button("Apply", PoseBrowserScale.H(24f)))
                    ApplyPendingFolderOperation();
                if (GUILayout.Button("Cancel", PoseBrowserScale.H(24f)))
                    CancelPendingFolderOperation();
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }

            GUILayout.Label(isLibraryRootScope ? "Library root" : "Selected folder", PoseBrowserScale.MinH(20f));
            if (isLibraryRootScope)
                GUILayout.Label("New folders go here.", PoseBrowserScale.MinH(21f));

            if (!string.IsNullOrEmpty(_folderActionError))
            {
                var c = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.4f);
                GUILayout.Label(_folderActionError, PoseBrowserScale.MaxH(40f));
                GUI.color = c;
            }

            if (!isLibraryRootScope)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Rename…", PoseBrowserScale.H(22f)))
                {
                    _renameFolderTarget = node!;
                    _renameFolderText = node!.Name;
                    _showRenameFolderPopup = true;
                    _showDeleteFolderConfirm = false;
                    _folderActionError = "";
                }
                if (GUILayout.Button("New folder…", PoseBrowserScale.H(22f)))
                {
                    _showNewChildFolderPopup = true;
                    _newChildFolderName = "";
                    _showDeleteFolderConfirm = false;
                    _folderActionError = "";
                }
                GUILayout.EndHorizontal();

                GUI.enabled = empty;
                if (GUILayout.Button("Delete folder…", PoseBrowserScale.H(22f)))
                {
                    _showDeleteFolderConfirm = true;
                    _folderActionError = "";
                }
                GUI.enabled = true;

                if (!empty && !_showDeleteFolderConfirm)
                    GUILayout.Label("(Delete: empty only)", PoseBrowserScale.MinH(21f));

                if (!IsPickingFolderDestination && _layoutTier == PoseBrowserLayoutTier.Normal && node != null)
                {
                    GUILayout.Space(4f);
                    if (GUILayout.Button("Export branch…", PoseBrowserScale.H(22f)))
                        ExportFolderBranchToDisk(node);
                }
            }
            else
            {
                if (GUILayout.Button("New folder…", PoseBrowserScale.H(22f)))
                {
                    _showNewChildFolderPopup = true;
                    _newChildFolderName = "";
                    _showDeleteFolderConfirm = false;
                    _folderActionError = "";
                }

                if (!IsPickingFolderDestination && _layoutTier == PoseBrowserLayoutTier.Normal)
                {
                    GUILayout.Space(4f);
                    if (GUILayout.Button("Export library tree…", PoseBrowserScale.H(22f)))
                        ExportLibraryRootTreeToDisk();
                }
            }

            if (_showNewChildFolderPopup)
            {
                GUILayout.Label("New subfolder name:");
                _newChildFolderName = GUILayout.TextField(_newChildFolderName);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create", PoseBrowserScale.H(22f)))
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
                if (GUILayout.Button("Cancel", PoseBrowserScale.H(22f)))
                {
                    _showNewChildFolderPopup = false;
                    _folderActionError = "";
                }
                GUILayout.EndHorizontal();
            }

            if (!isLibraryRootScope && _showDeleteFolderConfirm && empty && node != null)
            {
                GUILayout.Label($"Delete empty folder \"{node.Name}\"?", PoseBrowserScale.H(18f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm delete", PoseBrowserScale.H(24f)))
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
                if (GUILayout.Button("Cancel", PoseBrowserScale.H(24f)))
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
        private static float CardHorizontalMarginBudget(GUIStyle style)
        {
            int m = style.margin.left + style.margin.right;
            int b = style.border.left + style.border.right;
            // Boxed cells consume more width than GUILayout.Width (margins, borders; some skins under-report).
            return Mathf.Max(8f, m + b + 6f);
        }

        private float PoseCardHorizontalMarginBudget()
        {
            InitStyles();
            return CardHorizontalMarginBudget(_poseCardBaseStyle!);
        }

        private void ComputeGridCellLayout(
            float contentWidth,
            float marginH,
            out int columns,
            out float columnFootprintW,
            out float cellInnerW)
        {
            contentWidth = Mathf.Max(80f, contentWidth);

            // Card metrics live in scaled space: the slider value (raw px) and the Min/MaxCardSize clamp
            // range are all multiplied by the UI scale so column count stays consistent with the bigger cards.
            float scale = PoseBrowserScale.Factor;
            float minCard = MinCardSize * scale;
            float maxCard = MaxCardSize * scale;
            float target = Mathf.Clamp(_cardCellSize * GridCardSizeBoost * scale, minCard, maxCard);

            // Closed-form: the slider acts as a *minimum* inner width per column, so the largest column count
            //   inner = contentWidth/columns - marginH >= target  ⇒  columns <= contentWidth / (target + marginH)
            // The small tolerance keeps a column that *just* fits from being dropped by float rounding.
            columns = Mathf.Max(1, Mathf.FloorToInt((contentWidth + 0.5f) / (target + marginH)));

            columnFootprintW = contentWidth / columns;
            cellInnerW = Mathf.Clamp(columnFootprintW - marginH, minCard, maxCard);

            // Single bounded reconcile: if clamping/rounding pushed the row past the available width, drop a column.
            const float slack = 0.5f;
            while (columns > 1 && columns * (cellInnerW + marginH) > contentWidth + slack)
            {
                columns--;
                columnFootprintW = contentWidth / columns;
                cellInnerW = Mathf.Clamp(columnFootprintW - marginH, minCard, maxCard);
            }
        }

        private static float VerticalScrollbarWidth()
        {
            float vsb = GUI.skin.verticalScrollbar != null ? GUI.skin.verticalScrollbar.fixedWidth : 15f;
            return vsb < 10f ? 18f : vsb;
        }

        private void UpdatePoseGridLayout(float gridAvailW)
        {
            float vsb = VerticalScrollbarWidth();
            float marginH = PoseCardHorizontalMarginBudget();
            float contentWidth = Mathf.Max(80f, gridAvailW - vsb);
            ComputeGridCellLayout(contentWidth, marginH, out int columns, out float columnFootprintW, out float cellInnerW);
            float layoutWidth = Mathf.Max(80f, contentWidth - Mathf.Max(0, columns - 1) * GridCellGap);
            if (layoutWidth + 0.5f < contentWidth)
                ComputeGridCellLayout(layoutWidth, marginH, out columns, out columnFootprintW, out cellInnerW);
            _poseGridLayout = new PoseGridLayoutMetrics
            {
                Frame = Time.frameCount,
                GridAvailW = gridAvailW,
                ContentWidth = contentWidth,
                Columns = columns,
                ColumnFootprintW = columnFootprintW,
                CellInnerW = cellInnerW,
                UniformPoseCardOuterH = _poseGridLayout.UniformPoseCardOuterH,
                UniformTagBlockH = _poseGridLayout.UniformTagBlockH,
                UniformGroupTagBlockH = _poseGridLayout.UniformGroupTagBlockH
            };
        }

        private static float PoseCardVerticalChrome(GUIStyle cardStyle) =>
            cardStyle.padding.top + cardStyle.padding.bottom;

        private float MeasurePoseCardOuterHeight(
            GUIStyle cardStyle,
            float thumbInnerW,
            float tagBlockH)
        {
            return PoseCardVerticalChrome(cardStyle) + thumbInnerW + PoseCardNameRowH +
                   (tagBlockH > 0f ? tagBlockH : 0f);
        }

        private float MeasureGroupSegmentOuterHeight(
            PoseBrowserGroupSegment segment,
            float cellInnerW,
            float uniformPoseCardOuterH,
            float uniformGroupTagBlockH,
            GUIStyle groupCardStyle)
        {
            float h = PoseCardVerticalChrome(groupCardStyle);
            if (segment.ShowHeader)
            {
                h += 22f;
                if (segment.ShowTags && uniformGroupTagBlockH > 0f)
                    h += uniformGroupTagBlockH + 2f;
            }

            h += uniformPoseCardOuterH;
            return h;
        }

        private void UpdateMaxTagBlockHeight(ref float maxTagH, PoseGridItem item, float width)
        {
            if (item.Tags == null || item.Tags.Count == 0)
                return;

            if (item.CachedTagBlockTagCount == item.Tags.Count &&
                Mathf.Approximately(item.CachedTagBlockWidth, width))
            {
                maxTagH = Mathf.Max(maxTagH, item.CachedTagBlockHeight);
                return;
            }

            string tagStr = item.GetOrBuildTagString();
            float h = MeasureTagBlockHeight(tagStr, _tagWrapStyle!, width);
            item.CachedTagBlockHeight = h;
            item.CachedTagBlockWidth = width;
            item.CachedTagBlockTagCount = item.Tags.Count;
            maxTagH = Mathf.Max(maxTagH, h);
        }

        private void UpdateMaxTagBlockHeight(ref float maxTagH, IEnumerable<string> tags, float width)
        {
            if (tags == null || !tags.Any())
                return;
            string tagStr = string.Join(" · ", tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray());
            maxTagH = Mathf.Max(maxTagH, MeasureTagBlockHeight(tagStr, _tagWrapStyle!, width));
        }

        private void ComputeUniformGridRowMetrics(
            IList<PoseBrowserGridRow> gridRows,
            float cellInnerW)
        {
            InitStyles();
            InitGroupStyles();

            float innerW = Mathf.Max(40f, cellInnerW);
            float tagTextW = Mathf.Max(20f, innerW - PoseCardTextPadH * 2f);
            float maxPoseTagH = 0f;
            float maxGroupTagH = 0f;

            foreach (var gridRow in gridRows)
            {
                foreach (var gridCell in gridRow.Cells)
                {
                    if (gridCell.Kind == PoseBrowserGridCellKind.Pose)
                    {
                        UpdateMaxTagBlockHeight(ref maxPoseTagH, gridCell.Pose!.Item, tagTextW);
                        continue;
                    }

                    var segment = gridCell.GroupSegment!;
                    float segInnerW = Mathf.Max(40f,
                        segment.Poses.Count * cellInnerW + Mathf.Max(0, segment.Poses.Count - 1) * 4f);
                    if (segment.ShowTags)
                        UpdateMaxTagBlockHeight(ref maxGroupTagH, segment.GroupTags, segInnerW);
                    foreach (var pose in segment.Poses)
                        UpdateMaxTagBlockHeight(ref maxPoseTagH, pose.Item, tagTextW);
                }
            }

            float uniformTagBlockH = maxPoseTagH;
            float uniformGroupTagBlockH = maxGroupTagH;
            float uniformPoseCardOuterH = Mathf.Max(
                MeasurePoseCardOuterHeight(_poseCardBaseStyle!, innerW, uniformTagBlockH),
                MeasurePoseCardOuterHeight(_groupInnerCardStyle!, innerW, uniformTagBlockH));

            _gridRowOuterHeights.Clear();
            foreach (var gridRow in gridRows)
            {
                float rowH = uniformPoseCardOuterH;
                foreach (var gridCell in gridRow.Cells)
                {
                    if (gridCell.Kind != PoseBrowserGridCellKind.GroupSegment)
                        continue;
                    rowH = Mathf.Max(
                        rowH,
                        MeasureGroupSegmentOuterHeight(
                            gridCell.GroupSegment!,
                            cellInnerW,
                            uniformPoseCardOuterH,
                            uniformGroupTagBlockH,
                            _groupCardStyle!));
                }

                _gridRowOuterHeights.Add(rowH);
            }

            _poseGridLayout.UniformPoseCardOuterH = uniformPoseCardOuterH;
            _poseGridLayout.UniformTagBlockH = uniformTagBlockH;
            _poseGridLayout.UniformGroupTagBlockH = uniformGroupTagBlockH;
        }

        private float MeasureGridRowContentWidth(PoseBrowserGridRow row, float cellInnerW)
        {
            InitGroupStyles();
            const float innerCardGap = 4f;
            int groupHPad = _groupCardStyle!.padding.left + _groupCardStyle.padding.right;
            float w = 0f;
            int cellIdx = 0;
            foreach (var gridCell in row.Cells)
            {
                if (cellIdx++ > 0)
                    w += GridCellGap;

                if (gridCell.Kind == PoseBrowserGridCellKind.GroupSegment)
                {
                    int poseCount = gridCell.GroupSegment!.Poses.Count;
                    w += poseCount * cellInnerW + Mathf.Max(0, poseCount - 1) * innerCardGap + groupHPad;
                }
                else
                    w += cellInnerW;
            }

            return w;
        }

        /// <summary>If rows are wider than the scroll area, add columns (smaller cards) until they fit.</summary>
        private void RefineGridLayoutForRows(IList<PoseBrowserGridRow> gridRows)
        {
            if (gridRows.Count == 0)
                return;

            float contentWidth = _poseGridLayout.ContentWidth;
            float marginH = PoseCardHorizontalMarginBudget();
            int columns = _poseGridLayout.Columns;
            float columnFootprintW = _poseGridLayout.ColumnFootprintW;
            float cellInnerW = _poseGridLayout.CellInnerW;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float maxRowW = 0f;
                foreach (var row in gridRows)
                    maxRowW = Mathf.Max(maxRowW, MeasureGridRowContentWidth(row, cellInnerW));

                if (maxRowW <= contentWidth + 1f)
                    break;

                int nextCols = columns + 1;
                float avail = contentWidth - Mathf.Max(0, nextCols - 1) * GridCellGap;
                ComputeGridCellLayout(avail, marginH, out columns, out columnFootprintW, out cellInnerW);
            }

            _poseGridLayout.Columns = columns;
            _poseGridLayout.ColumnFootprintW = columnFootprintW;
            _poseGridLayout.CellInnerW = cellInnerW;
        }

        private void DrawGridPanel(float gridAvailW)
        {
            InitGroupStyles();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type == EventType.Layout || _poseGridLayout.Frame != Time.frameCount)
                UpdatePoseGridLayout(gridAvailW);

            // Process search/filter input before reading the display list — otherwise the grid
            // renders one keystroke behind (filter bar used to run after GetVisibleDisplayEntries).
            DrawPoseFilterBarRow();
            GUILayout.Space(4f);
            ClampCurrentPage();

            int columns = _poseGridLayout.Columns;
            float contentWidth = _poseGridLayout.ContentWidth;
            float columnFootprintW = _poseGridLayout.ColumnFootprintW;
            float cellInnerW = _poseGridLayout.CellInnerW;

            var visibleEntries = GetVisibleDisplayEntries();

            GUILayout.BeginHorizontal(PoseBrowserScale.H(22f));
            bool hasFilters = HasActivePoseContentFilters();
            if (GUILayout.Button(hasFilters ? _gcBtnSoloFiltered : _gcBtnSoloDefault, PoseBrowserScale.W(52f)))
                SelectAllStandalonePosesInView();
            if (GUILayout.Button(_gcBtnGroup, PoseBrowserScale.W(80f)))
                SelectAllGroupEntitiesInView();
            if (GUILayout.Button(hasFilters ? _gcBtnGroupPoseFiltered : _gcBtnGroupPoseDefault, PoseBrowserScale.W(72f)))
                SelectAllGroupedPosesInView();
            if (GUILayout.Button(hasFilters ? _gcBtnAllFiltered : _gcBtnAllDefault, PoseBrowserScale.W(44f)))
                SelectAllInCurrentFolderView();
            GUI.enabled = CanInvertSelection();
            if (GUILayout.Button(HasGroupEntitySelection() ? _gcBtnInvertGroup : _gcBtnInvertPose, PoseBrowserScale.W(58f)))
                InvertSelectionInView();
            GUI.enabled = true;
            if (GUILayout.Button(_gcBtnNone, PoseBrowserScale.W(52f)))
                ClearAllSelection();
            GUILayout.Space(6f);
            GUILayout.Label($"{_allItems.Count} in folder", PoseBrowserScale.W(78f));
            if (_itemsPerPage > 0 && CountDisplayPoses() > 0)
            {
                GUILayout.FlexibleSpace();
                int pages = Mathf.Max(1, Mathf.CeilToInt(CountDisplayPoses() / (float)_itemsPerPage));
                GUILayout.Label($"Page {_currentPage}/{pages} · {CountDisplayPoses()} shown", PoseBrowserScale.W(168f));
                GUI.enabled = _currentPage > 1;
                if (GUILayout.Button(_gcPagePrev, PoseBrowserScale.W(28f)))
                {
                    _currentPage--;
                    _gridScroll = Vector2.zero;
                    InvalidatePoseBrowserViewCaches();
                }
                GUI.enabled = _currentPage < pages;
                if (GUILayout.Button(_gcPageNext, PoseBrowserScale.W(28f)))
                {
                    _currentPage++;
                    _gridScroll = Vector2.zero;
                    InvalidatePoseBrowserViewCaches();
                }
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label(new GUIContent($"{CountDisplayPoses()} shown", "After search & tag filters."), PoseBrowserScale.W(72f));
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            // Outer = gridAvailW; contentWidth already subtracts the v-scrollbar for cell layout.
            _gridScroll = GUILayout.BeginScrollView(
                _gridScroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Width(gridAvailW),
                GUILayout.MaxWidth(gridAvailW),
                GUILayout.ExpandHeight(true));

            var gridRows = GetOrBuildGridRows(visibleEntries, columns);

            // Only the *inputs* to the (expensive) row-metrics pass matter: the available width
            // (drives base columns / cell size), the row/pose counts and import-preview mode.
            // GridAvailW is an input that RefineGridLayoutForRows never mutates, so it stays
            // stable across frames while idle — unlike _poseGridLayout.Columns, which the refine
            // step overwrites. Keying on it lets us skip the whole pass when nothing changed
            // instead of recomputing every frame.
            int displayCount = _displayEntries.Count;
            bool importPreview = ImportPreviewActive;
            bool layoutKeyChanged =
                !Mathf.Approximately(_gridUniformLayoutGridAvailW, _poseGridLayout.GridAvailW) ||
                _gridUniformLayoutRowCount != gridRows.Count ||
                _gridUniformLayoutDisplayCount != displayCount ||
                _gridUniformLayoutImportPreview != importPreview ||
                _gridRowOuterHeights.Count != gridRows.Count;

            if (layoutKeyChanged)
            {
                RefineGridLayoutForRows(gridRows);
                cellInnerW = _poseGridLayout.CellInnerW;
                columnFootprintW = _poseGridLayout.ColumnFootprintW;
                columns = _poseGridLayout.Columns;
                ComputeUniformGridRowMetrics(gridRows, cellInnerW);
                _gridUniformLayoutFrame = Time.frameCount;
                _gridUniformLayoutGridAvailW = _poseGridLayout.GridAvailW;
                _gridUniformLayoutColumns = columns;
                _gridUniformLayoutCellInnerW = cellInnerW;
                _gridUniformLayoutColumnFootprintW = columnFootprintW;
                _gridUniformLayoutRowCount = gridRows.Count;
                _gridUniformLayoutDisplayCount = displayCount;
                _gridUniformLayoutImportPreview = importPreview;
            }
            else
            {
                // Cache hit: UpdatePoseGridLayout reset Columns/CellInnerW to their unrefined base
                // this frame, so restore the refined values we computed earlier. Uniform* heights
                // and _gridRowOuterHeights persist on the struct/list and stay valid.
                columns = _gridUniformLayoutColumns;
                cellInnerW = _gridUniformLayoutCellInnerW;
                columnFootprintW = _gridUniformLayoutColumnFootprintW;
                _poseGridLayout.Columns = columns;
                _poseGridLayout.CellInnerW = cellInnerW;
                _poseGridLayout.ColumnFootprintW = columnFootprintW;
            }

            float uniformPoseCardOuterH = _poseGridLayout.UniformPoseCardOuterH;
            float uniformTagBlockH = _poseGridLayout.UniformTagBlockH;
            float uniformGroupTagBlockH = _poseGridLayout.UniformGroupTagBlockH;

            // --- Scroll virtualization: determine visible row range ---
            int rowCount = gridRows.Count;
            float viewportH = _gridScrollViewportH > 1f ? _gridScrollViewportH : 600f;
            // Safety clamp: depending on IMGUI state, GUILayoutUtility.GetLastRect() after
            // EndScrollView can report the scroll *content* height instead of the viewport.
            // If that leaks in, every row counts as visible and virtualization silently draws
            // the whole page. The visible window can never exceed the window height, so cap it.
            viewportH = Mathf.Min(viewportH, Mathf.Max(200f, windowRect.height));
            float scrollY = _gridScroll.y;

            int firstVisible = 0;
            int lastVisible = rowCount - 1;

            if (rowCount > 0 && _gridRowOuterHeights.Count == rowCount)
            {
                float yAccum = 0f;
                firstVisible = rowCount;
                lastVisible = -1;

                for (int r = 0; r < rowCount; r++)
                {
                    float rowH = _gridRowOuterHeights[r];
                    float rowTop = yAccum;
                    float rowBot = yAccum + rowH;
                    yAccum = rowBot + GridCellGap;

                    if (rowBot >= scrollY && rowTop <= scrollY + viewportH)
                    {
                        if (firstVisible > r) firstVisible = r;
                        lastVisible = r;
                    }
                }

                if (firstVisible > lastVisible)
                {
                    firstVisible = 0;
                    lastVisible = rowCount - 1;
                }
                else
                {
                    // Overscan one row each side so fast scrolling doesn't flash placeholders.
                    firstVisible = Mathf.Max(0, firstVisible - 1);
                    lastVisible = Mathf.Min(rowCount - 1, lastVisible + 1);
                }
            }

            // Emit space for rows above the visible range
            float spaceAbove = 0f;
            for (int r = 0; r < firstVisible; r++)
            {
                spaceAbove += _gridRowOuterHeights[r];
                if (r > 0) spaceAbove += GridCellGap;
            }
            if (firstVisible > 0)
                spaceAbove += GridCellGap;
            if (spaceAbove > 0f)
                GUILayout.Space(spaceAbove);

            // Compute displayIdx offset for the first visible row
            int displayIdx = _itemsPerPage > 0 ? (_currentPage - 1) * _itemsPerPage : 0;
            for (int r = 0; r < firstVisible; r++)
            {
                foreach (var cell in gridRows[r].Cells)
                {
                    if (cell.Kind == PoseBrowserGridCellKind.GroupSegment)
                        displayIdx += cell.GroupSegment!.Poses.Count;
                    else
                        displayIdx++;
                }
            }

            // Draw only visible rows
            for (int rowIdx = firstVisible; rowIdx <= lastVisible; rowIdx++)
            {
                if (rowIdx > firstVisible)
                    GUILayout.Space(GridCellGap);

                var gridRow = gridRows[rowIdx];
                float rowOuterH = rowIdx < _gridRowOuterHeights.Count
                    ? _gridRowOuterHeights[rowIdx]
                    : uniformPoseCardOuterH;

                GUILayout.BeginHorizontal(
                    GUILayout.Width(contentWidth),
                    GUILayout.MaxWidth(contentWidth),
                    GUILayout.MinHeight(rowOuterH),
                    GUILayout.ExpandWidth(false));
                int cellIdx = 0;
                foreach (var gridCell in gridRow.Cells)
                {
                    if (cellIdx++ > 0)
                        GUILayout.Space(GridCellGap);

                    if (gridCell.Kind == PoseBrowserGridCellKind.GroupSegment)
                    {
                        DrawGroupSegmentCell(
                            gridCell.GroupSegment!,
                            cellInnerW,
                            columnFootprintW,
                            uniformPoseCardOuterH,
                            uniformTagBlockH,
                            uniformGroupTagBlockH,
                            rowOuterH,
                            ref displayIdx);
                    }
                    else
                    {
                        DrawGridCell(
                            gridCell.Pose!,
                            displayIdx,
                            cellInnerW,
                            uniformPoseCardOuterH,
                            uniformTagBlockH);
                        displayIdx++;
                    }
                }

                GUILayout.FlexibleSpace();

                GUILayout.EndHorizontal();
            }

            // Emit space for rows below the visible range
            float spaceBelow = 0f;
            for (int r = lastVisible + 1; r < rowCount; r++)
            {
                spaceBelow += _gridRowOuterHeights[r];
                if (r > lastVisible + 1) spaceBelow += GridCellGap;
            }
            if (lastVisible < rowCount - 1)
                spaceBelow += GridCellGap;
            if (spaceBelow > 0f)
                GUILayout.Space(spaceBelow);

            GUILayout.EndScrollView();

            // Capture viewport height for next frame's virtualization
            if (Event.current.type == EventType.Repaint)
            {
                Rect svRect = GUILayoutUtility.GetLastRect();
                if (svRect.height > 1f)
                    _gridScrollViewportH = svRect.height;
            }

            GUILayout.EndVertical();
        }

        /// <summary>Repaint-only checkbox; unlike <see cref="GUI.Toggle"/> does not register extra GUILayout controls.</summary>
        private static void DrawCheckboxVisual(Rect rect, bool on)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            GUI.skin.toggle.Draw(rect, GUIContent.none, false, false, on, false);
        }

        private void DrawGridCell(
            PoseBrowserDisplayEntry entry,
            int displayIndex,
            float cellInnerW,
            float uniformPoseCardOuterH,
            float uniformTagBlockH,
            GUIStyle? cardStyleOverride = null)
        {
            InitStyles();
            InitGroupStyles();

            var item = entry.Item;
            GUIStyle cardBox = cardStyleOverride ?? _poseCardBaseStyle!;
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
                    cardBox = _groupInnerCardSelectedStyle!;
                else if (item.IsFavorite)
                    cardBox = _groupInnerCardFavoriteStyle!;
                else if (entry.IsDimmed)
                    cardBox = _groupInnerCardDimmedStyle!;
            }

            float layoutW = cellInnerW;
            float innerW = Mathf.Max(40f, cellInnerW);

            // Same as group segments: horizontal footprint is the card width only. A full columnFootprintW
            // wrapper left empty space beside the card and looked like double GridCellGap.
            GUILayout.BeginVertical(
                cardBox,
                GUILayout.Width(layoutW),
                GUILayout.MaxWidth(layoutW),
                GUILayout.MinHeight(uniformPoseCardOuterH),
                GUILayout.Height(uniformPoseCardOuterH),
                GUILayout.ExpandWidth(false));

            Rect thumbRect = GUILayoutUtility.GetRect(innerW, innerW, GUILayout.Width(innerW), GUILayout.Height(innerW));
            Texture2D tex = item.Thumbnail ?? _placeholderTex!;
            item.ThumbnailLastUsedFrame = Time.frameCount;
            RequestThumbnail(item);

            const float cbSize = 18f;
            var cbRect = new Rect(thumbRect.xMax - cbSize - 3f, thumbRect.y + 3f, cbSize, cbSize);
            Event ev = Event.current;
            if (ev.type == EventType.Repaint)
            {
                Color prev = GUI.color;
                if (entry.IsDimmed)
                    GUI.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit, false);
                if (IsPoseThumbnailLoading(item))
                    DrawThumbnailLoadingOverlay(thumbRect);
                GUI.color = prev;
                DrawCheckboxVisual(cbRect, item.IsSelected);
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

            var titleRowRect = GUILayoutUtility.GetRect(
                innerW,
                PoseCardNameRowH,
                GUILayout.Width(innerW),
                GUILayout.MaxWidth(innerW),
                GUILayout.ExpandWidth(false));
            float pad = PoseCardTextPadH;
            var starRect = new Rect(titleRowRect.x + pad, titleRowRect.y, PoseCardNameStarW, titleRowRect.height);
            float nameW = Mathf.Max(20f, titleRowRect.width - pad * 2f - PoseCardNameStarW);
            var nameRect = new Rect(starRect.xMax, titleRowRect.y, nameW, titleRowRect.height);

            if (Event.current.type == EventType.Repaint)
            {
                string label = item.DisplayName;
                var starStyle = _favoriteStyle!;
                var nameStyle = favoriteCard ? _favoriteCardNameStyle! : _poseCardNameStyle!;
                string shownName = GetCachedTruncatedName(item, label, nameStyle, nameW);
                GUI.Label(starRect, item.IsFavorite ? "★" : " ", starStyle);
                GUI.Label(nameRect, new GUIContent(shownName, label), nameStyle);
            }

            if (uniformTagBlockH > 0f)
            {
                float tagTextW = Mathf.Max(20f, innerW - pad * 2f);
                var tagRect = GUILayoutUtility.GetRect(
                    innerW,
                    uniformTagBlockH,
                    GUILayout.Width(innerW),
                    GUILayout.MaxWidth(innerW),
                    GUILayout.ExpandWidth(false));
                var tagTextRect = new Rect(tagRect.x + pad, tagRect.y, tagTextW, tagRect.height);
                if (item.Tags.Count > 0 && Event.current.type == EventType.Repaint)
                {
                    string plainTagStr = item.GetOrBuildTagString();
                    var tagStyle = favoriteCard ? _favoriteCardTagStyle! : _tagWrapStyle!;
                    bool highlightExcludedTags = entry.IsDimmed &&
                        !string.IsNullOrEmpty(item.GroupId) &&
                        _tagFiltersExclude.Overlaps(item.Tags);
                    string displayTagStr = highlightExcludedTags
                        ? BuildPoseCardTagRichText(plainTagStr, item.Tags)
                        : plainTagStr;
                    var drawStyle = highlightExcludedTags ? _tagWrapStyleRich! : tagStyle;
                    GUI.Label(tagTextRect, displayTagStr, drawStyle);
                }
            }

            GUILayout.EndVertical();
        }

        private static float MeasureTagBlockHeight(string tagText, GUIStyle style, float width)
        {
            float h = style.CalcHeight(new GUIContent(tagText), width);
            return Mathf.Max(Mathf.Ceil(h) + 2f, 16f);
        }

        private const string LabelEllipsis = "...";

        private static string GetCachedTruncatedName(PoseGridItem item, string label, GUIStyle style, float maxWidth)
        {
            if (item.CachedTruncatedName != null &&
                item.CachedTruncatedNameWidth == maxWidth &&
                ReferenceEquals(item.CachedTruncatedNameSource, label))
            {
                return item.CachedTruncatedName;
            }

            string result = TruncateWithEllipsis(label, style, maxWidth);
            item.CachedTruncatedName = result;
            item.CachedTruncatedNameWidth = maxWidth;
            item.CachedTruncatedNameSource = label;
            return result;
        }

        private static string TruncateWithEllipsis(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 1f)
                return text ?? "";

            if (style.CalcSize(new GUIContent(text)).x <= maxWidth)
                return text;

            float ellipsisW = style.CalcSize(new GUIContent(LabelEllipsis)).x;
            float budget = maxWidth - ellipsisW;
            if (budget <= 1f)
                return LabelEllipsis;

            int lo = 0;
            int hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (style.CalcSize(new GUIContent(text.Substring(0, mid))).x <= budget)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return lo <= 0 ? LabelEllipsis : text.Substring(0, lo) + LabelEllipsis;
        }

        private float TreeNodeLabelMaxWidth(int depth) =>
            Mathf.Max(40f, TreePanelWidth - 12f - VerticalScrollbarWidth() - depth * 16f - 24f);

        private string BuildPoseCardTagRichText(string cachedPlainTagStr, IEnumerable<string> tags)
        {
            const string excludedColor = "#FF5555";
            if (!_tagFiltersExclude.Overlaps(tags))
                return cachedPlainTagStr;

            var sortedTags = tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase);
            var parts = new List<string>();
            foreach (var tag in sortedTags)
            {
                string escaped = EscapeRichTextForLabel(tag);
                if (_tagFiltersExclude.Contains(tag))
                    parts.Add($"<color={excludedColor}>{escaped}</color>");
                else
                    parts.Add(escaped);
            }

            return string.Join(" · ", parts.ToArray());
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

        private void DrawBottomBar(float availableWidth)
        {
            float actionBarWrapWidth = Mathf.Max(120f, availableWidth - 20f);
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
                    PoseBrowserScale.H(36f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Cancel import", PoseBrowserScale.W(110f), PoseBrowserScale.H(24f)))
                    CancelPendingFolderOperation();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label(
                    "Pick the destination folder in the Folders panel (↑), then Apply or Cancel there.",
                    PoseBrowserScale.H(32f));
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

            // Scaled at the origin; threaded raw-free into the group/character action-button helpers below.
            float barBtnH = PoseBrowserScale.Px(26f);
            float barBtnMinW = PoseBrowserScale.Px(96f);
            bool oneGroupEntity = TryGetSingleSelectedGroup(out var fullGroup);
            var groupMembers = oneGroupEntity && fullGroup != null ? GetGroupMemberItems(fullGroup.Id) : null;
            bool multiGroupEntity = _selectedGroupIds.Count > 1;

            GUILayout.BeginVertical(GUI.skin.box);

            if (oneGroupEntity && fullGroup != null && groupMembers != null)
            {
                DrawGroupEntityActionBar(fullGroup, groupMembers, barBtnH, barBtnMinW, actionBarWrapWidth);
                DrawActionBarSeparator();
            }
            else if (multiGroupEntity)
            {
                DrawMultiGroupEntityActionBar(barBtnH, barBtnMinW, actionBarWrapWidth);
                DrawActionBarSeparator();
            }

            if (librarySelected.Count != 1 && _showItemAssociationPane)
                CloseItemAssociationPane();
            else if (librarySelected.Count == 1 && _showItemAssociationPane &&
                     (_itemAssociationPose == null ||
                      !string.Equals(_itemAssociationPose.FilePath, librarySelected[0].FilePath, StringComparison.OrdinalIgnoreCase)))
                OpenItemAssociationPane(librarySelected[0]);

            if (librarySelected.Count > 0)
            {
                var poseBar = new ActionBarWrapLayout();
                poseBar.Begin(actionBarWrapWidth);
                poseBar.Add(PoseBrowserScale.Px(100f), () => GUILayout.Label($"Poses: {librarySelected.Count}", PoseBrowserScale.W(100f)));

                if (librarySelected.Count == 1)
                {
                    poseBar.AddButton("Items", barBtnH, barBtnMinW, () =>
                    {
                        if (_showItemAssociationPane &&
                            _itemAssociationPose != null &&
                            string.Equals(_itemAssociationPose.FilePath, librarySelected[0].FilePath, StringComparison.OrdinalIgnoreCase))
                            CloseItemAssociationPane();
                        else
                            OpenItemAssociationPane(librarySelected[0]);
                    });

                    int selectedCharCount = GetCachedStudioCharacterDisplayNames().Count;
                    bool canUpdatePose = selectedCharCount == 1;
                    const string updatePoseNeedOneCharTip =
                        "For updating a pose, exactly one character has to be selected in Studio.";
                    // Custom (dimmed-but-clickable + reason tooltip) so it can't use AddButton's disabled path;
                    // reserve the measured width and render at that exact width so the wrap stays accurate.
                    float updatePoseW = poseBar.MeasureButton("Update pose", barBtnMinW);
                    poseBar.Add(updatePoseW, () =>
                    {
                        var prevBtnColor = GUI.color;
                        if (!canUpdatePose)
                            GUI.color = new Color(1f, 1f, 1f, 0.45f);
                        if (GUILayout.Button(
                                new GUIContent("Update pose", canUpdatePose ? "" : updatePoseNeedOneCharTip),
                                GUILayout.Height(barBtnH),
                                GUILayout.Width(updatePoseW))
                            && canUpdatePose)
                            ShowUpdatePoseOptions(librarySelected[0]);
                        GUI.color = prevBtnColor;
                    });

                    poseBar.AddButton("Rename…", barBtnH, 88f, () =>
                    {
                        _renamePoseText = librarySelected[0].DisplayName;
                        _renamePoseAlsoFile = true;
                        _showRenamePosePopup = true;
                    });
                }

                poseBar.AddButton("Tag selected", barBtnH, barBtnMinW, () =>
                {
                    _tagWindowForGroup = false;
                    _tagWindowGroupId = null;
                    OpenTagAssignWindow();
                });

                poseBar.AddButton("Toggle favorite", barBtnH, barBtnMinW, () =>
                {
                    foreach (var it in librarySelected)
                        _tagDb.ToggleFavorite(it);
                    NotifyLibraryCacheFavoriteChanged(librarySelected);
                    ResortPoseItemsInPlace();
                    ApplyFilters();
                });

                poseBar.AddButton("Thumbnails…", barBtnH, barBtnMinW,
                    () => StartThumbnailCapture(librarySelected));

                DrawMultiCharacterApplyButton(barBtnH, barBtnMinW, poseBar);
                poseBar.End();

                DrawActionBarSeparator();

                var fileBar = new ActionBarWrapLayout();
                fileBar.Begin(actionBarWrapWidth);
                DrawPoseGroupingActions(librarySelected, barBtnH, barBtnMinW, hideUngroup: oneGroupEntity, fileBar);
                fileBar.Add(PoseBrowserScale.Px(18f), () => DrawActionBarVerticalSeparator(barBtnH));

                bool canMoveCopyGroup = CanMoveCopyAsWholeGroup(librarySelected, out _);
                bool blockedGrouped = SelectionHasGroupedPose(librarySelected) && !canMoveCopyGroup;
                fileBar.AddButton("Export…", barBtnH, barBtnMinW,
                    ExportSelectedPosesToDisk, enabled: !blockedGrouped);

                fileBar.AddButton("Move to folder…", barBtnH, barBtnMinW,
                    () => BeginFolderOpForPoses(PendingFolderOperation.MovePoses), enabled: !blockedGrouped);

                fileBar.AddButton("Copy to folder…", barBtnH, barBtnMinW,
                    () => BeginFolderOpForPoses(PendingFolderOperation.CopyPoses), enabled: !blockedGrouped);

                fileBar.AddButton("Delete…", barBtnH, 88f,
                    () => _showDeletePosesConfirm = true);

                fileBar.End();

                if (_showDeletePosesConfirm)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(
                        $"Permanently delete {librarySelected.Count} pose file(s)? Each file is copied to !_AutoBackup first.",
                        PoseBrowserScale.H(36f));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Confirm delete", PoseBrowserScale.H(26f), PoseBrowserScale.MinW(120f)))
                    {
                        foreach (var it in librarySelected)
                        {
                            if (it.Thumbnail != null)
                                Destroy(it.Thumbnail);
                        }
                        foreach (var it in librarySelected)
                        {
                            _groupDb.RemoveItem(it);
                            _itemDb.RemovePoseKey(it.RelativePath(_dataService.PoseRootPath));
                        }
                        NotifyLibraryCachePosesDeleted(librarySelected);
                        _dataService.DeletePoseFiles(librarySelected, _tagDb);
                        _showDeletePosesConfirm = false;
                        ReloadCurrentView();
                    }
                    if (GUILayout.Button("Cancel", PoseBrowserScale.H(26f), PoseBrowserScale.MinW(80f)))
                        _showDeletePosesConfirm = false;
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Deselect all", GUILayout.Height(barBtnH), PoseBrowserScale.MinW(88f)))
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
                    PoseBrowserScale.H(28f));
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
                if (GUILayout.Button("OK", PoseBrowserScale.H(22f)))
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
                if (GUILayout.Button("Cancel", PoseBrowserScale.H(22f)))
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
                    if (GUILayout.Button("OK", PoseBrowserScale.H(22f)))
                    {
                        string oldPath = sel[0].FilePath;
                        string oldRel = sel[0].RelativePath(_dataService.PoseRootPath);
                        if (_dataService.RenamePoseDisplayNameAndOptionalFile(sel[0], _renamePoseText, _renamePoseAlsoFile, _tagDb))
                        {
                            if (_renamePoseAlsoFile && !string.Equals(oldPath, sel[0].FilePath, StringComparison.OrdinalIgnoreCase))
                            {
                                _groupDb.OnItemPathChanged(oldRel, sel[0]);
                                _itemDb.OnItemPathChanged(oldRel, sel[0]);
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
                    if (GUILayout.Button("Cancel", PoseBrowserScale.H(22f)))
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
                                _itemDb.OnItemPathChanged(oldRel, it);
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
                        {
                            var copy = _dataService.CopyPoseFileToFolder(it, dest, _tagDb);
                            if (copy != null)
                            {
                                _itemDb.CopyItemsFromTo(it, copy);
                                NotifyLibraryCachePoseCopied(copy);
                            }
                        }
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
                        var tex = CreateDisplayThumbnail(e.FileBytes);
                        if (tex != null)
                        {
                            item.Thumbnail = tex;
                        }
                        else
                        {
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

        private void ExportItemsToDisk(IList<PoseGridItem> items, string? saveDialogTitle = null)
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
                if (GUILayout.Button("Keep Thumbnail", PoseBrowserScale.H(24f)))
                {
                    DoUpdatePose(_pendingUpdateItem!, null);
                    _pendingUpdateMode = UpdateMode.None;
                }

                if (GUILayout.Button("New Thumbnail", PoseBrowserScale.H(24f)))
                {
                    _pendingUpdateMode = UpdateMode.NewThumb;
                    StartUpdateCapture(_pendingUpdateItem!);
                }
            }
            else
            {
                if (GUILayout.Button("No Thumbnail", PoseBrowserScale.H(24f)))
                {
                    DoUpdatePose(_pendingUpdateItem!, null);
                    _pendingUpdateMode = UpdateMode.None;
                }

                if (GUILayout.Button("Create Thumbnail", PoseBrowserScale.H(24f)))
                {
                    _pendingUpdateMode = UpdateMode.NewThumb;
                    StartUpdateCapture(_pendingUpdateItem!);
                }
            }

            if (GUILayout.Button("Cancel", PoseBrowserScale.H(24f)))
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

        private bool IsPoseThumbnailLoading(PoseGridItem item) =>
            item.Thumbnail == null
            && item.IsPng
            && string.IsNullOrEmpty(item.ImportPackEntryId)
            && _thumbnailsPendingLoad.Contains(item);

        private static void DrawThumbnailLoadingOverlay(Rect thumbRect)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(thumbRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = Color.white;
            var style = GUI.skin.label;
            TextAnchor prevAlign = style.alignment;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(thumbRect, _gcThumbnailLoading);
            style.alignment = prevAlign;
            GUI.color = prev;
        }

        /// <summary>Queues a single thumbnail for background load. Called from the draw path for
        /// cards that are actually visible, so loading naturally tracks the viewport.</summary>
        private void RequestThumbnail(PoseGridItem item)
        {
            if (item.Thumbnail != null || !item.IsPng || !string.IsNullOrEmpty(item.ImportPackEntryId))
                return;
            if (_thumbnailsPendingLoad.Add(item))
                _thumbnailLoadNeeded = true;
        }

        private void StopThumbnailLoading()
        {
            if (_thumbnailLoadCoroutine != null)
            {
                StopCoroutine(_thumbnailLoadCoroutine);
                _thumbnailLoadCoroutine = null;
            }

            _thumbnailsPendingLoad.Clear();
        }

        private void ReleaseThumbnailsOnHide()
        {
            StopThumbnailLoading();
            foreach (var item in _allItems)
            {
                if (item.Thumbnail != null)
                {
                    Destroy(item.Thumbnail);
                    item.Thumbnail = null;
                }
            }
        }

        private void EvictLruThumbnailsIfNeeded()
        {
            if (Time.frameCount - _thumbnailEvictionFrame < 60)
                return;
            _thumbnailEvictionFrame = Time.frameCount;

            int loadedCount = 0;
            foreach (var item in _allItems)
            {
                if (item.Thumbnail != null)
                    loadedCount++;
            }

            if (loadedCount <= ThumbnailLruMaxLoaded)
                return;

            int toEvict = loadedCount - ThumbnailLruMaxLoaded + ThumbnailLruEvictBatch;
            var candidates = new List<PoseItemFramePair>(loadedCount);
            foreach (var item in _allItems)
            {
                if (item.Thumbnail != null)
                    candidates.Add(new PoseItemFramePair(item, item.ThumbnailLastUsedFrame));
            }

            candidates.Sort((a, b) => a.Frame.CompareTo(b.Frame));

            int evicted = 0;
            for (int i = 0; i < candidates.Count && evicted < toEvict; i++)
            {
                var item = candidates[i].Item;
                if (item.ThumbnailLastUsedFrame >= Time.frameCount - 2)
                    continue;
                Destroy(item.Thumbnail);
                item.Thumbnail = null;
                evicted++;
            }
        }

        private static Texture2D? CreateDisplayThumbnail(byte[] pngBytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!tex.LoadImage(pngBytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            int maxSize = PoseDataService.MaxThumbnailDisplaySize;
            // Only downscale oversized previews; avoid a GPU readback purely for format conversion
            // (hitches scrolling on weak GPUs). Preserve aspect ratio when downscaling.
            if (tex.width > maxSize || tex.height > maxSize)
            {
                float scale = Mathf.Min((float)maxSize / tex.width, (float)maxSize / tex.height);
                tex = PoseDataService.ResizeTexture(
                    tex,
                    Mathf.Max(1, Mathf.RoundToInt(tex.width * scale)),
                    Mathf.Max(1, Mathf.RoundToInt(tex.height * scale)));
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private IEnumerator LoadThumbnailsCoroutine()
        {
            const int batchSize = 4;
            // Drain the pending set dynamically so on-demand additions made while we run
            // (e.g. the user scrolling new rows into view) are picked up in the same pass.
            while (_thumbnailsPendingLoad.Count > 0)
            {
                _thumbnailLoadBatch.Clear();
                foreach (var it in _thumbnailsPendingLoad)
                {
                    _thumbnailLoadBatch.Add(it);
                    if (_thumbnailLoadBatch.Count >= batchSize) break;
                }

                foreach (var item in _thumbnailLoadBatch)
                {
                    if (item.Thumbnail == null && item.IsPng)
                        item.Thumbnail = _dataService.LoadThumbnailTexture(item);
                    _thumbnailsPendingLoad.Remove(item);
                }
                yield return null;
            }
            _thumbnailLoadBatch.Clear();
            _thumbnailLoadCoroutine = null;
        }

        private void StartThumbnailCapture(List<PoseGridItem> items)
        {
            _thumbCapture.StartCapture(
                this,
                items,
                onApplyPose: ApplyPoseToSelectedWithUsage,
                onCaptured: CommitCapturedThumbnail,
                onComplete: () => { });
        }

        private void CommitCapturedThumbnail(PoseGridItem item, byte[] pngBytes)
        {
            string oldPath = item.FilePath;
            if (item.IsPng)
            {
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

            var tex = CreateDisplayThumbnail(pngBytes);
            if (item.Thumbnail != null) Destroy(item.Thumbnail);
            item.Thumbnail = tex;

            if (!string.Equals(oldPath, item.FilePath, StringComparison.OrdinalIgnoreCase))
                NotifyLibraryCachePoseMoved(oldPath, item);
            else
                _libraryCache.SyncMetadata(item);
        }

        private void DoSavePose()
        {
            if (StringEx.IsNullOrWhiteSpace(_savePoseName)) return;
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

            string oldPath = item.FilePath;
            string oldRel = item.RelativePath(_dataService.PoseRootPath);

            if (!_dataService.UpdatePose(item, chars[0], newPngBytes))
                return;

            if (!string.Equals(oldPath, item.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                _tagDb.OnItemPathChanged(oldRel, item);
                _groupDb.OnItemPathChanged(oldRel, item);
                _itemDb.OnItemPathChanged(oldRel, item);
                NotifyLibraryCachePoseMoved(oldPath, item);
            }
            else
                _libraryCache.AddOrUpdate(item);

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
                var tex = CreateDisplayThumbnail(newPngBytes);
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

        private static readonly Color PoseCardBaseTint = new Color(0.32f, 0.32f, 0.34f, 0.62f);
        private static readonly Color GroupCardBaseTint = new Color(0.14f, 0.14f, 0.16f, 0.94f);

        /// <summary>Chrome only (margin/padding); no skin.box 9-slice background.</summary>
        private static GUIStyle CreatePoseCardChromeTemplate()
        {
            var box = GUI.skin.box;
            return new GUIStyle
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                clipping = box.clipping
            };
        }

        private void InitStyles()
        {
            if (_selectedStyle == null)
            {
                var cardChrome = CreatePoseCardChromeTemplate();
                _poseCardBaseStyle = CardTintStyle(cardChrome, PoseCardBaseTint);

                _selectedStyle = CardTintStyle(_poseCardBaseStyle, new Color(0.22f, 0.48f, 0.98f, 0.88f));
                _favoriteCardStyle = CardTintStyle(_poseCardBaseStyle, new Color(0.95f, 0.82f, 0.22f, 0.72f));
                _dimmedCardStyle = CardTintStyle(_poseCardBaseStyle, new Color(0.45f, 0.45f, 0.45f, 0.35f));

                _favoriteStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = PoseBrowserScale.Font(14),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.85f, 0f) }
                };

                _poseCardNameStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };

                _favoriteCardNameStyle = new GUIStyle(_poseCardNameStyle)
                {
                    fontSize = PoseBrowserScale.Font(14),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.14f, 0.11f, 0.02f) }
                };

                _treeNodeStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 4, 0, 0),
                    wordWrap = false,
                    clipping = TextClipping.Clip
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

                _characterHintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(10, GUI.skin.label.fontSize - 1),
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
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
                    fontSize = PoseBrowserScale.Font(11),
                    richText = false
                };
            }

            float boxW = Mathf.Min(_poseBrowserTooltipStyle.CalcSize(new GUIContent(text)).x + 12f, PoseBrowserScale.Px(280f));
            float boxH = _poseBrowserTooltipStyle.CalcHeight(new GUIContent(text), boxW) + 8f;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            float x = Mathf.Clamp(mouse.x + 14f, 0f, windowRect.width - boxW);
            float y = Mathf.Clamp(mouse.y + 20f, 0f, windowRect.height - boxH);
            GUI.Label(new Rect(x, y, boxW, boxH), text, _poseBrowserTooltipStyle);
        }

        private static GUIStyle CardTintStyle(GUIStyle chromeTemplate, Color tint)
        {
            var s = new GUIStyle(chromeTemplate) { border = new RectOffset(0, 0, 0, 0) };
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
            GUILayout.Label("<b>Top bar</b>", rich);
            GUILayout.Label(
                "• <b>Chars</b> — multi-character priority list. Character label shows Studio selection.\n" +
                "• <b>Update v…</b> — appears when a newer Pose Browser release is available.\n" +
                "• <b>Save Pose</b> — save current character pose into the active folder (selected folder, pose root in <b>All poses</b> or <b>Favorites</b> view).\n" +
                "• <b>Import…</b> — open a pose pack <b>.zip</b> (manifest v2–v4; preview in the grid, then pick a destination folder and <b>Apply</b> in the folder footer).\n" +
                "• <b>Undo</b> / <b>Redo</b> / <b>History</b> — pose history for Studio-selected characters.\n" +
                "• <b>Stash</b> — quick pose clipboard (see <b>Pose stash</b> below). In compact views, <b>Stash</b> is on the character row.",
                rich);

            GUILayout.Space(4f);
            GUILayout.Label("<b>Grid — search & selection</b>", rich);
            GUILayout.Label(
                "• <b>Search</b> (above selection buttons) — filter by name/path. <b>.*</b> = case-insensitive regex (bad patterns show in red).\n" +
                "• <b>★</b> — only favorites. <b>Tags</b> — docked filter window. <b>Sort</b> — docked sort panel.\n" +
                "• <b>Solo</b> / <b>▦ Group</b> / <b>▦ Pose</b> / <b>All</b> / <b>Invert</b> / <b>None</b> — bulk checkbox selection in the current view.",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Pose groups (grid)</b>", rich);
            GUILayout.Label(
                "• Select <b>2+ ungrouped</b> poses → bottom bar <b>Group…</b> (name the group). <b>Ungroup</b> removes membership.\n" +
                "• <b>Group header</b> (▦ row) = select the <b>group entity</b> (rename, tags, export, <b>Apply to characters…</b>, <b>Group thumbnails…</b>, save/clear <b>positions</b>). Card checkboxes = individual poses.\n" +
                "• <b>Exclude</b> tag filters: hide ungrouped poses; grouped poses use <b>group + pose</b> tags. In a visible group, excluded tags dim cards (red tag text).\n" +
                "• <b>Grouped poses</b> / <b>Thumbnails</b> in the tag filter panel cycle neutral → hide (−) → only (+), like per-tag filters.\n" +
                "• Move/Copy one <b>full group</b> at a time. Data: <b>pose_groups.tsv</b> in Sandbox config.",
                rich);

            GUILayout.Space(4f);
            GUILayout.Label("<b>Group relative positions</b>", rich);
            GUILayout.Label(
                "Save character spacing and facing for a group and re-apply it with the group. The <b>first pose</b> in grid display order is the <b>anchor</b> (position and rotation reference). Every other pose stores a <b>local position offset</b> (in the anchor's frame), <b>relative rotation</b> (vs anchor), <b>maker body height</b>, and <b>Studio object scale</b> on that pose path. Persisted in <b>pose_groups.tsv</b> and v7 ZIP (<b>memberRelativeOffsets</b>, <b>memberRelativeRotations</b>, <b>memberBodyHeights</b>, <b>memberObjectScales</b>).\n\n" +
                "<b>To save positions</b> (<b>Save positions…</b> on the group bar):\n" +
                "1. Set up <b>Chars</b> and pose <b>Male</b> / <b>Female</b> tags if needed.\n" +
                "2. Select <b>exactly as many characters as poses</b> in Studio.\n" +
                "3. Apply the group (<b>Apply to characters…</b>) so poses map in <b>display order</b> (first pose → anchor character).\n" +
                "4. Arrange characters, then <b>Save positions…</b> without applying another pose in between (see button tooltip if disabled).\n\n" +
                "<b>To apply saved positions</b> — same apply path (same pose order and <b>Chars</b> priority). After poses are applied:\n" +
                "• <b>Apply relative positions</b> (global; group bar or <b>Options</b>) — each non-anchor character moves to <b>anchor position + anchor rotation × saved offset</b> (orbits with the anchor) and rotates to <b>anchor rotation × saved relative rotation</b>. The anchor character is not moved.\n" +
                "• <b>Adjust for body height</b> (needs relative positions on) — still applies the full offset, but <b>offset.y</b> is scaled from saved vs current body-height ratios on each pose path (spread ratio when heights differed at save; otherwise anchor or averaged scale). No fixed meter constant.\n" +
                "• <b>Adjust for object scale</b> (needs relative positions on) — scales saved offset <b>X/Y/Z</b> from saved vs current Studio object-scale ratios (same spread logic as body height). Relative rotation is unchanged.\n" +
                "• <b>Clear positions</b> — removes stored offsets, heights, scales, and rotations for that group.",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Thumbnails</b>", rich);
            GUILayout.Label(
                "<b>Thumbnails…</b> (selection bar, one or more poses checked) — capture preview PNGs for selected library poses. Each step applies that pose to Studio-selected character(s), then opens the capture overlay: drag/resize the green frame, <b>Capture</b>, <b>Skip</b>, <b>Auto-capture</b> (remaining poses after a configurable pause — Options / BepInEx), or <b>Cancel</b>.\n\n" +
                "<b>Group thumbnails…</b> (group entity bar when the <b>group header</b> is selected) — capture one thumbnail per pose in the group while all characters stay posed together:\n" +
                "• Requires <b>exactly as many Studio characters as poses</b>, with the same one-to-one gender / <b>Chars</b> assignment as multi-character apply (poses need not be applied beforehand).\n" +
                "• On start: applies <b>all</b> group poses at once (same path as <b>Apply to characters…</b>, including <b>Apply relative positions</b> / height / object-scale toggles when enabled).\n" +
                "• Overlay: frame the whole scene once. For each pose in display order, characters assigned to <b>other</b> poses render in Studio <b>simple color</b> (monocolor) so only the focus character shows full detail.\n" +
                "• <b>Capture</b> saves that pose's PNG and advances; <b>Skip</b> leaves that pose unchanged; <b>Auto-capture</b> chains the rest; <b>Cancel</b> restores all characters from monocolor. Not available during import preview.",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Multi-apply</b>", rich);
            GUILayout.Label(
                "• <b>Chars</b> (top bar) opens the <b>priority list</b> (top = first). <b>Load characters from scene</b>, <b>Remove missing from scene</b>, ↑↓ reorder, <b>m</b>/<b>f</b> gender button, ✕ remove. Saved in <b>pose_browser_character_config.json</b>.\n" +
                "• <b>Apply to characters…</b> when <b>2+ poses</b> selected, or <b>one group header</b> selected. Select characters in Studio first.\n" +
                "• <b>Male</b> / <b>Female</b> <i>pose</i> tags → next free character of that gender in list order. <b>Untagged</b> → list order top to bottom; each character gets at most <b>one</b> pose per apply (extras skipped; leftover chars may get a second-pass pose).\n" +
                "• Left-click thumbnail still applies <b>one</b> pose to <b>all</b> selected characters (separate from multi-apply).",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Pose history</b>", rich);
            GUILayout.Label(
                "• Each pose apply records <b>before</b> and <b>after</b> snapshots (absolute pose, position, rotation) per character. The pre-apply entry is skipped when unchanged from the current history point.\n" +
                "• <b>Undo</b> / <b>Redo</b> (top bar or shortcuts) affect only <b>Studio-selected</b> characters. <b>History</b> pane lists the same selection, grouped by character, newest first.\n" +
                "• Click a history line to jump to that snapshot; checkboxes choose pose / position / rotation. Data: <b>pose_browser_history.json</b>. Max entries per character: <b>Options</b> and BepInEx config.",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Pose stash</b>", rich);
            GUILayout.Label(
                "A temporary pose clipboard (FK/IK only — not saved to the library). Distinct from <b>History</b> (automatic undo timeline).\n\n" +
                "<b>Stash</b> (top bar, or character row in <b>List</b> / <b>Mini</b>) — toggles the stash panel open or closed. If already open (docked or floating), it closes. If closed, it reopens in the last mode you used (<b>docked</b> beside the browser or <b>floating</b>).\n\n" +
                "<b>Stash selected character</b> — capture from <b>exactly one</b> Studio-selected character. Each entry is labeled with character name and timestamp.\n" +
                "<b>Click an entry</b> — apply that stashed pose to <b>all</b> currently selected characters (one or many).\n" +
                "<b>x</b> on a row — delete that entry (<b>Yes</b> / <b>No</b> confirm). <b>Clear entire stash</b> at the bottom (confirm required).\n" +
                "<b>Auto-delete after apply</b> — remove an entry once applied.\n\n" +
                "<b>Docked pane</b> — side panel like <b>History</b>; closes when the main Pose Browser closes. <b>Float</b> undocks to a free window (drag title bar, resize ◢).\n" +
                "<b>Floating window</b> — <b>Dock</b> re-attaches beside the browser (or closes if the browser is hidden); <b>×</b> closes only the stash. Stays open when you close the main browser.\n\n" +
                "Hotkey <b>Toggle undocked pose stash</b> — open/close the <b>floating</b> stash only (Configuration Manager → <b>Pose Browser · Keyboard shortcuts</b>).\n" +
                "Data: <b>pose_stash.json</b>. Floating window size/position and dock vs float preference: <b>pose_browser_options.json</b>.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Compact views</b>", rich);
            GUILayout.Label(
                "<b>View (…)</b> in the top bar (Window section) cycles <b>Full → List → Mini → Full</b>; choice is saved. Each mode remembers its own window size, position, and resize (stored in <b>pose_browser_options.json</b>).\n" +
                "• <b>List</b> — optional folder tree (<b>Tree</b> toggle; separate saved window width with tree shown vs hidden, same position). Character row (<b>Chars</b>, Studio selection label, <b>Stash</b>). Scrollable list of <b>filtered</b> poses. <b>Prev / Next</b> applies in list order and wraps. Click a row for one pose; click a <b>▦ group</b> header for multi-character group apply.\n" +
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
                "• <b>Export branch…</b> / <b>Export library tree…</b> — <b>Full</b> layout only: folder footer when a folder is selected / at library root (v4 ZIP of that branch or the whole tree, includes pose groups when present).",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Import / export (ZIP v7)</b>", rich);
            GUILayout.Label(
                "• After <b>Import…</b>, the grid shows a preview: thumbnail click toggles inclusion (checkbox + Ctrl/Shift work). Use <b>Cancel import</b> in the bottom bar or <b>Cancel</b> in the folder footer to abort. <b>Tree branch</b> packs create a named subfolder under the destination you pick. v2–v5 packs still import.\n" +
                "• <b>Export…</b> in the <b>selection bar</b> saves checked library poses to a v7 <b>.zip</b> (tags/favorites; pose groups with offsets/rotations/heights/scales when fully selected).\n" +
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
                "• <b>Solo</b> / <b>▦ Group</b> / <b>▦ Pose</b> / <b>All</b> — ungrouped pose checkboxes; visible group headers (▦ entities); pose checkboxes inside groups; or everything (filtered results skip dimmed group members). <b>Invert</b> flips group headers if any ▦ group is selected, otherwise pose checkboxes (disabled when nothing selected). <b>None</b> clears both.",
                rich);

            GUILayout.Space(8f);
            GUILayout.Label("<b>Pose items (Items pane)</b>", rich);
            GUILayout.Label(
                "Select <b>exactly one</b> library pose, then <b>Items</b> in the bottom bar. The docked pane links Studio <b>workspace items</b> (props, accessories, etc.) to that pose.\n\n" +
                "<b>Add</b> — select <b>one character</b> in Studio and one or more <b>workspace items</b> (tree or guide). The label above <b>Add selected item(s)</b> lists what will be registered. Layout is saved relative to that character (anchor position/rotation, object scale, body height).\n\n" +
                "<b>Stored items</b> — each row: <b>☑</b> include in bulk load, <b>name button</b> loads that entry, <b>✎</b> rename label, <b>X</b> remove from the pose. Names in <b>bold</b> match an item currently selected in Studio (still a button).\n\n" +
                "<b>Load options</b> — toggle <b>Position</b>, <b>Rotation</b>, <b>Scale</b> independently. <b>Load as free</b> spawns without workspace tree parenting even when the item was saved on a body part (same world layout, scaled with the character).\n\n" +
                "<b>Load Selection</b> / <b>Load All</b> — need one Studio character; unchecked rows are skipped. Items respawn from the catalog (bundle paths in <b>pose_items.tsv</b> v5). Attached items restore workspace tree parenting when not loading as free.\n\n" +
                "⚠ Yellow banner if the selected character does not have this pose applied (save/load still allowed). Orange ⚠ on a row = warning from the last load (e.g. body part missing). Data: <b>pose_items.tsv</b> in Sandbox config.",
                rich);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Selection bar (bottom)</b>", rich);
            GUILayout.Label(
                "Shown when something is selected: <b>Items</b> (one pose), <b>Update Pose</b> (one), <b>Rename…</b>, <b>Tag Selected</b>, <b>Group…</b> / <b>Ungroup</b> / group tags, <b>Fav Selected</b>, <b>Thumbnails…</b>, <b>Export…</b> (v5 pose ZIP), <b>Move…</b> / <b>Copy…</b> (ungrouped poses or one full group), <b>Delete…</b>, <b>Deselect</b>. Group entity bar (header selected): includes <b>Group thumbnails…</b> among rename, apply, and layout controls.",
                rich);


#if HS2
            GUILayout.Space(8f);
            GUILayout.Label("<b>Heelz Control</b>", rich);
            GUILayout.Label(
                "• <b>Heelz</b> button (top bar, Full layout) opens the Heelz Control window. Also toggled via keyboard shortcut (<b>Configuration Manager → Pose Browser · Keyboard shortcuts → Toggle Heelz Control window</b>).\n" +
                "• Lists all scene characters with <b>On</b>/<b>Off</b> buttons to force heel hover on or off, and an <b>Auto</b> checkbox.\n" +
                "• When <b>Auto</b> is checked (default), applying a pose whose tags match a <b>Heels OFF</b> or <b>Heels ON</b> rule automatically sets On/Off for that character. When unchecked, tag rules are ignored and only manual toggles apply.\n" +
                "• <b>Tag Rules</b> section (bottom): click <b>Edit</b> to open a tag picker for each rule set. Tags created here appear in Pose Browser tag lists even if no pose uses them yet.\n" +
                "• Requires <b>HS2Heelz</b> installed. Without it the window shows a notice. Per-character overrides reset each session; tag rules persist in BepInEx config.",
                rich);
#endif

            GUILayout.Space(8f);
            GUILayout.Label("<b>Options panel</b>", rich);
            GUILayout.Label(
#if HS2
                "Card width, items per page (0 = all on one scroll), <b>Apply stored relative positions when applying a group</b>, <b>Adjust relative layout for body height (saved per pose)</b> (requires relative positions), select/deselect all filtered, and a read-only list of <b>keyboard shortcuts</b>. Assign keys in BepInEx <b>Configuration Manager</b> → section <b>Pose Browser · Keyboard shortcuts</b> (next/previous pose; next/previous browse target; undo/redo; toggle Heelz Control; toggle undocked pose stash; no text field focused).\n" +
#else
                "Card width, items per page (0 = all on one scroll), <b>Apply stored relative positions when applying a group</b>, <b>Adjust relative layout for body height (saved per pose)</b> (requires relative positions), select/deselect all filtered, and a read-only list of <b>keyboard shortcuts</b>. Assign keys in BepInEx <b>Configuration Manager</b> → section <b>Pose Browser · Keyboard shortcuts</b> (next/previous pose; next/previous browse target; undo/redo; toggle undocked pose stash; no text field focused).\n" +
#endif
                "Card width and page cap are mirrored in BepInEx under <b>Pose Browser</b>. Window positions, layout tier (<b>Full</b>/<b>List</b>/<b>Mini</b>), sort mode, and the group layout toggle live in <b>pose_browser_options.json</b> next to the other Sandbox config files.",
                rich);

            GUILayout.Space(10f);
            PoseBrowserWikiRegistration.DrawHelpWikiSection(rich);

            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("Close help", PoseBrowserScale.H(26f)))
                _showHelpPane = false;

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void RunPoseFileRepair()
        {
            if (_poseFileRepairProgress.IsRunning)
                return;

            _lastDatRepairMessage = "";
            if (_poseFileRepairCoroutine != null)
            {
                StopCoroutine(_poseFileRepairCoroutine);
                _poseFileRepairCoroutine = null;
            }

            _poseFileRepairCoroutine = StartCoroutine(PoseFileRepairRoutine());
        }

        private IEnumerator PoseFileRepairRoutine()
        {
            yield return PoseFileRepair.RepairLibraryCoroutine(
                _dataService.PoseRootPath,
                _dataService,
                _tagDb,
                _groupDb,
                _poseFileRepairProgress,
                _poseFileRepairResult);

            _poseFileRepairCoroutine = null;
            var result = _poseFileRepairResult;

            _lastDatRepairMessage =
                $"Scanned {result.FilesScanned} file(s): {result.Repaired} repaired, {result.Broken} broken, {result.Failed} failed, {result.AlreadyOk} OK.";

            SandboxServices.Log.LogInfo($"PoseBrowser: Pose file repair — {_lastDatRepairMessage}");

            if (result.Repaired > 0 || result.Broken > 0 || result.Failed > 0)
            {
                ScheduleLibraryCacheRebuild();
                _folderTree.Refresh();
                ReloadCurrentView();
            }
        }

        private static void DrawProgressBar(float fraction, float height = 18f)
        {
            fraction = Mathf.Clamp01(fraction);
            var bar = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), PoseBrowserScale.H(height));
            GUI.Box(bar, GUIContent.none);
            if (fraction > 0.001f)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0.35f, 0.75f, 0.45f, 0.95f);
                var fill = new Rect(bar.x + 2f, bar.y + 2f, Mathf.Max(0f, (bar.width - 4f) * fraction), bar.height - 4f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }

        private void DrawPoseFileRepairSection()
        {
            var wrap = GetOptionsWrapStyle();
            GUILayout.Label("Repair pose files", wrap);
            GUILayout.Label(
                "Scans all .png and .dat pose files for broken layouts (mislabeled .dat, duplicated embedded PNG previews, etc.). Repairs run only when you click the button; details are written to the BepInEx log.",
                wrap);

            if (_poseFileRepairProgress.IsRunning)
            {
                float fraction = _poseFileRepairProgress.TotalFiles > 0
                    ? _poseFileRepairProgress.FilesScanned / (float)_poseFileRepairProgress.TotalFiles
                    : 0f;
                DrawProgressBar(fraction);
                GUILayout.Label(
                    $"{_poseFileRepairProgress.Phase}  Checked {_poseFileRepairProgress.FilesScanned} / {_poseFileRepairProgress.TotalFiles} — {_poseFileRepairProgress.FaultyFound} faulty found",
                    wrap);
                GUI.enabled = false;
                GUILayout.Button("Repairing…", PoseBrowserScale.H(24f));
                GUI.enabled = true;
            }
            else if (GUILayout.Button("Repair library…", PoseBrowserScale.H(24f)))
            {
                RunPoseFileRepair();
            }

            if (!string.IsNullOrEmpty(_lastDatRepairMessage))
                GUILayout.Label(_lastDatRepairMessage, wrap);
        }

        private GUIStyle? _optionsWrapStyle;

        private GUIStyle GetOptionsWrapStyle()
        {
            if (_optionsWrapStyle == null)
                _optionsWrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            return _optionsWrapStyle;
        }

        private bool DrawOptionsToggle(bool value, string label)
        {
            GUILayout.BeginHorizontal();
            bool v = GUILayout.Toggle(value, GUIContent.none, PoseBrowserScale.W(18f));
            GUILayout.Label(label, GetOptionsWrapStyle(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return v;
        }

        private void DrawOptionsWindowContent(int id)
        {
            _optionsScroll = GUILayout.BeginScrollView(_optionsScroll, false, true, GUILayout.ExpandHeight(true));

            var wrap = GetOptionsWrapStyle();

            PoseBrowserConfig.Register(SandboxServices.Config);
            GUILayout.Label("UI scale — enlarges the whole Pose Browser (text, buttons, panels, cards). Helps on 4K / high-DPI. Config: UI scale.", wrap);
            float uiScale = PoseBrowserScale.Factor;
            float newUiScale = GUILayout.HorizontalSlider(uiScale, PoseBrowserScale.MinFactor, PoseBrowserScale.MaxFactor);
            newUiScale = Mathf.Round(newUiScale / 0.05f) * 0.05f; // snap to predictable 0.05 steps
            if (Mathf.Abs(newUiScale - uiScale) > 0.001f)
            {
                PoseBrowserConfig.UiScale!.Value = Mathf.Clamp(newUiScale, PoseBrowserScale.MinFactor, PoseBrowserScale.MaxFactor);
                // Cache rebuild (styles + grid metrics) happens automatically next frame via EnsureStyleCachesMatchScale.
            }
            GUILayout.Label($"{PoseBrowserScale.Factor:0.00}× UI scale");

            GUILayout.Space(10f);
            GUILayout.Label("Card / thumbnail width (px). Same value is saved under BepInEx → Pose Browser → Card column width.", wrap);
            float newCard = GUILayout.HorizontalSlider(_cardCellSize, MinCardSize, MaxCardSize);
            if (Mathf.Abs(newCard - _cardCellSize) > 0.001f)
                _cardCellSize = newCard;
            GUILayout.Label($"{Mathf.Round(_cardCellSize)} px column");

            GUILayout.Space(10f);
            GUILayout.Label("Pagination: max items per page (0 = show all). Config: Items per page (grid).", wrap);
            GUILayout.BeginHorizontal();
            _itemsPerPageEdit = GUILayout.TextField(_itemsPerPageEdit, PoseBrowserScale.W(56f));
            if (GUILayout.Button("Apply", PoseBrowserScale.W(52f), PoseBrowserScale.H(22f)))
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

            GUILayout.Space(10f);
            GUILayout.Label("Thumbnail auto-capture: pause before each shot (seconds). Config: Auto-capture delay (seconds).", wrap);
            PoseBrowserConfig.Register(SandboxServices.Config);
            float captureDelay = PoseBrowserConfig.AutoCaptureDelaySeconds!.Value;
            float newCaptureDelay = GUILayout.HorizontalSlider(captureDelay, 0.5f, 30f);
            if (Mathf.Abs(newCaptureDelay - captureDelay) > 0.01f)
            {
                PoseBrowserConfig.AutoCaptureDelaySeconds!.Value = newCaptureDelay;
                SavePersistedOptions();
            }
            GUILayout.Label($"{newCaptureDelay:0.0} s");

            GUILayout.Space(10f);
            GUILayout.Label("Pose history: max snapshots per character (oldest removed). Config: History entries per character.", wrap);
            int historyMax = PoseBrowserConfig.HistoryMaxEntries!.Value;
            int newHistoryMax = Mathf.RoundToInt(GUILayout.HorizontalSlider(historyMax, 10f, 5000f));
            if (newHistoryMax != historyMax)
            {
                PoseBrowserConfig.HistoryMaxEntries!.Value = newHistoryMax;
                _poseHistory.TrimAllTimelines(newHistoryMax);
                _poseHistory.SaveToDiskIfDirty();
            }
            GUILayout.Label($"{newHistoryMax} entries");

            GUILayout.Space(10f);
            bool freezeAnim = PoseBrowserConfig.FreezeAnimationSpeedOnApply!.Value;
            bool newFreezeAnim = DrawOptionsToggle(
                freezeAnim,
                "Set animation speed to 0 when applying a pose or history entry");
            if (newFreezeAnim != freezeAnim)
                PoseBrowserConfig.FreezeAnimationSpeedOnApply!.Value = newFreezeAnim;

            GUILayout.Space(10f);
            bool newApplyLayout = DrawOptionsToggle(
                _applyGroupRelativePositions,
                "Apply stored relative positions when applying a group");
            if (newApplyLayout != _applyGroupRelativePositions)
            {
                _applyGroupRelativePositions = newApplyLayout;
                if (!newApplyLayout)
                {
                    _applyGroupRelativeHeights = false;
                    _applyGroupRelativeObjectScales = false;
                }
            }

            GUI.enabled = _applyGroupRelativePositions;
            bool newApplyHeights = DrawOptionsToggle(
                _applyGroupRelativeHeights,
                "Adjust relative layout for body height (saved per pose)");
            GUI.enabled = true;
            if (newApplyHeights != _applyGroupRelativeHeights)
                _applyGroupRelativeHeights = newApplyHeights;

            GUI.enabled = _applyGroupRelativePositions;
            bool newApplyScales = DrawOptionsToggle(
                _applyGroupRelativeObjectScales,
                "Adjust relative layout for object scale (saved per pose)");
            GUI.enabled = true;
            if (newApplyScales != _applyGroupRelativeObjectScales)
                _applyGroupRelativeObjectScales = newApplyScales;

            GUILayout.Space(10f);
            GUILayout.Label("Compact list: hover thumbnail preview width (px). Config: Compact hover thumbnail width.", wrap);
            float newHoverW = GUILayout.HorizontalSlider(_compactHoverThumbnailWidth, 80f, 600f);
            if (Mathf.Abs(newHoverW - _compactHoverThumbnailWidth) > 0.5f)
            {
                _compactHoverThumbnailWidth = Mathf.Round(newHoverW);
                PoseBrowserConfig.Register(SandboxServices.Config);
                PoseBrowserConfig.CompactHoverThumbnailWidth!.Value = _compactHoverThumbnailWidth;
                SavePersistedOptions();
            }
            GUILayout.Label($"{Mathf.Round(_compactHoverThumbnailWidth)} px");

            GUILayout.Space(14f);
            DrawHotkeyOptionsSection(wrap);

            GUILayout.Space(12f);
            DrawPoseFileRepairSection();

            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select all filtered", PoseBrowserScale.H(24f)))
                SelectAllMatchingDisplayResults();
            if (GUILayout.Button("Deselect all", PoseBrowserScale.H(24f)))
                ClearAllSelection();
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();

            if (GUILayout.Button("Close panel", PoseBrowserScale.H(26f)))
            {
                SavePersistedOptions();
                _showOptionsPane = false;
            }

            if (GUI.changed)
                SavePersistedOptions();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        // ── Resize ──

        private static float WindowChromeHorizontalPadding()
        {
            var w = GUI.skin.window;
            return w.padding.left + w.padding.right + w.border.left + w.border.right + 8f;
        }

        private float ComputeContentMinimumWindowWidth()
        {
            if (_layoutTier == PoseBrowserLayoutTier.CompactList)
                return ComputeCompactListMinimumWindowWidth();
            if (_layoutTier != PoseBrowserLayoutTier.Normal)
                return EffectiveLayoutMinWidthFor(_layoutTier);

            float treeGrid = TreePanelWidth + GridPanelChromePad + MinCardSize + PoseCardHorizontalMarginBudget() + VerticalScrollbarWidth();
            return Mathf.Max(NormalTopBarMinWidth, treeGrid) + WindowChromeHorizontalPadding();
        }

        private float EffectiveResizeMinWidth() =>
            Mathf.Max(LayoutMinWidth, ComputeContentMinimumWindowWidth());

        private void HandleResize()
        {
            Event e = Event.current;
            if (e == null) return;

            float minW = EffectiveResizeMinWidth();
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
                windowRect.width = Mathf.Clamp(e.mousePosition.x - windowRect.x, minW, LayoutMaxWidth);
                windowRect.height = Mathf.Clamp(e.mousePosition.y - windowRect.y, LayoutMinHeight, LayoutMaxHeight);
                e.Use();
            }
            else if (_isResizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _isResizing = false;
                windowRect.width = Mathf.Clamp(windowRect.width, minW, LayoutMaxWidth);
                windowRect.height = Mathf.Clamp(windowRect.height, LayoutMinHeight, LayoutMaxHeight);
                CaptureWindowRectForCurrentTier();
                SavePersistedOptions();
                e.Use();
            }
        }

        private static string PersistedOptionsPath =>
            PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_browser_options.json");

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
                        float delay = data.autoCaptureDelaySeconds > 0f ? data.autoCaptureDelaySeconds : 2f;
                        PoseBrowserConfig.AutoCaptureDelaySeconds!.Value = Mathf.Clamp(delay, 0.5f, 30f);
                        SandboxServices.Config.Save();
                    }
                }

                if (data.optionsVersion >= 5)
                {
                    PoseBrowserConfig.Register(SandboxServices.Config);
                    float delay = data.autoCaptureDelaySeconds > 0f ? data.autoCaptureDelaySeconds : 2f;
                    PoseBrowserConfig.AutoCaptureDelaySeconds!.Value = Mathf.Clamp(delay, 0.5f, 30f);
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

                if (data.optionsVersion >= 6)
                    _compactListShowTree = data.compactListShowTree;

                if (data.optionsVersion >= 7)
                {
                    if (data.listNoTreeWindowW > 10f)
                    {
                        _savedListNoTreeW = data.listNoTreeWindowW;
                        _savedListNoTreeH = data.listNoTreeWindowH;
                    }
                }
                else if (_savedListW > 10f)
                {
                    _savedListNoTreeW = Mathf.Max(
                        CompactListMinWidthNoTree,
                        _savedListW - TreePanelWidth - GridPanelChromePad);
                    _savedListNoTreeH = _savedListH > 10f ? _savedListH : CompactListDefaultHeight;
                }

                if (data.optionsVersion >= 10)
                {
                    _tagFilterGroupsMode = ClampDisplayFilterMode(data.tagFilterGroupsMode);
                    _tagFilterThumbnailMode = ClampDisplayFilterMode(data.tagFilterThumbnailMode);
                }

                if (data.optionsVersion >= 11)
                    _applyGroupRelativePositions = data.applyGroupRelativePositions;
                else
                {
                    if (data.optionsVersion >= 8 && data.tagFilterExcludeGroups)
                        _tagFilterGroupsMode = PoseDisplayFilterMode.Exclude;
                    if (data.optionsVersion >= 9 && data.tagFilterExcludeNoThumbnail)
                        _tagFilterThumbnailMode = PoseDisplayFilterMode.Exclude;
                }

                if (data.optionsVersion >= 12)
                    _applyGroupRelativeHeights = data.applyGroupRelativeHeights;

                if (data.optionsVersion >= 14)
                    _applyGroupRelativeObjectScales = data.applyGroupRelativeObjectScales;

                if (data.optionsVersion >= 13)
                    _compactHoverThumbnailWidth = Mathf.Clamp(data.compactHoverThumbnailWidth, 80f, 600f);

                if (data.optionsVersion >= 15)
                {
                    if (data.stashFloatingW > 10f)
                    {
                        _savedStashFloatingW = data.stashFloatingW;
                        _savedStashFloatingH = data.stashFloatingH;
                        _savedStashFloatingX = data.stashFloatingX;
                        _savedStashFloatingY = data.stashFloatingY;
                    }

                    RestoreStashFloatingRectFromSaved();
                }

                if (data.optionsVersion >= 16)
                    _stashPreferUndocked = data.stashPreferUndocked;

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
                if (IsStashUndockedVisible)
                    CaptureStashFloatingRect();
                string path = PersistedOptionsPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new PoseBrowserPersistedOptions
                {
                    optionsVersion = PoseBrowserConfig.OptionsJsonVersion,
                    cardCellSize = _cardCellSize,
                    itemsPerPage = _itemsPerPage,
                    autoCaptureDelaySeconds = PoseBrowserConfig.AutoCaptureDelaySeconds?.Value ?? 2f,
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
                    listNoTreeWindowW = _savedListNoTreeW,
                    listNoTreeWindowH = _savedListNoTreeH,
                    miniWindowW = _savedMiniW,
                    miniWindowH = _savedMiniH,
                    miniWindowX = _savedMiniX,
                    miniWindowY = _savedMiniY,
                    compactListShowTree = _compactListShowTree,
                    tagFilterGroupsMode = (int)_tagFilterGroupsMode,
                    tagFilterThumbnailMode = (int)_tagFilterThumbnailMode,
                    applyGroupRelativePositions = _applyGroupRelativePositions,
                    applyGroupRelativeHeights = _applyGroupRelativeHeights,
                    applyGroupRelativeObjectScales = _applyGroupRelativeObjectScales,
                    compactHoverThumbnailWidth = _compactHoverThumbnailWidth,
                    stashFloatingW = _savedStashFloatingW,
                    stashFloatingH = _savedStashFloatingH,
                    stashFloatingX = _savedStashFloatingX,
                    stashFloatingY = _savedStashFloatingY,
                    stashPreferUndocked = _stashPreferUndocked
                };
                FileEx.WriteAllTextAtomic(
                    path,
                    JsonUtility.ToJson(data, true),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
                if (PoseBrowserConfig.CompactHoverThumbnailWidth != null)
                    _compactHoverThumbnailWidth = Mathf.Clamp(PoseBrowserConfig.CompactHoverThumbnailWidth.Value, 80f, 600f);
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
                if (PoseBrowserConfig.CompactHoverThumbnailWidth != null)
                    PoseBrowserConfig.CompactHoverThumbnailWidth.Value = _compactHoverThumbnailWidth;
                SandboxServices.Config.Save();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not write BepInEx Pose Browser config: {ex.Message}");
            }
        }

        private void MaybeProcessPoseBrowserWindowHotkeys()
        {
            if (_thumbCapture.IsActive) return;
            if (GUIUtility.keyboardControl != 0) return;

            PoseBrowserConfig.Register(SandboxServices.Config);

            if (PoseBrowserConfig.HotkeyToggleVisible!.Value.IsDown())
            {
                TogglePoseBrowserVisible();
                return;
            }

            if (PoseBrowserConfig.HotkeyToggleUndockedStash!.Value.IsDown())
            {
                ToggleUndockedStashViaHotkey();
                return;
            }

            if (PoseBrowserConfig.HotkeyUndo!.Value.IsDown())
            {
                PerformPoseHistoryUndo();
                return;
            }

            if (PoseBrowserConfig.HotkeyRedo!.Value.IsDown())
            {
                PerformPoseHistoryRedo();
                return;
            }

            if (!isVisible) return;

            if (PoseBrowserConfig.HotkeyToggleMinimize!.Value.IsDown())
            {
                TogglePoseBrowserMinimize();
                return;
            }
        }

        private void TogglePoseBrowserVisible()
        {
            var gui = FindObjectOfType<SandboxGUI>();
            if (gui == null)
            {
                SetVisible(!isVisible);
                return;
            }

            if (gui.IsPoseBrowserVisible)
                ClosePoseBrowser();
            else
                gui.SetPoseBrowserVisible(true);
        }

        private void TogglePoseBrowserMinimize()
        {
            if (_isMinimized)
            {
                RestoreFromMinimize();
                return;
            }

            Vector2 chipScreen = GUIUtility.GUIToScreenPoint(
                new Vector2(windowRect.xMax - MinimizedChipSize, windowRect.y + 8f));
            MinimizePoseBrowser(chipScreen);
        }

        private void MaybeProcessPoseBrowserHotkeys()
        {
            if (_thumbCapture.IsActive) return;
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
            {
                HotkeyStepPose(-1);
                return;
            }
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
            SelectCompactPose(idx);
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

        private const float HotkeyBindingColumnWidthBase = 128f;
        private float HotkeyBindingColumnWidth => PoseBrowserScale.Px(HotkeyBindingColumnWidthBase);

        private GUIStyle? _hotkeySectionBoxStyle;
        private GUIStyle? _hotkeyHeaderStyle;
        private GUIStyle? _hotkeyRowBoxStyle;
        private GUIStyle? _hotkeyActionStyle;
        private GUIStyle? _hotkeyBindingBadgeStyle;
        private GUIStyle? _hotkeyUnassignedBadgeStyle;

        private void InitHotkeyOptionStyles()
        {
            if (_hotkeySectionBoxStyle != null)
                return;

            _hotkeySectionBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };
            _hotkeySectionBoxStyle.normal.background = MakeTex(8, 8, new Color(0.09f, 0.09f, 0.11f, 1f));
            _hotkeySectionBoxStyle.border = GUI.skin.box.border;

            _hotkeyHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.72f, 0.76f, 0.82f, 1f) }
            };

            _hotkeyRowBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 5, 5),
                margin = new RectOffset(0, 0, 3, 3)
            };
            _hotkeyRowBoxStyle.normal.background = MakeTex(8, 8, new Color(0.12f, 0.12f, 0.15f, 1f));
            _hotkeyRowBoxStyle.border = GUI.skin.box.border;

            _hotkeyActionStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.93f, 0.96f, 1f) }
            };

            _hotkeyBindingBadgeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 4, 4),
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            _hotkeyBindingBadgeStyle.normal.background = MakeTex(6, 6, new Color(0.18f, 0.24f, 0.32f, 1f));
            _hotkeyBindingBadgeStyle.normal.textColor = new Color(0.82f, 0.9f, 1f, 1f);
            _hotkeyBindingBadgeStyle.border = GUI.skin.box.border;

            _hotkeyUnassignedBadgeStyle = new GUIStyle(_hotkeyBindingBadgeStyle)
            {
                fontStyle = FontStyle.Italic
            };
            _hotkeyUnassignedBadgeStyle.normal.background = MakeTex(6, 6, new Color(0.14f, 0.14f, 0.16f, 1f));
            _hotkeyUnassignedBadgeStyle.normal.textColor = new Color(0.55f, 0.58f, 0.62f, 1f);
        }

        private void DrawHotkeyOptionsSection(GUIStyle introStyle)
        {
            GUILayout.Label("Keyboard shortcuts", introStyle);
            GUILayout.Label(
                "Read-only overview. Assign keys in BepInEx → Configuration Manager → Pose Browser · Keyboard shortcuts.",
                introStyle);
            GUILayout.Space(6f);

            InitHotkeyOptionStyles();
            PoseBrowserConfig.Register(SandboxServices.Config);

            GUILayout.BeginVertical(_hotkeySectionBoxStyle!);
            DrawHotkeyColumnHeader();
            DrawHotkeyReadonlyRow("Next browse (folder step)", PoseBrowserConfig.HotkeyNextBrowse);
            DrawHotkeyReadonlyRow("Previous browse (folder step)", PoseBrowserConfig.HotkeyPrevBrowse);
            DrawHotkeyReadonlyRow("Next pose", PoseBrowserConfig.HotkeyNextPose);
            DrawHotkeyReadonlyRow("Previous pose", PoseBrowserConfig.HotkeyPrevPose);
            DrawHotkeyReadonlyRow("Toggle Pose Browser window", PoseBrowserConfig.HotkeyToggleVisible);
            DrawHotkeyReadonlyRow("Toggle minimize (PB chip)", PoseBrowserConfig.HotkeyToggleMinimize);
            DrawHotkeyReadonlyRow("Undo pose change", PoseBrowserConfig.HotkeyUndo);
            DrawHotkeyReadonlyRow("Redo pose change", PoseBrowserConfig.HotkeyRedo);
            DrawHotkeyReadonlyRow("Toggle undocked pose stash", PoseBrowserConfig.HotkeyToggleUndockedStash);
            GUILayout.EndVertical();
        }

        private void DrawHotkeyColumnHeader()
        {
            GUILayout.BeginHorizontal(PoseBrowserScale.H(20f));
            GUILayout.Label("Action", _hotkeyHeaderStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("Key", _hotkeyHeaderStyle, GUILayout.Width(HotkeyBindingColumnWidth));
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        private void DrawHotkeyReadonlyRow(string label, ConfigEntry<KeyboardShortcut>? entry)
        {
            if (entry == null)
                return;

            InitHotkeyOptionStyles();
            KeyboardShortcut shortcut = entry.Value;
            bool unassigned = IsHotkeyUnassigned(shortcut);
            string bindingText = FormatHotkeyBindingText(shortcut);
            var badgeStyle = unassigned ? _hotkeyUnassignedBadgeStyle! : _hotkeyBindingBadgeStyle!;

            GUILayout.BeginVertical(_hotkeyRowBoxStyle!);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                new GUIContent(label, entry.Description?.Description ?? ""),
                _hotkeyActionStyle,
                GUILayout.ExpandWidth(true),
                PoseBrowserScale.MinH(26f));
            GUILayout.Label(bindingText, badgeStyle, GUILayout.Width(HotkeyBindingColumnWidth), PoseBrowserScale.MinH(26f));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static bool IsHotkeyUnassigned(KeyboardShortcut shortcut) =>
            shortcut.MainKey == KeyCode.None;

        private static string FormatHotkeyBindingText(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
                return "Not assigned";

            string text = shortcut.ToString();
            if (StringEx.IsNullOrWhiteSpace(text) ||
                string.Equals(text, "Not set", StringComparison.OrdinalIgnoreCase))
                return "Not assigned";

            return text;
        }
    }

    [Serializable]
    internal sealed class PoseBrowserPersistedOptions
    {
        public int optionsVersion;
        public float cardCellSize = 140f;
        public int itemsPerPage;
        public float autoCaptureDelaySeconds = 2f;
        public int poseSortMode = 3;
        public bool sortAscending = true;
        public int layoutTier;
        public float fullWindowW, fullWindowH, fullWindowX, fullWindowY;
        public float listWindowW, listWindowH, listWindowX, listWindowY;
        public float listNoTreeWindowW, listNoTreeWindowH;
        public float miniWindowW, miniWindowH, miniWindowX, miniWindowY;
        public bool compactListShowTree = true;
        public int tagFilterGroupsMode;
        public int tagFilterThumbnailMode;
        public bool tagFilterExcludeGroups;
        public bool tagFilterExcludeNoThumbnail;
        public bool applyGroupRelativePositions = true;
        public bool applyGroupRelativeHeights;
        public bool applyGroupRelativeObjectScales;
        public float compactHoverThumbnailWidth = 200f;
        public float stashFloatingW;
        public float stashFloatingH;
        public float stashFloatingX;
        public float stashFloatingY;
        public bool stashPreferUndocked;
    }
}
