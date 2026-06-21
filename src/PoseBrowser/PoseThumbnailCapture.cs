using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Pose Browser thumbnail capture — the shared session with Pose-specific framing/capture.</summary>
    internal sealed class PoseThumbnailCapture : ThumbnailCaptureSession<PoseGridItem>
    {
        protected override string GetDisplayName(PoseGridItem item) => item.DisplayName ?? string.Empty;

        protected override float ScalePx(float value) => PoseBrowserScale.Px(value);

        protected override float AutoCaptureDelaySeconds()
        {
            PoseBrowserConfig.Register(SandboxServices.Config);
            var entry = PoseBrowserConfig.AutoCaptureDelaySeconds;
            return entry == null ? 2f : Mathf.Clamp(entry.Value, 0.5f, 30f);
        }

        protected override Texture2D CaptureScreenArea(Camera camera, Rect screenRect) =>
            PoseDataService.CaptureScreenArea(camera, screenRect);
    }
}
