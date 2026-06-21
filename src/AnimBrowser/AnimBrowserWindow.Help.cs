using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const float HelpPaneDefaultWidthBase = 380f;
        private float HelpPaneDefaultWidth => AnimBrowserScale.Px(HelpPaneDefaultWidthBase);

        private bool _showHelpPane;
        private Rect _helpWindowRect;
        private Vector2 _helpScroll;

        private GUIStyle? _helpHeaderStyle;
        private GUIStyle? _helpBodyStyle;
        private GUIStyle? _helpIntroStyle;
        private GUIStyle? _helpRichStyle;

        private static readonly GUIContent GcHelpOn = new GUIContent("Help ▶", "Hide the help panel");
        private static readonly GUIContent GcHelpOff = new GUIContent("Help", "Show a guide to every Anim Browser feature");

        /// <summary>A help topic: a heading plus body lines. A line starting with "• " renders as a bullet.</summary>
        private sealed class HelpTopic
        {
            public readonly string Title;
            public readonly string[] Lines;
            public HelpTopic(string title, string[] lines)
            {
                Title = title;
                Lines = lines;
            }
        }

        private static readonly HelpTopic[] HelpTopics =
        {
            new HelpTopic("What is the Anim Browser?", new[]
            {
                "A browser for every Studio animation. Find an animation, click it, and it is applied to the "
                + "character(s) you have selected in Studio. You can also tidy the list up by renaming, "
                + "grouping and merging categories so it is easier to navigate.",
            }),
            new HelpTopic("Getting started", new[]
            {
                "• Select one or more characters in the Studio workspace tree first.",
                "• Pick a sub-category on the left, then click an animation to apply it.",
                "• If nothing happens when you click an animation, you probably have no character selected.",
            }),
            new HelpTopic("Category tree (left side)", new[]
            {
                "Shows animation groups and their sub-categories. Only sub-categories contain animations.",
                "• Click the arrow (► / ▼) to expand or collapse a group.",
                "• Click a sub-category to show its animations on the right.",
                "• ↻ at the top reloads the catalog if animations were added or changed.",
                "• Groups are collapsed by default the first time the catalog loads.",
            }),
            new HelpTopic("Grid and list views", new[]
            {
                "The Grid button (top bar) switches between a thumbnail grid and a compact text list.",
                "• Grid shows a preview image per animation; List is faster to scan when you know the names.",
                "• Card size in the Options panel changes how large the grid thumbnails are.",
            }),
            new HelpTopic("Search", new[]
            {
                "Type in the search box to filter animations by name. Matching animations are highlighted; "
                + "sub-categories whose name matches are shown too. Clear the box to see everything again.",
            }),
            new HelpTopic("Applying animations & grouped cards", new[]
            {
                "Clicking an animation applies it to every selected character.",
                "Some cards are 'grouped' and bundle related animations under one tile. Small buttons on the card "
                + "let you pick which one to apply:",
                "• m / f (and m2, f2…): the male / female version, or each participant.",
                "• in / loop / out: the intro, looping, or outro part of a sequence.",
                "• 1 / 2 / 3…: numbered variants when there is no gender or phase.",
            }),
            new HelpTopic("Playback controls", new[]
            {
                "Open the Controls panel (top bar) to adjust the animation currently playing on the selected "
                + "character(s). It only appears once a character with an animation is selected.",
                "• Speed: slider or type a value to slow down / speed up.",
                "• Pause / Play and Time: freeze playback and scrub to a specific moment.",
                "• Force loop: keep a one-shot animation repeating.",
                "• Restart animation / Restart all in scene: replay from the beginning.",
                "• The Controls panel can be docked next to the window or floated freely; the floating panel "
                + "stays usable even when the main window is closed.",
            }),
            new HelpTopic("Characters & priority", new[]
            {
                "The character section in the top bar shows who the animation will be applied to.",
                "Open the Characters panel to set a priority order used when several characters are selected "
                + "at once (top = highest priority). Useful for paired animations so each role goes to the "
                + "right character.",
            }),
            new HelpTopic("Renaming", new[]
            {
                "Give friendlier names to anything: select a sub-category, group, card or animation and use "
                + "Rename… (in the tree action bar or the panel below the grid). Names are remembered and only "
                + "change what you see — the original is never lost.",
            }),
            new HelpTopic("Grouping animations into one card", new[]
            {
                "Select two or more animations (use the checkbox on each card), then 'Group selected…'. A review "
                + "panel opens where you can confirm the bundle and adjust each animation's role (gender / phase). "
                + "Use 'Ungroup' to split a card back into separate animations.",
            }),
            new HelpTopic("Merging categories", new[]
            {
                "Combine sub-categories that belong together into a single entry.",
                "• Select two or more sub-categories of the same group, then 'Merge categories…'.",
                "• Select a merged entry plus another sub-category to add it to the merge.",
                "• If the button is greyed out, hover it: it explains why (e.g. the sub-categories are in "
                + "different top-level groups).",
            }),
            new HelpTopic("Merging groups", new[]
            {
                "Combine whole top-level groups; matching sub-categories (by name) are lined up automatically.",
                "• Select two or more groups, then 'Merge groups…'.",
                "• Select an existing merged group plus another group and use 'Add to group merge…' to extend "
                + "it without starting over.",
            }),
            new HelpTopic("Joining & splitting sub-categories", new[]
            {
                "Inside a merged group, sub-categories with different names can be joined into one.",
                "• Select the sub-categories (across the merged groups), then 'Join subcategories…'.",
                "• 'Split subcategories' on a joined entry separates them again.",
            }),
            new HelpTopic("Undoing merges", new[]
            {
                "• 'Unmerge' removes a merge and restores the original groups / categories.",
                "• 'Unmerge subcategory' pulls a single sub-category back out of a group merge.",
                "Nothing is deleted — unmerging always brings the original catalog entries back.",
            }),
            new HelpTopic("The review panel", new[]
            {
                "Whenever you merge or group, a review panel opens so you can check the result before applying.",
                "• Adjust each animation's gender and phase with the small buttons.",
                "• Skip keeps a single animation at its original place instead of grouping it.",
                "• As singles shows animations individually inside the merged category without bundling them.",
                "• Confirm applies everything at once; Cancel discards it with no changes made.",
            }),
            new HelpTopic("Thumbnails & hover preview", new[]
            {
                "Grid cards can show captured PNG thumbnails (UserData/com.hs2.sandbox/anim_thumbnails) or, on HS2, "
                + "a live stick-figure hover preview while the cursor is on the card.",
                "• Options → Thumbnails: capture all listed or missing-only; uses the same green-frame overlay as Pose Browser.",
                "• Select cards with checkboxes, then Capture thumbnail(s)… in the content action bar.",
                "• Options → Hover animation preview (HS2): toggle preview and set camera angle, rotation speed, and pitch.",
            }),
            new HelpTopic("Options panel", new[]
            {
                "• UI scale: enlarge the whole browser for high-resolution screens.",
                "• Card size: thumbnail size in the grid.",
                "• Hover animation preview: live stick-figure in the hovered card thumbnail (HS2 grid; works without scene characters).",
                "• Preview camera angle / rotation speed / pitch: framing for hover preview (HS2).",
                "• Thumbnails: capture screen PNGs for the current sub-category or selected cards.",
                "• Options → Debug → Dump preview skeleton data: writes embedded rig + joint diagnostics to BepInEx config (share the file for stick-figure tuning).",
                "• Hide non-Studio animation lists: hide odd 'Group 101 / Category 2018' entries that come "
                + "from H-scene-only lists. On by default.",
                "• Group controls by proximity: only merge playback controls for characters that are close together (3.5 units).",
                "• Keyboard shortcuts: overview of keys (assigned in BepInEx configuration).",
                "• Dissolve all groups: reset every group and merge you have made.",
            }),
            new HelpTopic("Window tips", new[]
            {
                "• Drag the title bar to move; drag the ◢ corner to resize.",
                "• − minimises to a small 'AB' chip you can drag and click to restore; × closes the browser.",
                "• Panels (Controls, Characters, Options, Help, Review) dock to the right and follow the window.",
                "• Japanese animation names are translated automatically when a translator is available.",
            }),
        };

        private void InitHelpStyles()
        {
            if (_helpHeaderStyle != null)
                return;

            _helpHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.9f, 1f, 1f) }
            };
            _helpBodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _helpIntroStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.78f, 0.8f, 0.84f, 1f) }
            };
            _helpRichStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
        }

        private void DrawHelpWindowContent(int paneId)
        {
            InitHelpStyles();

            GUILayout.Label(
                "A quick guide to everything the Anim Browser can do. Keep it open while you explore.",
                _helpIntroStyle);
            GUILayout.Space(4f);

            _helpScroll = GUILayout.BeginScrollView(_helpScroll, false, true, GUILayout.ExpandHeight(true));

            for (int t = 0; t < HelpTopics.Length; t++)
            {
                HelpTopic topic = HelpTopics[t];
                if (t > 0)
                    GUILayout.Space(10f);
                GUILayout.Label(topic.Title, _helpHeaderStyle);
                GUILayout.Space(2f);
                for (int i = 0; i < topic.Lines.Length; i++)
                    GUILayout.Label(topic.Lines[i], _helpBodyStyle);
            }

            GUILayout.Space(12f);
            AnimBrowserWikiRegistration.DrawHelpWikiSection(_helpRichStyle!);

            GUILayout.Space(8f);
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            if (GUILayout.Button("Close help", AnimBrowserScale.H(24f)))
            {
                _showHelpPane = false;
                _options.showHelpPane = false;
                SavePersistedOptions();
            }
        }
    }
}
