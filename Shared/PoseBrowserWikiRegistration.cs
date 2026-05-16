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
        public const string PageThumbnails = "Thumbnails";
        public const string PageOptionsData = "Options & data files";

        private static bool _registerSucceeded;
        private static bool _loggedMissingWiki;
        private static ManualLogSource? _log;
        private static object? _api;
        private static MethodInfo? _registerPage;
        private static MethodInfo? _openPage;
        private static MethodInfo? _openImage;
        private static Texture2D? _wikiBannerTex;

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
                InvokeRegister(WikiCategoryRoot, PageThumbnails, DrawWikiThumbnails);
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

        /// <summary>Opens a wiki page if HS2Wiki API is available (safe no-op otherwise).</summary>
        public static void TryOpenWikiPage(string category, string pageName)
        {
            if (_openPage == null && _api == null && !TryResolveApi(out _api, out _, out _openPage, out _openImage))
                return;
            if (_openPage == null || _api == null) return;
            try
            {
                _openPage.Invoke(_api, new object[] { category, pageName });
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"OpenWikiPage failed: {ex.Message}");
            }
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
            NavButton("→ Pose files & actions", WikiCategoryRoot, PagePoseFiles);
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
            GUILayout.Label("<b>All poses</b> — recursive listing from the pose root (every .png/.dat pose in subfolders).");
            GUILayout.Label("<b>Root only</b> — files directly in the pose root, no subfolders.");
            GUILayout.Label("<b>Folder rows</b> — click the name to show only that folder (non-recursive). Use ►/▼ to expand or collapse.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Footer actions</b> (under the tree)");
            GUILayout.Label("• <b>Library root</b>: only <b>New folder…</b> (creates a subfolder under the pose root).");
            GUILayout.Label("• <b>Selected folder</b>: <b>Rename…</b>, <b>New folder…</b>, <b>Delete folder…</b> (only if the folder is empty).");

            GUILayout.Space(8f);
            NavButton("← Overview", WikiCategoryRoot, PageOverview);
            NavButton("→ Search & filters", WikiCategoryRoot, PageSearchFilters);
        }

        private static void DrawWikiSearchFilters()
        {
            GUILayout.Label("<size=17><b>Search & filters</b></size>");
            GUILayout.Label("Filters apply to the <i>current folder view</i> or <i>All poses</i>, then affect the grid and counts.");

            GUILayout.Space(4f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Search</b> — filters by display name / path. Toggle <b>.*</b> for case-insensitive regex; invalid patterns show an error line under the bar.");
            GUILayout.Label("<b>★</b> — show only poses marked favorite (see Tags / Fav Selected).");
            GUILayout.Label("<b>AND / OR</b> — combines active tag filters: every selected tag must match (AND) or any one (OR).");
            GUILayout.Label("<b>Tags (n)</b> — opens the tag checklist; <b>Clear All</b> removes tag filters (not tags on files).");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            NavButton("← Folders & library", WikiCategoryRoot, PageFolders);
            NavButton("→ Grid & selection", WikiCategoryRoot, PageGridSelection);
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

            GUILayout.Space(8f);
            NavButton("← Search & filters", WikiCategoryRoot, PageSearchFilters);
            NavButton("→ Pose files & actions", WikiCategoryRoot, PagePoseFiles);
        }

        private static void DrawWikiPoseFiles()
        {
            GUILayout.Label("<size=17><b>Pose files & actions</b></size>");
            GUILayout.Label("Appears when at least one card is selected.");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Save Pose</b> (top bar) — writes the current character pose into the <i>active save folder</i>: selected folder, or root when using <b>All poses</b>.");
            GUILayout.Label("<b>Update Pose</b> (one selected) — overwrite file from the scene; choose keeping or regenerating the thumbnail.");
            GUILayout.Label("<b>Rename…</b> — optional rename of file to match display name.");
            GUILayout.Label("<b>Tag Selected</b> — add/remove tags for all selected items (database-backed).");
            GUILayout.Label("<b>Fav Selected</b> — toggle favorite flag (★ filter).");
            GUILayout.Label("<b>Move… / Copy…</b> — pick destination in the <b>Folders</b> tree (<b>Root only</b> or a folder; highlighted), then <b>Apply</b> or <b>Cancel</b> in the folder footer. The grid does not reload while picking, so your selection is preserved. <b>New folder…</b> still works. Tags move with paths when applicable.");
            GUILayout.Label("<b>Delete…</b> — copies into <b>!_AutoBackup</b> then removes files; confirmations are required.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>Character</b> row — shows Studio selection count and names (tooltip). Non-character selections are ignored for pose apply and save.");

            GUILayout.Space(8f);
            NavButton("← Grid & selection", WikiCategoryRoot, PageGridSelection);
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
            NavButton("← Pose files & actions", WikiCategoryRoot, PagePoseFiles);
            NavButton("→ Options & data files", WikiCategoryRoot, PageOptionsData);
        }

        private static void DrawWikiOptionsData()
        {
            GUILayout.Label("<size=17><b>Options & data files</b></size>");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Options</b> side panel");
            GUILayout.Label("• <b>Card width slider</b> — minimum card width; the grid adds columns or stretches cards to fill the row.");
            GUILayout.Label("• <b>Pagination</b> — 0 = infinite scroll; otherwise cap items per page.");
            GUILayout.Label("• <b>Select all filtered / Deselect all</b> — bulk selection in the current filtered list.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            string cfg = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox");
            GUILayout.Label($"<b>Config folder</b> <color=#cccc66>{cfg}</color>");
            GUILayout.Label("• <b>pose_browser_options.json</b> — card width, items per page.");
            GUILayout.Label("• <b>pose_tags.tsv</b> — primary tag database (atomic save).");

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
