using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// In-memory index of every pose under the library root for fast "All poses" / "Favorites" views.
    /// Built in the background after Studio loads; updated incrementally when possible.
    /// </summary>
    public sealed class PoseLibraryIndexCache
    {
        private readonly object _lock = new object();
        private List<PoseGridItem> _all = new List<PoseGridItem>();
        private readonly Dictionary<string, PoseGridItem> _byPath = new Dictionary<string, PoseGridItem>(StringComparer.OrdinalIgnoreCase);
        private bool _ready;
        private bool _stale;

        public bool IsReady
        {
            get { lock (_lock) return _ready && !_stale; }
        }

        public bool IsStale
        {
            get { lock (_lock) return _stale || !_ready; }
        }

        public bool IsBuilding { get; private set; }

        public int Count
        {
            get { lock (_lock) return _all.Count; }
        }

        public void MarkStale()
        {
            lock (_lock)
            {
                _stale = true;
                _ready = false;
            }
        }

        public IEnumerator BuildCoroutine(PoseDataService data, PoseTagDatabase tags)
        {
            IsBuilding = true;
            var scratch = new List<PoseGridItem>();
            yield return data.LoadPosesRecursiveCoroutine(data.PoseRootPath, scratch, filesPerYield: 8);

            foreach (var item in scratch)
                tags.ApplyToItem(item);

            lock (_lock)
            {
                DestroyThumbnails(_all);
                _all = scratch;
                RebuildIndex();
                _ready = true;
                _stale = false;
            }

            IsBuilding = false;
        }

        public bool TryGetAllSnapshot(out List<PoseGridItem> items)
        {
            lock (_lock)
            {
                if (!_ready || _stale)
                {
                    items = null!;
                    return false;
                }

                items = new List<PoseGridItem>(_all);
                return true;
            }
        }

        public bool TryGetFavoritesSnapshot(out List<PoseGridItem> items)
        {
            lock (_lock)
            {
                if (!_ready || _stale)
                {
                    items = null!;
                    return false;
                }

                items = _all.Where(i => i.IsFavorite).ToList();
                return true;
            }
        }

        public void SyncMetadata(PoseGridItem source)
        {
            if (string.IsNullOrEmpty(source.FilePath)) return;
            lock (_lock)
            {
                if (!_ready || _stale) return;
                if (!_byPath.TryGetValue(source.FilePath, out var cached) || ReferenceEquals(cached, source))
                    return;

                cached.IsFavorite = source.IsFavorite;
                cached.DisplayName = source.DisplayName;
                cached.LastUsedUtc = source.LastUsedUtc;
                cached.Tags = new HashSet<string>(source.Tags, StringComparer.OrdinalIgnoreCase);
            }
        }

        public void AddOrUpdate(PoseGridItem item)
        {
            if (string.IsNullOrEmpty(item.FilePath)) return;
            string key = Path.GetFullPath(item.FilePath);
            lock (_lock)
            {
                if (!_ready || _stale) return;

                if (_byPath.TryGetValue(key, out var existing))
                {
                    PreserveThumbnail(existing, item);
                    int idx = _all.IndexOf(existing);
                    if (idx >= 0)
                        _all[idx] = item;
                    _byPath[key] = item;
                    if (!ReferenceEquals(existing, item))
                        DestroyThumbnail(existing);
                }
                else
                {
                    _all.Add(item);
                    _byPath[key] = item;
                }
            }
        }

        public void RemovePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            string key = Path.GetFullPath(filePath);
            lock (_lock)
            {
                if (!_ready || _stale) return;
                if (!_byPath.TryGetValue(key, out var existing)) return;
                _byPath.Remove(key);
                _all.Remove(existing);
                DestroyThumbnail(existing);
            }
        }

        public void RemovePaths(IEnumerable<string> filePaths)
        {
            foreach (var path in filePaths)
                RemovePath(path);
        }

        public void Clear()
        {
            lock (_lock)
            {
                DestroyThumbnails(_all);
                _all = new List<PoseGridItem>();
                _byPath.Clear();
                _ready = false;
                _stale = true;
            }
        }

        private void RebuildIndex()
        {
            _byPath.Clear();
            foreach (var item in _all)
            {
                if (string.IsNullOrEmpty(item.FilePath)) continue;
                _byPath[Path.GetFullPath(item.FilePath)] = item;
            }
        }

        private static void PreserveThumbnail(PoseGridItem from, PoseGridItem to)
        {
            if (to.Thumbnail == null && from.Thumbnail != null)
                to.Thumbnail = from.Thumbnail;
        }

        private static void DestroyThumbnail(PoseGridItem item)
        {
            if (item.Thumbnail != null)
            {
                UnityEngine.Object.Destroy(item.Thumbnail);
                item.Thumbnail = null;
            }
        }

        private static void DestroyThumbnails(IEnumerable<PoseGridItem> items)
        {
            foreach (var item in items)
                DestroyThumbnail(item);
        }
    }
}
