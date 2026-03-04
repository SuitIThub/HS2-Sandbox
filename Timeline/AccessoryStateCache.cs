using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Caches Studio accessory UI parent GameObjects so we can set accessory state by pressing
    /// the correct Button (On / Off). Fetched once when the Action Timeline window is first opened.
    /// Path: StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Slot
    /// Each slot is a direct child of "Slot"; each has two Button children (On, Off). The first slot
    /// (Slot01(Clone)) is the "all slots" control in Studio; the command shows it as "All Slots".
    /// </summary>
    public static class AccessoryStateCache
    {
        /// <summary>GameObject name of the slot that controls all accessories at once. Shown as "All Slots" in the command UI.</summary>
        public const string SlotNameAllSlots = "Slot01(Clone)";

        private const string PathSlotRoot = "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Slot";

        private static bool _fetched;
        private static readonly List<AccessorySlotEntry> Slots = new List<AccessorySlotEntry>();

        public static bool IsFetched => _fetched;

        public static void EnsureFetched(MonoBehaviour runner)
        {
            if (_fetched) return;
            if (runner != null && runner.isActiveAndEnabled)
                runner.StartCoroutine(FetchNextFrame());
        }

        public static void ClearCache()
        {
            _fetched = false;
            Slots.Clear();
        }

        private static IEnumerator FetchNextFrame()
        {
            yield return null;
            Fetch();
        }

        private static void Fetch()
        {
            Slots.Clear();
            GameObject? slotRoot = GameObject.Find(PathSlotRoot);
            if (slotRoot == null) return;

            for (int i = 0; i < slotRoot.transform.childCount; i++)
            {
                Transform child = slotRoot.transform.GetChild(i);
                int buttonCount = CountButtonChildren(child);
                if (buttonCount >= 2 && !string.IsNullOrEmpty(child.name))
                    Slots.Add(new AccessorySlotEntry(child.name, child.gameObject, 2)); // On, Off only
            }

            _fetched = true;
        }

        private static int CountButtonChildren(Transform parent)
        {
            int n = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                // Only count GameObjects whose name starts with "Button"; ignore TextMeshPro etc.
                if (t.gameObject.name.StartsWith("Button", StringComparison.OrdinalIgnoreCase))
                    n++;
            }
            return n;
        }

        private static IReadOnlyList<Transform> GetButtonTransforms(Transform parent)
        {
            var list = new List<Transform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (t.gameObject.name.StartsWith("Button", StringComparison.OrdinalIgnoreCase))
                    list.Add(t);
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            return list;
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
            if (entry?.GameObject == null) return false;
            if (stateIndex < 0 || stateIndex >= entry.StateCount) return false;

            IReadOnlyList<Transform> buttons = GetButtonTransforms(entry.GameObject.transform);
            if (stateIndex >= buttons.Count) return false;

            Transform btnTransform = buttons[stateIndex];
            var button = btnTransform.GetComponent<Button>();
            if (button == null) return false;

            InvokePress(button);
            return true;
        }

        private static void InvokePress(Button button)
        {
            var method = button.GetType().GetMethod("Press", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(button, null);
                return;
            }
            button.onClick?.Invoke();
        }

        private sealed class AccessorySlotEntry
        {
            public readonly string DisplayName;
            public readonly GameObject GameObject;
            public readonly int StateCount;

            public AccessorySlotEntry(string displayName, GameObject gameObject, int stateCount)
            {
                DisplayName = displayName;
                GameObject = gameObject;
                StateCount = stateCount;
            }
        }
    }
}
