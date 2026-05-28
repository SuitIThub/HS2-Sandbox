using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Provides a stable mapping from accessory slots to Studio's accessory state API.
    /// No longer depends on finding UI GameObjects; instead uses fixed indices into MPCharCtrl.stateInfo.
    /// </summary>
    public static class AccessoryStateCache
    {
        /// <summary>Virtual name for the control that toggles all accessory slots at once.</summary>
        public const string SlotNameAllSlots = "All Slots";

        private static bool _fetched;
        private static readonly List<AccessorySlotEntry> Slots = new List<AccessorySlotEntry>();

        public static bool IsFetched => _fetched;

        public static void EnsureFetched(MonoBehaviour runner)
        {
            if (_fetched) return;
            Fetch();
        }

        public static void ClearCache()
        {
            _fetched = false;
            Slots.Clear();
        }

        private static void Fetch()
        {
            Slots.Clear();

            // First, a synthetic "All Slots" entry that we display specially in the UI.
            Slots.Add(new AccessorySlotEntry(SlotNameAllSlots, 2, -1));

            // Then 20 concrete slots, with indices 0..19.
            for (int i = 0; i < 20; i++)
            {
                string name = $"Slot {i + 1:00}";
                Slots.Add(new AccessorySlotEntry(name, 2, i));
            }

            _fetched = true;
        }

        public static IReadOnlyList<string> GetSlotNames()
        {
            return Slots.Select(s => s.DisplayName).ToList();
        }

        public static int GetStateCount(string slotKey)
        {
            if (string.IsNullOrEmpty(slotKey)) return 2;
            var entry = Slots.FirstOrDefault(s => string.Equals(s.DisplayName, slotKey, StringComparison.OrdinalIgnoreCase));
            return entry?.StateCount ?? 2;
        }

        public static bool PressState(string slotKey, int stateIndex)
        {
            if (string.IsNullOrEmpty(slotKey)) return false;

            var entry = Slots.FirstOrDefault(s => string.Equals(s.DisplayName, slotKey, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                return false;

            int clampedState = Mathf.Clamp(stateIndex, 0, entry.StateCount - 1);
            bool turnOn = clampedState == 0;

            // Prefer the direct MPCharCtrl.stateInfo API when available
            if (string.Equals(entry.DisplayName, SlotNameAllSlots, StringComparison.OrdinalIgnoreCase))
            {
                bool any = false;
                for (int i = 0; i < Slots.Count; i++)
                {
                    var s = Slots[i];
                    if (s.SlotIndex < 0)
                        continue;
                    if (StudioCharStateBridge.TrySetAccessoryState(s.SlotIndex, turnOn))
                        any = true;
                }
                if (any)
                    return true;
            }
            else if (entry.SlotIndex >= 0)
            {
                if (StudioCharStateBridge.TrySetAccessoryState(entry.SlotIndex, turnOn))
                    return true;
            }

            // No UI fallback anymore; rely solely on the stateInfo API
            return false;
        }

        private sealed class AccessorySlotEntry
        {
            public readonly string DisplayName;
            public readonly int StateCount;
            public readonly int SlotIndex;

            public AccessorySlotEntry(string displayName, int stateCount, int slotIndex)
            {
                DisplayName = displayName;
                StateCount = stateCount;
                SlotIndex = slotIndex;
            }
        }
    }
}
