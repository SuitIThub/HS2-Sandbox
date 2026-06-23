using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Anim Browser thumbnail capture — shared session with Anim-specific framing/capture.</summary>
    internal sealed class AnimThumbnailCapture : ThumbnailCaptureSession<AnimDisplayEntry>
    {
        public const int OutputSize = 256;

        protected override string GetDisplayName(AnimDisplayEntry e) =>
            e.IsGroup ? (e.Group?.GetDisplayLabel() ?? "Group") : (e.Single?.DisplayName ?? "Animation");

        protected override float ScalePx(float value) => AnimBrowserScale.Px(value);

        protected override float AutoCaptureDelaySeconds() => 2f;

        protected override Texture2D CaptureScreenArea(Camera camera, Rect screenRect) =>
            ScreenCapture2D.CaptureScreenArea(camera, screenRect, OutputSize);
    }

    public partial class AnimBrowserWindow
    {
        private AnimThumbnailCapture? _thumbCapture;

        private AnimThumbnailCapture ThumbCapture => _thumbCapture ??= new AnimThumbnailCapture();

        internal bool IsThumbnailCaptureActive => _thumbCapture != null && _thumbCapture.IsActive;

        // While capturing, the apply-target characters are paused so each applied animation holds a
        // single frame; LateUpdate re-asserts the chosen progress position so it doesn't flicker, and
        // the original playback speeds are restored once the session ends.
        private readonly Dictionary<OCIChar, float> _thumbCaptureSavedSpeeds = new Dictionary<OCIChar, float>();
        private List<OCIChar>? _thumbCaptureHoldChars;

        /// <summary>Stable per-entry storage key for a thumbnail (group vs single).</summary>
        internal static string ThumbnailKey(AnimDisplayEntry entry)
        {
            if (entry.IsGroup && entry.Group != null)
            {
                AnimGridItem rep = entry.Group.ThumbnailItem;
                return "g_" + (rep != null ? rep.CatalogKey : entry.Group.Id);
            }
            return "s_" + (entry.Single?.CatalogKey ?? string.Empty);
        }

        private void StartThumbnailCapture(bool onlyMissing)
        {
            if (IsThumbnailCaptureActive)
                return;

            AnimThumbnailStore.InvalidateExistence();

            var queue = new List<AnimDisplayEntry>();
            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                AnimDisplayEntry entry = _visibleEntries[i];
                if (onlyMissing && AnimThumbnailStore.Has(ThumbnailKey(entry)))
                    continue;
                queue.Add(entry);
            }

            if (queue.Count == 0)
                return;

            // Capturing drives the scene (applies each animation); suspend the hover preview meanwhile.
            OnPreviewHidden();
            BeginThumbCaptureHold();

            ThumbCapture.StartCapture(
                this,
                queue,
                onApplyItem: ApplyEntry,
                onCaptured: CommitCapturedThumbnail,
                onComplete: () => { },
                onPrepareCapture: ApplyCaptureProgress);
            RestoreThumbCaptureRect();
        }

        /// <summary>Capture thumbnails for the currently selected/marked entries (content action bar button).</summary>
        private void StartThumbnailCaptureForSelection()
        {
            if (IsThumbnailCaptureActive)
                return;

            var queue = new List<AnimDisplayEntry>();
            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                if (IsEntrySelected(_visibleEntries[i]))
                    queue.Add(_visibleEntries[i]);
            }

            if (queue.Count == 0)
                return;

            AnimThumbnailStore.InvalidateExistence();

            // Capturing drives the scene (applies each animation); suspend the hover preview meanwhile.
            OnPreviewHidden();
            BeginThumbCaptureHold();

            ThumbCapture.StartCapture(
                this,
                queue,
                onApplyItem: ApplyEntry,
                onCaptured: CommitCapturedThumbnail,
                onComplete: () => { },
                onPrepareCapture: ApplyCaptureProgress);
            RestoreThumbCaptureRect();
        }

        private void CommitCapturedThumbnail(AnimDisplayEntry entry, byte[] png)
        {
            AnimThumbnailStore.Save(ThumbnailKey(entry), png);
        }

        /// <summary>Pause the apply-target characters for the duration of the capture session, saving
        /// their speeds so they can be restored afterwards. Pausing lets each applied animation hold a
        /// single frame that <see cref="ApplyCaptureProgress"/> / the LateUpdate hold can seek.</summary>
        private void BeginThumbCaptureHold()
        {
            _thumbCaptureSavedSpeeds.Clear();
            _thumbCaptureHoldChars = new List<OCIChar>();

            IList<OCIChar> selection = GetSelectionForApply();
            for (int i = 0; i < selection.Count; i++)
            {
                OCIChar oci = selection[i];
                if (oci == null || _thumbCaptureSavedSpeeds.ContainsKey(oci))
                    continue;
                _thumbCaptureSavedSpeeds[oci] = oci.animeSpeed;
                _thumbCaptureHoldChars.Add(oci);
            }

            if (_thumbCaptureHoldChars.Count > 0)
                AnimPlaybackService.SetSpeed(_thumbCaptureHoldChars, 0f);
        }

        /// <summary>End the capture hold: restore the playback speeds captured in <see cref="BeginThumbCaptureHold"/>.</summary>
        private void EndThumbCaptureHold()
        {
            if (_thumbCaptureHoldChars == null)
                return;

            foreach (KeyValuePair<OCIChar, float> kvp in _thumbCaptureSavedSpeeds)
            {
                if (kvp.Key == null)
                    continue;
                try { kvp.Key.animeSpeed = kvp.Value; }
                catch { /* character gone */ }
            }

            _thumbCaptureHoldChars = null;
            _thumbCaptureSavedSpeeds.Clear();
        }

        /// <summary>Hold the paused capture characters at the progress slider position. Called every
        /// LateUpdate while capturing (paused animators otherwise revert the seek next frame), and once
        /// more right before each frame is grabbed.</summary>
        private void ThumbCaptureHoldLateUpdate()
        {
            if (_thumbCaptureHoldChars == null)
                return;

            if (IsThumbnailCaptureActive)
            {
                AnimPlaybackService.SetNormalizedTime(_thumbCaptureHoldChars, Mathf.Clamp01(ThumbCapture.CaptureProgress01));
                return;
            }

            // Capture finished or was cancelled — restore playback and remember the box placement.
            EndThumbCaptureHold();
            SaveThumbCaptureRect();
        }

        /// <summary>Apply the persisted capture-box position/size (clamped to the screen). The base
        /// session has just set a centred default in StartCapture, so this overrides it when saved.</summary>
        private void RestoreThumbCaptureRect()
        {
            float size = _options.thumbCaptureRectSize;
            if (size <= 0f)
                return;
            size = Mathf.Min(size, Mathf.Min(Screen.width, Screen.height) - 16f);
            float x = Mathf.Clamp(_options.thumbCaptureRectX, 0f, Mathf.Max(0f, Screen.width - size));
            float y = Mathf.Clamp(_options.thumbCaptureRectY, 0f, Mathf.Max(0f, Screen.height - size));
            ThumbCapture.CaptureRect = new Rect(x, y, size, size);
        }

        /// <summary>Persist the current capture-box placement so the next session reopens it in place.</summary>
        private void SaveThumbCaptureRect()
        {
            if (_thumbCapture == null)
                return;
            Rect r = _thumbCapture.CaptureRect;
            if (r.width < 1f)
                return;
            if (Mathf.Approximately(r.x, _options.thumbCaptureRectX) &&
                Mathf.Approximately(r.y, _options.thumbCaptureRectY) &&
                Mathf.Approximately(r.width, _options.thumbCaptureRectSize))
                return;
            _options.thumbCaptureRectX = r.x;
            _options.thumbCaptureRectY = r.y;
            _options.thumbCaptureRectSize = r.width;
            SavePersistedOptions();
        }

        /// <summary>Seek the held capture characters' current animation to the progress slider value
        /// (0–1). Called right before each frame is grabbed so the thumbnail freezes that exact frame.</summary>
        private void ApplyCaptureProgress(float progress01)
        {
            IList<OCIChar> targets = _thumbCaptureHoldChars ?? GetSelectionForApply();
            if (targets.Count == 0)
                return;
            AnimPlaybackService.SetNormalizedTime(targets, Mathf.Clamp01(progress01));
        }

        private void DrawThumbnailCaptureOverlay()
        {
            if (_thumbCapture != null && _thumbCapture.IsActive)
                _thumbCapture.DrawOverlay();
        }

        /// <summary>Stored thumbnail for an entry, or null if none captured yet.</summary>
        private static Texture2D? GetStoredThumbnail(AnimDisplayEntry entry)
        {
            return AnimThumbnailStore.TryGetTexture(ThumbnailKey(entry), out Texture2D? tex) ? tex : null;
        }
    }
}
