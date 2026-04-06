using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Provides a stable mapping from clothing parts to Studio's clothing state API.
    /// No longer depends on finding UI GameObjects; instead uses fixed indices into MPCharCtrl.stateInfo.
    /// </summary>
    public static class ClothingStateCache
    {
        public const string PartKeyAllClothing = "All Clothing";

        private static bool _fetched;
        private static readonly List<ClothingPartEntry> Parts = new List<ClothingPartEntry>();

        public static bool IsFetched => _fetched;

        public static void EnsureFetched(MonoBehaviour runner)
        {
            if (_fetched) return;
            Fetch();
        }

        public static void ClearCache()
        {
            _fetched = false;
            Parts.Clear();
        }

        private static void Fetch()
        {
            Parts.Clear();

            // All clothing toggle (uses OnClickCosState)
            Parts.Add(new ClothingPartEntry(PartKeyAllClothing, 3, -1));

            // Individual clothing detail types (uses OnClickClothingDetails)
            // Order and indices must match Studio's internal mapping:
            // 0 = Top, 1 = Bottom, 2 = Inner Top, 3 = Inner Bottom, 4 = Stockings,
            // 5 = Gloves, 6 = Socks, 7 = Shoes.
            AddDetail("Top", 0, 3);
            AddDetail("Bottom", 1, 3);
            AddDetail("Inner Layer (Top)", 2, 3);
            AddDetail("Inner Layer (Bottom)", 3, 3);
            AddDetail("Stockings", 4, 3);
            // Gloves, Socks, Shoes are 2-state only (On / Off)
            AddDetail("Gloves", 5, 2);
            AddDetail("Socks", 6, 2);
            AddDetail("Shoes", 7, 2);

            _fetched = true;
        }

        private static void AddDetail(string displayName, int clothingTypeIndex, int stateCount)
        {
            Parts.Add(new ClothingPartEntry(displayName, stateCount, clothingTypeIndex));
        }

        public static IReadOnlyList<string> GetPartNames()
        {
            return Parts.Select(p => p.DisplayName).ToList();
        }

        public static int GetStateCount(string partKey)
        {
            if (string.IsNullOrEmpty(partKey)) return 2;
            var entry = Parts.FirstOrDefault(p => string.Equals(p.DisplayName, partKey, StringComparison.OrdinalIgnoreCase));
            return entry?.StateCount ?? 2;
        }

        public static bool PressState(string partKey, int stateIndex)
        {
            if (string.IsNullOrEmpty(partKey)) return false;

            var entry = Parts.FirstOrDefault(p => string.Equals(p.DisplayName, partKey, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return false;

            int clampedState = Mathf.Clamp(stateIndex, 0, entry.StateCount - 1);

            if (string.Equals(entry.DisplayName, PartKeyAllClothing, StringComparison.OrdinalIgnoreCase))
            {
                // 0: On, 1: Half, 2: Off for full outfit
                return StudioCharStateBridge.TrySetOutfitState(clampedState);
            }

            if (entry.ClothingTypeIndex < 0)
                return false;

            // 0: On, 1: Half, 2: Off for 3-state parts; 0: On, 1: Off for 2-state parts
            return StudioCharStateBridge.TrySetClothingDetailState(entry.ClothingTypeIndex, clampedState);
        }

        private sealed class ClothingPartEntry
        {
            public readonly string DisplayName;
            public readonly int StateCount;
            public readonly int ClothingTypeIndex;

            public ClothingPartEntry(string displayName, int stateCount, int clothingTypeIndex)
            {
                DisplayName = displayName;
                StateCount = stateCount;
                ClothingTypeIndex = clothingTypeIndex;
            }
        }
    }
}
