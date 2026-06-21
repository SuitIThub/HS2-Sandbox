using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Persistent thumbnail store for the Anim Browser. Thumbnails can't live next to the animations
    /// (those are read-only game/sideloader bundles), so captured PNGs are written to a dedicated
    /// folder under the game's UserData and loaded on demand (with a small in-memory LRU of textures).
    /// </summary>
    internal static class AnimThumbnailStore
    {
        private const int MaxResidentTextures = 128;

        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();
        private static readonly LinkedList<string> Lru = new LinkedList<string>();
        private static readonly Dictionary<string, LinkedListNode<string>> LruNodes = new Dictionary<string, LinkedListNode<string>>();

        // Cache of which keys have a file on disk (null = not scanned yet).
        private static HashSet<string>? _existing;

        public static string Directory
        {
            get
            {
                try
                {
                    return PathEx.Combine(UserData.Path, "com.hs2.sandbox", "anim_thumbnails");
                }
                catch
                {
                    return PathEx.Combine("UserData", "com.hs2.sandbox", "anim_thumbnails");
                }
            }
        }

        public static bool Has(string key)
        {
            string safe = Sanitize(key);
            EnsureExistingScanned();
            return _existing!.Contains(safe);
        }

        /// <summary>Returns the stored thumbnail texture for the key, loading it from disk if needed.</summary>
        public static bool TryGetTexture(string key, out Texture2D? texture)
        {
            texture = null;
            string safe = Sanitize(key);

            if (TextureCache.TryGetValue(safe, out Texture2D? cached) && cached != null)
            {
                Touch(safe);
                texture = cached;
                return true;
            }

            if (!Has(key))
                return false;

            string path = PathFor(safe);
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch
            {
                return false;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "AnimThumb:" + safe };
            if (!tex.LoadImage(bytes))
            {
                Object.Destroy(tex);
                return false;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            TextureCache[safe] = tex;
            Register(safe);
            Trim();
            texture = tex;
            return true;
        }

        public static void Save(string key, byte[] png)
        {
            if (png == null || png.Length == 0)
                return;

            string safe = Sanitize(key);
            try
            {
                string dir = Directory;
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                FileEx.WriteAllBytesAtomic(PathFor(safe), png);
            }
            catch (System.Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimThumbnailStore: save failed for '" + key + "': " + ex.Message);
                return;
            }

            EnsureExistingScanned();
            _existing!.Add(safe);
            // Drop any stale cached texture so the new image is picked up.
            EvictTexture(safe);
        }

        public static void Delete(string key)
        {
            string safe = Sanitize(key);
            try
            {
                string path = PathFor(safe);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignored
            }

            _existing?.Remove(safe);
            EvictTexture(safe);
        }

        /// <summary>Forget the in-memory file-existence scan (call after external changes / before a fresh capture run).</summary>
        public static void InvalidateExistence() => _existing = null;

        public static void ClearTextureCache()
        {
            foreach (KeyValuePair<string, Texture2D> kvp in TextureCache)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value);
            }
            TextureCache.Clear();
            Lru.Clear();
            LruNodes.Clear();
        }

        private static void EnsureExistingScanned()
        {
            if (_existing != null)
                return;

            _existing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            try
            {
                string dir = Directory;
                if (System.IO.Directory.Exists(dir))
                {
                    foreach (string file in System.IO.Directory.GetFiles(dir, "*.png"))
                        _existing.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch
            {
                // ignored — treat as empty
            }
        }

        private static string PathFor(string safeKey) => Path.Combine(Directory, safeKey + ".png");

        private static string Sanitize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "_";

            var sb = new System.Text.StringBuilder(key.Length);
            foreach (char c in key)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }

        private static void EvictTexture(string safe)
        {
            if (TextureCache.TryGetValue(safe, out Texture2D? tex))
            {
                if (tex != null)
                    Object.Destroy(tex);
                TextureCache.Remove(safe);
            }
            if (LruNodes.TryGetValue(safe, out LinkedListNode<string>? node) && node != null)
            {
                Lru.Remove(node);
                LruNodes.Remove(safe);
            }
        }

        private static void Register(string key)
        {
            if (LruNodes.TryGetValue(key, out LinkedListNode<string>? existing) && existing != null)
            {
                Lru.Remove(existing);
                Lru.AddFirst(existing);
                return;
            }
            LruNodes[key] = Lru.AddFirst(key);
        }

        private static void Touch(string key) => Register(key);

        private static void Trim()
        {
            while (Lru.Count > MaxResidentTextures)
            {
                string oldest = Lru.Last!.Value;
                Lru.RemoveLast();
                LruNodes.Remove(oldest);
                if (TextureCache.TryGetValue(oldest, out Texture2D? tex))
                {
                    if (tex != null)
                        Object.Destroy(tex);
                    TextureCache.Remove(oldest);
                }
            }
        }
    }
}
