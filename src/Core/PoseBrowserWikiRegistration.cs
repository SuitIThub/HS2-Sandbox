using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Registers Pose Browser documentation with <see href="https://github.com/SuitIThub/HS2Wiki">HS2Wiki</see>
    /// when present (soft dependency via reflection). See <c>docs/PoseBrowser-HS2Wiki-Manual.md</c> for the full manual.
    /// </summary>
    internal static class PoseBrowserWikiRegistration
    {
        public const string WikiCategoryRoot = "HS2 Sandbox/Pose Browser";
        public const string WikiCategoryAdvanced = "HS2 Sandbox/Pose Browser/Advanced";

        public const string PageOverview = "Overview";
        public const string PageFolders = "Folders & library";
        public const string PageSearchFilters = "Search & filters";
        public const string PageGridSelection = "Grid & selection";
        public const string PagePoseFiles = "Pose files & actions";
        public const string PagePoseItems = "Pose items";
        public const string PageImportExport = "Import & export (ZIP)";
        public const string PageThumbnails = "Thumbnails";
        public const string PagePoseGroups = "Pose groups";
        public const string PageMultiCharacterApply = "Multi-character apply";
        public const string PageOptionsData = "Options & data files";

        public const string WikiDownloadUrl = "https://github.com/SuIT-pub/HS2Wiki";

        private static bool _registerSucceeded;
        private static bool _loggedMissingWiki;
        private static ManualLogSource? _log;
        private static object? _api;
        private static object? _wikiPluginInstance;
        private static MethodInfo? _registerPage;
        private static MethodInfo? _openPage;
        private static MethodInfo? _openImage;
        private static FieldInfo? _wikiUiShowField;
        private static Texture2D? _wikiBannerTex;

        /// <summary>True when the HS2Wiki assembly is loaded and the API resolved (pages may still fail to register).</summary>
        public static bool IsWikiInstalled =>
            _registerSucceeded || TryResolveApi(out _, out _, out _, out _);

        public static void TryRegister(ManualLogSource log)
        {
            _log = log;
            if (_registerSucceeded) return;

            if (!TryResolveApi(out _api, out _registerPage, out _openPage, out _openImage))
            {
                if (!_loggedMissingWiki)
                {
                    _loggedMissingWiki = true;
                    log.LogInfo("HS2Wiki not loaded; Pose Browser wiki pages were not registered (optional).");
                }
                return;
            }

            try
            {
                InvokeRegister(WikiCategoryRoot, PageOverview, DrawWikiOverview);
                InvokeRegister(WikiCategoryRoot, PageFolders, DrawWikiFolders);
                InvokeRegister(WikiCategoryRoot, PageSearchFilters, DrawWikiSearchFilters);
                InvokeRegister(WikiCategoryRoot, PageGridSelection, DrawWikiGridSelection);
                InvokeRegister(WikiCategoryRoot, PagePoseFiles, DrawWikiPoseFiles);
                InvokeRegister(WikiCategoryRoot, PagePoseItems, DrawWikiPoseItems);
                InvokeRegister(WikiCategoryRoot, PageImportExport, DrawWikiImportExport);
                InvokeRegister(WikiCategoryRoot, PageThumbnails, DrawWikiThumbnails);
                InvokeRegister(WikiCategoryRoot, PagePoseGroups, DrawWikiPoseGroups);
                InvokeRegister(WikiCategoryRoot, PageMultiCharacterApply, DrawWikiMultiCharacterApply);
                InvokeRegister(WikiCategoryAdvanced, "Tag storage & migration", DrawWikiTagStorage);
                InvokeRegister(WikiCategoryRoot, PageOptionsData, DrawWikiOptionsData);

                _registerSucceeded = true;
                log.LogInfo("Registered Pose Browser pages with HS2Wiki (F3).");
            }
            catch (Exception ex)
            {
                log.LogWarning($"Pose Browser wiki registration failed: {ex.Message}");
            }
        }

        /// <summary>Opens the HS2Wiki window (<c>_uiShow</c> on <c>WikiPlugin</c>) if the plugin is loaded.</summary>
        public static void TryShowWikiWindow()
        {
            if (!TryGetWikiPluginInstance(out object? plugin) || plugin == null)
                return;

            try
            {
                _wikiUiShowField ??= plugin.GetType().GetField(
                    "_uiShow",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _wikiUiShowField?.SetValue(plugin, true);
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"ShowWikiWindow failed: {ex.Message}");
            }
        }

        /// <summary>Opens the wiki window and navigates to a page if HS2Wiki is available (safe no-op otherwise).</summary>
        public static void TryOpenWikiPage(string category, string pageName)
        {
            if (_openPage == null && _api == null && !TryResolveApi(out _api, out _, out _openPage, out _openImage))
                return;
            if (_openPage == null || _api == null) return;
            try
            {
                TryShowWikiWindow();
                _openPage.Invoke(_api, new object[] { category, pageName });
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"OpenWikiPage failed: {ex.Message}");
            }
        }

        /// <summary>HS2Wiki block for the in-game Help pane.</summary>
        public static void DrawHelpWikiSection(GUIStyle rich)
        {
            GUILayout.Label("<b>HS2Wiki</b>", rich);
            if (!IsWikiInstalled)
            {
                GUILayout.Label(
                    "<b>HS2Wiki</b> is not loaded. Install it for the full Pose Browser manual (linked pages, images) in a separate window. Download:",
                    rich);
                if (GUILayout.Button("Open HS2Wiki on GitHub", GUILayout.Height(26f)))
                    Application.OpenURL(WikiDownloadUrl);
                GUILayout.Label(
                    "After installing, restart Studio. Pages appear under <b>HS2 Sandbox / Pose Browser</b> (default key <b>F3</b>).",
                    rich);
                return;
            }

            GUILayout.Label(
                "Open the full manual in <b>HS2Wiki</b> (<b>F3</b> by default). These buttons open the wiki window on the selected page:",
                rich);
            if (GUILayout.Button("Wiki: Overview", GUILayout.Height(24f)))
                TryOpenWikiPage(WikiCategoryRoot, PageOverview);
            if (GUILayout.Button("Wiki: Pose groups", GUILayout.Height(24f)))
                TryOpenWikiPage(WikiCategoryRoot, PagePoseGroups);
            if (GUILayout.Button("Wiki: Multi-character apply", GUILayout.Height(24f)))
                TryOpenWikiPage(WikiCategoryRoot, PageMultiCharacterApply);
            if (GUILayout.Button("Wiki: Pose items", GUILayout.Height(24f)))
                TryOpenWikiPage(WikiCategoryRoot, PagePoseItems);
        }

        public static void TryOpenPoseIconImage()
        {
            string? path = TryGetPoseIconPath();
            if (string.IsNullOrEmpty(path)) return;
            if (_openImage == null && _api == null && !TryResolveApi(out _api, out _, out _openPage, out _openImage))
                return;
            if (_openImage == null || _api == null) return;
            try
            {
                _openImage.Invoke(_api, new object[] { path! });
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"OpenImage failed: {ex.Message}");
            }
        }

        private static void InvokeRegister(string category, string pageName, Action drawAction)
        {
            _registerPage!.Invoke(_api!, new object[] { category, pageName, drawAction });
        }

        private static bool TryResolveApi(out object? api, out MethodInfo? registerPage, out MethodInfo? openPage, out MethodInfo? openImage)
        {
            api = null;
            registerPage = null;
            openPage = null;
            openImage = null;

            Type? wikiPluginType = Type.GetType("HS2Wiki.WikiPlugin, HS2Wiki");
            if (wikiPluginType == null) return false;

            FieldInfo? apiField = wikiPluginType.GetField("PublicAPI", BindingFlags.Public | BindingFlags.Static);
            if (apiField == null) return false;

            api = apiField.GetValue(null);
            if (api == null) return false;

            registerPage = api.GetType().GetMethod("RegisterPage", new[] { typeof(string), typeof(string), typeof(Action) });
            openPage = api.GetType().GetMethod("OpenPage", new[] { typeof(string), typeof(string) });
            openImage = api.GetType().GetMethod("OpenImage", new[] { typeof(string) });

            return registerPage != null;
        }

        private static bool TryGetWikiPluginInstance(out object? plugin)
        {
            plugin = _wikiPluginInstance;
            if (plugin != null)
                return true;

            if (_api == null && !TryResolveApi(out _api, out _, out _openPage, out _openImage))
                return false;

            if (_api == null)
                return false;

            try
            {
                FieldInfo? pluginField = _api.GetType().GetField(
                    "_plugin",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                plugin = pluginField?.GetValue(_api);
                if (plugin != null)
                    _wikiPluginInstance = plugin;
                return plugin != null;
            }
            catch
            {
                return false;
            }
        }

        private static string? TryGetPoseIconPath()
        {
            string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) return null;
            string p = Path.Combine(dir, "pose-icon.png");
            return File.Exists(p) ? p : null;
        }

        private static Texture2D WikiBannerTexture()
        {
            if (_wikiBannerTex == null)
                _wikiBannerTex = ToolbarIconLoader.LoadPng("pose-icon.png");
            return _wikiBannerTex;
        }

        private static void NavButton(string label, string category, string page)
        {
            if (GUILayout.Button(label, GUILayout.Height(26f)))
                TryOpenWikiPage(category, page);
        }

        private static void DrawWikiOverview()
        {
            GUILayout.Label("<size=18><b>Pose Browser</b></size>");
            GUILayout.Label("<i>Browse, tag, and manage Studio pose files under UserData/studio/pose.</i>");
            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>Recent releases:</b> <b>Pose Browser 5.0.0</b> adds <b>Pose items</b> — register workspace props per pose, load with position/rotation/scale toggles and optional free placement (<b>pose_items.tsv</b> v5). " +
                "<b>3.2+</b> — group relative positions and object-scale layout. <b>3.0.0</b> — pose groups, tag include/exclude, multi-character apply. <b>2.0.0</b> — Full/List/Mini layouts, Sort, ★ Favorites, keyboard shortcuts. v2+ ZIP import/export — see pages below.");

            GUILayout.Space(8f);
            var tex = WikiBannerTexture();
            if (tex != null && tex.width > 4)
            {
                GUILayout.Label("<b>Toolbar icon</b> (click to open in image viewer):");
                if (GUILayout.Button(tex, GUI.skin.box, GUILayout.Width(Mathf.Min(128f, tex.width)), GUILayout.Height(Mathf.Min(128f, tex.height))))
                    TryOpenPoseIconImage();
                GUILayout.Space(8f);
            }

            GUILayout.Label("<b>Quick navigation</b>");
            NavButton("→ Folders & library", WikiCategoryRoot, PageFolders);
            NavButton("→ Search & filters", WikiCategoryRoot, PageSearchFilters);
            NavButton("→ Grid & selection", WikiCategoryRoot, PageGridSelection);
            NavButton("→ Pose groups", WikiCategoryRoot, PagePoseGroups);
            NavButton("→ Multi-character apply", WikiCategoryRoot, PageMultiCharacterApply);
            NavButton("→ Pose files & actions", WikiCategoryRoot, PagePoseFiles);
            NavButton("→ Pose items", WikiCategoryRoot, PagePoseItems);
            NavButton("→ Import & export (ZIP)", WikiCategoryRoot, PageImportExport);
            NavButton("→ Thumbnails", WikiCategoryRoot, PageThumbnails);
            NavButton("→ Options & data files", WikiCategoryRoot, PageOptionsData);
            NavButton("→ Tag storage & migration", WikiCategoryAdvanced, "Tag storage & migration");

            GUILayout.Space(10f);
            GUILayout.Label(
                "<b>In-window help</b>: open the Pose Browser, then use the <b>Help</b> button for a short on-screen reference.");
        }

        private static void DrawWikiFolders()
        {
            GUILayout.Label("<size=17><b>Folders & library</b></size>");
            GUILayout.Space(4f);
            GUILayout.Label(
                "The left pane mirrors folders under <b>UserData/studio/pose</b>. Use <b>↻</b> to rescan the tree after changes outside the browser.");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>All poses</b> — recursive listing from the pose root (every supported pose in subfolders).");
            GUILayout.Label("<b>★ Favorites</b> — every favorited pose library-wide (virtual view; <b>Save Pose</b> while this row is active writes into the pose root).");
            GUILayout.Label("<b>Root only</b> — files directly in the pose root, no subfolders.");
            GUILayout.Label("<b>Folder rows</b> — click the name to show only that folder (non-recursive). Use ►/▼ to expand or collapse.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Footer actions</b> (under the tree)");
            GUILayout.Label("• <b>Library root</b>: <b>New folder…</b>; in <b>Full</b> layout, <b>Export library tree…</b> saves a v2 branch ZIP of the entire library tree.");
            GUILayout.Label("• <b>Selected folder</b>: <b>Rename…</b>, <b>New folder…</b>, <b>Delete folder…</b> (only if the folder is empty). In <b>Full</b> layout, <b>Export branch…</b> exports that subtree as a v2 ZIP.");
            GUILayout.Label("• During <b>Move</b>/<b>Copy</b>/<b>import</b>, <b>Apply</b> / <b>Cancel</b> appear at the <i>top</i> of the footer after you pick a destination.");

            GUILayout.Space(8f);
            NavButton("← Overview", WikiCategoryRoot, PageOverview);
            NavButton("→ Search & filters", WikiCategoryRoot, PageSearchFilters);
        }

        private static void DrawWikiSearchFilters()
        {
            GUILayout.Label("<size=17><b>Search & filters</b></size>");
            GUILayout.Label("Filters apply to the <i>current folder view</i>, <i>All poses</i>, or <i>★ Favorites</i>, then affect the grid and counts. Use <b>Sort</b> on the top bar for order (Name, file dates, Last used).");

            GUILayout.Space(4f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Search</b> — filters by display name / path. Toggle <b>.*</b> for case-insensitive regex; invalid patterns show an error line under the bar.");
            GUILayout.Label("<b>★</b> — show only poses marked favorite (see Tags / Fav Selected).");
            GUILayout.Label("<b>AND / OR</b> — combines active tag filters: every selected tag must match (AND) or any one (OR).");
            GUILayout.Label("<b>Tags (n)</b> — docked <b>Tag filter</b> window: click each tag to cycle <b>neutral → + include → − exclude</b>. <b>AND / OR</b> is inside the tag window for include rules.");
            GUILayout.Label("<b>Exclude</b> — hides ungrouped poses with excluded tags; grouped segments stay visible with excluded members <b>dimmed</b> (red tag text on cards).");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            NavButton("← Folders & library", WikiCategoryRoot, PageFolders);
            NavButton("→ Grid & selection", WikiCategoryRoot, PageGridSelection);
            NavButton("→ Pose groups", WikiCategoryRoot, PagePoseGroups);
        }

        private static void DrawWikiGridSelection()
        {
            GUILayout.Label("<size=17><b>Grid & selection</b></size>");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Card</b> — thumbnail, name, optional tag line. Checkbox (corner) toggles selection without affecting the pose.");
            GUILayout.Label("<b>Left-click</b> (thumbnail) — single select and <b>apply pose</b> to selected Studio character(s).");
            GUILayout.Label("<b>Ctrl+click</b> — toggle item in the selection.");
            GUILayout.Label("<b>Shift+click</b> — range-select within the filtered list (from last anchor).");
            GUILayout.Label("<b>Right-click</b> — apply this pose <i>without</i> changing the current selection.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>Pagination</b> — when <i>max items per page</i> is set in Options, use ◀ ▶ and the page label. Column count grows with window width (see Options).");

            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>Import preview</b> — after <b>Import…</b>, cards from the ZIP appear in the grid; thumbnail click toggles whether a pose is checked (checkbox + Ctrl/Shift behave like normal). You are not browsing disk files until you cancel or finish the import.");

            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>Pose groups</b> — members of a group appear inside a bordered segment with a header row (▦ name, optional group tags). See <b>Pose groups</b> for selection vs group entity, filters, and export.");

            GUILayout.Space(8f);
            NavButton("← Search & filters", WikiCategoryRoot, PageSearchFilters);
            NavButton("→ Pose groups", WikiCategoryRoot, PagePoseGroups);
            NavButton("→ Multi-character apply", WikiCategoryRoot, PageMultiCharacterApply);
        }

        private static void DrawWikiPoseFiles()
        {
            GUILayout.Label("<size=17><b>Pose files & actions</b></size>");
            GUILayout.Label(
                "<b>Import…</b> is always on the top bar. The <b>selection bar</b> actions below need at least one selected <i>library</i> pose (import preview uses its own bottom-bar hints).");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Import…</b> (top bar) — read a v2 pose pack <b>.zip</b>; see <b>Import & export (ZIP)</b> for the workflow.");
            GUILayout.Label("<b>Save Pose</b> (top bar) — writes the current character pose into the <i>active save folder</i>: selected folder, pose root when <b>All poses</b> or <b>★ Favorites</b> is active.");
            GUILayout.Label("<b>Update Pose</b> (one selected) — overwrite file from the scene; choose keeping or regenerating the thumbnail.");
            GUILayout.Label("<b>Rename…</b> — optional rename of file to match display name.");
            GUILayout.Label("<b>Grouping</b> — <b>Group…</b> (2+ ungrouped poses), <b>Ungroup</b>. Group header selection shows a separate bar: rename, tags, export, apply, save/clear positions. See <b>Pose groups</b>.");
            GUILayout.Label("<b>Items</b> (exactly one pose selected) — register Studio workspace items for that pose; see <b>Pose items</b>.");
            GUILayout.Label("<b>Tag Selected</b> — tag window in <b>assign</b> mode for <i>pose</i> tags on all selected items.");
            GUILayout.Label("<b>Fav Selected</b> — toggle favorite flag (★ filter).");
            GUILayout.Label("<b>Export…</b> — writes selected on-disk poses to a v2 <b>.zip</b> (embedded tags/favorites metadata).");
            GUILayout.Label("<b>Move… / Copy…</b> — pick destination in the <b>Folders</b> tree (<b>Root only</b> or a folder; highlighted), then <b>Apply</b> or <b>Cancel</b> in the folder footer. The grid does not reload while picking, so your selection is preserved. <b>New folder…</b> still works. Tags move with paths when applicable.");
            GUILayout.Label("<b>Delete…</b> — copies into <b>!_AutoBackup</b> then removes files; confirmations are required.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>Character</b> row — <b>Chars</b> opens priority lists; with multiple poses or one full group, <b>Apply to characters…</b> maps poses onto Studio-selected characters. See <b>Multi-character apply</b>.");

            GUILayout.Space(8f);
            NavButton("← Grid & selection", WikiCategoryRoot, PageGridSelection);
            NavButton("→ Pose items", WikiCategoryRoot, PagePoseItems);
            NavButton("→ Pose groups", WikiCategoryRoot, PagePoseGroups);
            NavButton("→ Multi-character apply", WikiCategoryRoot, PageMultiCharacterApply);
            NavButton("→ Import & export (ZIP)", WikiCategoryRoot, PageImportExport);
        }

        private static void DrawWikiPoseItems()
        {
            GUILayout.Label("<size=17><b>Pose items</b></size>");
            GUILayout.Label(
                "Link Studio <b>workspace items</b> (props, accessories, etc.) to a <b>single library pose</b>. When you apply that pose and load its items, layout is restored relative to one character — including optional parenting to a body-part row in the workspace tree.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Open the pane</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("1. Select <b>exactly one</b> pose in the library grid (not import preview).");
            GUILayout.Label("2. Click <b>Items</b> in the bottom selection bar.");
            GUILayout.Label("3. The <b>Items</b> pane docks beside Help / Options / Tags (same chain as other side panels).");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Add items to a pose</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• Select <b>one character</b> in Studio (extra selected objects are ignored for add/load).");
            GUILayout.Label("• Select one or more <b>workspace items</b> (tree row and/or item guide).");
            GUILayout.Label("• The line above <b>Add selected item(s)</b> lists names that will be registered (<b>Will add: …</b>).");
            GUILayout.Label("• Each entry stores catalog ids, bundle paths (for respawn after restart), transform layout, optional body-part attach path, and Studio attach offsets when parented.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Stored list (per pose)</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>☑</b> — include row in <b>Load Selection</b> (unchecked rows are skipped).");
            GUILayout.Label("<b>Name button</b> — load that one entry immediately.");
            GUILayout.Label("<b>✎</b> — rename the display label (saved in <b>pose_items.tsv</b>).");
            GUILayout.Label("<b>X</b> — remove the entry from this pose.");
            GUILayout.Label("<b>Bold name</b> — same catalog item is currently selected in Studio (still a button).");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Load options</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Position</b> / <b>Rotation</b> / <b>Scale</b> — turn off to keep the spawned item’s default on that axis.");
            GUILayout.Label("<b>Load as free</b> — do not reparent in the workspace tree even when the item was saved on a body part; world layout still follows the character (scaled with current object scale / body height).");
            GUILayout.Label("<b>Load Selection</b> — checked rows only. <b>Load All</b> — every stored row.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Layout & scaling</b>");
            GUILayout.Label(
                "• Saved <b>anchor-relative</b> position and rotation vs the character guide at save time.\n" +
                "• On load, position and item scale adjust for the character’s current <b>Studio object scale</b> and <b>body height</b> (same ratio logic as group relative positions).\n" +
                "• Items saved on a <b>body part</b> store a workspace tree path and Studio <b>changeAmount</b> attach offsets (v5 TSV). Default load reparents via the tree; <b>Load as free</b> uses the same world layout without parenting.\n" +
                "• ⚠ <b>Yellow</b> banner — selected character does not have this pose applied (save/load still work). Orange ⚠ on a row — last load warning (e.g. body part not found).");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Data file</b>");
            GUILayout.Label(
                "<b>pose_items.tsv</b> (v5) under <b>BepInEx/config/com.hs2.sandbox/</b>, keyed by pose path relative to the library. Move/rename pose files update keys through the browser like tags.");

            GUILayout.Space(8f);
            NavButton("← Pose files & actions", WikiCategoryRoot, PagePoseFiles);
            NavButton("→ Pose groups", WikiCategoryRoot, PagePoseGroups);
            NavButton("→ Options & data files", WikiCategoryRoot, PageOptionsData);
        }

        private static void DrawWikiPoseGroups()
        {
            GUILayout.Label("<size=17><b>Pose groups</b></size>");
            GUILayout.Label(
                "A <b>pose group</b> is a named set of library poses that stay together in the grid, share optional <b>group tags</b>, and can be exported/imported inside v5 ZIP packs (v2–v4 packs still import; layout fields optional). Membership, optional <b>relative offsets</b>, and <b>body heights per pose</b> are stored in <b>pose_groups.tsv</b> (config folder), keyed by pose file paths.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Creating and editing groups</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("1. Select <b>two or more ungrouped</b> poses in the bottom bar (checkboxes or thumbnail selection).");
            GUILayout.Label("2. Click <b>Group…</b>, enter a name, confirm.");
            GUILayout.Label("3. To add poses later, select ungrouped poses + at least one member of the target group, then <b>Group…</b> again (merges into the existing group when applicable).");
            GUILayout.Label("4. <b>Ungroup</b> removes selected poses from their groups (does not delete pose files).");
            GUILayout.Label("5. <b>Rename…</b> / <b>Tags…</b> / <b>Export…</b> appear on the <b>group action bar</b> when the group header is selected as a group entity.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Relative positions (save & apply)</b>");
            GUILayout.Label(
                "Optional layout when a group is applied multi-character. Everything is keyed by <b>pose path</b> and <b>assignment order</b> from <b>Chars</b> priority (grid display order) — not by which Studio character card you used at save time.");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Anchor (first pose)</b>");
            GUILayout.Label("• The <b>first pose</b> in display order is the anchor.");
            GUILayout.Label("• At save: anchor world position + body height are recorded on that pose path.");
            GUILayout.Label("• At apply: anchor character is <b>not moved</b> by layout; other characters use <b>anchor position + anchor rotation × saved local offset</b> (orbit with anchor).");
            GUILayout.EndVertical();
            GUILayout.Space(4f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Requirements to enable Save positions…</b>");
            GUILayout.Label("• Not during import preview.");
            GUILayout.Label("• The group was the <b>last</b> thing you applied with <b>Apply to characters…</b> — no other pose applied since.");
            GUILayout.Label("• Studio selection: <b>exactly as many characters as poses</b> in the group.");
            GUILayout.Label("• <b>One-to-one gender match</b> via <b>Chars</b> lists and pose tags (see <b>Multi-character apply</b>).");
            GUILayout.EndVertical();
            GUILayout.Space(4f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Workflow — save</b>");
            GUILayout.Label("1. Set up <b>Chars</b> and pose tags if needed.");
            GUILayout.Label("2. Select characters in Studio → apply group → arrange scene.");
            GUILayout.Label("3. <b>Save positions…</b> stores per other pose: local <b>offset</b> and <b>rotation</b> vs anchor, plus <b>body height</b> on every pose path.");
            GUILayout.EndVertical();
            GUILayout.Space(4f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Workflow — apply</b>");
            GUILayout.Label("Same apply path and assignment order. After poses are applied:");
            GUILayout.Label("• <b>Apply relative positions</b> (global) — <b>anchor + rotated offset</b> (orbits with anchor) and relative rotation on each non-anchor pose.");
            GUILayout.Label("• <b>Adjust for body height</b> (global; requires relative positions) — same full offset, but <b>offset.y</b> is scaled from saved vs current body-height ratios on each pose path (no hardcoded world multiplier).");
            GUILayout.Label("• <b>Clear positions</b> — removes offsets and heights for that group.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Two kinds of selection</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Group entity</b> — click the <b>group header</b> (▦ row). Highlights the segment; shows the group bar (rename, tags, export, apply, save/clear positions, <b>Apply relative positions</b>, <b>Adjust for body height</b>). Ctrl+click toggles group entities; Shift+click range-selects group headers in the filtered list.");
            GUILayout.Label("<b>Pose members</b> — checkboxes / thumbnail clicks on cards inside the segment. Used for move, copy, delete, tag selected, partial export, etc.");
            GUILayout.Label("During <b>import preview</b>, clicking the group header toggles <b>all member checkboxes</b> for import (not group-entity mode).");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Group tags vs pose tags</b>");
            GUILayout.Label(
                "• <b>Group tags</b> — edited via <b>Group tags…</b>; used for <b>filtering</b> (include/exclude in the tag window) and shown on the group header.\n" +
                "• <b>Pose tags</b> — per-card tags (e.g. <b>Male</b> / <b>Female</b> for multi-character apply).\n" +
                "• <b>Exclude</b> filters: ungrouped poses with an excluded tag are hidden; inside a visible group <b>all members stay</b> but cards with excluded pose tags are <b>dimmed</b> and tag names show in <b>red</b>.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Grid layout</b>");
            GUILayout.Label(
                "Grouped poses render in a bordered block. Large groups may continue on the next row (continuation header without repeating tags). Sort order applies to <b>groups as blocks</b> and to ungrouped poses.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Move / copy / delete</b>");
            GUILayout.Label(
                "• <b>Move…</b> / <b>Copy…</b> — works on ungrouped poses, or when exactly <b>one full group</b> is selected (all members). The whole group moves/copies together.\n" +
                "• <b>Delete…</b> — can remove group members or entire groups per confirmation.\n" +
                "• <b>Export…</b> — include group metadata (<b>memberRelativeOffsets</b>, <b>memberBodyHeights</b> when saved) when every member of a group is selected, or use <b>Export…</b> from the group bar.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Import / export (ZIP v5)</b>");
            GUILayout.Label(
                "v3+ packs include <b>groups[]</b> in <b>metadata.json</b>; v4 adds <b>memberRelativeOffsets</b>; v5 adds <b>memberBodyHeights</b> (parallel to members). On import preview, groups appear as segments; layout imports when present. v2 packs without groups still import as flat poses.");

            GUILayout.Space(8f);
            NavButton("← Grid & selection", WikiCategoryRoot, PageGridSelection);
            NavButton("→ Multi-character apply", WikiCategoryRoot, PageMultiCharacterApply);
            NavButton("→ Pose files & actions", WikiCategoryRoot, PagePoseFiles);
        }

        private static void DrawWikiMultiCharacterApply()
        {
            GUILayout.Label("<size=17><b>Multi-character apply</b></size>");
            GUILayout.Label(
                "Use this workflow when a scene has <b>several characters</b> and you want to apply <b>multiple poses at once</b> (or an entire <b>group</b>) with predictable pairing — for example a male pose on the male character and a female pose on the female character.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>When the button appears</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Apply to characters…</b> shows on the character row or group bar when:");
            GUILayout.Label("• <b>Two or more</b> library poses are selected (checkboxes), or");
            GUILayout.Label("• Exactly <b>one group entity</b> is selected (group header) — all members in <b>display order</b> are used.");
            GUILayout.Label("Not available during ZIP import preview. Requires at least one character selected in Studio.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Setup — Chars window</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("1. Open <b>Chars</b> (docked pane, like Tags / Sort).");
            GUILayout.Label("2. <b>Load characters from scene</b> appends new characters (gender from scene). <b>Remove missing from scene</b> deletes orange (not found) slots.");
            GUILayout.Label("3. Reorder with <b>↑</b> / <b>↓</b> — <b>top = highest priority</b>.");
            GUILayout.Label("4. <b>m</b> / <b>f</b> toggles gender for that slot; <b>✕</b> removes it. <color=#ffbb88>Orange</color> = not in scene; <color=#88ffaa>green</color> = selected in Studio.");
            GUILayout.Label("5. The list is saved to <b>pose_browser_character_config.json</b> in the Sandbox config folder.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Studio selection</b>");
            GUILayout.Label(
                "Select the characters you want to pose in Studio (workspace / gizmo). The character row shows count and names. Only <b>characters</b> count — props and accessories are ignored. Multi-apply uses <b>intersection</b> of Studio selection and your priority lists (plus unlisted selected characters at the end for untagged poses).");

            GUILayout.Space(6f);
            GUILayout.Label("<b>How poses are assigned</b>");
            GUILayout.Label("Processing follows the <b>pose list order</b> (grid display order for groups). Each character receives <b>at most one pose per apply</b> — later poses never overwrite an earlier assignment on the same character.");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Male / Female pose tags</b> (case-insensitive, on the pose only)");
            GUILayout.Label("• Pose tagged <b>Male</b> (not Female) → next available selected character marked <b>m</b> in the priority list.");
            GUILayout.Label("• Pose tagged <b>Female</b> → next available selected character marked <b>f</b> in list order.");
            GUILayout.Label("• Characters not in the list (or wrong gender for a tagged pose) are skipped for that pose.");
            GUILayout.Label("• If both Male and Female tags are present, or neither tag is present, the pose is treated as <b>untagged</b> (see below).");
            GUILayout.EndVertical();

            GUILayout.Space(4f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Untagged poses</b>");
            GUILayout.Label("• Build priority order: top-to-bottom in <b>Chars</b>, then any selected characters not in the list.");
            GUILayout.Label("• First pass: pose 1 → first free slot, pose 2 → second free slot, etc. Extra poses with no free character are <b>skipped</b>.");
            GUILayout.Label("• Second pass: any selected character still without a pose gets a pose by cycling through the pose list (only if eligible for that pose’s gender tag).");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Example workflows</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Couple (M + F tags)</b> — Group with two poses tagged Male and Female. Select both characters in Studio. Select the group header → <b>Apply to characters…</b>. Optionally save positions after arranging spacing, then re-apply the group later with layout restored.");
            GUILayout.Label("<b>Five generic poses, three characters</b> — Untagged poses; only the top three priority characters are posed; poses 4–5 skipped.");
            GUILayout.Label("<b>Two poses, four characters</b> — After the first pass, two characters remain; second pass applies poses 1 and 2 again to them (different characters, not overwrites).");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>Single-pose apply</b> (left-click thumbnail) still applies one pose to <b>all</b> selected Studio characters at once — separate from multi-character mapping.");

            GUILayout.Space(8f);
            NavButton("← Pose groups", WikiCategoryRoot, PagePoseGroups);
            NavButton("→ Pose files & actions", WikiCategoryRoot, PagePoseFiles);
            NavButton("↑ Overview", WikiCategoryRoot, PageOverview);
        }

        private static void DrawWikiImportExport()
        {
            GUILayout.Label("<size=17><b>Import & export (ZIP v2 / v3)</b></size>");
            GUILayout.Label(
                "Pose Browser exchanges packs as <b>.zip</b> archives with <b>manifest.json</b>, <b>metadata.json</b>, and pose binaries under <b>poses/</b>. <b>v3</b> adds optional <b>groups[]</b> in metadata (names, group tags, member paths). The reader only accepts <b>stored</b> (uncompressed) ZIP entries—recompressing with Deflate breaks import.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Import…</b>");
            GUILayout.Label("1. Choose a pack file. A <b>preview grid</b> replaces the normal listing.");
            GUILayout.Label("2. Check poses to import (thumbnail toggles; or checkbox with Ctrl/Shift range). <b>Cancel import</b> in the bottom bar abandons the operation.");
            GUILayout.Label("3. Pick a destination: click <b>Root only</b> or a folder name in <b>Folders</b> so the footer shows <b>Apply</b>/<b>Cancel</b> for the import.");
            GUILayout.Label("4. <b>Apply</b> writes files. <b>Tree branch</b> packs create one new subfolder (named in the manifest) under the folder you picked.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Export…</b> (selection bar)");
            GUILayout.Label("Select poses that already live in your library, then export a flat v5 ZIP with tags/favorites and pose groups (when fully selected; offsets and body heights when saved) in metadata.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Export branch…</b> / <b>Export library tree…</b>");
            GUILayout.Label("In <b>Full</b> layout, the folder footer offers <b>Export branch…</b> when a folder is selected, and <b>Export library tree…</b> at library root—hierarchical branch packs for sharing whole subtrees.");

            GUILayout.Space(6f);
            GUILayout.Label(
                "Modders: full field reference and JSON examples — <b>Modules/PoseBrowser/POSE_ZIP_FORMAT.md</b> in the HS2-Sandbox repository.");

            GUILayout.Space(8f);
            NavButton("← Pose files & actions", WikiCategoryRoot, PagePoseFiles);
            NavButton("→ Thumbnails", WikiCategoryRoot, PageThumbnails);
        }

        private static void DrawWikiThumbnails()
        {
            GUILayout.Label("<size=17><b>Thumbnails</b></size>");
            GUILayout.Label(
                "Use <b>Thumbs…</b> on the selection to capture new preview images. The overlay guides posing the camera or frame; complete or cancel from the overlay controls.");

            GUILayout.Space(6f);
            GUILayout.Label("Missing thumbnails use a neutral placeholder until a file is loaded or regenerated.");

            GUILayout.Space(8f);
            NavButton("← Import & export (ZIP)", WikiCategoryRoot, PageImportExport);
            NavButton("→ Pose items", WikiCategoryRoot, PagePoseItems);
            NavButton("→ Pose groups", WikiCategoryRoot, PagePoseGroups);
            NavButton("→ Options & data files", WikiCategoryRoot, PageOptionsData);
        }

        private static void DrawWikiOptionsData()
        {
            GUILayout.Label("<size=17><b>Options & data files</b></size>");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Options</b> side panel");
            GUILayout.Label("• <b>Card width slider</b> — minimum card width; the grid adds columns or stretches cards to fill the row.");
            GUILayout.Label("• <b>Pagination</b> — 0 = infinite scroll; otherwise cap items per page.");
            GUILayout.Label("• <b>Apply stored relative positions when applying a group</b> — global layout toggle (see <b>Pose groups</b>).");
            GUILayout.Label("• <b>Adjust relative layout for body height (saved per pose)</b> — scales saved <b>offset.y</b> from body-height ratios; requires relative positions.");
            GUILayout.Label("• <b>Select all filtered / Deselect all</b> — bulk selection in the current filtered list.");
            GUILayout.Label("• <b>Keyboard shortcuts</b> — read-only here; assign in Configuration Manager under <b>Pose Browser · Keyboard shortcuts</b> (next/previous pose; next/previous browse target matching Mini/List folder stepping). Active while the browser is focused unless a text field holds keyboard focus.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            string cfg = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox");
            GUILayout.Label($"<b>Config folder</b> <color=#cccc66>{cfg}</color>");
            GUILayout.Label("• <b>pose_browser_options.json</b> — layout tier (Full/List/Mini) with remembered window rects per mode, sort mode + direction, card width, items per page.");
            GUILayout.Label("• <b>pose_tags.tsv</b> — per-pose tags and favorites (atomic save).");
            GUILayout.Label("• <b>pose_groups.tsv</b> — group membership, tags, relative offsets, body heights per pose (v3 TSV).");
            GUILayout.Label("• <b>pose_items.tsv</b> — workspace items registered per pose (catalog paths, layout, attach data; v5 TSV).");
            GUILayout.Label("• <b>pose_browser_character_config.json</b> — unified character priority list for multi-character apply.");
            GUILayout.Label("• BepInEx <b>Pose Browser</b> section — <b>Card column width</b> and <b>Items per page</b> mirrored from Options.");

            GUILayout.Space(8f);
            NavButton("← Thumbnails", WikiCategoryRoot, PageThumbnails);
            NavButton("→ Tag storage (advanced)", WikiCategoryAdvanced, "Tag storage & migration");
        }

        private static void DrawWikiTagStorage()
        {
            GUILayout.Label("<size=17><b>Tag storage & migration</b></size>");
            GUILayout.Label(
                "Tags and favorites are stored in <b>pose_tags.tsv</b> under the Sandbox config folder. Keys are tied to pose paths relative to the library root.");

            GUILayout.Space(4f);
            GUILayout.Label(
                "A legacy <b>pose_tags.json</b> may be imported once if present. The TSV format is text-safe and avoids Unity JsonUtility pitfalls for nested data.");

            GUILayout.Space(8f);
            NavButton("← Options & data files", WikiCategoryRoot, PageOptionsData);
            NavButton("↑ Overview", WikiCategoryRoot, PageOverview);
        }
    }
}
