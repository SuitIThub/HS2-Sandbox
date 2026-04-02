using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HS2SandboxPlugin
{
    public class MultiPathSearchBarManager : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 1f;
        private const float SearchBarWidth = 90f;
        private const float SearchBarHeight = 20f;

        private static readonly string[] DefaultParentPaths =
        {
            "StudioScene/Canvas Main Menu/02_Manipulate/51_WearCustom/Category Panel/Viewport/Content",
        };

        private static readonly CategoryPathChain[] DefaultCategoryRelationships =
        {
            
        };

        private readonly Dictionary<string, SearchBarBinding> _bindings = new Dictionary<string, SearchBarBinding>(StringComparer.Ordinal);
        private float _nextRefreshTime;
        private Font? _defaultFont;

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            RefreshBindings();
        }

        private void RefreshBindings()
        {
            CleanupBindings();

            foreach (string path in GetConfiguredFlatPaths())
            {
                GameObject? parent = GameObject.Find(path);
                if (parent == null)
                    continue;

                string bindingKey = "flat:" + path;
                if (_bindings.ContainsKey(bindingKey))
                    continue;

                SearchBarBinding? binding = TryCreateFlatBinding(path, parent);
                if (binding != null)
                    _bindings[bindingKey] = binding;
            }

            foreach (HierarchicalRootDefinition root in GetHierarchicalRoots())
            {
                GameObject? searchParent = GameObject.Find(root.SearchParentPath);
                if (searchParent == null)
                    continue;

                string bindingKey = "tree:" + root.SearchParentPath;
                if (_bindings.ContainsKey(bindingKey))
                    continue;

                SearchBarBinding? binding = TryCreateHierarchicalBinding(root, searchParent);
                if (binding != null)
                    _bindings[bindingKey] = binding;
            }
        }

        private void CleanupBindings()
        {
            List<string>? staleKeys = null;

            foreach (KeyValuePair<string, SearchBarBinding> pair in _bindings)
            {
                if (pair.Value.Parent == null || pair.Value.SearchRoot == null)
                {
                    staleKeys ??= new List<string>();
                    staleKeys.Add(pair.Key);
                }
            }

            if (staleKeys == null)
                return;

            foreach (string key in staleKeys)
                _bindings.Remove(key);
        }

        private IEnumerable<string> GetConfiguredFlatPaths()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < DefaultParentPaths.Length; i++)
            {
                string path = (DefaultParentPaths[i] ?? "").Trim();
                if (path.Length == 0 || !seen.Add(path))
                    continue;

                yield return path;
            }

            string raw = SandboxConfig.AdditionalSearchBarParentPaths?.Value ?? "";
            string[] lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string path = line.Trim();
                if (path.Length == 0 || !seen.Add(path))
                    continue;

                yield return path;
            }
        }

        private IEnumerable<HierarchicalRootDefinition> GetHierarchicalRoots()
        {
            var groups = new Dictionary<string, List<CategoryPathChain>>(StringComparer.Ordinal);

            for (int i = 0; i < DefaultCategoryRelationships.Length; i++)
            {
                CategoryPathChain relation = DefaultCategoryRelationships[i];
                if (!relation.IsValid)
                    continue;

                string searchParentPath = relation.Paths[0];
                if (string.IsNullOrWhiteSpace(searchParentPath))
                    continue;

                string rootPath = searchParentPath;
                if (!groups.TryGetValue(rootPath, out List<CategoryPathChain>? list))
                {
                    list = new List<CategoryPathChain>();
                    groups.Add(rootPath, list);
                }

                list.Add(relation);
            }

            foreach (KeyValuePair<string, List<CategoryPathChain>> pair in groups)
                yield return new HierarchicalRootDefinition(pair.Key, pair.Value);
        }

        private SearchBarBinding? TryCreateFlatBinding(string path, GameObject parent)
        {
            if (FindExistingInjectedSearchBar(parent.transform) != null)
            {
                SandboxServices.Log.LogInfo($"Search bar already exists for '{path}'.");
                return null;
            }

            GameObject searchRoot = CreateSearchBar(parent.transform);
            InputField inputField = searchRoot.GetComponent<InputField>();
            inputField.onValueChanged.AddListener(value => ApplyFlatFilter(parent.transform, searchRoot.transform, value));

            ApplyFlatFilter(parent.transform, searchRoot.transform, "");

            SandboxServices.Log.LogInfo($"Injected search bar into '{path}'.");
            return new SearchBarBinding(parent, searchRoot, null);
        }

        private SearchBarBinding? TryCreateHierarchicalBinding(HierarchicalRootDefinition root, GameObject searchParent)
        {
            if (FindExistingInjectedSearchBar(searchParent.transform) != null)
            {
                SandboxServices.Log.LogInfo($"Search bar already exists for hierarchical root '{root.SearchParentPath}'.");
                return null;
            }

            var relationMap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < root.Relationships.Count; i++)
            {
                IReadOnlyList<string> paths = root.Relationships[i].Paths;
                for (int j = 0; j < paths.Count - 1; j++)
                    relationMap[paths[j]] = paths[j + 1];
            }

            GameObject searchRoot = CreateSearchBar(searchParent.transform);
            InputField inputField = searchRoot.GetComponent<InputField>();
            inputField.onValueChanged.AddListener(value => ApplyHierarchicalFilter(root.SearchParentPath, searchRoot.transform, relationMap, value));

            ApplyHierarchicalFilter(root.SearchParentPath, searchRoot.transform, relationMap, "");

            SandboxServices.Log.LogInfo($"Injected hierarchical search bar into '{root.SearchParentPath}'.");
            return new SearchBarBinding(searchParent, searchRoot, root.SearchParentPath);
        }

        private GameObject CreateSearchBar(Transform parent)
        {
            Font font = _defaultFont ??= Resources.GetBuiltinResource<Font>("Arial.ttf");
            GameObject searchRoot = new GameObject("HS2SandboxSearchBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            searchRoot.transform.SetParent(parent, false);
            searchRoot.transform.SetSiblingIndex(0);

            RectTransform rootRect = searchRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(SearchBarWidth, SearchBarHeight);
            rootRect.anchoredPosition = Vector2.zero;

            Image background = searchRoot.GetComponent<Image>();
            background.color = new Color32(0xC8, 0xC7, 0xC3, 0xFF);

            LayoutElement layout = searchRoot.GetComponent<LayoutElement>();
            layout.preferredWidth = SearchBarWidth;
            layout.preferredHeight = SearchBarHeight;
            layout.minWidth = SearchBarWidth;
            layout.minHeight = SearchBarHeight;

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(searchRoot.transform, false);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(6f, 2f);
            textAreaRect.offsetMax = new Vector2(-6f, -2f);

            GameObject placeholderGo = CreateTextElement("Placeholder", textArea.transform, font, "Search...", new Color(1f, 1f, 1f, 0.7f), FontStyle.Italic);
            GameObject textGo = CreateTextElement("Text", textArea.transform, font, "", Color.white, FontStyle.Normal);

            InputField inputField = searchRoot.AddComponent<InputField>();
            inputField.textComponent = textGo.GetComponent<Text>();
            inputField.placeholder = placeholderGo.GetComponent<Graphic>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.caretColor = Color.white;
            inputField.selectionColor = new Color(0.25f, 0.25f, 0.25f, 0.85f);

            return searchRoot;
        }

        private static Transform? FindExistingInjectedSearchBar(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == "HS2SandboxSearchBar")
                    return child;
            }

            return null;
        }

        private static GameObject CreateTextElement(string name, Transform parent, Font font, string text, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = 12;
            label.fontStyle = style;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.color = color;
            label.text = text;

            return go;
        }

        private static void ApplyFlatFilter(Transform parent, Transform searchRoot, string value)
        {
            string needle = (value ?? "").Trim();

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == searchRoot)
                    continue;

                bool visible = needle.Length == 0 || ChildMatches(child.gameObject, needle);
                child.gameObject.SetActive(visible);
            }
        }

        private static void ApplyHierarchicalFilter(string searchParentPath, Transform searchRoot, IReadOnlyDictionary<string, string> relationMap, string value)
        {
            GameObject? parent = GameObject.Find(searchParentPath);
            if (parent == null)
                return;

            string needle = (value ?? "").Trim();

            for (int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                if (child == searchRoot)
                    continue;

                string categoryPath = GetTransformPath(child);
                bool visible = ApplyCategoryNodeFilter(categoryPath, child, relationMap, needle);
                child.gameObject.SetActive(visible);
            }
        }

        private static bool ApplyCategoryNodeFilter(string categoryPath, Transform categoryTransform, IReadOnlyDictionary<string, string> relationMap, string needle)
        {
            if (!relationMap.TryGetValue(categoryPath, out string? valuesPath))
                return needle.Length == 0 || ChildMatches(categoryTransform.gameObject, needle);

            GameObject? valuesRoot = GameObject.Find(valuesPath);
            if (valuesRoot == null)
                return false;

            bool anyVisible = false;

            for (int i = 0; i < valuesRoot.transform.childCount; i++)
            {
                Transform child = valuesRoot.transform.GetChild(i);
                string childPath = GetTransformPath(child);

                bool childVisible = relationMap.ContainsKey(childPath)
                    ? ApplyCategoryNodeFilter(childPath, child, relationMap, needle)
                    : needle.Length == 0 || ChildMatches(child.gameObject, needle);

                child.gameObject.SetActive(childVisible);
                anyVisible |= childVisible;
            }

            return anyVisible;
        }

        private static bool ChildMatches(GameObject gameObject, string needle)
        {
            if (TryGetTextMember(gameObject) is not string haystack || haystack.Length == 0)
                return false;

            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string? TryGetTextMember(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component? component = components[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                if (!string.Equals(type.Name, "ListNode", StringComparison.Ordinal))
                    continue;

                PropertyInfo? property = type.GetProperty("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
                {
                    string? value = property.GetValue(component, null) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }

                FieldInfo? field = type.GetField("text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(string))
                {
                    string? value = field.GetValue(component) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }

            return null;
        }

        private static string GetTransformPath(Transform transform)
        {
            var segments = new List<string>();
            Transform? current = transform;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string? GetParentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return null;

            return path.Substring(0, slash);
        }

        private readonly struct CategoryPathChain
        {
            public CategoryPathChain(params string[] paths)
            {
                Paths = NormalizePaths(paths);
            }

            public IReadOnlyList<string> Paths { get; }
            public bool IsValid => Paths.Count >= 2;

            private static string[] NormalizePaths(string[]? paths)
            {
                if (paths == null || paths.Length == 0)
                    return Array.Empty<string>();

                var list = new List<string>(paths.Length);
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = (paths[i] ?? "").Trim();
                    if (path.Length > 0)
                        list.Add(path);
                }

                return list.ToArray();
            }
        }

        private sealed class HierarchicalRootDefinition
        {
            public HierarchicalRootDefinition(string searchParentPath, List<CategoryPathChain> relationships)
            {
                SearchParentPath = searchParentPath;
                Relationships = relationships;
            }

            public string SearchParentPath { get; }
            public List<CategoryPathChain> Relationships { get; }
        }

        private sealed class SearchBarBinding
        {
            public SearchBarBinding(GameObject parent, GameObject searchRoot, string? logicalParentPath)
            {
                Parent = parent;
                SearchRoot = searchRoot;
                LogicalParentPath = logicalParentPath;
            }

            public GameObject Parent { get; }
            public GameObject SearchRoot { get; }
            public string? LogicalParentPath { get; }
        }
    }
}

