using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class AnimThumbnailService
    {
        private const int MaxResident = 96;
        private static readonly Dictionary<string, Texture2D> PlaceholderCache = new Dictionary<string, Texture2D>();
        private static readonly LinkedList<string> LruKeys = new LinkedList<string>();
        private static readonly Dictionary<string, LinkedListNode<string>> LruNodes = new Dictionary<string, LinkedListNode<string>>();

        private static Texture2D? _genericPlaceholder;

        public static Texture2D GetPlaceholder(AnimGridItem item, int sizePx)
        {
            int size = Mathf.Clamp(sizePx, 48, 256);
            string key = item.CatalogKey + "@" + size;
            if (PlaceholderCache.TryGetValue(key, out Texture2D? tex) && tex != null)
            {
                TouchLru(key);
                return tex;
            }

            tex = CreatePlaceholderTexture(item.DisplayName, size);
            PlaceholderCache[key] = tex;
            RegisterLru(key);
            TrimLru();
            return tex;
        }

        public static Texture2D GetGenericPlaceholder(int sizePx)
        {
            int size = Mathf.Clamp(sizePx, 48, 256);
            if (_genericPlaceholder != null && _genericPlaceholder.width == size)
                return _genericPlaceholder;
            _genericPlaceholder = CreatePlaceholderTexture("Anim", size);
            return _genericPlaceholder;
        }

        public static void ReleaseItemThumbnail(AnimGridItem item)
        {
            var tex = item.Thumbnail;
            if (tex == null)
                return;
            if (PlaceholderCache.ContainsValue(tex))
                return;
            Object.Destroy(tex);
            item.Thumbnail = null;
        }

        public static void ClearAll()
        {
            foreach (var kvp in PlaceholderCache)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value);
            }
            PlaceholderCache.Clear();
            LruKeys.Clear();
            LruNodes.Clear();
            if (_genericPlaceholder != null)
            {
                Object.Destroy(_genericPlaceholder);
                _genericPlaceholder = null;
            }
        }

        private static Texture2D CreatePlaceholderTexture(string label, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var fill = new Color(0.18f, 0.2f, 0.24f, 1f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = fill;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.name = "AnimBrowserPlaceholder:" + label;
            return tex;
        }

        private static void RegisterLru(string key)
        {
            if (LruNodes.TryGetValue(key, out LinkedListNode<string>? node) && node != null)
            {
                LruKeys.Remove(node);
                LruKeys.AddFirst(node);
                return;
            }

            var newNode = LruKeys.AddFirst(key);
            LruNodes[key] = newNode;
        }

        private static void TouchLru(string key)
        {
            if (!LruNodes.TryGetValue(key, out LinkedListNode<string>? node) || node == null)
                return;
            LruKeys.Remove(node);
            LruKeys.AddFirst(node);
        }

        private static void TrimLru()
        {
            while (LruKeys.Count > MaxResident)
            {
                string key = LruKeys.Last.Value;
                LruKeys.RemoveLast();
                LruNodes.Remove(key);
                if (PlaceholderCache.TryGetValue(key, out Texture2D? tex) && tex != null)
                    Object.Destroy(tex);
                PlaceholderCache.Remove(key);
            }
        }
    }
}
