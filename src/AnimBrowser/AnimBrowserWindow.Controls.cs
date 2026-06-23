using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private bool _controlsInitialized;
        private bool _controlsGroupByProximity = true;
        private int _controlsSelectionFingerprint = -1;
        private Vector2 _controlsScroll;

        private readonly List<AnimPlaybackControlGroup> _controlGroups = new List<AnimPlaybackControlGroup>();

        private GUIStyle? _controlsSectionTitleStyle;
        private GUIStyle? _controlsFieldLabelStyle;
        private GUIStyle? _controlsInfoStyle;
        private GUIStyle? _controlsGroupTitleStyle;

        private sealed class AnimPlaybackControlGroup
        {
            public bool HasAnimation;
            public AnimCatalogRef CatalogRef;
            public string DisplayGroupId = "";
            public AnimAnimationMetadata Metadata;
            public string Title = "";
            public string CharactersSummary = "";
            public readonly List<string> LoadedVariantLines = new List<string>();
            public readonly List<OCIChar> Characters = new List<OCIChar>();
            public float Speed = 1f;
            public float SpeedBeforePause = 1f;
            public bool IsPaused;
            public float Pattern;
            public float ScrubTime;
            public bool ScrubUserOverride;
            public bool HasClipLength;
            public float ClipLengthSeconds = 1f;
            public int ScrubTimeLabelMillis = -1;
            public string ScrubTimeLabel = "0.00 s";
            public int TimingLabelKey = -1;
            public string TimingLabel = "";
            public bool ShowPattern;
            public bool OptionVisible = true;
            public bool ForceLoop;
            public readonly List<CharExtraControlEntry> ExtraControls = new List<CharExtraControlEntry>();
        }

        private sealed class CharExtraControlEntry
        {
            public OCIChar Char = null!;
            public string DisplayName = "";
            public bool HasExtra1;
            public bool HasExtra2;
            public string Extra1Label = "";
            public string Extra2Label = "";
            public float Extra1;
            public float Extra2;
        }

        private static readonly GUIContent GcSpeed = new GUIContent("Speed");
        private static readonly GUIContent GcPause = new GUIContent("❚❚", "Pause playback");
        private static readonly GUIContent GcPlay = new GUIContent("▶", "Resume playback");
        private static readonly GUIContent GcMotion = new GUIContent("Motion");
        private static readonly GUIContent GcScrub = new GUIContent("Time");
        private static readonly GUIContent GcShowItems = new GUIContent("Show items");
        private static readonly GUIContent GcForceLoop = new GUIContent("Force loop");
        private static readonly GUIContent GcRestartGroup = new GUIContent("Restart animation");
        private static readonly GUIContent GcRestartAll = new GUIContent("Restart all in scene");
        private static readonly GUIContent GcNoSelection = new GUIContent("Select character(s) to adjust playback controls.");

        private void InitControlsStyles()
        {
            InitStyles();
            if (_controlsSectionTitleStyle != null)
                return;

            _controlsSectionTitleStyle = new GUIStyle(_reviewSectionTitleStyle!)
            {
                margin = new RectOffset(4, 4, 2, 4)
            };

            _controlsGroupTitleStyle = new GUIStyle(_controlsSectionTitleStyle)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            _controlsFieldLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                margin = new RectOffset(0, 4, 2, 2)
            };

            _controlsInfoStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = Mathf.Max(10, GUI.skin.label.fontSize - 1),
                margin = new RectOffset(2, 2, 1, 1)
            };
        }

        private void InitControlsState()
        {
            _controlsInitialized = true;
            SyncControlsFromSelectionIfChanged(force: true);
        }

        private void LateUpdate()
        {
            // While a thumbnail capture is running the apply-target characters are paused and held at
            // the chosen progress frame; this owns the pose (and restores speeds when it ends), so it
            // runs regardless of whether the controls pane is open.
            if (_thumbCaptureHoldChars != null)
            {
                ThumbCaptureHoldLateUpdate();
                if (IsThumbnailCaptureActive)
                    return;
            }

            // While paused (speed 0) the animator's automatic update keeps re-sampling its own stored
            // frame every frame. A scrub applied from OnGUI is therefore reverted on the next animator
            // update, which shows up as a 1-frame flicker. Re-assert the held scrub position here:
            // LateUpdate runs after the animator update and before rendering, so our pose is what gets
            // drawn. Only runs while the controls are visible and a group is actually paused.
            if (!IsAnyControlsVisible)
                return;

            for (int i = 0; i < _controlGroups.Count; i++)
            {
                AnimPlaybackControlGroup g = _controlGroups[i];
                if (g.HasAnimation && g.IsPaused)
                    AnimPlaybackService.SetNormalizedTime(g.Characters, g.ScrubTime);
            }
        }

        private void DrawControlsWindowContent(int paneId)
        {
            DrawControlsPaneHeader(showUndockButton: true, showDockButton: false, showCloseButton: false);
            DrawControlsPaneBody();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawControlsPaneBody()
        {
            InitControlsStyles();
            RefreshStudioSelectionCacheIfDue(force: false);
            bool hasSelection = GetCachedStudioHasSelectedCharacters();

            _controlsScroll = GUILayout.BeginScrollView(_controlsScroll);

            if (!hasSelection)
            {
                GUILayout.Label(GcNoSelection, _controlsInfoStyle);
            }
            else
            {
                for (int i = 0; i < _controlGroups.Count; i++)
                    DrawPlaybackControlGroup(_controlGroups[i]);
            }

            GUILayout.Space(8f);
            if (GUILayout.Button(GcRestartAll, AnimBrowserScale.H(28f)))
                RestartAllInSceneFromControls();

            GUILayout.EndScrollView();
        }

        private void DrawPlaybackControlGroup(AnimPlaybackControlGroup group)
        {
            BeginControlsSection(group.Title);

            if (group.HasAnimation)
            {
                GUI.enabled = true; // keep the scrubber interactive regardless of pause/speed state
                DrawAnimationMetadata(group);
                DrawScrubControl(group);
                DrawGroupCharactersSummary(group.CharactersSummary);

                GUI.enabled = true;
                DrawSpeedControlForGroup(group);
                if (group.ShowPattern)
                    DrawFloatControl(GcMotion.text, ref group.Pattern, v =>
                    {
                        group.Pattern = v;
                        AnimPlaybackService.SetPattern(group.Characters, v);
                    });

                DrawGroupExtraControls(group);

                bool newOptionVisible = GUILayout.Toggle(group.OptionVisible, GcShowItems);
                if (newOptionVisible != group.OptionVisible)
                {
                    group.OptionVisible = newOptionVisible;
                    AnimPlaybackService.SetOptionVisible(group.Characters, group.OptionVisible);
                }

                bool newForceLoop = GUILayout.Toggle(group.ForceLoop, GcForceLoop);
                if (newForceLoop != group.ForceLoop)
                {
                    group.ForceLoop = newForceLoop;
                    AnimPlaybackService.SetForceLoop(group.Characters, group.ForceLoop);
                }

                if (GUILayout.Button(GcRestartGroup, AnimBrowserScale.H(26f)))
                    RestartGroupPlayback(group);
            }
            else
            {
                DrawGroupCharactersSummary(group.CharactersSummary);
                GUILayout.Label("No animation loaded.", _controlsInfoStyle);
            }

            GUI.enabled = true;
            EndControlsSection();
        }

        private void DrawAnimationMetadata(AnimPlaybackControlGroup group)
        {
            AnimAnimationMetadata metadata = group.Metadata;

            if (!metadata.IsValid && group.LoadedVariantLines.Count == 0)
                return;

            if (group.LoadedVariantLines.Count > 0)
            {
                for (int i = 0; i < group.LoadedVariantLines.Count; i++)
                    GUILayout.Label(group.LoadedVariantLines[i], _controlsInfoStyle);
                GUILayout.Space(2f);
            }

            RefreshGroupClipTiming(group);
            if (!string.IsNullOrEmpty(group.TimingLabel))
                DrawInfoLine("Length", group.TimingLabel);

            if (metadata.IsValid &&
                (string.IsNullOrEmpty(group.DisplayGroupId) || group.LoadedVariantLines.Count <= 1))
            {
                DrawInfoLine("Path", metadata.CatalogPath);
                DrawInfoLine("ID", metadata.CatalogKey);
                if (!string.IsNullOrEmpty(metadata.ClipName))
                    DrawInfoLine("Clip", metadata.ClipName);
                DrawInfoLine("Source", metadata.SourceLabel);
                if (!string.IsNullOrEmpty(metadata.AssetPathLine))
                    DrawInfoLine("Asset", metadata.AssetPathLine);
                if (!string.IsNullOrEmpty(metadata.Manifest) &&
                    !metadata.SourceLabel.StartsWith("Sideloader", System.StringComparison.Ordinal))
                    DrawInfoLine("Manifest", metadata.Manifest);
            }

            GUILayout.Space(4f);
        }

        private void DrawInfoLine(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            GUILayout.Label(label + ": " + value, _controlsInfoStyle);
        }

        private void DrawGroupCharactersSummary(string summary)
        {
            if (string.IsNullOrEmpty(summary))
                return;
            DrawInfoLine("Characters", summary);
            GUILayout.Space(2f);
        }

        private void DrawScrubControl(AnimPlaybackControlGroup group)
        {
            RefreshGroupClipTiming(group);
            group.ScrubTime = AnimPlaybackService.SanitizeNormalizedTime(group.ScrubTime);
            TrySyncScrubTimeFromPlayback(group);

            float scrubMax = group.HasClipLength ? group.ClipLengthSeconds : 1f;
            if (!AnimPlaybackService.IsFinitePlaybackTime(scrubMax) || scrubMax <= 0f)
                scrubMax = 1f;
            float scrubSeconds = Mathf.Clamp(group.ScrubTime * scrubMax, 0f, scrubMax);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(GcScrub, _controlsSectionTitleStyle);
            GUILayout.BeginHorizontal();
            float newSeconds = GUILayout.HorizontalSlider(scrubSeconds, 0f, scrubMax);
            GUILayout.Label(GetScrubTimeLabel(group, scrubSeconds, scrubMax), AnimBrowserScale.W(96f));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(newSeconds, scrubSeconds))
            {
                scrubSeconds = Mathf.Clamp(newSeconds, 0f, scrubMax);
                group.ScrubTime = scrubMax > 0f ? scrubSeconds / scrubMax : 0f;
                group.ScrubUserOverride = true;
                InvalidateScrubTimeLabel(group);
                if (group.HasClipLength)
                    AnimPlaybackService.SetPlaybackSeconds(group.Characters, scrubSeconds, group.ClipLengthSeconds);
                else
                    AnimPlaybackService.SetNormalizedTime(group.Characters, group.ScrubTime);
            }

            if (group.ScrubUserOverride && GUIUtility.hotControl == 0)
                group.ScrubUserOverride = false;

            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private static void RefreshGroupClipTiming(AnimPlaybackControlGroup group)
        {
            bool hasLength = AnimPlaybackService.TryGetGroupClipLengthSeconds(group.Characters, out float clipLength)
                && clipLength > 0f;
            group.HasClipLength = hasLength;
            if (hasLength && !Mathf.Approximately(clipLength, group.ClipLengthSeconds))
            {
                group.ClipLengthSeconds = clipLength;
                InvalidateScrubTimeLabel(group);
                group.TimingLabelKey = -1;
            }
            else if (!hasLength)
            {
                group.ClipLengthSeconds = 1f;
            }

            int speedCenti = Mathf.RoundToInt(Mathf.Max(GetGroupEffectiveSpeed(group), 0.01f) * 100f);
            int lengthMillis = group.HasClipLength ? Mathf.RoundToInt(group.ClipLengthSeconds * 1000f) : 0;
            int timingKey = lengthMillis * 1000 + speedCenti;
            if (timingKey == group.TimingLabelKey)
                return;

            group.TimingLabelKey = timingKey;
            if (!group.HasClipLength)
            {
                group.TimingLabel = "";
                return;
            }

            float effectiveSpeed = GetGroupEffectiveSpeed(group);
            float effectiveSeconds = group.ClipLengthSeconds / Mathf.Max(effectiveSpeed, 0.01f);
            if (Mathf.Approximately(effectiveSpeed, 1f))
            {
                group.TimingLabel = AnimPlaybackService.FormatDurationSeconds(group.ClipLengthSeconds);
                return;
            }

            group.TimingLabel = AnimPlaybackService.FormatDurationSeconds(group.ClipLengthSeconds)
                + " ("
                + AnimPlaybackService.FormatDurationSeconds(effectiveSeconds)
                + " @ "
                + effectiveSpeed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + "×)";
        }

        private static void TrySyncScrubTimeFromPlayback(AnimPlaybackControlGroup group)
        {
            if (group.IsPaused || group.ScrubUserOverride || GUIUtility.hotControl != 0)
                return;

            EventType eventType = Event.current.type;
            if (eventType != EventType.Layout && eventType != EventType.Repaint)
                return;

            if (!AnimPlaybackService.TryGetGroupNormalizedTime(group.Characters, out float liveTime))
                return;

            if (!AnimPlaybackService.IsFinitePlaybackTime(liveTime))
                return;

            liveTime = AnimPlaybackService.SanitizeNormalizedTime(liveTime);
            if (Mathf.Approximately(liveTime, group.ScrubTime))
                return;

            group.ScrubTime = liveTime;
            InvalidateScrubTimeLabel(group);
        }

        private static void InvalidateScrubTimeLabel(AnimPlaybackControlGroup group)
        {
            group.ScrubTimeLabelMillis = -1;
        }

        private static string GetScrubTimeLabel(AnimPlaybackControlGroup group, float scrubSeconds, float scrubMax)
        {
            int labelMillis = Mathf.RoundToInt(scrubSeconds * 1000f);
            int maxMillis = Mathf.RoundToInt(scrubMax * 1000f);
            int cacheKey = labelMillis * 100000 + maxMillis + (group.HasClipLength ? 1 : 0);
            if (cacheKey == group.ScrubTimeLabelMillis)
                return group.ScrubTimeLabel;

            group.ScrubTimeLabelMillis = cacheKey;
            if (group.HasClipLength)
            {
                group.ScrubTimeLabel = AnimPlaybackService.FormatDurationSeconds(scrubSeconds)
                    + " / "
                    + AnimPlaybackService.FormatDurationSeconds(scrubMax);
            }
            else
            {
                group.ScrubTimeLabel = scrubSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    + " / 1.00";
            }

            return group.ScrubTimeLabel;
        }

        private void DrawSpeedControlForGroup(AnimPlaybackControlGroup group)
        {
            const float maxSpeed = 3f;
            const float minSpeed = 0f; // 0 = paused; the slider is allowed to reach it.
            // While paused the slider sits at 0 so dragging it is the same gesture as the pause button.
            float displaySpeed = group.IsPaused ? 0f : group.Speed;

            GUILayout.BeginHorizontal();
            GUIContent playPauseIcon = group.IsPaused ? GcPlay : GcPause;
            if (GUILayout.Button(playPauseIcon, AnimBrowserScale.W(24f), AnimBrowserScale.H(22f)))
            {
                if (group.IsPaused)
                    ResumeGroupPlayback(group);
                else
                    PauseGroupPlayback(group);
            }

            GUILayout.Label(GcSpeed.text, _controlsFieldLabelStyle, AnimBrowserScale.W(48f));
            float newSpeed = GUILayout.HorizontalSlider(displaySpeed, minSpeed, maxSpeed);
            string speedText = GUILayout.TextField(displaySpeed.ToString("0.00"), AnimBrowserScale.W(44f));
            GUILayout.EndHorizontal();
            if (!float.TryParse(speedText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedSpeed))
                parsedSpeed = displaySpeed;
            if (!Mathf.Approximately(newSpeed, displaySpeed))
                parsedSpeed = newSpeed;

            parsedSpeed = Mathf.Clamp(parsedSpeed, minSpeed, maxSpeed);
            if (Mathf.Approximately(parsedSpeed, displaySpeed))
                return;

            ApplyGroupSpeedFromSlider(group, parsedSpeed);
        }

        private static void ApplyGroupSpeedFromSlider(AnimPlaybackControlGroup group, float speed)
        {
            group.TimingLabelKey = -1;

            // Anything at or below the engine's minimum is treated as a pause so the slider can hit 0.
            if (speed <= AnimBrowserConfig.MinPlaybackSpeed)
            {
                PauseGroupPlayback(group);
                return;
            }

            group.IsPaused = false;
            group.SpeedBeforePause = speed;
            group.Speed = speed;
            AnimPlaybackService.SetSpeed(group.Characters, group.Speed);
        }

        private static void PauseGroupPlayback(AnimPlaybackControlGroup group)
        {
            if (group.IsPaused)
                return;

            SnapshotGroupScrubTimeFromPlayback(group);

            float minSpeed = AnimBrowserConfig.MinPlaybackSpeed;
            group.SpeedBeforePause = Mathf.Max(group.Speed, minSpeed);
            group.IsPaused = true;
            group.TimingLabelKey = -1;
            AnimPlaybackService.SetSpeed(group.Characters, 0f);
        }

        private static void SnapshotGroupScrubTimeFromPlayback(AnimPlaybackControlGroup group)
        {
            if (!AnimPlaybackService.TryGetGroupNormalizedTime(group.Characters, out float liveTime))
                return;
            if (!AnimPlaybackService.IsFinitePlaybackTime(liveTime))
                return;

            group.ScrubTime = AnimPlaybackService.SanitizeNormalizedTime(liveTime);
            InvalidateScrubTimeLabel(group);
        }

        private static void ResumeGroupPlayback(AnimPlaybackControlGroup group)
        {
            if (!group.IsPaused)
                return;

            group.IsPaused = false;
            group.Speed = group.SpeedBeforePause;
            group.TimingLabelKey = -1;
            AnimPlaybackService.SetSpeed(group.Characters, group.Speed);
        }

        private static float GetGroupEffectiveSpeed(AnimPlaybackControlGroup group) =>
            group.IsPaused ? group.SpeedBeforePause : group.Speed;

        // Restart needs to also reset the held scrub position to 0; otherwise LateUpdate keeps re-asserting
        // the previous scrub frame on paused groups and the restart appears to do nothing.
        private static void RestartGroupPlayback(AnimPlaybackControlGroup group)
        {
            AnimPlaybackService.RestartSelected(group.Characters);
            group.ScrubTime = 0f;
            InvalidateScrubTimeLabel(group);
            if (group.IsPaused)
                AnimPlaybackService.SetNormalizedTime(group.Characters, 0f);
        }

        private void RestartAllInSceneFromControls()
        {
            AnimPlaybackService.RestartAllInScene();
            for (int i = 0; i < _controlGroups.Count; i++)
            {
                AnimPlaybackControlGroup g = _controlGroups[i];
                g.ScrubTime = 0f;
                InvalidateScrubTimeLabel(g);
                if (g.HasAnimation && g.IsPaused)
                    AnimPlaybackService.SetNormalizedTime(g.Characters, 0f);
            }
        }

        private void DrawFloatControl(string label, ref float value, System.Action<float> onChanged)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _controlsFieldLabelStyle, AnimBrowserScale.W(56f));
            float newValue = GUILayout.HorizontalSlider(value, 0f, 1f);
            string text = GUILayout.TextField(value.ToString("0.00"), AnimBrowserScale.W(44f));
            GUILayout.EndHorizontal();
            if (!float.TryParse(text, out float parsed))
                parsed = value;
            parsed = Mathf.Clamp01(parsed);
            if (!Mathf.Approximately(newValue, value))
                parsed = newValue;
            if (!Mathf.Approximately(parsed, value))
                onChanged(parsed);
        }

        private void DrawGroupExtraControls(AnimPlaybackControlGroup group)
        {
            bool showCharNames = group.ExtraControls.Count > 1;
            for (int i = 0; i < group.ExtraControls.Count; i++)
            {
                CharExtraControlEntry entry = group.ExtraControls[i];
                if (!entry.HasExtra1 && !entry.HasExtra2)
                    continue;

                if (showCharNames)
                    GUILayout.Label(entry.DisplayName, _controlsFieldLabelStyle);

                if (entry.HasExtra1)
                {
                    float extra1 = entry.Extra1;
                    DrawFloatControl(entry.Extra1Label, ref extra1, v =>
                    {
                        entry.Extra1 = v;
                        AnimPlaybackService.SetExtraParam(entry.Char, 0, v);
                    });
                }

                if (entry.HasExtra2)
                {
                    float extra2 = entry.Extra2;
                    DrawFloatControl(entry.Extra2Label, ref extra2, v =>
                    {
                        entry.Extra2 = v;
                        AnimPlaybackService.SetExtraParam(entry.Char, 1, v);
                    });
                }
            }
        }

        private void BeginControlsSection(string title)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            if (!string.IsNullOrEmpty(title))
                GUILayout.Label(title, _controlsSectionTitleStyle);
        }

        private void EndControlsSection()
        {
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void SyncControlsFromSelectionIfChanged(bool force = false)
        {
            int fingerprint = ComputeControlsSelectionFingerprint();
            if (!force && fingerprint == _controlsSelectionFingerprint && _controlsInitialized)
                return;

            _controlsSelectionFingerprint = fingerprint;
            RebuildControlGroupsFromSelection();
            _controlsInitialized = true;
        }

        private void RebuildControlGroupsFromSelection()
        {
            _controlGroups.Clear();

            var animationBuckets = new Dictionary<AnimPlaybackGroupKey, List<CharSelectionEntry>>();
            var selected = _cachedStudioSelectedChars;
            for (int i = 0; i < selected.Count; i++)
            {
                OCIChar? oci = selected[i];
                if (oci == null)
                    continue;

                AnimPlaybackGroupKey animKey = AnimPlaybackGroupKey.FromCharacter(oci, _groupStore);
                if (!animationBuckets.TryGetValue(animKey, out List<CharSelectionEntry>? entries))
                {
                    entries = new List<CharSelectionEntry>();
                    animationBuckets[animKey] = entries;
                }

                string name = i < _cachedStudioSelectedCharNames.Count
                    ? _cachedStudioSelectedCharNames[i]
                    : StudioCharacterSelection.GetDisplayName(oci);
                entries.Add(new CharSelectionEntry
                {
                    Oci = oci,
                    Name = name,
                    HasPosition = TryGetCharacterWorldPosition(oci, out Vector3 position),
                    Position = position
                });
            }

            var proximityClusters = new List<List<CharSelectionEntry>>();
            foreach (var kvp in animationBuckets)
            {
                List<CharSelectionEntry> entries = kvp.Value;
                if (!_controlsGroupByProximity || entries.Count <= 1)
                {
                    AddControlGroupFromEntries(kvp.Key, entries);
                    continue;
                }

                BuildProximityClusters(entries, AnimBrowserConfig.ControlsProximityRadius, proximityClusters);
                for (int c = 0; c < proximityClusters.Count; c++)
                    AddControlGroupFromEntries(kvp.Key, proximityClusters[c]);
            }

            _controlGroups.Sort(CompareControlGroups);
        }

        private void AddControlGroupFromEntries(AnimPlaybackGroupKey animKey, List<CharSelectionEntry> entries)
        {
            if (entries.Count == 0)
                return;

            var group = new AnimPlaybackControlGroup
            {
                HasAnimation = animKey.HasAnimation,
                CatalogRef = animKey.ToCatalogRef(),
                DisplayGroupId = animKey.DisplayGroupId
            };

            for (int i = 0; i < entries.Count; i++)
            {
                CharSelectionEntry entry = entries[i];
                group.Characters.Add(entry.Oci);
                group.CharactersSummary = string.IsNullOrEmpty(group.CharactersSummary)
                    ? entry.Name
                    : group.CharactersSummary + ", " + entry.Name;
            }

            PopulateGroupPresentation(group);

            OCIChar? primary = group.Characters.Count > 0 ? group.Characters[0] : null;
            if (primary != null && AnimPlaybackService.TryReadControlsFromCharacter(primary, out AnimPlaybackControlsState state))
            {
                if (state.Speed <= AnimBrowserConfig.MinPlaybackSpeed)
                {
                    group.IsPaused = true;
                    // Resume at normal speed rather than the frozen near-zero value.
                    group.SpeedBeforePause = 1f;
                    group.Speed = 1f;
                }
                else
                {
                    group.Speed = state.Speed;
                    group.SpeedBeforePause = state.Speed;
                }
                group.Pattern = state.Pattern;
                group.ScrubTime = AnimPlaybackService.SanitizeNormalizedTime(state.NormalizedTime);
                group.ShowPattern = state.Capabilities.HasPattern;
                group.OptionVisible = state.OptionVisible;
                group.ForceLoop = state.ForceLoop;
            }

            for (int ci = 0; ci < group.Characters.Count; ci++)
            {
                OCIChar charOci = group.Characters[ci];
                if (!AnimPlaybackService.TryReadControlsFromCharacter(charOci, out AnimPlaybackControlsState charState))
                    continue;

                if (charState.Capabilities.HasPattern)
                    group.ShowPattern = true;

                if (!charState.Capabilities.HasExtra1 && !charState.Capabilities.HasExtra2)
                    continue;

                group.ExtraControls.Add(new CharExtraControlEntry
                {
                    Char = charOci,
                    DisplayName = StudioCharacterSelection.GetDisplayName(charOci),
                    HasExtra1 = charState.Capabilities.HasExtra1,
                    HasExtra2 = charState.Capabilities.HasExtra2,
                    Extra1Label = AnimControlCapabilityService.FormatParamLabel(charState.Capabilities.Extra1ParamName, 0),
                    Extra2Label = AnimControlCapabilityService.FormatParamLabel(charState.Capabilities.Extra2ParamName, 1),
                    Extra1 = charState.Extra1,
                    Extra2 = charState.Extra2
                });
            }

            _controlGroups.Add(group);
        }

        private static void BuildProximityClusters(
            List<CharSelectionEntry> entries,
            float radius,
            List<List<CharSelectionEntry>> clustersOut)
        {
            clustersOut.Clear();
            int count = entries.Count;
            if (count == 0)
                return;

            if (count == 1)
            {
                clustersOut.Add(new List<CharSelectionEntry> { entries[0] });
                return;
            }

            int[] parent = new int[count];
            for (int i = 0; i < count; i++)
                parent[i] = i;

            float radiusSqr = radius * radius;
            for (int i = 0; i < count; i++)
            {
                if (!entries[i].HasPosition)
                    continue;
                for (int j = i + 1; j < count; j++)
                {
                    if (!entries[j].HasPosition)
                        continue;
                    if ((entries[i].Position - entries[j].Position).sqrMagnitude <= radiusSqr)
                        UnionProximityCluster(parent, i, j);
                }
            }

            var clusterMap = new Dictionary<int, List<CharSelectionEntry>>();
            for (int i = 0; i < count; i++)
            {
                int root = FindProximityClusterRoot(parent, i);
                if (!clusterMap.TryGetValue(root, out List<CharSelectionEntry>? cluster))
                {
                    cluster = new List<CharSelectionEntry>();
                    clusterMap[root] = cluster;
                    clustersOut.Add(cluster);
                }

                cluster.Add(entries[i]);
            }
        }

        private static int FindProximityClusterRoot(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        private static void UnionProximityCluster(int[] parent, int a, int b)
        {
            int rootA = FindProximityClusterRoot(parent, a);
            int rootB = FindProximityClusterRoot(parent, b);
            if (rootA == rootB)
                return;
            parent[rootB] = rootA;
        }

        private static bool TryGetCharacterWorldPosition(OCIChar oci, out Vector3 position)
        {
            position = Vector3.zero;
            try
            {
                if (oci?.guideObject?.transformTarget != null)
                {
                    position = oci.guideObject.transformTarget.position;
                    return true;
                }

                if (oci?.charInfo != null)
                {
                    position = oci.charInfo.transform.position;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private struct CharSelectionEntry
        {
            public OCIChar Oci;
            public string Name;
            public Vector3 Position;
            public bool HasPosition;
        }

        private void PopulateGroupPresentation(AnimPlaybackControlGroup group)
        {
            group.LoadedVariantLines.Clear();
            if (!group.HasAnimation)
            {
                group.Title = "No animation";
                group.Metadata = default;
                return;
            }

            if (!string.IsNullOrEmpty(group.DisplayGroupId))
            {
                AnimDisplayGroupData? displayGroup = _groupStore.FindDisplayGroup(group.DisplayGroupId);
                group.Title = displayGroup != null && !string.IsNullOrEmpty(displayGroup.Name)
                    ? StudioAutoTranslation.Resolve(displayGroup.Name)
                    : "Animation group";

                var seenRefs = new HashSet<AnimCatalogRef>();
                AnimCatalogRef primaryRef = default;
                bool havePrimaryRef = false;

                for (int i = 0; i < group.Characters.Count; i++)
                {
                    OCIChar charOci = group.Characters[i];
                    if (!TryGetCharacterCatalogRef(charOci, out AnimCatalogRef catalogRef))
                        continue;

                    string charName = StudioCharacterSelection.GetDisplayName(charOci);
                    AnimAnimationMetadata variantMeta = AnimCatalogResolve.BuildMetadata(catalogRef, _catalog);
                    group.LoadedVariantLines.Add(charName + ": " + variantMeta.DisplayName + " (" + variantMeta.CatalogKey + ")");

                    if (seenRefs.Add(catalogRef))
                    {
                        if (!havePrimaryRef)
                        {
                            primaryRef = catalogRef;
                            havePrimaryRef = true;
                        }
                    }
                }

                group.CatalogRef = havePrimaryRef ? primaryRef : group.CatalogRef;
                group.Metadata = havePrimaryRef
                    ? AnimCatalogResolve.BuildMetadata(primaryRef, _catalog)
                    : default;
                return;
            }

            if (!TryGetCharacterCatalogRef(group.Characters[0], out AnimCatalogRef soloRef))
            {
                group.Title = "Animation";
                group.Metadata = default;
                return;
            }

            group.CatalogRef = soloRef;
            group.Metadata = AnimCatalogResolve.BuildMetadata(soloRef, _catalog);
            group.Title = group.Metadata.DisplayName;
        }

        private static bool TryGetCharacterCatalogRef(OCIChar oci, out AnimCatalogRef catalogRef)
        {
            catalogRef = default;
            try
            {
                var animeInfo = oci.oiCharInfo?.animeInfo;
                if (animeInfo == null || !animeInfo.exist)
                    return false;
                catalogRef = new AnimCatalogRef(animeInfo.group, animeInfo.category, animeInfo.no);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int CompareControlGroups(AnimPlaybackControlGroup a, AnimPlaybackControlGroup b)
        {
            if (a.HasAnimation != b.HasAnimation)
                return a.HasAnimation ? -1 : 1;
            return string.Compare(a.Title, b.Title, System.StringComparison.OrdinalIgnoreCase);
        }

        private int ComputeControlsSelectionFingerprint()
        {
            int hash = 17;
            hash = unchecked(hash * 31 + (_controlsGroupByProximity ? 1 : 0));
            var selected = _cachedStudioSelectedChars;
            for (int i = 0; i < selected.Count; i++)
            {
                OCIChar? oci = selected[i];
                hash = unchecked(hash * 31 + (oci != null ? oci.GetHashCode() : 0));
                if (oci == null)
                    continue;

                hash = unchecked(hash * 31 + AnimPlaybackGroupKey.FromCharacter(oci, _groupStore).GetHashCode());

                if (_controlsGroupByProximity && TryGetCharacterWorldPosition(oci, out Vector3 pos))
                {
                    float bucket = AnimBrowserConfig.ControlsProximityRadius * 0.5f;
                    hash = unchecked(hash * 31 + Mathf.RoundToInt(pos.x / bucket));
                    hash = unchecked(hash * 31 + Mathf.RoundToInt(pos.y / bucket));
                    hash = unchecked(hash * 31 + Mathf.RoundToInt(pos.z / bucket));
                }

                try
                {
                    hash = unchecked(hash * 31 + (oci.isHAnime ? 1 : 0));
                    hash = unchecked(hash * 31 + (oci.isAnimeMotion ? 1 : 0));

                    string[] animeParam = oci.animeParam;
                    if (animeParam != null)
                    {
                        if (animeParam.Length > 0)
                            hash = unchecked(hash * 31 + (animeParam[0]?.GetHashCode() ?? 0));
                        if (animeParam.Length > 1)
                            hash = unchecked(hash * 31 + (animeParam[1]?.GetHashCode() ?? 0));
                    }
                }
                catch
                {
                    // ignored
                }
            }

            return hash;
        }

        private enum AnimPlaybackBucketKind
        {
            None = 0,
            Catalog = 1,
            DisplayGroup = 2
        }

        private readonly struct AnimPlaybackGroupKey : System.IEquatable<AnimPlaybackGroupKey>
        {
            public readonly AnimPlaybackBucketKind Kind;
            public readonly int Group;
            public readonly int Category;
            public readonly int No;
            public readonly string DisplayGroupId;

            public bool HasAnimation => Kind != AnimPlaybackBucketKind.None;

            private AnimPlaybackGroupKey(AnimPlaybackBucketKind kind, int group, int category, int no, string displayGroupId)
            {
                Kind = kind;
                Group = group;
                Category = category;
                No = no;
                DisplayGroupId = displayGroupId ?? "";
            }

            public static AnimPlaybackGroupKey FromCharacter(OCIChar oci, AnimGroupStore groupStore)
            {
                try
                {
                    var animeInfo = oci.oiCharInfo?.animeInfo;
                    if (animeInfo == null || !animeInfo.exist)
                        return new AnimPlaybackGroupKey(AnimPlaybackBucketKind.None, -1, -1, -1, "");

                    var catalogRef = new AnimCatalogRef(animeInfo.group, animeInfo.category, animeInfo.no);
                    AnimDisplayGroupData? displayGroup = groupStore.GetGroupForRef(catalogRef);
                    if (displayGroup != null && !string.IsNullOrEmpty(displayGroup.Id))
                    {
                        return new AnimPlaybackGroupKey(
                            AnimPlaybackBucketKind.DisplayGroup,
                            animeInfo.group,
                            animeInfo.category,
                            animeInfo.no,
                            displayGroup.Id);
                    }

                    return new AnimPlaybackGroupKey(
                        AnimPlaybackBucketKind.Catalog,
                        animeInfo.group,
                        animeInfo.category,
                        animeInfo.no,
                        "");
                }
                catch
                {
                    // ignored
                }

                return new AnimPlaybackGroupKey(AnimPlaybackBucketKind.None, -1, -1, -1, "");
            }

            public AnimCatalogRef ToCatalogRef() => new AnimCatalogRef(Group, Category, No);

            public bool Equals(AnimPlaybackGroupKey other)
            {
                if (Kind != other.Kind)
                    return false;
                if (Kind == AnimPlaybackBucketKind.DisplayGroup)
                    return string.Equals(DisplayGroupId, other.DisplayGroupId, System.StringComparison.Ordinal);
                if (Kind == AnimPlaybackBucketKind.Catalog)
                    return Group == other.Group && Category == other.Category && No == other.No;
                return true;
            }

            public override bool Equals(object? obj) => obj is AnimPlaybackGroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Kind;
                    if (Kind == AnimPlaybackBucketKind.DisplayGroup)
                        return hash * 31 + DisplayGroupId.GetHashCode();
                    hash = hash * 31 + Group;
                    hash = hash * 31 + Category;
                    hash = hash * 31 + No;
                    return hash;
                }
            }
        }
    }
}
