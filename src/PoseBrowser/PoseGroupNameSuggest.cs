using System;
using System.Collections.Generic;
using System.Linq;

namespace HS2SandboxPlugin
{
    /// <summary>Suggests a shared group name from pose display names that differ only by simple variants (A/B, F/M, 1/2, etc.).</summary>
    public static class PoseGroupNameSuggest
    {
        public static string Suggest(IList<string> poseNames)
        {
            if (poseNames == null || poseNames.Count == 0)
                return "";

            var names = poseNames
                .Select(n => (n ?? "").Trim())
                .Where(n => n.Length > 0)
                .ToList();

            if (names.Count == 0)
                return "";
            if (names.Count == 1)
                return names[0];

            if (TrySuggest(names, out string suggested) && IsAcceptableSuggestion(names, suggested))
                return suggested;

            return names[0];
        }

        private static bool TrySuggest(List<string> names, out string result)
        {
            if (TryTrailingSingleCharVariant(names, out result))
                return true;
            if (TryTokenAlignVariant(names, out result))
                return true;
            if (TryCommonPrefixSuffixVariant(names, out result))
                return true;

            result = "";
            return false;
        }

        /// <summary>C1-1A + C1-1B → C1-1</summary>
        private static bool TryTrailingSingleCharVariant(List<string> names, out string result)
        {
            result = "";
            if (names.Any(n => n.Length < 2))
                return false;

            var bases = new List<string>(names.Count);
            var suffixes = new List<char>(names.Count);
            foreach (var n in names)
            {
                char last = n[n.Length - 1];
                if (!char.IsLetterOrDigit(last))
                    return false;
                bases.Add(n.Substring(0, n.Length - 1));
                suffixes.Add(last);
            }

            if (bases.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
                return false;
            if (suffixes.Distinct().Count() < 2)
                return false;

            result = bases[0].TrimEnd();
            return result.Length > 0;
        }

        /// <summary>Test a 4 + Test b 4 → Test 4; Testname 4 a + Testname 4 b → Testname 4; Test F 1 + Test M 1 → Test 1</summary>
        private static bool TryTokenAlignVariant(List<string> names, out string result)
        {
            result = "";
            var tokenized = names.Select(SplitTokens).ToList();
            int count = tokenized[0].Length;
            if (count == 0 || tokenized.Any(t => t.Length != count))
                return false;

            var merged = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var column = tokenized.Select(t => t[i]).ToList();
                if (column.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
                {
                    merged.Add(column[0]);
                    continue;
                }

                if (IsSingleLetterVariantColumn(column))
                    continue;

                if (TryMergeGenderPrefixedTokens(column, out string combined))
                {
                    merged.Add(combined);
                    continue;
                }

                return false;
            }

            if (merged.Count == 0)
                return false;

            result = CollapseSpaces(string.Join(" ", merged.ToArray()));
            return result.Length > 0;
        }

        /// <summary>Test FA + Test MA → Test A (prefix/suffix on remainder after shared leading text).</summary>
        private static bool TryCommonPrefixSuffixVariant(List<string> names, out string result)
        {
            result = "";
            int lcp = CommonPrefixLength(names);
            var tails = names.Select(n => n.Substring(lcp)).ToList();
            if (tails.Any(t => t.Length == 0))
                return false;

            int lcs = CommonSuffixLength(tails);
            string prefix = names[0].Substring(0, lcp);
            string suffix = lcs > 0 ? tails[0].Substring(tails[0].Length - lcs) : "";
            var middles = tails.Select(t => t.Substring(0, t.Length - lcs).Trim()).ToList();

            if (!middles.All(IsSimpleVariantMiddle))
                return false;

            result = CollapseSpaces((prefix + suffix).Trim());
            if (result.Length == 0 && middles.All(string.IsNullOrEmpty))
                result = names[0];

            return result.Length > 0;
        }

        private static bool TryMergeGenderPrefixedTokens(IList<string> tokens, out string merged)
        {
            merged = "";
            if (tokens.Count < 2)
                return false;

            int lcs = CommonSuffixLength(tokens);
            if (lcs == 0)
                return false;

            var prefixes = tokens.Select(t => t.Substring(0, t.Length - lcs)).ToList();
            if (!prefixes.All(p => p.Length == 1 && (p[0] == 'F' || p[0] == 'M' || p[0] == 'f' || p[0] == 'm')))
                return false;

            merged = tokens[0].Substring(tokens[0].Length - lcs);
            return merged.Length > 0;
        }

        private static bool IsSingleLetterVariantColumn(IList<string> tokens)
        {
            if (tokens.Count < 2)
                return false;
            if (!tokens.All(t => t.Length == 1 && char.IsLetterOrDigit(t[0])))
                return false;

            return tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
        }

        private static bool IsSimpleVariantMiddle(string middle)
        {
            if (string.IsNullOrEmpty(middle))
                return true;

            middle = middle.Trim();
            if (middle.Length == 0)
                return true;
            if (middle.Length == 1)
                return char.IsLetterOrDigit(middle[0]);

            return middle.Equals("F", StringComparison.OrdinalIgnoreCase)
                   || middle.Equals("M", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAcceptableSuggestion(List<string> names, string suggested)
        {
            if (StringEx.IsNullOrWhiteSpace(suggested))
                return false;

            suggested = suggested.Trim();
            return TrySuggest(names, out string again) &&
                   string.Equals(again, suggested, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] SplitTokens(string name) =>
            name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private static int CommonPrefixLength(IList<string> strings)
        {
            if (strings.Count == 0)
                return 0;

            int min = strings.Min(s => s.Length);
            int i = 0;
            for (; i < min; i++)
            {
                char c = char.ToUpperInvariant(strings[0][i]);
                for (int j = 1; j < strings.Count; j++)
                {
                    if (char.ToUpperInvariant(strings[j][i]) != c)
                        return i;
                }
            }

            return i;
        }

        private static int CommonSuffixLength(IList<string> strings)
        {
            if (strings.Count == 0)
                return 0;

            int min = strings.Min(s => s.Length);
            int i = 0;
            for (; i < min; i++)
            {
                char c = char.ToUpperInvariant(strings[0][strings[0].Length - 1 - i]);
                for (int j = 1; j < strings.Count; j++)
                {
                    string s = strings[j];
                    if (char.ToUpperInvariant(s[s.Length - 1 - i]) != c)
                        return i;
                }
            }

            return i;
        }

        private static string CollapseSpaces(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts.ToArray());
        }
    }
}
