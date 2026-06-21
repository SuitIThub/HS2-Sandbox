using System.Collections.Generic;
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

            ThumbCapture.StartCapture(
                this,
                queue,
                onApplyItem: ApplyEntry,
                onCaptured: CommitCapturedThumbnail,
                onComplete: () => { });
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

            ThumbCapture.StartCapture(
                this,
                queue,
                onApplyItem: ApplyEntry,
                onCaptured: CommitCapturedThumbnail,
                onComplete: () => { });
        }

        private void CommitCapturedThumbnail(AnimDisplayEntry entry, byte[] png)
        {
            AnimThumbnailStore.Save(ThumbnailKey(entry), png);
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
