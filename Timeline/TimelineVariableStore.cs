using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Central store for timeline variables (string and integer). One instance per timeline run.
    /// Variable names are case-insensitive. Variables are created on first set.
    /// String fields support interpolation: [varName] is replaced by the variable's value (string or int).
    /// </summary>
    public class TimelineVariableStore
    {
        /// <summary>Matches [varName] for interpolation. Variable name is group 1.</summary>
        private static readonly Regex InterpolationPattern = new Regex(@"\[([^\]]*)\]", RegexOptions.Compiled);
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _lists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public void SetString(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            _strings[name.Trim()] = value ?? "";
        }

        public string GetString(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return _strings.TryGetValue(name.Trim(), out var v) ? v : "";
        }

        public bool HasString(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _strings.ContainsKey(name.Trim());
        }

        public void SetInt(string name, int value)
        {
            if (string.IsNullOrEmpty(name)) return;
            _ints[name.Trim()] = value;
        }

        public int GetInt(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            return _ints.TryGetValue(name.Trim(), out var v) ? v : 0;
        }

        public bool HasInt(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _ints.ContainsKey(name.Trim());
        }

        public void SetList(string name, IReadOnlyList<string> values)
        {
            if (string.IsNullOrEmpty(name)) return;
            var list = new List<string>();
            if (values != null)
            {
                foreach (string v in values)
                    list.Add(v ?? "");
            }
            _lists[name.Trim()] = list;
        }

        public List<string> GetList(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<string>();
            return _lists.TryGetValue(name.Trim(), out var list) ? new List<string>(list) : new List<string>();
        }

        public bool HasList(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _lists.ContainsKey(name.Trim());
        }

        /// <summary>Removes a variable from all types (string, int, list). No-op if name is empty.</summary>
        public void Remove(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var key = name.Trim();
            _strings.Remove(key);
            _ints.Remove(key);
            _lists.Remove(key);
        }

        /// <summary>Removes all variables (strings, ints, lists).</summary>
        public void Clear()
        {
            _strings.Clear();
            _ints.Clear();
            _lists.Clear();
        }

        /// <summary>Copies all variables from another store into this one (overwrites same names).</summary>
        public void CopyFrom(TimelineVariableStore other)
        {
            if (other == null) return;
            foreach (var (n, v) in other.GetAllStrings())
                SetString(n, v);
            foreach (var (n, v) in other.GetAllInts())
                SetInt(n, v);
            foreach (var (n, list) in other.GetAllLists())
                SetList(n, list);
        }

        public IEnumerable<(string name, string value)> GetAllStrings()
        {
            foreach (var kv in _strings)
                yield return (kv.Key, kv.Value ?? "");
        }

        public IEnumerable<(string name, int value)> GetAllInts()
        {
            foreach (var kv in _ints)
                yield return (kv.Key, kv.Value);
        }

        public IEnumerable<(string name, List<string> value)> GetAllLists()
        {
            foreach (var kv in _lists)
                yield return (kv.Key, kv.Value != null ? new List<string>(kv.Value) : new List<string>());
        }

        /// <summary>
        /// Resolves an operand: int variable by name, literal number, or interpolated text (e.g. [varName]) parsed as number. Returns 0 if invalid.
        /// </summary>
        public int ResolveIntOperand(string nameOrLiteral)
        {
            return TryResolveIntOperand(nameOrLiteral, out int v) ? v : 0;
        }

        /// <summary>
        /// Tries to resolve an operand to an int: int variable by name, literal number, or interpolated text (e.g. [stringVar] or [intVar]) parsed as number.
        /// Use when the operand might be a string variable whose value is not yet numeric; on failure the command can show a Resolve button and retry.
        /// </summary>
        public bool TryResolveIntOperand(string nameOrLiteral, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(nameOrLiteral)) return false;
            var key = nameOrLiteral.Trim();
            if (_ints.TryGetValue(key, out int v)) { value = v; return true; }
            if (int.TryParse(key, out int n)) { value = n; return true; }
            string resolved = Interpolate(key).Trim();
            return int.TryParse(resolved, out value);
        }

        /// <summary>
        /// Replaces all [varName] in text with the variable value (string vars as-is, int vars as ToString()). Missing vars become "".
        /// </summary>
        public string Interpolate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            return InterpolationPattern.Replace(text, m =>
            {
                string name = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(name)) return "";
                if (_strings.TryGetValue(name, out string s)) return s ?? "";
                if (_ints.TryGetValue(name, out int n)) return n.ToString();
                return "";
            });
        }

        /// <summary>
        /// True if every [varName] in text refers to an existing variable (string or int). Used for validation.
        /// </summary>
        public bool IsValidInterpolation(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            foreach (Match m in InterpolationPattern.Matches(text))
            {
                string name = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (!_strings.ContainsKey(name) && !_ints.ContainsKey(name))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// True if the value is a valid integer literal, the name of an existing integer variable, or valid interpolation (e.g. [varName]).
        /// String variables are allowed in number fields; at runtime parsing may fail and the command can show a Resolve button.
        /// </summary>
        public bool IsValidIntOperand(string nameOrLiteral)
        {
            if (string.IsNullOrWhiteSpace(nameOrLiteral)) return false;
            var key = nameOrLiteral.Trim();
            if (int.TryParse(key, out _)) return true;
            if (_ints.ContainsKey(key)) return true;
            if (key.IndexOf('[') >= 0 && IsValidInterpolation(key)) return true;
            return false;
        }

        /// <summary>
        /// Returns all variables for display: (name, "string" or "int" or "list", value as string). Order: strings first, then ints, then lists, by name.
        /// </summary>
        public List<(string name, string type, string value)> GetSnapshotForDisplay()
        {
            var list = new List<(string, string, string)>();
            foreach (var kv in _strings)
                list.Add((kv.Key, "string", kv.Value ?? ""));
            foreach (var kv in _ints)
                list.Add((kv.Key, "int", kv.Value.ToString()));
            foreach (var kv in _lists)
            {
                int n = kv.Value?.Count ?? 0;
                string value = n == 0 ? "(empty)" : n == 1 ? kv.Value![0] : $"{n} items";
                if (value.Length > 80) value = value.Substring(0, 77) + "...";
                list.Add((kv.Key, "list", value));
            }
            list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
