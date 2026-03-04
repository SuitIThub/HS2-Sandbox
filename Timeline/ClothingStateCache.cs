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
    /// Caches Studio clothing UI parent GameObjects so we can set clothing state by pressing
    /// the correct Button (On / Half / Off). Fetched once when the Action Timeline window is first opened.
    /// </summary>
    public static class ClothingStateCache
    {
        private const string PathAllClothing = "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Cos";
        private const string PathClothingDetails = "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Clothing Details";

        public const string PartKeyAllClothing = "All Clothing";

        private static bool _fetched;
        private static readonly List<ClothingPartEntry> Parts = new List<ClothingPartEntry>();

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
            Parts.Clear();
        }

        private static IEnumerator FetchNextFrame()
        {
            yield return null;
            Fetch();
        }

        private static void Fetch()
        {
            Parts.Clear();
            GameObject? cos = GameObject.Find(PathAllClothing);
            if (cos != null)
            {
                int stateCount = CountButtonChildren(cos.transform);
                if (stateCount > 0)
                    Parts.Add(new ClothingPartEntry(PartKeyAllClothing, cos, stateCount));
            }

            GameObject? detailsRoot = GameObject.Find(PathClothingDetails);
            if (detailsRoot != null)
            {
                for (int i = 0; i < detailsRoot.transform.childCount; i++)
                {
                    Transform child = detailsRoot.transform.GetChild(i);
                    int stateCount = CountButtonChildren(child);
                    if (stateCount > 0 && !string.IsNullOrEmpty(child.name))
                        Parts.Add(new ClothingPartEntry(child.name, child.gameObject, stateCount));
                }
            }

            _fetched = true;
        }

        private static int CountButtonChildren(Transform parent)
        {
            int n = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject.name.StartsWith("Button", StringComparison.OrdinalIgnoreCase))
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

        private sealed class ClothingPartEntry
        {
            public readonly string DisplayName;
            public readonly GameObject GameObject;
            public readonly int StateCount;

            public ClothingPartEntry(string displayName, GameObject gameObject, int stateCount)
            {
                DisplayName = displayName;
                GameObject = gameObject;
                StateCount = stateCount;
            }
        }
    }
}
