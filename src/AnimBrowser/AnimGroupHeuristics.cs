using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>Name-based heuristics for inferring phase and gender of catalog animations.
    /// The Studio catalog stores no gender/phase metadata, so we infer it from naming conventions
    /// (Japanese 男/女 prefixes, Female/Male tokens, in/loop/out suffixes). Results are always
    /// user-correctable in the grouping review window.</summary>
    internal static class AnimGroupHeuristics
    {
        private static readonly string[] PhaseLoopTokens = { "loop", "ループ" };
        private static readonly string[] PhaseInTokens = { "in", "イン" };
        private static readonly string[] PhaseOutTokens = { "out", "アウト" };

        public static AnimPhase DetectPhase(string? name, out string baseName)
        {
            baseName = (name ?? string.Empty).Trim();
            if (baseName.Length == 0)
                return AnimPhase.None;

            if (TryStripSuffix(baseName, PhaseLoopTokens, out string stripped))
            {
                baseName = stripped;
                return AnimPhase.Loop;
            }
            if (TryStripSuffix(baseName, PhaseOutTokens, out stripped))
            {
                baseName = stripped;
                return AnimPhase.Out;
            }
            if (TryStripSuffix(baseName, PhaseInTokens, out stripped))
            {
                baseName = stripped;
                return AnimPhase.In;
            }
            return AnimPhase.None;
        }

        public static AnimGender DetectGender(string? name, out string baseName)
        {
            baseName = (name ?? string.Empty).Trim();
            if (baseName.Length == 0)
                return AnimGender.Unknown;

            if (baseName[0] == '男')
            {
                baseName = baseName.Substring(1).Trim();
                StripLeadingGenderLabelSeparator(ref baseName);
                return AnimGender.Male;
            }
            if (baseName[0] == '女')
            {
                baseName = baseName.Substring(1).Trim();
                StripLeadingGenderLabelSeparator(ref baseName);
                return AnimGender.Female;
            }

            if (TryStripWord(ref baseName, "female") || TryStripWord(ref baseName, "girl") ||
                TryStripWord(ref baseName, "woman") || TryStripTrailingToken(ref baseName, "f"))
            {
                StripLeadingGenderLabelSeparator(ref baseName);
                return AnimGender.Female;
            }
            if (TryStripWord(ref baseName, "male") || TryStripWord(ref baseName, "man") ||
                TryStripWord(ref baseName, "boy") || TryStripTrailingToken(ref baseName, "m"))
            {
                StripLeadingGenderLabelSeparator(ref baseName);
                return AnimGender.Male;
            }

            return AnimGender.Unknown;
        }

        /// <summary>Gender hint from a category or group label (e.g. "subcat 1 f", "cat 1m").</summary>
        public static AnimGender DetectGenderFromContext(string? contextName)
        {
            if (string.IsNullOrEmpty(contextName))
                return AnimGender.Unknown;
            return DetectGenderFromCatalogSegment(contextName);
        }

        /// <summary>Gender from animation display name only (phase stripped).</summary>
        public static AnimGender DetectGenderFromDisplayName(string? animName)
        {
            DetectPhase(animName, out string afterPhase);
            return DetectGender(afterPhase, out _);
        }

        /// <summary>Gender from catalog path / group / category context (animation name ignored).</summary>
        public static AnimGender DetectGenderFromCatalogContext(string? catalogPath, string? extraContext = null)
        {
            if (!string.IsNullOrEmpty(extraContext))
            {
                AnimGender gender = DetectGenderFromCatalogSegment(extraContext);
                if (gender != AnimGender.Unknown)
                    return gender;
            }

            if (string.IsNullOrEmpty(catalogPath))
                return AnimGender.Unknown;

            AnimGender fromPath = DetectGenderFromCatalogSegment(catalogPath);
            if (fromPath != AnimGender.Unknown)
                return fromPath;

            int slash = catalogPath.IndexOf(" / ", StringComparison.Ordinal);
            if (slash < 0)
                return AnimGender.Unknown;

            string category = catalogPath.Substring(slash + 3).Trim();
            AnimGender fromCategory = DetectGenderFromCatalogSegment(category);
            if (fromCategory != AnimGender.Unknown)
                return fromCategory;

            string group = catalogPath.Substring(0, slash).Trim();
            return DetectGenderFromCatalogSegment(group);
        }

        /// <summary>Infer gender from animation name, then catalog path segments (group / category).</summary>
        public static AnimGender DetectGenderForItem(
            string? animName,
            string? catalogPath,
            string? extraContext = null)
        {
            AnimGender gender = DetectGenderFromDisplayName(animName);
            if (gender != AnimGender.Unknown)
                return gender;

            return DetectGenderFromCatalogContext(catalogPath, extraContext);
        }

        private static AnimGender DetectGenderFromCatalogSegment(string segment)
        {
            if (StringEx.IsNullOrWhiteSpace(segment))
                return AnimGender.Unknown;

            string s = segment.Trim();
            AnimGender fromName = DetectGender(s, out _);
            if (fromName != AnimGender.Unknown)
                return fromName;

            string lowered = s.ToLowerInvariant();
            if (s[0] == '男' || lowered.StartsWith("male", StringComparison.Ordinal) ||
                lowered.StartsWith("man ", StringComparison.Ordinal) || lowered.EndsWith(" man", StringComparison.Ordinal))
            {
                return AnimGender.Male;
            }
            if (s[0] == '女' || lowered.StartsWith("female", StringComparison.Ordinal) ||
                lowered.StartsWith("woman ", StringComparison.Ordinal) || lowered.EndsWith(" woman", StringComparison.Ordinal))
            {
                return AnimGender.Female;
            }
            if (lowered.EndsWith("female", StringComparison.Ordinal) || lowered.EndsWith(" f", StringComparison.Ordinal) ||
                lowered.EndsWith("1f", StringComparison.Ordinal) || s.EndsWith("女", StringComparison.Ordinal))
            {
                return AnimGender.Female;
            }
            if (lowered.EndsWith("male", StringComparison.Ordinal) || lowered.EndsWith(" m", StringComparison.Ordinal) ||
                lowered.EndsWith("1m", StringComparison.Ordinal) || s.EndsWith("男", StringComparison.Ordinal))
            {
                return AnimGender.Male;
            }

            return AnimGender.Unknown;
        }

        /// <summary>Normalized clustering key: lowercase base with gender and phase markers removed.</summary>
        public static string NormalizeBase(string? name)
        {
            DetectPhase(name, out string withoutPhase);
            DetectGender(withoutPhase, out string withoutGender);
            return CollapseSpaces(withoutGender).ToLowerInvariant();
        }

        /// <summary>Normalized sub-category key for cross-group pairing (gender suffix stripped).</summary>
        public static string NormalizeCategoryKey(string? categoryName)
        {
            if (StringEx.IsNullOrWhiteSpace(categoryName))
                return string.Empty;
            DetectGender(categoryName!.Trim(), out string withoutGender);
            return NormalizeBase(withoutGender);
        }

        /// <summary>Merge bucket key for a subcategory within one top-level group. Identically named
        /// siblings (e.g. two "Cowgirl" rows) get distinct ordinals sorted by <paramref name="categoryId"/>.</summary>
        public static string BuildSubcategoryMergeBucketKey(
            int categoryId,
            string? categoryName,
            IList<AnimCategorySiblingEntry> categoriesInGroup)
        {
            string norm = NormalizeCategoryKey(categoryName);
            if (norm.Length == 0)
                norm = (categoryName ?? string.Empty).Trim().ToLowerInvariant();

            int ordinal = 0;
            for (int i = 0; i < categoriesInGroup.Count; i++)
            {
                AnimCategorySiblingEntry entry = categoriesInGroup[i];
                int id = entry.CategoryId;
                string name = entry.Name;
                string siblingNorm = NormalizeCategoryKey(name);
                if (siblingNorm.Length == 0)
                    siblingNorm = name.Trim().ToLowerInvariant();
                if (!string.Equals(siblingNorm, norm, StringComparison.Ordinal))
                    continue;
                if (id == categoryId)
                    return norm + "#" + ordinal.ToString(CultureInfo.InvariantCulture);
                ordinal++;
            }

            return norm + "#0";
        }

        /// <summary>Human-readable suffix for duplicate subcategory names (e.g. bucket "cowgirl#1" → " (2)").</summary>
        public static string FormatSubcategoryDisambiguatorSuffix(string bucketKey)
        {
            if (string.IsNullOrEmpty(bucketKey))
                return string.Empty;
            int hash = bucketKey.LastIndexOf('#');
            if (hash < 0 || hash >= bucketKey.Length - 1)
                return string.Empty;
            if (!int.TryParse(
                    bucketKey.Substring(hash + 1),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int ordinal) ||
                ordinal <= 0)
            {
                return string.Empty;
            }
            return " (" + (ordinal + 1).ToString(CultureInfo.InvariantCulture) + ")";
        }

        public static string FormatMergedSubcategoryDisplayName(string displayName, string bucketKey)
        {
            string suffix = FormatSubcategoryDisambiguatorSuffix(bucketKey);
            return suffix.Length > 0 ? displayName + suffix : displayName;
        }

        public static List<AnimCategorySiblingEntry> BuildSortedCategorySiblingList(IList<AnimCategoryNode> children)
        {
            var list = new List<AnimCategorySiblingEntry>(children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                AnimCategoryNode child = children[i];
                list.Add(new AnimCategorySiblingEntry(child.CategoryId, child.Name));
            }
            list.Sort((a, b) =>
            {
                int cmp = string.Compare(
                    StudioAutoTranslation.Resolve(a.Name),
                    StudioAutoTranslation.Resolve(b.Name),
                    StringComparison.CurrentCultureIgnoreCase);
                if (cmp != 0)
                    return cmp;
                return a.CategoryId.CompareTo(b.CategoryId);
            });
            return list;
        }

        private static bool TryStripSuffix(string text, string[] tokens, out string result)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (text.Length > token.Length &&
                    text.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                {
                    result = text.Substring(0, text.Length - token.Length).TrimEnd();
                    return true;
                }
            }
            result = text;
            return false;
        }

        private static bool TryStripWord(ref string text, string word)
        {
            int idx = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;
            bool leftOk = idx == 0 || !char.IsLetter(text[idx - 1]);
            int after = idx + word.Length;
            bool rightOk = after >= text.Length || !char.IsLetter(text[after]);
            if (!leftOk || !rightOk)
                return false;
            if (idx == 0)
            {
                text = text.Substring(after).TrimStart();
                return true;
            }
            text = CollapseSpaces((text.Substring(0, idx) + " " + text.Substring(after)).Trim());
            return true;
        }

        /// <summary>After a leading gender label (e.g. "Female:"), drop punctuation before the rest.</summary>
        private static void StripLeadingGenderLabelSeparator(ref string text)
        {
            text = text.TrimStart();
            while (text.Length > 0)
            {
                char c = text[0];
                if (c == ':' || c == '：' || c == '-' || c == '_' || c == '|')
                {
                    text = text.Substring(1).TrimStart();
                    continue;
                }
                break;
            }
        }

        private static bool TryStripTrailingToken(ref string text, string token)
        {
            string trimmed = text.TrimEnd();
            if (trimmed.Length <= token.Length + 1)
                return false;
            if (!trimmed.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                return false;
            char before = trimmed[trimmed.Length - token.Length - 1];
            if (before != ' ' && before != '_' && before != '-')
                return false;
            text = trimmed.Substring(0, trimmed.Length - token.Length - 1).TrimEnd();
            return true;
        }

        private static string CollapseSpaces(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            var sb = new StringBuilder(text.Length);
            bool prevSpace = false;
            foreach (char c in text)
            {
                if (c == ' ')
                {
                    if (!prevSpace && sb.Length > 0)
                        sb.Append(' ');
                    prevSpace = true;
                }
                else
                {
                    sb.Append(c);
                    prevSpace = false;
                }
            }
            return sb.ToString().Trim();
        }
    }
}
