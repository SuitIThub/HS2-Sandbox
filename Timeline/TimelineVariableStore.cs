using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HS2SandboxPlugin
{
    /// <summary>Scalar kinds used by set/get and conversions (string, int, bool).</summary>
    public enum VariableScalarKind
    {
        String,
        Int,
        Bool
    }

    /// <summary>
    /// Central store for timeline variables (string, integer, bool, list, dictionary). One instance per timeline run.
    /// Variable names are case-insensitive. Variables are created on first set.
    /// String fields support interpolation: [varName] is replaced by the variable's value (string or int).
    /// Dictionary values are stored as strings internally; cast to int on read where requested.
    /// </summary>
    public class TimelineVariableStore
    {
        /// <summary>Matches [varName] for interpolation. Variable name is group 1.</summary>
        private static readonly Regex InterpolationPattern = new Regex(@"\[([^\]]*)\]", RegexOptions.Compiled);
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _lists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> _dicts = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

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

        public void SetBool(string name, bool value)
        {
            if (string.IsNullOrEmpty(name)) return;
            _bools[name.Trim()] = value;
        }

        public bool GetBool(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _bools.TryGetValue(name.Trim(), out var v) && v;
        }

        public bool HasBool(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _bools.ContainsKey(name.Trim());
        }

        /// <summary>Clears other scalar slots for this name so only one of string/int/bool exists.</summary>
        public void SetStringExclusive(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            string k = name.Trim();
            _ints.Remove(k);
            _bools.Remove(k);
            _strings[k] = value ?? "";
        }

        public void SetIntExclusive(string name, int value)
        {
            if (string.IsNullOrEmpty(name)) return;
            string k = name.Trim();
            _strings.Remove(k);
            _bools.Remove(k);
            _ints[k] = value;
        }

        public void SetBoolExclusive(string name, bool value)
        {
            if (string.IsNullOrEmpty(name)) return;
            string k = name.Trim();
            _strings.Remove(k);
            _ints.Remove(k);
            _bools[k] = value;
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

        /// <summary>Clears string/int/bool/dict for this name, then sets the list variable.</summary>
        public void SetListExclusive(string name, IReadOnlyList<string> values)
        {
            if (string.IsNullOrEmpty(name)) return;
            string k = name.Trim();
            _strings.Remove(k);
            _ints.Remove(k);
            _bools.Remove(k);
            _dicts.Remove(k);
            SetList(k, values);
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

        // ── Dict ────────────────────────────────────────────────────────────────

        /// <summary>Replaces the entire dictionary variable with the given contents (shallow-copied). Creates it if absent.</summary>
        public void SetDict(string name, Dictionary<string, string> value)
        {
            if (string.IsNullOrEmpty(name)) return;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (value != null)
                foreach (var kv in value)
                    dict[kv.Key ?? ""] = kv.Value ?? "";
            _dicts[name.Trim()] = dict;
        }

        /// <summary>Clears string/int/bool/list for this name, then sets the dict variable.</summary>
        public void SetDictExclusive(string name, Dictionary<string, string> value)
        {
            if (string.IsNullOrEmpty(name)) return;
            string k = name.Trim();
            _strings.Remove(k);
            _ints.Remove(k);
            _bools.Remove(k);
            _lists.Remove(k);
            SetDict(k, value);
        }

        /// <summary>Sets a single key inside a dictionary variable. The dictionary is created if it does not exist.</summary>
        public void SetDictValue(string name, string key, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            string trimmedName = name.Trim();
            if (!_dicts.TryGetValue(trimmedName, out var dict))
            {
                dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _dicts[trimmedName] = dict;
            }
            dict[key ?? ""] = value ?? "";
        }

        /// <summary>Returns the raw string value for the given key, or empty string if the dict or key is absent.</summary>
        public string GetDictValue(string name, string key)
        {
            TryGetDictValue(name, key, out string v);
            return v;
        }

        /// <summary>Tries to read a key from a dictionary variable. Returns false when the dict or key is absent.</summary>
        public bool TryGetDictValue(string name, string key, out string value)
        {
            value = "";
            if (string.IsNullOrEmpty(name)) return false;
            if (!_dicts.TryGetValue(name.Trim(), out var dict)) return false;
            return dict.TryGetValue(key ?? "", out value!);
        }

        /// <summary>Returns a shallow copy of the dictionary variable, or an empty dictionary if absent.</summary>
        public Dictionary<string, string> GetDict(string name)
        {
            if (string.IsNullOrEmpty(name))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return _dicts.TryGetValue(name.Trim(), out var d)
                ? new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool HasDict(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _dicts.ContainsKey(name.Trim());
        }

        public IEnumerable<(string name, Dictionary<string, string> value)> GetAllDicts()
        {
            foreach (var kv in _dicts)
                yield return (kv.Key, new Dictionary<string, string>(kv.Value, StringComparer.OrdinalIgnoreCase));
        }

        // ── Checkpoints ──────────────────────────────────────────────────────────

        private readonly HashSet<string> _checkpointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Registers a checkpoint name (design-time or pre-scan). Case-insensitive.</summary>
        public void RegisterCheckpoint(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _checkpointNames.Add(name.Trim());
        }

        /// <summary>Returns true if a checkpoint with the given name has been registered.</summary>
        public bool HasCheckpoint(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _checkpointNames.Contains(name.Trim());
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        /// <summary>Removes a variable from all types (string, int, bool, list, dict). No-op if name is empty.</summary>
        public void Remove(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var key = name.Trim();
            _strings.Remove(key);
            _ints.Remove(key);
            _bools.Remove(key);
            _lists.Remove(key);
            _dicts.Remove(key);
        }

        /// <summary>Removes all variables (strings, ints, bools, lists, dicts).</summary>
        public void Clear()
        {
            _strings.Clear();
            _ints.Clear();
            _bools.Clear();
            _lists.Clear();
            _dicts.Clear();
        }

        /// <summary>
        /// Copies all variables from another store into this one (overwrites same names).
        /// Merges registered checkpoint names so nested validation (e.g. subtimeline rows) still sees
        /// forward jumps and full-tree checkpoints after copying.
        /// </summary>
        public void CopyFrom(TimelineVariableStore other)
        {
            if (other == null) return;
            foreach (var (n, v) in other.GetAllStrings())
                SetString(n, v);
            foreach (var (n, v) in other.GetAllInts())
                SetInt(n, v);
            foreach (var (n, v) in other.GetAllBools())
                SetBool(n, v);
            foreach (var (n, list) in other.GetAllLists())
                SetList(n, list);
            foreach (var (n, dict) in other.GetAllDicts())
                SetDict(n, dict);
            foreach (string name in other._checkpointNames)
                RegisterCheckpoint(name);
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

        public IEnumerable<(string name, bool value)> GetAllBools()
        {
            foreach (var kv in _bools)
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
        /// Bool variables and bool-like strings ("True"/"False") convert to 1/0.
        /// Use when the operand might be a string variable whose value is not yet numeric; on failure the command can show a Resolve button and retry.
        /// </summary>
        public bool TryResolveIntOperand(string nameOrLiteral, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(nameOrLiteral)) return false;
            var key = nameOrLiteral.Trim();
            if (_ints.TryGetValue(key, out int v)) { value = v; return true; }
            if (_bools.TryGetValue(key, out bool b)) { value = b ? 1 : 0; return true; }
            if (int.TryParse(key, out int n)) { value = n; return true; }
            if (TryParseBoolText(key, out bool litBool)) { value = litBool ? 1 : 0; return true; }
            string resolved = Interpolate(key).Trim();
            if (int.TryParse(resolved, out value)) return true;
            if (TryParseBoolText(resolved, out bool rb)) { value = rb ? 1 : 0; return true; }
            return false;
        }

        /// <summary>
        /// Tries to resolve an operand to bool: bool variable, int variable (nonzero = true), or literals / interpolated text ("True"/"False", 1/0, etc.).
        /// </summary>
        public bool TryResolveBoolOperand(string nameOrLiteral, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(nameOrLiteral)) return false;
            var key = nameOrLiteral.Trim();
            if (_bools.TryGetValue(key, out value)) return true;
            if (_ints.TryGetValue(key, out int iv)) { value = iv != 0; return true; }
            if (TryParseBoolText(key, out value)) return true;
            if (int.TryParse(key, out int lit)) { value = lit != 0; return true; }
            string resolved = Interpolate(key).Trim();
            if (TryParseBoolText(resolved, out value)) return true;
            if (int.TryParse(resolved, out int r)) { value = r != 0; return true; }
            return false;
        }

        /// <summary>Parses "True"/"False" (exact), standard bool strings, or integer 0/1 in a string.</summary>
        public static bool TryParseBoolText(string? text, out bool value)
        {
            value = false;
            if (text == null) return false;
            string t = text.Trim();
            if (t.Length == 0) return false;
            if (string.Equals(t, "True", StringComparison.Ordinal)) { value = true; return true; }
            if (string.Equals(t, "False", StringComparison.Ordinal)) { value = false; return true; }
            if (bool.TryParse(t, out value)) return true;
            if (int.TryParse(t, out int n)) { value = n != 0; return true; }
            return false;
        }

        /// <summary>
        /// Replaces all [varName] in text with the variable value (string as-is, int as digits, bool as "True"/"False"). Missing vars become "".
        /// </summary>
        public string Interpolate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            return InterpolationPattern.Replace(text, m =>
            {
                string name = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(name)) return "";
                if (_strings.TryGetValue(name, out string s)) return s ?? "";
                if (_bools.TryGetValue(name, out bool b)) return b ? "True" : "False";
                if (_ints.TryGetValue(name, out int n)) return n.ToString();
                return "";
            });
        }

        /// <summary>
        /// True if every [varName] in text refers to an existing variable (string, bool, or int). Used for validation.
        /// </summary>
        public bool IsValidInterpolation(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            foreach (Match m in InterpolationPattern.Matches(text))
            {
                string name = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (!_strings.ContainsKey(name) && !_ints.ContainsKey(name) && !_bools.ContainsKey(name))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// True if the value is a valid integer literal, the name of an existing integer or bool variable, or valid interpolation (e.g. [varName]).
        /// String variables are allowed in number fields; at runtime parsing may fail and the command can show a Resolve button.
        /// </summary>
        public bool IsValidIntOperand(string nameOrLiteral)
        {
            if (string.IsNullOrWhiteSpace(nameOrLiteral)) return false;
            var key = nameOrLiteral.Trim();
            if (int.TryParse(key, out _)) return true;
            if (_ints.ContainsKey(key)) return true;
            if (_bools.ContainsKey(key)) return true;
            if (TryParseBoolText(key, out _)) return true;
            if (key.IndexOf('[') >= 0 && IsValidInterpolation(key)) return true;
            return false;
        }

        /// <summary>
        /// True if the operand can resolve to a bool (literal, bool/int variable, or interpolatable text).
        /// </summary>
        public bool IsValidBoolOperand(string nameOrLiteral)
        {
            if (string.IsNullOrWhiteSpace(nameOrLiteral)) return false;
            var key = nameOrLiteral.Trim();
            if (_bools.ContainsKey(key)) return true;
            if (_ints.ContainsKey(key)) return true;
            if (TryParseBoolText(key, out _)) return true;
            if (int.TryParse(key, out _)) return true;
            if (key.IndexOf('[') >= 0 && IsValidInterpolation(key)) return true;
            return false;
        }

        /// <summary>
        /// Reads a scalar variable (string, bool, or int — same precedence as <see cref="Interpolate"/>) and converts to the requested kind.
        /// </summary>
        public bool TryCopyScalar(string sourceName, string targetName, VariableScalarKind targetKind)
        {
            string s = sourceName.Trim();
            string t = targetName.Trim();
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t)) return false;
            if (!HasString(s) && !HasBool(s) && !HasInt(s)) return false;
            switch (targetKind)
            {
                case VariableScalarKind.String:
                    SetStringExclusive(t, ConvertScalarToString(s));
                    return true;
                case VariableScalarKind.Int:
                    SetIntExclusive(t, ConvertScalarToInt(s));
                    return true;
                case VariableScalarKind.Bool:
                    SetBoolExclusive(t, ConvertScalarToBool(s));
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>String form of a scalar for storage in string vars: same order as <see cref="Interpolate"/> — string wins, then bool, then int.</summary>
        public string ConvertScalarToString(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string k = name.Trim();
            if (HasString(k)) return GetString(k) ?? "";
            if (HasBool(k)) return GetBool(k) ? "True" : "False";
            if (HasInt(k)) return GetInt(k).ToString();
            return "";
        }

        public int ConvertScalarToInt(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            string k = name.Trim();
            if (HasString(k))
            {
                string raw = (GetString(k) ?? "").Trim();
                if (TryParseBoolText(raw, out bool b)) return b ? 1 : 0;
                return int.TryParse(raw, out int n) ? n : 0;
            }
            if (HasBool(k)) return GetBool(k) ? 1 : 0;
            if (HasInt(k)) return GetInt(k);
            return 0;
        }

        public bool ConvertScalarToBool(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string k = name.Trim();
            if (HasString(k))
            {
                string raw = (GetString(k) ?? "").Trim();
                if (TryParseBoolText(raw, out bool b)) return b;
                if (int.TryParse(raw, out int n)) return n != 0;
                return false;
            }
            if (HasBool(k)) return GetBool(k);
            if (HasInt(k)) return GetInt(k) != 0;
            return false;
        }

        /// <summary>
        /// Returns all variables for display: (name, type label, value summary). Order: strings, ints, lists, dicts — sorted by name within each group.
        /// </summary>
        public List<(string name, string type, string value)> GetSnapshotForDisplay()
        {
            var list = new List<(string, string, string)>();
            foreach (var kv in _strings)
                list.Add((kv.Key, "string", kv.Value ?? ""));
            foreach (var kv in _ints)
                list.Add((kv.Key, "int", kv.Value.ToString()));
            foreach (var kv in _bools)
                list.Add((kv.Key, "bool", kv.Value ? "True" : "False"));
            foreach (var kv in _lists)
            {
                int n = kv.Value?.Count ?? 0;
                string value = n == 0 ? "(empty)" : n == 1 ? kv.Value![0] : $"{n} items";
                if (value.Length > 80) value = value.Substring(0, 77) + "...";
                list.Add((kv.Key, "list", value));
            }
            foreach (var kv in _dicts)
            {
                int n = kv.Value?.Count ?? 0;
                string value = n == 0 ? "(empty)" : $"{n} entries";
                list.Add((kv.Key, "dict", value));
            }
            list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
