using System.Collections;
using System.Collections.Generic;
using Manager;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Loads animation clips via the game's AssetBundleManager (sideloader-aware).</summary>
    internal static class AnimClipLoader
    {
        private const int MaxCachedClips = 32;

        // Real-time budget for a single clip load before giving up. We yield every frame while
        // waiting, so the game stays responsive — sideloader bundles load asynchronously over
        // frames and MUST NOT be busy-waited on the main thread (that deadlocks the loader).
        private const float MaxLoadSeconds = 20f;

        private sealed class CacheEntry
        {
            public AnimationClip? Clip;
            public RuntimeAnimatorController? Controller;
            public bool IsHumanoid;
            public string Key = string.Empty;
        }

        private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>();
        private static readonly LinkedList<string> LruKeys = new LinkedList<string>();
        private static readonly Dictionary<string, LinkedListNode<string>> LruNodes = new Dictionary<string, LinkedListNode<string>>();

        public static bool TryGetCached(AnimGridItem item, out AnimationClip? clip, out bool isHumanoid)
        {
            clip = null;
            isHumanoid = false;
            if (item == null)
                return false;

            string key = BuildCacheKey(item);
            if (!Cache.TryGetValue(key, out CacheEntry? entry) || entry?.Clip == null)
                return false;

            TouchLru(key);
            clip = entry.Clip;
            isHumanoid = entry.IsHumanoid;
            return true;
        }

        /// <summary>The RuntimeAnimatorController the cached clip came from (KK needs it assigned to
        /// the preview Animator so SampleAnimation binds — see <see cref="AnimPreviewEmbeddedRig"/>).</summary>
        public static bool TryGetCachedController(AnimGridItem item, out RuntimeAnimatorController? controller)
        {
            controller = null;
            if (item == null)
                return false;

            string key = BuildCacheKey(item);
            if (!Cache.TryGetValue(key, out CacheEntry? entry) || entry == null)
                return false;

            controller = entry.Controller;
            return controller != null;
        }

        public static IEnumerator LoadClipCoroutine(AnimGridItem item, AnimGender gender, int ordinal,
            System.Action<AnimationClip?, bool, string> onComplete)
        {
            if (item == null)
            {
                onComplete(null, false, "No item");
                yield break;
            }

            string key = BuildCacheKey(item);
            if (Cache.TryGetValue(key, out CacheEntry? existing) && existing?.Clip != null)
            {
                TouchLru(key);
                onComplete(existing.Clip, existing.IsHumanoid, string.Empty);
                yield break;
            }

            if (!AnimCatalogResolve.TryGetLoadInfo(item.Group, item.Category, item.No, out Info.AnimeLoadInfo? loadInfo) ||
                loadInfo == null)
            {
                onComplete(null, false, "Missing load info");
                yield break;
            }

            string bundlePath = loadInfo.bundlePath ?? string.Empty;
            string fileName = loadInfo.fileName ?? string.Empty;
            string clipName = loadInfo.clip ?? string.Empty;
            string manifest = loadInfo.manifest ?? string.Empty;

            if (string.IsNullOrEmpty(bundlePath) || string.IsNullOrEmpty(fileName))
            {
                onComplete(null, false, "Missing bundle path or file name");
                yield break;
            }

            // How the game's OCIChar.LoadAnime resolves the actual clip: it loads the base controller
            // (bundlePath/fileName, shared across many entries) and — for H anims (HAnimeLoadInfo) —
            // applies an OVERRIDE controller (overrideFile) on top. That override is the per-entry
            // (position + role) differentiator. We mirror this: try the override controller first
            // (its clips are the specific ones for this exact catalog entry), then the base.
            var candidates = new List<BundleAsset>();
            if (loadInfo is Info.HAnimeLoadInfo hAnim && hAnim.overrideFile != null &&
                !string.IsNullOrEmpty(hAnim.overrideFile.bundlePath) && !string.IsNullOrEmpty(hAnim.overrideFile.fileName))
            {
                candidates.Add(new BundleAsset(hAnim.overrideFile.bundlePath, hAnim.overrideFile.fileName, hAnim.overrideFile.manifest ?? string.Empty));
            }
            candidates.Add(new BundleAsset(bundlePath, fileName, manifest));

            float deadline = Time.realtimeSinceStartup + MaxLoadSeconds;
            AnimationClip? clip = null;
            RuntimeAnimatorController? clipController = null;
            string error = string.Empty;

            for (int ci = 0; ci < candidates.Count && clip == null; ci++)
            {
                BundleAsset cand = candidates[ci];
                AssetBundleLoadAssetOperation? op = null;
                try
                {
                    op = AssetBundleManager.LoadAsset(cand.Bundle, cand.File, typeof(RuntimeAnimatorController), cand.Manifest);
                }
                catch (System.Exception ex)
                {
                    error = ex.Message;
                    continue;
                }

                if (op == null)
                    continue;

                while (!IsOpDone(op) && Time.realtimeSinceStartup < deadline)
                    yield return null;

                if (!IsOpDone(op))
                {
                    error = "Clip load timed out";
                    continue;
                }

                clip = ExtractClip(op, clipName, gender, ordinal, out clipController);
            }

            if (clip == null)
            {
                SandboxServices.Log.LogDebug("AnimClipLoader " + key + " (" + gender + ordinal + ") clip='" + clipName + "' not found");
                onComplete(null, false, string.IsNullOrEmpty(error) ? "Clip '" + clipName + "' not found in bundle" : error);
                yield break;
            }

            var entry = new CacheEntry { Clip = clip, Controller = clipController, IsHumanoid = clip.humanMotion, Key = key };
            Cache[key] = entry;
            RegisterLru(key);
            TrimLru();
            onComplete(clip, clip.humanMotion, string.Empty);
        }

        /// <summary>A loadable controller asset: bundle + asset name + manifest.</summary>
        private readonly struct BundleAsset
        {
            public readonly string Bundle;
            public readonly string File;
            public readonly string Manifest;

            public BundleAsset(string bundle, string file, string manifest)
            {
                Bundle = bundle;
                File = file;
                Manifest = manifest ?? string.Empty;
            }
        }

        private static bool IsOpDone(AssetBundleLoadAssetOperation op)
        {
            try
            {
                return op.IsDone();
            }
            catch
            {
                return true; // treat a throwing op as finished so we don't spin forever
            }
        }

        /// <summary>
        /// Pulls the wanted AnimationClip out of a completed load op: first from the loaded
        /// RuntimeAnimatorController's clips, then from all clips the bundle load surfaced, then the
        /// op's own asset (for bundles where the entry is a clip rather than a controller).
        /// </summary>
        private static AnimationClip? ExtractClip(AssetBundleLoadAssetOperation op, string clipName, AnimGender gender, int ordinal,
            out RuntimeAnimatorController? controller)
        {
            controller = null;
            try { controller = op.GetAsset<RuntimeAnimatorController>(); } catch { }

            if (controller != null)
            {
                AnimationClip? viaController = FindByName(controller.animationClips, clipName, gender, ordinal);
                if (viaController != null)
                    return viaController;
            }

            AnimationClip[]? all = null;
            try { all = op.GetAllAssets<AnimationClip>(); } catch { }
            AnimationClip? viaBundle = FindByName(all, clipName, gender, ordinal);
            if (viaBundle != null)
                return viaBundle;

            AnimationClip? direct = null;
            try { direct = op.GetAsset<AnimationClip>(); } catch { }
            return direct;
        }

        // Character-height tags, medium first (used as the default preview height).
        private static readonly string[] HeightTags = { "M", "S", "L" };

        /// <summary>
        /// Resolves the catalog clip name to an actual clip, role-aware. Two H-anim packaging schemes
        /// exist: (a) separate controller per role with a plain/height-prefixed clip, (b) ONE shared
        /// controller whose clips encode the role — female participant N as '&lt;H&gt;_&lt;name&gt;&lt;N+1&gt;'
        /// (M_SLoop1, M_SLoop2) and the male as '&lt;H&gt;_D_&lt;name&gt;' (M_D_SLoop, D=dan/male). Role
        /// variants are tried first, then height suffix/prefix, then generic fallbacks.
        /// </summary>
        private static AnimationClip? FindByName(AnimationClip[]? clips, string clipName, AnimGender gender, int ordinal)
        {
            if (clips == null || clips.Length == 0)
                return null;
            if (string.IsNullOrEmpty(clipName))
                return clips.Length == 1 ? clips[0] : null;

            const System.StringComparison OIC = System.StringComparison.OrdinalIgnoreCase;
            string idx = (ordinal + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

            // 0) role-specific variant inside a shared controller
            foreach (string h in HeightTags)
            {
                if (gender == AnimGender.Female)
                {
                    AnimationClip? f = First(clips, c => string.Equals(c.name, h + "_" + clipName + idx, OIC));
                    if (f != null)
                        return f;
                }
                else if (gender == AnimGender.Male)
                {
                    AnimationClip? mi = First(clips, c => string.Equals(c.name, h + "_D_" + clipName + idx, OIC));
                    if (mi != null)
                        return mi;
                    AnimationClip? md = First(clips, c => string.Equals(c.name, h + "_D_" + clipName, OIC));
                    if (md != null)
                        return md;
                }
            }
            if (gender == AnimGender.Female)
            {
                AnimationClip? f = First(clips, c => string.Equals(c.name, clipName + idx, OIC));
                if (f != null)
                    return f;
            }
            else if (gender == AnimGender.Male)
            {
                AnimationClip? md = First(clips, c => string.Equals(c.name, "D_" + clipName, OIC));
                if (md != null)
                    return md;
            }

            // 1) exact
            AnimationClip? m = First(clips, c => string.Equals(c.name, clipName, OIC));
            if (m != null)
                return m;

            // 2) height SUFFIX — abdata studio clips: '<name>_M/_S/_L' (e.g. cook_move_00_M)
            foreach (string h in HeightTags)
            {
                m = First(clips, c => string.Equals(c.name, clipName + "_" + h, OIC));
                if (m != null)
                    return m;
            }

            // 3) height PREFIX — H anims: 'S_/M_/L_<name>' (e.g. M_OrgasmF_IN)
            foreach (string h in HeightTags)
            {
                m = First(clips, c => string.Equals(c.name, h + "_" + clipName, OIC));
                if (m != null)
                    return m;
            }

            // 4) generic variant catch-all: '<name>_…' or '…_<name>'
            m = First(clips, c => c.name.StartsWith(clipName + "_", OIC));
            if (m != null)
                return m;
            m = First(clips, c => c.name.EndsWith("_" + clipName, OIC));
            if (m != null)
                return m;

            return clips.Length == 1 ? clips[0] : null;
        }

        private static AnimationClip? First(AnimationClip[] clips, System.Func<AnimationClip, bool> pred)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && pred(clips[i]))
                    return clips[i];
            }

            return null;
        }

        public static void ClearCache()
        {
            Cache.Clear();
            LruKeys.Clear();
            LruNodes.Clear();
        }

        private static string BuildCacheKey(AnimGridItem item) => item.CatalogKey;

        private static void TouchLru(string key)
        {
            if (!LruNodes.TryGetValue(key, out LinkedListNode<string>? node) || node == null)
                return;
            LruKeys.Remove(node);
            LruKeys.AddFirst(node);
        }

        private static void RegisterLru(string key)
        {
            if (LruNodes.TryGetValue(key, out LinkedListNode<string>? existing) && existing != null)
            {
                LruKeys.Remove(existing);
                LruKeys.AddFirst(existing);
                return;
            }

            var node = LruKeys.AddFirst(key);
            LruNodes[key] = node;
        }

        private static void TrimLru()
        {
            while (LruKeys.Count > MaxCachedClips)
            {
                string oldest = LruKeys.Last!.Value;
                LruKeys.RemoveLast();
                LruNodes.Remove(oldest);
                Cache.Remove(oldest);
            }
        }
    }
}
