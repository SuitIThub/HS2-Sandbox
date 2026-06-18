using System;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Registers Anim Browser documentation with <see href="https://github.com/SuitIThub/HS2Wiki">HS2Wiki</see>
    /// when present (soft dependency via reflection). See <c>docs/AnimBrowser-HS2Wiki-Manual.md</c> for the full manual.
    /// The in-game Help pane carries a short version; these pages are the long-form guide.
    /// </summary>
    internal static class AnimBrowserWikiRegistration
    {
        public const string WikiCategoryRoot = "HS2 Sandbox/Anim Browser";

        public const string PageOverview = "Overview";
        public const string PageGettingStarted = "Getting started";
        public const string PageBrowsing = "Browsing & search";
        public const string PageApplying = "Applying animations";
        public const string PagePlayback = "Playback controls";
        public const string PageCharacters = "Characters & priority";
        public const string PageGrouping = "Grouping animations";
        public const string PageMerging = "Merging categories & groups";
        public const string PageReview = "The review panel";
        public const string PageOptions = "Options & data files";
        public const string PageTroubleshooting = "Troubleshooting";

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

        /// <summary>True when the HS2Wiki assembly is loaded and the API resolved.</summary>
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
                    log.LogInfo("HS2Wiki not loaded; Anim Browser wiki pages were not registered (optional).");
                }
                return;
            }

            try
            {
                InvokeRegister(WikiCategoryRoot, PageOverview, DrawWikiOverview);
                InvokeRegister(WikiCategoryRoot, PageGettingStarted, DrawWikiGettingStarted);
                InvokeRegister(WikiCategoryRoot, PageBrowsing, DrawWikiBrowsing);
                InvokeRegister(WikiCategoryRoot, PageApplying, DrawWikiApplying);
                InvokeRegister(WikiCategoryRoot, PagePlayback, DrawWikiPlayback);
                InvokeRegister(WikiCategoryRoot, PageCharacters, DrawWikiCharacters);
                InvokeRegister(WikiCategoryRoot, PageGrouping, DrawWikiGrouping);
                InvokeRegister(WikiCategoryRoot, PageMerging, DrawWikiMerging);
                InvokeRegister(WikiCategoryRoot, PageReview, DrawWikiReview);
                InvokeRegister(WikiCategoryRoot, PageOptions, DrawWikiOptions);
                InvokeRegister(WikiCategoryRoot, PageTroubleshooting, DrawWikiTroubleshooting);

                _registerSucceeded = true;
                log.LogInfo("Registered Anim Browser pages with HS2Wiki (F3).");
            }
            catch (Exception ex)
            {
                log.LogWarning($"Anim Browser wiki registration failed: {ex.Message}");
            }
        }

        /// <summary>Opens the HS2Wiki window if the plugin is loaded.</summary>
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

        /// <summary>HS2Wiki block for the in-game Help pane: links to the full manual or a download prompt.</summary>
        public static void DrawHelpWikiSection(GUIStyle rich)
        {
            GUILayout.Label("<b>Full manual (HS2Wiki)</b>", rich);
            if (!IsWikiInstalled)
            {
                GUILayout.Label(
                    "Install <b>HS2Wiki</b> for the complete, detailed Anim Browser manual (linked pages, images) "
                    + "in its own window. This Help panel is the short version.",
                    rich);
                if (GUILayout.Button("Open HS2Wiki on GitHub", GUILayout.Height(24f)))
                    Application.OpenURL(WikiDownloadUrl);
                GUILayout.Label(
                    "After installing, restart Studio. Pages appear under <b>HS2 Sandbox / Anim Browser</b> (default key <b>F3</b>).",
                    rich);
                return;
            }

            GUILayout.Label(
                "Open the full manual in <b>HS2Wiki</b> (<b>F3</b> by default). These buttons jump to a page:",
                rich);
            if (GUILayout.Button("Wiki: Overview", GUILayout.Height(22f)))
                TryOpenWikiPage(WikiCategoryRoot, PageOverview);
            if (GUILayout.Button("Wiki: Merging categories & groups", GUILayout.Height(22f)))
                TryOpenWikiPage(WikiCategoryRoot, PageMerging);
            if (GUILayout.Button("Wiki: Grouping animations", GUILayout.Height(22f)))
                TryOpenWikiPage(WikiCategoryRoot, PageGrouping);
            if (GUILayout.Button("Wiki: Playback controls", GUILayout.Height(22f)))
                TryOpenWikiPage(WikiCategoryRoot, PagePlayback);
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

        public static void TryOpenIconImage()
        {
            string? path = TryGetIconPath();
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

        private static string? TryGetIconPath()
        {
            string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) return null;
            string p = Path.Combine(dir, "anim-icon.png");
            return File.Exists(p) ? p : null;
        }

        private static Texture2D WikiBannerTexture()
        {
            if (_wikiBannerTex == null)
                _wikiBannerTex = ToolbarIconLoader.LoadPng("anim-icon.png");
            return _wikiBannerTex;
        }

        private static void NavButton(string label, string category, string page)
        {
            if (GUILayout.Button(label, GUILayout.Height(26f)))
                TryOpenWikiPage(category, page);
        }

        // ---- Pages ----------------------------------------------------------

        private static void DrawWikiOverview()
        {
            GUILayout.Label("<size=18><b>Anim Browser</b></size>");
            GUILayout.Label("<i>Find any Studio animation, apply it to your selected characters, and organize the list to your liking.</i>");
            GUILayout.Space(6f);
            GUILayout.Label(
                "The Anim Browser lists every animation Studio knows about in a category tree with thumbnails. "
                + "Click an animation to apply it to the character(s) you have selected in Studio. You can also "
                + "tidy the list with <b>renames</b>, bundle related animations into one <b>card</b>, and <b>merge</b> "
                + "categories or whole groups so navigation is faster.");

            GUILayout.Space(8f);
            var tex = WikiBannerTexture();
            if (tex != null && tex.width > 4)
            {
                GUILayout.Label("<b>Toolbar icon</b> (click to open in image viewer):");
                if (GUILayout.Button(tex, GUI.skin.box, GUILayout.Width(Mathf.Min(128f, tex.width)), GUILayout.Height(Mathf.Min(128f, tex.height))))
                    TryOpenIconImage();
                GUILayout.Space(8f);
            }

            GUILayout.Label("<b>Quick navigation</b>");
            NavButton("→ Getting started", WikiCategoryRoot, PageGettingStarted);
            NavButton("→ Browsing & search", WikiCategoryRoot, PageBrowsing);
            NavButton("→ Applying animations", WikiCategoryRoot, PageApplying);
            NavButton("→ Playback controls", WikiCategoryRoot, PagePlayback);
            NavButton("→ Characters & priority", WikiCategoryRoot, PageCharacters);
            NavButton("→ Grouping animations", WikiCategoryRoot, PageGrouping);
            NavButton("→ Merging categories & groups", WikiCategoryRoot, PageMerging);
            NavButton("→ The review panel", WikiCategoryRoot, PageReview);
            NavButton("→ Options & data files", WikiCategoryRoot, PageOptions);
            NavButton("→ Troubleshooting", WikiCategoryRoot, PageTroubleshooting);

            GUILayout.Space(10f);
            GUILayout.Label("<b>In-window help</b>: open the Anim Browser and click <b>Help</b> for a short on-screen reference.");
        }

        private static void DrawWikiGettingStarted()
        {
            GUILayout.Label("<size=17><b>Getting started</b></size>");
            GUILayout.Label("Three steps to apply your first animation:");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("1. In the Studio workspace tree, <b>select the character(s)</b> you want to animate.");
            GUILayout.Label("2. In the Anim Browser, click a <b>sub-category</b> on the left to list its animations.");
            GUILayout.Label("3. <b>Click an animation</b> — it is applied to every selected character at once.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Nothing happens when I click?</b>");
            GUILayout.Label(
                "You almost certainly have no character selected in Studio, or the selected object is a prop / "
                + "accessory rather than a character. The character row in the top bar shows who is targeted.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Opening and closing the window</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• Toggle the browser with its <b>toolbar icon</b> on the Studio left toolbar.");
            GUILayout.Label("• <b>−</b> minimises to a small <b>AB</b> chip (drag it around; click to restore).");
            GUILayout.Label("• <b>×</b> closes the browser.");
            GUILayout.Label("• Drag the title bar to move; drag the <b>◢</b> corner to resize.");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            NavButton("← Overview", WikiCategoryRoot, PageOverview);
            NavButton("→ Browsing & search", WikiCategoryRoot, PageBrowsing);
        }

        private static void DrawWikiBrowsing()
        {
            GUILayout.Label("<size=17><b>Browsing & search</b></size>");

            GUILayout.Label("<b>The category tree (left)</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• Shows animation <b>groups</b> and their <b>sub-categories</b>. Only sub-categories hold animations.");
            GUILayout.Label("• Click the <b>► / ▼</b> arrow to expand or collapse a group.");
            GUILayout.Label("• Click a <b>sub-category</b> to show its animations on the right.");
            GUILayout.Label("• <b>↻</b> reloads the catalog (after adding mods / animations).");
            GUILayout.Label("• Groups start collapsed the first time the catalog loads.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Grid vs list view</b>");
            GUILayout.Label(
                "The <b>Grid / List</b> button (top bar) switches between a thumbnail grid and a compact text list. "
                + "Grid shows a preview per animation; List is quicker to scan when you know the names. Thumbnail size "
                + "is set by <b>Card size</b> in Options.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Search</b>");
            GUILayout.Label(
                "Type in the search box to filter animations by name. Matching animations are highlighted and "
                + "sub-categories whose name matches are shown too. Clear the box to see everything again.");

            GUILayout.Space(8f);
            NavButton("← Getting started", WikiCategoryRoot, PageGettingStarted);
            NavButton("→ Applying animations", WikiCategoryRoot, PageApplying);
        }

        private static void DrawWikiApplying()
        {
            GUILayout.Label("<size=17><b>Applying animations</b></size>");
            GUILayout.Label("Clicking an animation applies it to <b>every</b> character selected in Studio.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Grouped cards</b>");
            GUILayout.Label(
                "Some tiles are <b>grouped cards</b> that bundle related animations under one preview. Small buttons "
                + "on the card pick which one to apply:");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>m / f</b> (and <b>m2, f2</b>…) — the male / female version, or each participant in a paired animation.");
            GUILayout.Label("<b>in / loop / out</b> — the intro, looping, or outro part of a sequence.");
            GUILayout.Label("<b>1 / 2 / 3</b>… — numbered variants when there is no gender or phase to label.");
            GUILayout.EndVertical();
            GUILayout.Label(
                "Grouped cards are created by you (see <b>Grouping animations</b>) or proposed automatically when you "
                + "merge categories.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Selecting without applying</b>");
            GUILayout.Label(
                "Each card has a small <b>checkbox</b> in the corner — use it to select animations (for grouping) "
                + "without applying them.");

            GUILayout.Space(8f);
            NavButton("← Browsing & search", WikiCategoryRoot, PageBrowsing);
            NavButton("→ Playback controls", WikiCategoryRoot, PagePlayback);
            NavButton("→ Grouping animations", WikiCategoryRoot, PageGrouping);
        }

        private static void DrawWikiPlayback()
        {
            GUILayout.Label("<size=17><b>Playback controls</b></size>");
            GUILayout.Label(
                "Open the <b>Controls</b> panel (top bar) to adjust the animation currently playing on the selected "
                + "character(s). It only shows content once a character with an animation is selected.");

            GUILayout.Space(6f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Speed</b> — slider or type a value to slow down / speed up playback.");
            GUILayout.Label("<b>Pause / Play</b> and <b>Time</b> — freeze playback and scrub to a specific moment.");
            GUILayout.Label("<b>Force loop</b> — keep a one-shot animation repeating.");
            GUILayout.Label("<b>Restart animation</b> / <b>Restart all in scene</b> — replay from the beginning.");
            GUILayout.Label("<b>Show items</b> — reveal the individual animations loaded on the character.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Docked or floating</b>");
            GUILayout.Label(
                "The Controls panel can be docked beside the main window or floated as an independent window. The "
                + "floating panel stays usable even when the main Anim Browser window is closed, so you can keep just "
                + "the playback controls on screen.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Grouping controls by proximity</b>");
            GUILayout.Label(
                "When several characters share an animation, their controls are merged into one box. The Options "
                + "toggle <b>Group controls by proximity</b> only merges characters that are physically close in the scene.");

            GUILayout.Space(8f);
            NavButton("← Applying animations", WikiCategoryRoot, PageApplying);
            NavButton("→ Characters & priority", WikiCategoryRoot, PageCharacters);
        }

        private static void DrawWikiCharacters()
        {
            GUILayout.Label("<size=17><b>Characters & priority</b></size>");
            GUILayout.Label(
                "The character section in the top bar shows who an animation will be applied to — the character(s) "
                + "you have selected in Studio. Props and accessories are ignored.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Priority list (Characters panel)</b>");
            GUILayout.Label(
                "Open the <b>Characters</b> panel to set a priority order used when several characters are selected at "
                + "once. <b>Top = highest priority</b>. This matters for paired (male/female) animations so each role "
                + "lands on the intended character.");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• Reorder with the arrows; the top entry is filled first.");
            GUILayout.Label("• The list is remembered between sessions.");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            NavButton("← Playback controls", WikiCategoryRoot, PagePlayback);
            NavButton("→ Grouping animations", WikiCategoryRoot, PageGrouping);
            NavButton("→ Merging categories & groups", WikiCategoryRoot, PageMerging);
        }

        private static void DrawWikiGrouping()
        {
            GUILayout.Label("<size=17><b>Grouping animations into one card</b></size>");
            GUILayout.Label(
                "Bundle several related animations into a single tile so the grid is less cluttered and variants are "
                + "one click apart.");

            GUILayout.Space(6f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("1. Tick the <b>checkbox</b> on two or more animation cards.");
            GUILayout.Label("2. Click <b>Group selected…</b> (panel below the grid).");
            GUILayout.Label("3. The <b>review panel</b> opens — confirm the bundle and adjust each animation's role.");
            GUILayout.Label("4. Click <b>Confirm</b>. The animations now share one card with role buttons.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Roles on the card</b>");
            GUILayout.Label(
                "In the review you set each animation's <b>gender</b> (m / f / m2 / f2…) and <b>phase</b> "
                + "(in / loop / out). Those become the buttons on the finished card. The browser guesses roles from "
                + "the names; you only fix what it got wrong.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Ungroup</b>");
            GUILayout.Label(
                "Select a grouped card and click <b>Ungroup</b> to split it back into separate animations. The "
                + "animations themselves are never deleted.");

            GUILayout.Space(8f);
            NavButton("← Applying animations", WikiCategoryRoot, PageApplying);
            NavButton("→ The review panel", WikiCategoryRoot, PageReview);
            NavButton("→ Merging categories & groups", WikiCategoryRoot, PageMerging);
        }

        private static void DrawWikiMerging()
        {
            GUILayout.Label("<size=17><b>Merging categories & groups</b></size>");
            GUILayout.Label(
                "Merging reshapes the <b>category tree</b> so related content lives together. Nothing is ever deleted "
                + "— merges are a display layer you can always undo. Select tree nodes (Ctrl+click for several), then "
                + "use the buttons under the tree.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Merge categories</b> (sub-categories of one group)");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• Select two or more sub-categories of the <b>same group</b>, then <b>Merge categories…</b>.");
            GUILayout.Label("• Select an existing merged entry plus another sub-category to <b>extend</b> the merge.");
            GUILayout.Label("• If the button is greyed out, hover it — it explains why (for example the sub-categories "
                + "live in different top-level groups, which needs a group merge first).");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Merge groups</b> (whole top-level groups)");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• Select two or more <b>groups</b>, then <b>Merge groups…</b>. Sub-categories with the same "
                + "name are lined up together automatically.");
            GUILayout.Label("• Select a merged group plus another group and use <b>Add to group merge…</b> to add it "
                + "without starting over.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Joining & splitting sub-categories</b> (inside a group merge)");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• <b>Join subcategories…</b> — select sub-categories from the merged groups that should be "
                + "one entry even though their names differ (e.g. \"Cowgirl\" and \"Cow girl\").");
            GUILayout.Label("• <b>Split subcategories</b> — on a joined entry, separates the parts again (they stay "
                + "inside the merged group).");
            GUILayout.Label("• Two sub-categories that belong to the <b>same</b> source group are combined as a real "
                + "category merge instead — same result: one entry.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Undoing merges</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("• <b>Unmerge</b> — removes a merge and restores the original groups / categories.");
            GUILayout.Label("• <b>Unmerge subcategory</b> — pulls a single sub-category back out of a group merge; the "
                + "rest stays merged.");
            GUILayout.Label("• <b>Dissolve all groups</b> (Options) — resets every merge and group you have made.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Renaming</b>");
            GUILayout.Label(
                "Select any node (or a card / animation) and use <b>Rename…</b> to give it a friendlier label. Names "
                + "are remembered and only change what you see; the original is never lost.");

            GUILayout.Space(6f);
            GUILayout.Label(
                "Every merge opens the <b>review panel</b> first, so you can check the result before it is applied. "
                + "Cancel discards it with no changes.");

            GUILayout.Space(8f);
            NavButton("← Grouping animations", WikiCategoryRoot, PageGrouping);
            NavButton("→ The review panel", WikiCategoryRoot, PageReview);
            NavButton("↑ Overview", WikiCategoryRoot, PageOverview);
        }

        private static void DrawWikiReview()
        {
            GUILayout.Label("<size=17><b>The review panel</b></size>");
            GUILayout.Label(
                "Whenever you group animations or merge categories / groups, a <b>review panel</b> docks to the right "
                + "so you can check and tweak the result <i>before</i> anything is applied.");

            GUILayout.Space(6f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Gender / phase buttons</b> — fix each animation's role (m / f / m2…, and in / loop / out). "
                + "The browser pre-fills its best guess from the names.");
            GUILayout.Label("<b>Skip</b> — keep one animation at its original category instead of grouping it.");
            GUILayout.Label("<b>As singles</b> — show these animations individually inside the merged category, without "
                + "bundling them into a card.");
            GUILayout.Label("<b>Confirm</b> — apply everything at once.");
            GUILayout.Label("<b>Cancel</b> — discard the whole review; nothing is changed.");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Empty review?</b>");
            GUILayout.Label(
                "An empty review just means no animations could be auto-bundled into cards — the merge / join itself is "
                + "still valid. Click <b>Confirm</b> and the categories are combined; you simply get no grouped cards.");

            GUILayout.Space(6f);
            GUILayout.Label("<b>Headings (sections)</b>");
            GUILayout.Label(
                "Proposed cards are grouped under headings by their original sub-category. Click a heading to collapse "
                + "it while reviewing a long list.");

            GUILayout.Space(8f);
            NavButton("← Merging categories & groups", WikiCategoryRoot, PageMerging);
            NavButton("→ Options & data files", WikiCategoryRoot, PageOptions);
        }

        private static void DrawWikiOptions()
        {
            GUILayout.Label("<size=17><b>Options & data files</b></size>");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>UI scale</b> — enlarge the whole browser (text, buttons, cards) for 4K / high-DPI screens.");
            GUILayout.Label("<b>Card size</b> — thumbnail width in the grid.");
            GUILayout.Label("<b>Hide non-Studio animation lists</b> — hide odd <i>Group 101 / Category 2018</i> entries that "
                + "come from H-scene-only lists. On by default; turn off to see everything.");
            GUILayout.Label("<b>Group controls by proximity</b> — only merge playback controls for characters that are "
                + "close together in the scene.");
            GUILayout.Label("<b>Keyboard shortcuts</b> — read-only overview; assign keys in BepInEx Configuration Manager "
                + "under <b>Anim Browser · Keyboard shortcuts</b>.");
            GUILayout.Label("<b>Dissolve all groups</b> — remove every display group and tree merge you have created (reset).");
            GUILayout.EndVertical();

            GUILayout.Space(6f);
            GUILayout.Label("<b>Where settings are stored</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Config folder: <color=#cccc66>BepInEx/config/com.hs2.sandbox/</color>");
            GUILayout.Label("• <b>anim_browser_options.json</b> — window size/position, view mode, panel widths, card size, "
                + "UI preferences.");
            GUILayout.Label("• <b>anim_browser_groups.json</b> — your renames, grouped cards, and category / group merges.");
            GUILayout.EndVertical();
            GUILayout.Label("Both are plain files; keep a backup before editing by hand.");

            GUILayout.Space(8f);
            NavButton("← The review panel", WikiCategoryRoot, PageReview);
            NavButton("→ Troubleshooting", WikiCategoryRoot, PageTroubleshooting);
        }

        private static void DrawWikiTroubleshooting()
        {
            GUILayout.Label("<size=17><b>Troubleshooting</b></size>");

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Clicking an animation does nothing</b> — select a character in Studio first; props / "
                + "accessories are ignored.");
            GUILayout.Label("<b>The list is empty / missing entries</b> — clear the search box; check <b>Hide non-Studio "
                + "animation lists</b> in Options; press <b>↻</b> to reload the catalog.");
            GUILayout.Label("<b>A merge button is greyed out</b> — hover it for the reason (e.g. cross-group merge needs a "
                + "group merge first).");
            GUILayout.Label("<b>The review panel is empty</b> — normal when no cards can be auto-built; click <b>Confirm</b> "
                + "and the merge still applies.");
            GUILayout.Label("<b>Wrong character gets a paired animation</b> — set the order in the <b>Characters</b> panel "
                + "(top = first) and use the m / f buttons on the card.");
            GUILayout.Label("<b>I want to start over</b> — <b>Dissolve all groups</b> in Options removes all your merges "
                + "and grouped cards.");
            GUILayout.Label("<b>Wiki pages missing</b> — is HS2Wiki installed? Restart Studio after installing it.");
            GUILayout.EndVertical();

            GUILayout.Space(8f);
            NavButton("← Options & data files", WikiCategoryRoot, PageOptions);
            NavButton("↑ Overview", WikiCategoryRoot, PageOverview);
        }
    }
}
