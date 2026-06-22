using System;
using System.Collections;
using System.Collections.Generic;
using KKAPI.Studio;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow : SubWindow
    {
        private enum AnimBrowserViewMode
        {
            Grid = 0,
            List = 1
        }

        private const float DockedPaneGapBase = 4f;
        private float DockedPaneGap => AnimBrowserScale.Px(DockedPaneGapBase);
        private const float TreePanelWidthDefaultBase = 308f;
        private const float TopBarRowHeightBase = 24f;
        private float TopBarRowHeight => AnimBrowserScale.Px(TopBarRowHeightBase);
        private float TopBarTotalHeight => TopBarRowHeight * 2f + AnimBrowserScale.Px(4f);
        private const float TopBarControlHeightBase = 22f;
        private const float GridPanelChromePadBase = 24f;
        private float GridPanelChromePad => AnimBrowserScale.Px(GridPanelChromePadBase);
        private const float ControlsPaneDefaultWidthBase = 384f;
        private float ControlsPaneDefaultWidth => AnimBrowserScale.Px(ControlsPaneDefaultWidthBase);
        private const float MinCardSize = 96f;
        private const float MaxCardSize = 280f;
        private const float GridCellGapBase = 3f;
        private float GridCellGap => AnimBrowserScale.Px(GridCellGapBase);
        private const float ListRowHeightBase = 24f;
        private float ListRowHeight => AnimBrowserScale.Px(ListRowHeightBase);
        private const float MinWindowWidthBase = 640f;
        private float MinWindowWidth => AnimBrowserScale.Px(MinWindowWidthBase);
        private const float MinWindowHeightBase = 360f;
        private float MinWindowHeight => AnimBrowserScale.Px(MinWindowHeightBase);
        private const float ResizeHandleSizeBase = 18f;
        private float ResizeHandleSize => AnimBrowserScale.Px(ResizeHandleSizeBase);
        private const float GridWindowDefaultWidthBase = 1020f;
        private const float GridWindowDefaultHeightBase = 560f;
        private const float ListWindowDefaultWidthBase = 720f;
        private const float ListWindowDefaultHeightBase = 480f;
        private const float WindowChromeScrollbarWidthBase = 20f;

        private static float LayoutMaxWidth => AnimBrowserScale.Px(1400f);
        private static float LayoutMaxHeight => AnimBrowserScale.Px(1000f);

        private float TreePanelWidth => _treePanelWidth;

        private static readonly GUIContent GcViewGrid = new GUIContent("Grid", "Thumbnail grid view");
        private static readonly GUIContent GcViewList = new GUIContent("List", "Compact list without thumbnails");
        private static readonly GUIContent GcControlsOn = new GUIContent("Controls ▶", "Hide controls pane");
        private static readonly GUIContent GcControlsOff = new GUIContent("Controls", "Show controls pane");
        private static readonly GUIContent GcCatalogLoading = new GUIContent("Loading animation catalog…");
        private static readonly GUIContent GcCatalogEmpty = new GUIContent("No animations in catalog.");
        private static readonly GUIContent GcSearchNoResults = new GUIContent("No animations match the search.");
        private static readonly GUIContent GcLoadPhaseCatalog = new GUIContent("Catalog", "Loading animation catalog in the background");
        private static readonly GUIContent GcLoadPhaseTree = new GUIContent("Tree", "Building display tree in the background");
        private static readonly GUIContent GcLoadPhaseCache = new GUIContent("Cache", "Caching animation lists in the background");

        private readonly AnimCatalogService _catalog = new AnimCatalogService();
        private readonly AnimGroupStore _groupStore = new AnimGroupStore();
        private readonly AnimBrowserPersistedOptions _options = new AnimBrowserPersistedOptions();
        private readonly List<AnimDisplayEntry> _visibleEntries = new List<AnimDisplayEntry>();
        private readonly List<bool> _visibleEntrySearchDimmed = new List<bool>();
        private readonly List<AnimViewNode> _flatTreeNodes = new List<AnimViewNode>();
        private readonly List<TreeRowDrawState> _treeDrawRows = new List<TreeRowDrawState>();
        private bool _pendingFlatTreeRebuild;
        private bool _flatTreeValid;
        private bool _pendingCatalogReadyUi;
        private bool _pendingDisplayTreeInvalidate;
        private AnimDisplayCatalog _displayCatalog = null!;
        private bool _hideNonStudioCatalogAnimations = true;

        private AnimBrowserViewMode _viewMode = AnimBrowserViewMode.Grid;
        private Rect _controlsWindowRect;
        private Vector2 _treeScroll;
        private Vector2 _gridScroll;
        private Vector2 _listScroll;
        private float _gridScrollViewportH;
        private float _listScrollViewportH;
        private AnimViewNode? _selectedTreeNode;
        private float _cardCellSize = 120f;
        private float _treePanelWidth;
        private bool _isResizing;
        private float _cachedStyleScaleFactor = float.NaN;
        private string _gridCountLabel = string.Empty;
        private bool _autoTranslateEnabled = true;
        private float _nextTranslationResolveTime;
        private Coroutine? _catalogWarmupCoroutine;
        private Coroutine? _displayWarmupCoroutine;
        private bool _treeGroupsCollapsedByDefault;

        private readonly AnimBrowserUpdateCheck _updateCheck = new AnimBrowserUpdateCheck();
        private Coroutine? _updateCheckCoroutine;

        protected override void Start()
        {
            base.Start();
            windowID = SandboxImguiWindowIds.AnimBrowser.Main;
            windowTitle = "Anim Browser";
            AnimBrowserPersistence.Load(_options);
            EnsurePersistedWindowRectsInitialized();
            _viewMode = (AnimBrowserViewMode)Mathf.Clamp(_options.viewMode, 0, 1);
            _showCharacterConfigPane = _options.showCharacterConfigPane;
            _showOptionsPane = _options.showOptionsPane;
            _showHelpPane = _options.showHelpPane;
            _cardCellSize = Mathf.Clamp(_options.cardCellSize, MinCardSize, MaxCardSize);
            _controlsGroupByProximity = _options.controlsGroupByProximity;
            _hideNonStudioCatalogAnimations = _options.hideNonStudioCatalogAnimations;
            ApplyPreviewCameraOptionsToStage();
            ApplyTreePanelWidthFromOptions();
            RestoreWindowRectForViewMode(_viewMode);
            SyncDockedPaneRectsToMainWindow();
            RestoreControlsPaneStateFromOptions();
            EnsureMinimumOptionsPaneWidth();
            _displayCatalog = new AnimDisplayCatalog(_catalog, _groupStore);
            _displayCatalog.SetHideNonStudioCatalogAnimations(_hideNonStudioCatalogAnimations);
            _groupStore.Changed += OnGroupStoreChanged;
            ApplyAutoTranslateConfig(force: true);
            StudioAutoTranslation.TranslationsUpdated += OnAutoTranslationsUpdated;
            _groupStore.Load();
            InitControlsState();

            // Fire-and-forget update check. The coroutine swallows all network/parse errors and
            // lands in Unavailable, so a failed check never disrupts the window.
            try
            {
                _updateCheckCoroutine = StartCoroutine(_updateCheck.RunCheck());
            }
            catch (System.Exception ex)
            {
                SandboxServices.Log.LogDebug("Anim Browser update check could not start: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            StudioAutoTranslation.TranslationsUpdated -= OnAutoTranslationsUpdated;
            _groupStore.Changed -= OnGroupStoreChanged;
            if (_updateCheckCoroutine != null)
            {
                StopCoroutine(_updateCheckCoroutine);
                _updateCheckCoroutine = null;
            }
            if (_catalogWarmupCoroutine != null)
            {
                StopCoroutine(_catalogWarmupCoroutine);
                _catalogWarmupCoroutine = null;
            }
            if (_displayWarmupCoroutine != null)
            {
                StopCoroutine(_displayWarmupCoroutine);
                _displayWarmupCoroutine = null;
            }
            SavePersistedOptions();
        }

        private void Update()
        {
            ProcessDeferredUiInvalidation();
            TryStartCatalogWarmup();
            HandleControlsHotkeys();

            TickPreviewSystem();

            if (!isVisible)
                return;

            ApplyAutoTranslateConfig(force: false);

            if (!StudioAutoTranslation.IsAvailable && Time.unscaledTime >= _nextTranslationResolveTime)
            {
                _nextTranslationResolveTime = Time.unscaledTime + 2f;
                StudioAutoTranslation.RetryResolution();
            }
        }

        private void ProcessDeferredUiInvalidation()
        {
            if (_pendingCatalogReadyUi)
            {
                _pendingCatalogReadyUi = false;
                if (!_treeGroupsCollapsedByDefault)
                {
                    CollapseAllTreeGroups();
                    _treeGroupsCollapsedByDefault = true;
                }
                InvalidateAnimBrowserViewCaches();
            }

            if (_pendingDisplayTreeInvalidate)
            {
                _pendingDisplayTreeInvalidate = false;
                _displayCatalog.InvalidateTree();
                _flatTreeValid = false;
                _flatTreeNodes.Clear();
            }

            if (!_pendingFlatTreeRebuild)
                return;
            _pendingFlatTreeRebuild = false;
            _flatTreeValid = false;
            _flatTreeNodes.Clear();
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (!visible)
            {
                _isMinimized = false;
                OnMainAnimBrowserHidden();
            }
        }

        public override void DrawWindow()
        {
            var prevSkin = GUI.skin;
            GUI.skin = GetScaledAnimBrowserSkin(prevSkin);
            try
            {
                DrawWindowScaled();
            }
            finally
            {
                GUI.skin = prevSkin;
            }
        }

        private void DrawWindowScaled()
        {
            EnsureStyleCachesMatchScale();

            if (_showUndockedControls)
                DrawUndockedControlsWindow();

            if (!isVisible)
                return;

            DrawThumbnailCaptureOverlay();

            if (_isMinimized)
            {
                DrawMinimizedRestoreChip();
                return;
            }

            HandleResize();
            float minW = EffectiveResizeMinWidth();
            float minH = MinWindowHeight;
            windowRect.width = Mathf.Clamp(windowRect.width, minW, LayoutMaxWidth);
            windowRect.height = Mathf.Clamp(windowRect.height, minH, LayoutMaxHeight);
            windowRect.x = Mathf.Clamp(windowRect.x, 4f, Mathf.Max(4f, Screen.width - windowRect.width - 4f));
            windowRect.y = Mathf.Clamp(windowRect.y, 4f, Mathf.Max(4f, Screen.height - windowRect.height - 4f));

            // GUILayout.Window grows to content min-size; that fights shrinking while dragging.
            float lockedW = windowRect.width;
            float lockedH = windowRect.height;
            var windowIn = new Rect(windowRect.x, windowRect.y, lockedW, lockedH);

            windowRect = GUILayout.Window(windowID, windowIn, DrawWindowContent, windowTitle);

            // Pin size back unconditionally — only drag-updated x/y from the returned rect are kept
            // (see PoseBrowser DrawWindow; prevents sub-pixel "creep" at some UI scales).
            windowRect.width = lockedW;
            windowRect.height = lockedH;
            if (!_isResizing)
            {
                windowRect.width = Mathf.Clamp(windowRect.width, minW, LayoutMaxWidth);
                windowRect.height = Mathf.Clamp(windowRect.height, minH, LayoutMaxHeight);
            }

            if (IsControlsDockedVisible || _showCharacterConfigPane || _showReviewPane || _showOptionsPane || _showHelpPane)
            {
                bool layoutPass = Event.current.type == EventType.Layout;
                if (layoutPass)
                    SyncAllDockedPaneRects();
                DrawAllDockedPanes();
            }

            // List-view hover preview popup, drawn on top of the window + docked panes.
            DrawListPreviewPopup();
        }

        protected override void DrawWindowContent(int id)
        {
            ProcessDeferredUiInvalidation();
            InitStyles();
            Color prevGuiColor = GUI.color;
            Color prevBgColor = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            try
            {
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                DrawTopBar();

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawTreePanel();
                DrawMainPanel();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                FinishMainWindowChrome(id);
                HandleDeselectHotkey();
            }
            finally
            {
                GUI.color = prevGuiColor;
                GUI.backgroundColor = prevBgColor;
            }
        }

        private void DrawTopBar()
        {
            float controlH = TopBarControlHeightBase;

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal(AnimBrowserScale.H(TopBarRowHeightBase));
            DrawWindowChromeButtons(controlH);
            GUILayout.Space(4f);
            bool gridMode = _viewMode == AnimBrowserViewMode.Grid;
            if (GUILayout.Button(gridMode ? GcViewGrid : GcViewList, AnimBrowserScale.W(56f), AnimBrowserScale.H(controlH)))
                ToggleViewMode();
            GUILayout.Space(6f);
            if (GUILayout.Button(IsAnyControlsVisible ? GcControlsOn : GcControlsOff, AnimBrowserScale.W(88f), AnimBrowserScale.H(controlH)))
                ToggleControlsFromMainWindow();
            if (GUILayout.Button(_showOptionsPane ? GcOptionsOn : GcOptionsOff, AnimBrowserScale.W(78f), AnimBrowserScale.H(controlH)))
            {
                _showOptionsPane = !_showOptionsPane;
                _options.showOptionsPane = _showOptionsPane;
                SavePersistedOptions();
            }
            if (GUILayout.Button(_showHelpPane ? GcHelpOn : GcHelpOff, AnimBrowserScale.W(64f), AnimBrowserScale.H(controlH)))
            {
                _showHelpPane = !_showHelpPane;
                _options.showHelpPane = _showHelpPane;
                SavePersistedOptions();
            }
            GUILayout.FlexibleSpace();
            DrawUpdateNotice(controlH);
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            GUILayout.BeginHorizontal(AnimBrowserScale.H(TopBarRowHeightBase));
            DrawAnimSearchBar();
            GUILayout.Space(8f);
            DrawTopBarCharacterSection(controlH);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>Right-aligned "Update vX.Y.Z" button shown only when a newer release exists.
        /// Clicking opens the direct download (or the releases page as fallback). Silent otherwise.</summary>
        private void DrawUpdateNotice(float controlHeight)
        {
            if (_updateCheck.State != AnimBrowserUpdateCheck.Status.UpdateAvailable)
                return;

            string remote = _updateCheck.RemoteVersion ?? "?";
            string url = _updateCheck.DownloadUrl ?? AnimBrowserVersionInfo.LatestReleasePageUrl;
            bool directDll = url.IndexOf(".dll", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string tip = directDll
                ? "A newer Anim Browser is available — click to download the updated .dll directly."
                : "A newer Anim Browser is available — click to open the releases page.";

            var label = new GUIContent("Update v" + remote, tip);
            if (GUILayout.Button(label, GUI.skin.button, AnimBrowserScale.H(controlHeight), AnimBrowserScale.MinW(108f)))
            {
                try
                {
                    Application.OpenURL(url);
                }
                catch (System.Exception ex)
                {
                    SandboxServices.Log.LogWarning("Anim Browser: opening update URL failed: " + ex.Message);
                }
            }
        }

        private void DrawTreePanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(TreePanelWidth), GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Categories", GUILayout.Width(AnimBrowserScale.Px(72f)));
            DrawTreeBackgroundLoadIndicator();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("↻", AnimBrowserScale.W(24f), AnimBrowserScale.H(18f)))
                RefreshCatalog();
            GUILayout.EndHorizontal();

            GUILayout.Space(2f);

            _treeScroll = GUILayout.BeginScrollView(
                _treeScroll,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            if (IsCatalogLoading())
            {
                if (Event.current.type == EventType.Layout)
                    _treeDrawRows.Clear();
                GUILayout.Label(GcCatalogLoading);
            }
            else
            {
                RefreshTreeDrawSnapshotIfNeeded();
                if (_treeDrawRows.Count == 0)
                    GUILayout.Label(GcCatalogEmpty);
                else
                {
                    for (int i = 0; i < _treeDrawRows.Count; i++)
                        DrawTreeNodeRow(_treeDrawRows[i]);
                }
            }

            GUILayout.EndScrollView();
            DrawTreeActionBar();
            GUILayout.EndVertical();
        }

        private void DrawMainPanel()
        {
            float gridAvailW = Mathf.Max(120f, windowRect.width - TreePanelWidth - GridPanelChromePad);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MaxWidth(gridAvailW));
            if (IsCatalogLoading())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(GcCatalogLoading);
                GUILayout.FlexibleSpace();
            }
            else
            {
                RebuildVisibleItemsIfNeeded();
                if (!IsLeafContentNode(_selectedTreeNode))
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(GcSelectSubcategory);
                    GUILayout.FlexibleSpace();
                }
                else if (_visibleEntries.Count == 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(IsSearchActive ? GcSearchNoResults : GcCatalogEmpty);
                    GUILayout.FlexibleSpace();
                }
                else if (_viewMode == AnimBrowserViewMode.Grid)
                    DrawGridView(gridAvailW);
                else
                    DrawListView();
            }
            DrawContentActionBar(gridAvailW);
            GUILayout.EndVertical();
        }

        private void DrawGridView(float gridAvailW)
        {
            if (Event.current.type == EventType.Layout || _gridLayoutColumns <= 0 ||
                !Mathf.Approximately(_gridLayoutAvailW, gridAvailW))
                UpdateGridLayoutIfNeeded(gridAvailW);

            int columns = _gridLayoutColumns;
            float cellInnerW = _gridLayoutCellInnerW;
            float contentWidth = _gridLayoutContentWidth;
            float cardOuterH = _gridLayoutCardOuterH;
            float rowH = cardOuterH + GridCellGap;
            int rowCount = columns > 0 ? Mathf.CeilToInt(_visibleEntries.Count / (float)columns) : 0;

            if (Event.current.type == EventType.Repaint)
                _gridCountLabel = _visibleEntries.Count + " item" + (_visibleEntries.Count == 1 ? string.Empty : "s");

            GUILayout.BeginHorizontal(AnimBrowserScale.H(22f));
            GUILayout.Label(_gridCountLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            _gridScroll = GUILayout.BeginScrollView(
                _gridScroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Width(gridAvailW),
                GUILayout.MaxWidth(gridAvailW),
                GUILayout.ExpandHeight(true));

            float viewportH = _gridScrollViewportH > 1f ? _gridScrollViewportH : windowRect.height;
            viewportH = Mathf.Min(viewportH, Mathf.Max(200f, windowRect.height));
            float scrollY = _gridScroll.y;
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollY / rowH));
            int visibleRows = Mathf.CeilToInt(viewportH / rowH) + 1;
            int lastRow = Mathf.Min(rowCount - 1, firstRow + visibleRows);
            firstRow = Mathf.Max(0, firstRow - 1);
            lastRow = Mathf.Min(rowCount - 1, lastRow + 1);

            if (firstRow > 0)
                GUILayout.Space(firstRow * rowH);

            for (int row = firstRow; row <= lastRow; row++)
            {
                if (row > firstRow)
                    GUILayout.Space(GridCellGap);

                GUILayout.BeginHorizontal(
                    GUILayout.Width(contentWidth),
                    GUILayout.MaxWidth(contentWidth),
                    GUILayout.MinHeight(cardOuterH),
                    GUILayout.ExpandWidth(false));

                for (int col = 0; col < columns; col++)
                {
                    if (col > 0)
                        GUILayout.Space(GridCellGap);

                    int idx = row * columns + col;
                    if (idx >= _visibleEntries.Count)
                    {
                        GUILayout.Space(cellInnerW);
                        continue;
                    }

                    DrawGridEntry(_visibleEntries[idx], idx, cellInnerW, cardOuterH, _visibleEntrySearchDimmed[idx]);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            int rowsAfter = rowCount - (lastRow + 1);
            if (rowsAfter > 0)
                GUILayout.Space(rowsAfter * rowH);

            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
            {
                Rect svRect = GUILayoutUtility.GetLastRect();
                if (svRect.height > 1f)
                    _gridScrollViewportH = svRect.height;
            }
        }

        private void DrawGridEntry(AnimDisplayEntry entry, int visibleIndex, float cellInnerW, float cardOuterH, bool searchDimmed)
        {
            if (entry.IsGroup)
                DrawGroupGridCard(entry, visibleIndex, cellInnerW, cardOuterH, searchDimmed);
            else
                DrawSingleGridCard(entry, visibleIndex, cellInnerW, cardOuterH, searchDimmed);
        }

        private void DrawSingleGridCard(AnimDisplayEntry entry, int visibleIndex, float cellInnerW, float cardOuterH, bool searchDimmed)
        {
            AnimGridItem item = entry.Single!;
            InitStyles();
            GUIStyle cardBox = IsEntrySelected(entry) ? _animCardSelectedStyle! : _animCardBaseStyle!;
            float innerW = Mathf.Max(40f, cellInnerW);

            GUILayout.BeginVertical(
                cardBox,
                GUILayout.Width(cellInnerW),
                GUILayout.MaxWidth(cellInnerW),
                GUILayout.MinHeight(cardOuterH),
                GUILayout.Height(cardOuterH),
                GUILayout.ExpandWidth(false));

            Rect thumbRect = GUILayoutUtility.GetRect(innerW, innerW, GUILayout.Width(innerW), GUILayout.Height(innerW));
            // Prefer a captured thumbnail; otherwise fall back to the solid placeholder.
            Texture2D? tex = GetStoredThumbnail(entry);
            if (tex == null)
            {
                tex = item.Thumbnail;
                if (tex == null && !item.ThumbnailFailed && !item.ThumbnailRequested)
                {
                    item.ThumbnailRequested = true;
                    item.Thumbnail = AnimThumbnailService.GetPlaceholder(item, Mathf.RoundToInt(innerW));
                    tex = item.Thumbnail;
                }
            }

            Rect cbRect = GridCheckboxRect(thumbRect);
            Event ev = Event.current;
            Color prevGuiColor = GUI.color;
            BeginSearchDimDraw(searchDimmed, ref prevGuiColor);
            if (ev.type == EventType.Repaint)
            {
                if (IsPreviewHoverIndex(visibleIndex))
                    DrawPreviewInThumbRect(thumbRect);
                else if (tex != null)
                    GUI.DrawTexture(thumbRect, tex, ScaleMode.ScaleToFit, false);
                else
                    GUI.Box(thumbRect, GUIContent.none);
                DrawCheckboxVisual(cbRect, IsEntrySelected(entry));
            }
            else if (ev.type == EventType.MouseDown && (ev.button == 0 || ev.button == 1))
            {
                if (ev.button == 0 && TryHandleEntryCheckbox(entry, visibleIndex, cbRect, ev))
                {
                }
                else if (thumbRect.Contains(ev.mousePosition))
                {
                    HandleEntryActivate(entry, visibleIndex);
                    ev.Use();
                }
            }

            EmitPreviewHoverSensor(thumbRect, visibleIndex);

            var titleRowRect = GUILayoutUtility.GetRect(
                innerW,
                CardNameRowH,
                GUILayout.Width(innerW),
                GUILayout.MaxWidth(innerW),
                GUILayout.ExpandWidth(false));
            float nameW = Mathf.Max(20f, titleRowRect.width - CardTextPadH * 2f);
            var nameRect = new Rect(titleRowRect.x + CardTextPadH, titleRowRect.y, nameW, titleRowRect.height);

            if (ev.type == EventType.Repaint)
            {
                string shownName = GetCachedTruncatedName(item, _animCardNameStyle!, nameW);
                string catalogName = _displayCatalog.GetAnimationRenameSeed(item);
                string tooltip = _displayCatalog.GetItemDisplayLabel(item);
                if (!string.Equals(tooltip, catalogName, StringComparison.Ordinal))
                    tooltip = catalogName + "\n" + tooltip;
                if (searchDimmed)
                    tooltip += SearchDimmedEntryTooltipSuffix;
                GUI.Label(nameRect, new GUIContent(shownName, tooltip), _animCardNameStyle!);
            }
            else if (ev.type == EventType.MouseDown && ev.button == 0 && nameRect.Contains(ev.mousePosition))
            {
                HandleEntryActivate(entry, visibleIndex);
                ev.Use();
            }

            EmitPreviewHoverSensor(nameRect, visibleIndex);

            EndSearchDimDraw(searchDimmed, prevGuiColor);
            GUILayout.EndVertical();
        }

        private void DrawListView()
        {
            InitStyles();
            if (Event.current.type == EventType.Repaint)
                _gridCountLabel = _visibleEntries.Count + " item" + (_visibleEntries.Count == 1 ? string.Empty : "s");

            GUILayout.BeginHorizontal(AnimBrowserScale.H(22f));
            GUILayout.Label(_gridCountLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            _listScroll = GUILayout.BeginScrollView(
                _listScroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.ExpandHeight(true));

            float viewportH = _listScrollViewportH > 1f ? _listScrollViewportH : windowRect.height;
            viewportH = Mathf.Min(viewportH, Mathf.Max(200f, windowRect.height));
            float rowH = ListRowHeight;
            float scrollY = _listScroll.y;
            int first = Mathf.Max(0, Mathf.FloorToInt(scrollY / rowH) - 1);
            int visible = Mathf.CeilToInt(viewportH / rowH) + 3;
            int last = Mathf.Min(_visibleEntries.Count - 1, first + visible);

            if (first > 0)
                GUILayout.Space(first * rowH);

            for (int i = first; i <= last; i++)
                DrawListEntryRow(_visibleEntries[i], i, _visibleEntrySearchDimmed[i]);

            int after = _visibleEntries.Count - (last + 1);
            if (after > 0)
                GUILayout.Space(after * rowH);

            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
            {
                Rect svRect = GUILayoutUtility.GetLastRect();
                if (svRect.height > 1f)
                    _listScrollViewportH = svRect.height;
            }
        }

        private void DrawTreeNodeRow(TreeRowDrawState row)
        {
            AnimViewNode node = row.Node;
            GUILayout.BeginHorizontal();

            GUILayout.Space(node.Depth * 16f);

            if (row.HasExpand)
            {
                string arrow = IsNodeExpanded(node) ? "▼" : "►";
                if (GUILayout.Button(arrow, AnimBrowserScale.W(20f), AnimBrowserScale.H(20f)))
                {
                    ToggleNodeExpanded(node);
                    InvalidateAnimBrowserViewCaches();
                }
            }
            else
            {
                GUILayout.Space(24f);
            }

            bool selected = _selectedTreeNodeIds.Contains(node.Id);
            var style = selected ? _treeNodeSelectedStyle! : _treeNodeStyle!;
            float treeLabelW = TreeNodeLabelMaxWidth(node.Depth);
            string shownName = GetCachedTreeNodeLabel(node, style, treeLabelW);
            if (GUILayout.Button(new GUIContent(shownName, node.Name), style, AnimBrowserScale.H(20f), GUILayout.ExpandWidth(true)))
            {
                OnTreeNodeClicked(node, Event.current);
            }

            GUILayout.EndHorizontal();
        }

        private void RefreshTreeDrawSnapshotIfNeeded()
        {
            if (Event.current.type != EventType.Layout)
                return;

            BuildFlatTreeIfNeeded();
            RebuildSearchHitCacheIfNeeded();
            _treeDrawRows.Clear();
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                AnimViewNode node = _flatTreeNodes[i];
                if (!IsTreeNodeVisibleForSearch(node))
                    continue;
                _treeDrawRows.Add(new TreeRowDrawState
                {
                    Node = node,
                    HasExpand = node.IsGroup && node.Children.Count > 0
                });
            }
        }

        private bool IsCatalogLoading() =>
            !_catalog.BuildComplete || _catalog.RequiresRebuild || _catalog.BuildInProgress;

        private bool TryGetBackgroundLoadState(out float progress, out GUIContent phaseLabel)
        {
            if (_catalogWarmupCoroutine != null || IsCatalogLoading())
            {
                progress = _catalog.BuildProgress;
                phaseLabel = GcLoadPhaseCatalog;
                return true;
            }

            if (_displayWarmupCoroutine != null || _displayCatalog.WarmupInProgress)
            {
                progress = _displayCatalog.WarmupProgress;
                if (string.Equals(_displayCatalog.WarmupStatusText, "Cache", StringComparison.Ordinal))
                    phaseLabel = GcLoadPhaseCache;
                else
                    phaseLabel = GcLoadPhaseTree;
                return true;
            }

            progress = 0f;
            phaseLabel = GcLoadPhaseCatalog;
            return false;
        }

        private void DrawTreeBackgroundLoadIndicator()
        {
            if (!TryGetBackgroundLoadState(out float progress, out GUIContent phaseLabel))
                return;

            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(true), AnimBrowserScale.H(22f));
            GUILayout.Label(phaseLabel, AnimBrowserScale.W(44f));
            DrawInlineProgressBar(progress, 0f, AnimBrowserScale.Px(10f), expandWidth: true);
            GUILayout.EndHorizontal();
        }

        private static void DrawInlineProgressBar(float fraction, float width, float height, bool expandWidth = false)
        {
            fraction = Mathf.Clamp01(fraction);
            Rect bar = expandWidth
                ? GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(height))
                : GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            if (Event.current.type != EventType.Repaint)
                return;

            GUI.Box(bar, GUIContent.none);
            if (fraction <= 0.001f)
                return;

            Color prev = GUI.color;
            GUI.color = new Color(0.35f, 0.75f, 0.45f, 0.95f);
            var fill = new Rect(bar.x + 1f, bar.y + 1f, Mathf.Max(0f, (bar.width - 2f) * fraction), bar.height - 2f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void TryStartCatalogWarmup()
        {
            if (!StudioAPI.StudioLoaded)
                return;
            if (_catalogWarmupCoroutine != null)
                return;
            if (_catalog.BuildComplete && !_catalog.RequiresRebuild)
            {
                TryStartDisplayWarmupInBackground();
                return;
            }

            _catalogWarmupCoroutine = StartCoroutine(WarmupCatalogCoroutine());
        }

        private void TryStartDisplayWarmupInBackground()
        {
            if (_displayCatalog.WarmupComplete || _displayWarmupCoroutine != null)
                return;
            _displayWarmupCoroutine = StartCoroutine(WarmupDisplayCoroutine());
        }

        private IEnumerator WarmupDisplayCoroutine()
        {
            yield return _displayCatalog.WarmupCoroutine();
            _displayWarmupCoroutine = null;
        }

        private IEnumerator WarmupCatalogCoroutine()
        {
            const int maxCatalogPasses = 512;
            for (int pass = 0; pass < maxCatalogPasses; pass++)
            {
                if (!_catalog.BuildComplete || _catalog.RequiresRebuild)
                    yield return _catalog.WarmupCoroutine();
                else
                    break;

                if (_catalog.BuildComplete && !_catalog.RequiresRebuild)
                    break;
                yield return null;
            }

            _catalogWarmupCoroutine = null;
            if (_catalog.BuildComplete && !_catalog.RequiresRebuild)
            {
                OnCatalogReady();
                TryStartDisplayWarmupInBackground();
            }
        }

        private void OnCatalogReady()
        {
            _pendingCatalogReadyUi = true;
        }

        private void CollapseAllTreeGroups()
        {
            IList<AnimViewNode> groups = _displayCatalog.RootGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                AnimViewNode node = groups[i];
                if (node.IsGroup)
                    _collapsedNodeIds.Add(node.Id);
            }
        }

        private void RefreshCatalog()
        {
            if (_catalogWarmupCoroutine != null)
            {
                StopCoroutine(_catalogWarmupCoroutine);
                _catalogWarmupCoroutine = null;
            }
            if (_displayWarmupCoroutine != null)
            {
                StopCoroutine(_displayWarmupCoroutine);
                _displayWarmupCoroutine = null;
            }
            _catalog.Invalidate();
            _selectedTreeNode = null;
            _selectedTreeNodeIds.Clear();
            _displayCatalog.InvalidateTree();
            InvalidateAnimBrowserViewCaches();
            TryStartCatalogWarmup();
        }

        private void OnAnimationClicked(AnimGridItem item)
        {
            var selected = GetApplyTargetCharacters();
            if (selected.Count == 0)
                return;
            AnimPlaybackService.ApplyAnimation(item, selected);
            SyncControlsFromSelectionIfChanged(force: true);
        }

        private void ToggleViewMode()
        {
            CaptureWindowRectForCurrentViewMode();
            _viewMode = _viewMode == AnimBrowserViewMode.Grid ? AnimBrowserViewMode.List : AnimBrowserViewMode.Grid;
            _options.viewMode = (int)_viewMode;
            RestoreWindowRectForViewMode(_viewMode);
            SyncDockedPaneRectsToMainWindow();
            SavePersistedOptions();
            _gridScroll = Vector2.zero;
            _listScroll = Vector2.zero;
            InvalidateAnimBrowserViewCaches();
        }

        private void SelectTreeNode(AnimViewNode node)
        {
            if (_treeRenameActive && !string.Equals(_treeRenameNodeId, node.Id, StringComparison.Ordinal))
                CancelTreeRename();
            _selectedTreeNode = node;
            _selectedTreeNodeIds.Clear();
            _selectedTreeNodeIds.Add(node.Id);
            InvalidateContentViewCaches();
        }

        private void InvalidateContentViewCaches()
        {
            _cachedVisibleItemsValid = false;
        }

        private void InvalidateAnimBrowserViewCaches(bool rebuildFlatTree = true)
        {
            InvalidateContentViewCaches();
            InvalidateSearchHitCache();
            if (rebuildFlatTree)
                _pendingFlatTreeRebuild = true;
        }

        private void BuildFlatTreeIfNeeded()
        {
            if (_flatTreeValid)
                return;

            _flatTreeNodes.Clear();
            var groups = _displayCatalog.RootGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                _flatTreeNodes.Add(g);
                if (!IsNodeExpanded(g))
                    continue;
                for (int c = 0; c < g.Children.Count; c++)
                    _flatTreeNodes.Add(g.Children[c]);
            }

            ResolveSelectedNodeFromFlatTree();
            _flatTreeValid = true;
        }

        private void ResolveSelectedNodeFromFlatTree()
        {
            string? selectedId = _selectedTreeNode?.Id;
            _selectedTreeNode = null;
            if (selectedId == null)
                return;
            for (int i = 0; i < _flatTreeNodes.Count; i++)
            {
                if (string.Equals(_flatTreeNodes[i].Id, selectedId, StringComparison.Ordinal))
                {
                    _selectedTreeNode = _flatTreeNodes[i];
                    return;
                }
            }
        }

        private void RebuildVisibleItemsIfNeeded()
        {
            if (_cachedVisibleItemsValid)
                return;
            _visibleEntries.Clear();
            _visibleEntrySearchDimmed.Clear();
            if (!IsLeafContentNode(_selectedTreeNode))
            {
                _cachedVisibleItemsValid = true;
                return;
            }

            bool categoryNameHit = SelectedSubcategoryNameMatchesSearch();
            var entries = _displayCatalog.GetEntries(_selectedTreeNode!);
            for (int i = 0; i < entries.Count; i++)
            {
                AnimDisplayEntry entry = entries[i];
                bool entryHit = !IsSearchActive || EntryMatchesSearch(entry);
                if (!entryHit && !categoryNameHit)
                    continue;
                _visibleEntries.Add(entry);
                _visibleEntrySearchDimmed.Add(categoryNameHit && !entryHit);
            }

            _cachedVisibleItemsValid = true;
        }

        private void OnGroupStoreChanged()
        {
            _pendingDisplayTreeInvalidate = true;
            InvalidateAnimBrowserViewCaches();
            _controlsSelectionFingerprint = -1;
            TryStartDisplayWarmupInBackground();
        }

        private void OnAutoTranslationsUpdated()
        {
            _catalog.InvalidateDisplayCaches();
            _displayCatalog?.InvalidateEntries();
            InvalidateReviewDisplayCaches();
            InvalidateAnimBrowserViewCaches();
        }

        private void ApplyAutoTranslateConfig(bool force)
        {
            bool enabled = AnimBrowserConfig.AutoTranslate?.Value ?? true;
            if (!force && enabled == _autoTranslateEnabled)
                return;

            _autoTranslateEnabled = enabled;
            StudioAutoTranslation.SetEnabled(enabled);
            if (!enabled)
                StudioAutoTranslation.ClearCache();
            _catalog.InvalidateDisplayCaches();
            _displayCatalog?.InvalidateEntries();
            InvalidateAnimBrowserViewCaches();
        }

        private void EnsurePersistedWindowRectsInitialized()
        {
            if (_options.gridWindowW <= 10f || _options.gridWindowH <= 10f)
            {
                _options.gridWindowX = 120f;
                _options.gridWindowY = 80f;
                _options.gridWindowW = AnimBrowserScale.Px(GridWindowDefaultWidthBase);
                _options.gridWindowH = AnimBrowserScale.Px(GridWindowDefaultHeightBase);
            }

            if (_options.listWindowW <= 10f || _options.listWindowH <= 10f)
            {
                _options.listWindowX = 120f;
                _options.listWindowY = 80f;
                _options.listWindowW = AnimBrowserScale.Px(ListWindowDefaultWidthBase);
                _options.listWindowH = AnimBrowserScale.Px(ListWindowDefaultHeightBase);
            }
        }

        private void CaptureOptionsFromWindow()
        {
            CaptureWindowRectForCurrentViewMode();
            _options.viewMode = (int)_viewMode;
            _options.showControlsPane = IsAnyControlsVisible;
            _options.controlsPreferUndocked = _controlsPreferUndocked;
            if (IsControlsUndockedVisible)
                CaptureControlsFloatingRect();
            _options.showCharacterConfigPane = _showCharacterConfigPane;
            _options.showOptionsPane = _showOptionsPane;
            _options.cardCellSize = _cardCellSize;
            _options.controlsGroupByProximity = _controlsGroupByProximity;
            _options.hideNonStudioCatalogAnimations = _hideNonStudioCatalogAnimations;
            _options.treePanelWidth = _treePanelWidth;
            _options.controlsPaneWidth = _controlsWindowRect.width;
            _options.characterConfigPaneWidth = _characterConfigWindowRect.width;
            _options.optionsPaneWidth = _optionsWindowRect.width;
            if (_helpWindowRect.width > 10f)
                _options.helpPaneWidth = _helpWindowRect.width;
            _options.optionsVersion = AnimBrowserConfig.OptionsJsonVersion;
            // Legacy single-rect fields mirror the active view mode for older builds.
            _options.windowX = windowRect.x;
            _options.windowY = windowRect.y;
            _options.windowW = windowRect.width;
            _options.windowH = windowRect.height;
        }

        private void CaptureWindowRectForCurrentViewMode()
        {
            if (_viewMode == AnimBrowserViewMode.Grid)
            {
                _options.gridWindowX = windowRect.x;
                _options.gridWindowY = windowRect.y;
                _options.gridWindowW = windowRect.width;
                _options.gridWindowH = windowRect.height;
            }
            else
            {
                _options.listWindowX = windowRect.x;
                _options.listWindowY = windowRect.y;
                _options.listWindowW = windowRect.width;
                _options.listWindowH = windowRect.height;
            }
        }

        private void RestoreWindowRectForViewMode(AnimBrowserViewMode mode)
        {
            float x;
            float y;
            float w;
            float h;
            if (mode == AnimBrowserViewMode.Grid)
                GetSavedGridWindowRect(out x, out y, out w, out h);
            else
                GetSavedListWindowRect(out x, out y, out w, out h);

            float minW = EffectiveResizeMinWidth();
            w = Mathf.Clamp(w, minW, LayoutMaxWidth);
            h = Mathf.Clamp(h, MinWindowHeight, LayoutMaxHeight);
            x = Mathf.Clamp(x, 4f, Mathf.Max(4f, Screen.width - w - 4f));
            y = Mathf.Clamp(y, 4f, Mathf.Max(4f, Screen.height - h - 4f));
            windowRect = new Rect(x, y, w, h);
        }

        private void GetSavedGridWindowRect(out float x, out float y, out float w, out float h)
        {
            x = _options.gridWindowX;
            y = _options.gridWindowY;
            w = _options.gridWindowW;
            h = _options.gridWindowH;
            if (w > 10f && h > 10f)
                return;
            x = 120f;
            y = 80f;
            w = AnimBrowserScale.Px(GridWindowDefaultWidthBase);
            h = AnimBrowserScale.Px(GridWindowDefaultHeightBase);
        }

        private void GetSavedListWindowRect(out float x, out float y, out float w, out float h)
        {
            x = _options.listWindowX;
            y = _options.listWindowY;
            w = _options.listWindowW;
            h = _options.listWindowH;
            if (w > 10f && h > 10f)
                return;
            x = 120f;
            y = 80f;
            w = AnimBrowserScale.Px(ListWindowDefaultWidthBase);
            h = AnimBrowserScale.Px(ListWindowDefaultHeightBase);
        }

        private void SyncDockedPaneRectsToMainWindow()
        {
            _controlsWindowRect = new Rect(
                windowRect.xMax + DockedPaneGap,
                windowRect.y,
                _options.controlsPaneWidth > 10f ? _options.controlsPaneWidth : ControlsPaneDefaultWidth,
                windowRect.height);
            _characterConfigWindowRect = new Rect(
                windowRect.xMax + DockedPaneGap,
                windowRect.y,
                _options.characterConfigPaneWidth > 10f ? _options.characterConfigPaneWidth : CharacterPaneDefaultWidth,
                windowRect.height);
            _optionsWindowRect = new Rect(
                windowRect.xMax + DockedPaneGap,
                windowRect.y,
                _options.optionsPaneWidth > 10f ? _options.optionsPaneWidth : OptionsPaneDefaultWidth,
                windowRect.height);
            _helpWindowRect = new Rect(
                windowRect.xMax + DockedPaneGap,
                windowRect.y,
                _options.helpPaneWidth > 10f ? _options.helpPaneWidth : HelpPaneDefaultWidth,
                windowRect.height);
        }

        private void SavePersistedOptions()
        {
            CaptureOptionsFromWindow();
            AnimBrowserPersistence.Save(_options);
        }

        private void DrawDockedPaneWindow(
            int paneId,
            ref Rect paneRect,
            GUI.WindowFunction drawContent,
            string title,
            float minWidth)
        {
            float widthBefore = paneRect.width;
            paneRect = GUILayout.Window(
                paneId,
                paneRect,
                drawContent,
                title,
                GUILayout.MinWidth(minWidth),
                AnimBrowserScale.MinH(120f));
            if (widthBefore > 1f && paneRect.width > widthBefore + 0.5f)
                paneRect.width = widthBefore;
            paneRect.x = Mathf.Clamp(paneRect.x, 4f, Mathf.Max(4f, Screen.width - paneRect.width - 4f));
            paneRect.y = Mathf.Clamp(paneRect.y, 4f, Mathf.Max(4f, Screen.height - paneRect.height - 4f));
            IMGUIUtils.EatInputInRect(paneRect);
        }

        private void FinishMainWindowChrome(int id)
        {
            if (Event.current.type == EventType.Repaint)
                TryCapturePreviewHoverFromTooltip();

            var resizeHandle = new Rect(
                windowRect.width - ResizeHandleSize,
                windowRect.height - ResizeHandleSize,
                ResizeHandleSize,
                ResizeHandleSize);
            GUI.Box(resizeHandle, "◢");
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - ResizeHandleSize, TopBarTotalHeight));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private void ApplyTreePanelWidthFromOptions()
        {
            float stored = _options.treePanelWidth;
            if (stored <= 205f)
            {
                _treePanelWidth = AnimBrowserScale.Px(TreePanelWidthDefaultBase);
                return;
            }

            // Values ≤ base default were saved as unscaled layout bases; larger values are scaled pixels.
            _treePanelWidth = stored <= TreePanelWidthDefaultBase
                ? AnimBrowserScale.Px(stored)
                : stored;
        }

        private float ComputeContentMinimumWindowWidth()
        {
            float treeGrid = TreePanelWidth + GridPanelChromePad + MinCardSize + AnimBrowserScale.Px(WindowChromeScrollbarWidthBase);
            return Mathf.Max(MinWindowWidth, treeGrid);
        }

        private float EffectiveResizeMinWidth() => ComputeContentMinimumWindowWidth();

        private void HandleResize()
        {
            Event? e = Event.current;
            if (e == null)
                return;

            float minW = EffectiveResizeMinWidth();
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
                windowRect.width = Mathf.Clamp(e.mousePosition.x - windowRect.x, minW, LayoutMaxWidth);
                windowRect.height = Mathf.Clamp(e.mousePosition.y - windowRect.y, MinWindowHeight, LayoutMaxHeight);
                e.Use();
            }
            else if (_isResizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _isResizing = false;
                windowRect.width = Mathf.Clamp(windowRect.width, minW, LayoutMaxWidth);
                windowRect.height = Mathf.Clamp(windowRect.height, MinWindowHeight, LayoutMaxHeight);
                CaptureWindowRectForCurrentViewMode();
                SavePersistedOptions();
                e.Use();
            }
        }

        private static GUISkin? _scaledSkin;
        private static GUISkin? _scaledSkinBase;
        private static float _scaledSkinFactor = -1f;

        private static GUISkin GetScaledAnimBrowserSkin(GUISkin baseSkin)
        {
            AnimBrowserScale.CaptureBaseFont(baseSkin);
            float f = AnimBrowserScale.Factor;

            if (Mathf.Approximately(f, 1f))
                return baseSkin;

            if (_scaledSkin != null && _scaledSkinBase == baseSkin && Mathf.Approximately(_scaledSkinFactor, f))
                return _scaledSkin;

            var skin = UnityEngine.Object.Instantiate(baseSkin);
            int scaledBase = AnimBrowserScale.Font(AnimBrowserScale.BaseFontSize);

            void ScaleFont(GUIStyle? s)
            {
                if (s == null)
                    return;
                s.fontSize = s.fontSize > 0 ? Mathf.Max(1, Mathf.RoundToInt(s.fontSize * f)) : scaledBase;
                if (s.fixedHeight > 0f)
                    s.fixedHeight *= f;
                if (s.fixedWidth > 0f)
                    s.fixedWidth *= f;
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
            {
                foreach (var cs in skin.customStyles)
                    ScaleFont(cs);
            }

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

        private void EnsureStyleCachesMatchScale()
        {
            float f = AnimBrowserScale.Factor;
            if (_cachedStyleScaleFactor.Equals(f))
                return;
            _cachedStyleScaleFactor = f;
            InvalidateAnimBrowserStyleCaches();
            InvalidateGridLayoutCache();
        }

        private void EnsureMinimumOptionsPaneWidth()
        {
            float minW = AnimBrowserScale.Px(380f);
            if (_options.optionsPaneWidth <= 10f || _options.optionsPaneWidth < minW)
                _options.optionsPaneWidth = OptionsPaneDefaultWidth;
        }

        private string GetCachedStudioSelectedCharNamesSummary()
        {
            var names = GetCachedStudioCharacterDisplayNames();
            if (names.Count == 0)
                return string.Empty;
            if (names.Count == 1)
                return names[0];
            return names[0] + " +" + (names.Count - 1);
        }

        private struct TreeRowDrawState
        {
            public AnimViewNode Node;
            public bool HasExpand;
        }
    }
}
