using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Captures a screen-space rect from a camera into a square texture (for thumbnails).</summary>
    internal static class ScreenCapture2D
    {
        /// <summary>Renders the camera, reads the given screen rect, and returns it resized to outputSize². Caller destroys the texture.</summary>
        public static Texture2D CaptureScreenArea(Camera camera, Rect screenRect, int outputSize)
        {
            var rt = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            rt.antiAliasing = 8;

            RenderTexture? prevTarget = camera.targetTexture;
            RenderTexture? prevActive = RenderTexture.active;

            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(Mathf.Max(1, (int)screenRect.width), Mathf.Max(1, (int)screenRect.height), TextureFormat.RGB24, false);
            tex.ReadPixels(screenRect, 0, 0, false);
            tex.Apply();

            camera.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return ResizeTexture(tex, outputSize, outputSize);
        }

        public static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            if (source.width == width && source.height == height && source.format == TextureFormat.RGB24)
                return source;

            var rt = RenderTexture.GetTemporary(width, height);
            rt.filterMode = FilterMode.Bilinear;
            RenderTexture? prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            result.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(source);

            return result;
        }
    }
}
