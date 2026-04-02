using System;
using System.Collections.Generic;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>User-edited values for a subtimeline param (shown on the parent Sub row and persisted in the subtimeline JSON).</summary>
    public class SubTimelineParamInputs
    {
        public string StringText = "";
        public string IntText = "0";
        public bool BoolValue;
        public List<string> ListItems { get; } = new List<string>();
        public Dictionary<string, string> Dict { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void ClearList() => ListItems.Clear();

        public void SetListFromArray(string[]? values)
        {
            ListItems.Clear();
            if (values == null) return;
            foreach (string v in values)
            {
                string t = (v ?? "").Trim();
                if (t.Length > 0) ListItems.Add(t);
            }
        }

        public static string SerializeDictLines(Dictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return "";
            var lines = new StringBuilder();
            foreach (var kv in dict)
            {
                if (lines.Length > 0) lines.Append('\n');
                lines.Append(kv.Key).Append('=').Append(kv.Value);
            }
            return lines.ToString();
        }

        public static Dictionary<string, string> ParseDictLines(string text)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) return dict;
            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                int eq = trimmed.IndexOf('=');
                if (eq < 0) continue;
                string k = trimmed.Substring(0, eq).Trim();
                string v = trimmed.Substring(eq + 1);
                if (k.Length > 0) dict[k] = v;
            }
            return dict;
        }
    }
}
