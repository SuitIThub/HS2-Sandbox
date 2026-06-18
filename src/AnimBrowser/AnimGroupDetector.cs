using System;
using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    /// <summary>Generates display-group proposals from a set of catalog animations by inferring
    /// phase (in/loop/out) and gender (m/f/f2…) roles from names and node context. All output is
    /// editable in the review window before it is committed.</summary>
    internal static class AnimGroupDetector
    {
        public static List<AnimDisplayGroupData> Detect(
            IList<AnimGridItem> items,
            Func<AnimGridItem, string?> genderContextProvider,
            Func<AnimGridItem, string> categoryNameProvider,
            Func<AnimGridItem, string> catalogPathProvider,
            bool pairWithinSubcategory,
            Func<AnimGridItem, string>? subcategoryBucketKeyProvider = null)
        {
            if (items == null || items.Count < 2)
                return new List<AnimDisplayGroupData>();

            if (!pairWithinSubcategory)
                return DetectBucket(items, genderContextProvider, categoryNameProvider, catalogPathProvider, subcategoryBucketKeyProvider);

            var buckets = new Dictionary<string, List<AnimGridItem>>(StringComparer.Ordinal);
            var order = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                AnimGridItem item = items[i];
                if (item == null)
                    continue;
                string key = ResolveSubcategoryBucketKey(item, categoryNameProvider, subcategoryBucketKeyProvider);
                if (!buckets.TryGetValue(key, out List<AnimGridItem>? list))
                {
                    list = new List<AnimGridItem>();
                    buckets[key] = list;
                    order.Add(key);
                }
                list.Add(item);
            }

            var result = new List<AnimDisplayGroupData>();
            for (int i = 0; i < order.Count; i++)
                result.AddRange(DetectBucket(buckets[order[i]], genderContextProvider, categoryNameProvider, catalogPathProvider, subcategoryBucketKeyProvider));
            return result;
        }

        /// <summary>Builds a single group from exactly the supplied items (manual grouping), inferring
        /// roles per item. Unlike <see cref="Detect"/>, does not require matching animation names.</summary>
        public static AnimDisplayGroupData? DetectSingleGroup(
            IList<AnimGridItem> items,
            Func<AnimGridItem, string?> genderContextProvider,
            Func<AnimGridItem, string> categoryNameProvider,
            Func<AnimGridItem, string> catalogPathProvider)
        {
            if (items == null || items.Count < 2)
                return null;

            return BuildManualGroup(items, genderContextProvider, categoryNameProvider, catalogPathProvider);
        }

        private static AnimDisplayGroupData? BuildManualGroup(
            IList<AnimGridItem> items,
            Func<AnimGridItem, string?> genderContextProvider,
            Func<AnimGridItem, string> categoryNameProvider,
            Func<AnimGridItem, string> catalogPathProvider)
        {
            var cluster = BuildCandidates(items, genderContextProvider, catalogPathProvider);
            if (cluster.Count < 2)
                return null;

            return BuildGroupFromCluster(
                cluster,
                categoryNameProvider,
                catalogPathProvider,
                genderContextProvider,
                subcategoryBucketKeyProvider: null,
                reviewSectionKey: "_manual");
        }

        private static List<Candidate> BuildCandidates(
            IList<AnimGridItem> items,
            Func<AnimGridItem, string?> genderContextProvider,
            Func<AnimGridItem, string> catalogPathProvider)
        {
            var cluster = new List<Candidate>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                AnimGridItem item = items[i];
                if (item == null)
                    continue;

                AnimPhase phase = AnimGroupHeuristics.DetectPhase(item.DisplayName, out string afterPhase);
                AnimGender gender = AnimGroupHeuristics.DetectGenderForItem(
                    item.DisplayName,
                    catalogPathProvider(item),
                    genderContextProvider(item));
                cluster.Add(new Candidate { Item = item, Phase = phase, Gender = gender });
            }
            return cluster;
        }

        private static AnimDisplayGroupData? BuildGroupFromCluster(
            List<Candidate> cluster,
            Func<AnimGridItem, string> categoryNameProvider,
            Func<AnimGridItem, string> catalogPathProvider,
            Func<AnimGridItem, string?> genderContextProvider,
            Func<AnimGridItem, string>? subcategoryBucketKeyProvider,
            string reviewSectionKey)
        {
            if (cluster.Count < 2)
                return null;

            PreferMixedGenderFromContext(cluster, genderContextProvider, catalogPathProvider);
            ResolveUnknownGenders(cluster);
            CollapsePhasesIfSinglePhaseOnly(cluster);
            AssignGenderOrdinals(cluster);

            AnimGridItem anchor = cluster[0].Item;
            string sectionLabel = catalogPathProvider(anchor);
            if (subcategoryBucketKeyProvider != null)
            {
                string subKey = ResolveSubcategoryBucketKey(anchor, categoryNameProvider, subcategoryBucketKeyProvider);
                string suffix = AnimGroupHeuristics.FormatSubcategoryDisambiguatorSuffix(subKey);
                if (suffix.Length > 0 && sectionLabel.Length > 0)
                    sectionLabel += suffix;
            }

            var data = new AnimDisplayGroupData
            {
                Id = AnimGroupStore.NewId(),
                Name = SuggestName(cluster),
                ReviewSectionKey = reviewSectionKey,
                ReviewSectionLabel = sectionLabel
            };
            for (int c = 0; c < cluster.Count; c++)
            {
                Candidate cand = cluster[c];
                data.Members.Add(new AnimGroupMemberData
                {
                    Ref = new AnimCatalogRef(cand.Item.Group, cand.Item.Category, cand.Item.No),
                    Phase = cand.Phase,
                    Gender = cand.Gender,
                    GenderOrdinal = cand.GenderOrdinal
                });
            }
            return data;
        }

        /// <summary>Recomputes gender ordinals after the user edited roles in the review window.</summary>
        public static void ReassignOrdinals(AnimDisplayGroupData data)
        {
            var perGenderPhase = new Dictionary<long, int>();
            int unknownSlot = 0;
            data.Members.Sort((a, b) => a.Ref.No.CompareTo(b.Ref.No));
            foreach (var member in data.Members)
            {
                if (member.Gender == AnimGender.Unknown)
                {
                    member.GenderOrdinal = unknownSlot++;
                    continue;
                }
                long bucket = ((long)member.Gender << 8) | (long)member.Phase;
                int next = perGenderPhase.TryGetValue(bucket, out int n) ? n : 0;
                member.GenderOrdinal = next;
                perGenderPhase[bucket] = next + 1;
            }
        }

        private static string ResolveSubcategoryBucketKey(
            AnimGridItem item,
            Func<AnimGridItem, string> categoryNameProvider,
            Func<AnimGridItem, string>? subcategoryBucketKeyProvider)
        {
            if (subcategoryBucketKeyProvider != null)
                return subcategoryBucketKeyProvider(item);
            string key = AnimGroupHeuristics.NormalizeCategoryKey(categoryNameProvider(item));
            return key.Length > 0 ? key : item.Category.ToString();
        }

        private static List<AnimDisplayGroupData> DetectBucket(
            IList<AnimGridItem> items,
            Func<AnimGridItem, string?> genderContextProvider,
            Func<AnimGridItem, string> categoryNameProvider,
            Func<AnimGridItem, string> catalogPathProvider,
            Func<AnimGridItem, string>? subcategoryBucketKeyProvider)
        {
            var result = new List<AnimDisplayGroupData>();
            if (items == null || items.Count < 2)
                return result;

            var clusters = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
            var order = new List<string>();

            for (int i = 0; i < items.Count; i++)
            {
                AnimGridItem item = items[i];
                if (item == null)
                    continue;

                AnimPhase phase = AnimGroupHeuristics.DetectPhase(item.DisplayName, out string afterPhase);
                AnimGender gender = AnimGroupHeuristics.DetectGenderForItem(
                    item.DisplayName,
                    catalogPathProvider(item),
                    genderContextProvider(item));

                string key = AnimGroupHeuristics.NormalizeBase(item.DisplayName);
                if (key.Length == 0)
                    key = item.CatalogKey;
                if (subcategoryBucketKeyProvider != null)
                {
                    string subKey = ResolveSubcategoryBucketKey(item, categoryNameProvider, subcategoryBucketKeyProvider);
                    key = subKey + "\0" + key;
                }

                if (!clusters.TryGetValue(key, out List<Candidate>? list))
                {
                    list = new List<Candidate>();
                    clusters[key] = list;
                    order.Add(key);
                }
                list.Add(new Candidate { Item = item, Phase = phase, Gender = gender });
            }

            for (int i = 0; i < order.Count; i++)
            {
                List<Candidate> cluster = clusters[order[i]];
                if (cluster.Count < 2)
                    continue;

                string sectionKey = subcategoryBucketKeyProvider != null
                    ? ResolveSubcategoryBucketKey(cluster[0].Item, categoryNameProvider, subcategoryBucketKeyProvider)
                    : ResolveSubcategoryBucketKey(cluster[0].Item, categoryNameProvider, null);
                AnimDisplayGroupData? data = BuildGroupFromCluster(
                    cluster,
                    categoryNameProvider,
                    catalogPathProvider,
                    genderContextProvider,
                    subcategoryBucketKeyProvider,
                    sectionKey);
                if (data != null)
                    result.Add(data);
            }

            return result;
        }

        /// <summary>When every animation name implies the same gender but catalog paths imply a mix
        /// (e.g. both named "Girl Climax" under "Additional man H" vs "Additional woman H"), prefer the
        /// mixed assignment from path/context over assigning the same gender to all.</summary>
        private static void PreferMixedGenderFromContext(
            List<Candidate> cluster,
            Func<AnimGridItem, string?> genderContextProvider,
            Func<AnimGridItem, string> catalogPathProvider)
        {
            if (cluster.Count < 2)
                return;

            AnimGender? unanimousNameGender = null;
            bool nameGendersConflict = false;
            bool contextHasMale = false;
            bool contextHasFemale = false;

            for (int i = 0; i < cluster.Count; i++)
            {
                AnimGender nameGender = AnimGroupHeuristics.DetectGenderFromDisplayName(cluster[i].Item.DisplayName);
                if (nameGender == AnimGender.Unknown)
                    continue;

                if (!unanimousNameGender.HasValue)
                    unanimousNameGender = nameGender;
                else if (unanimousNameGender.Value != nameGender)
                    nameGendersConflict = true;
            }

            if (nameGendersConflict || !unanimousNameGender.HasValue)
                return;

            for (int i = 0; i < cluster.Count; i++)
            {
                Candidate c = cluster[i];
                AnimGender contextGender = AnimGroupHeuristics.DetectGenderFromCatalogContext(
                    catalogPathProvider(c.Item),
                    genderContextProvider(c.Item));
                if (contextGender == AnimGender.Male)
                    contextHasMale = true;
                else if (contextGender == AnimGender.Female)
                    contextHasFemale = true;
            }

            if (!contextHasMale || !contextHasFemale)
                return;

            for (int i = 0; i < cluster.Count; i++)
            {
                Candidate c = cluster[i];
                AnimGender contextGender = AnimGroupHeuristics.DetectGenderFromCatalogContext(
                    catalogPathProvider(c.Item),
                    genderContextProvider(c.Item));
                if (contextGender != AnimGender.Unknown)
                    c.Gender = contextGender;
            }
        }

        private static void ResolveUnknownGenders(List<Candidate> cluster)
        {
            bool hasMale = false;
            bool hasFemale = false;
            bool hasUnknown = false;
            for (int i = 0; i < cluster.Count; i++)
            {
                Candidate c = cluster[i];
                if (c.Gender == AnimGender.Male) hasMale = true;
                else if (c.Gender == AnimGender.Female) hasFemale = true;
                else hasUnknown = true;
            }

            if (!hasUnknown)
                return;

            if (hasFemale && !hasMale)
            {
                SetUnknown(cluster, AnimGender.Male);
                return;
            }
            if (hasMale && !hasFemale)
            {
                SetUnknown(cluster, AnimGender.Female);
                return;
            }
        }

        private static void SetUnknown(List<Candidate> cluster, AnimGender gender)
        {
            for (int i = 0; i < cluster.Count; i++)
            {
                if (cluster[i].Gender == AnimGender.Unknown)
                    cluster[i].Gender = gender;
            }
        }

        private static void CollapsePhasesIfSinglePhaseOnly(List<Candidate> cluster)
        {
            var distinctPhases = new HashSet<AnimPhase>();
            for (int i = 0; i < cluster.Count; i++)
            {
                AnimPhase phase = cluster[i].Phase;
                if (phase != AnimPhase.None)
                    distinctPhases.Add(phase);
            }
            if (distinctPhases.Count > 1)
                return;
            for (int i = 0; i < cluster.Count; i++)
                cluster[i].Phase = AnimPhase.None;
        }

        private static void AssignGenderOrdinals(List<Candidate> cluster)
        {
            var perGenderPhase = new Dictionary<long, int>();
            cluster.Sort((a, b) =>
            {
                int cmp = a.Item.Sort.CompareTo(b.Item.Sort);
                if (cmp != 0) return cmp;
                return a.Item.No.CompareTo(b.Item.No);
            });

            for (int i = 0; i < cluster.Count; i++)
            {
                Candidate c = cluster[i];
                long bucket = ((long)c.Gender << 8) | (long)c.Phase;
                int next = perGenderPhase.TryGetValue(bucket, out int n) ? n : 0;
                c.GenderOrdinal = next;
                perGenderPhase[bucket] = next + 1;
            }
        }

        private static string SuggestName(List<Candidate> cluster)
        {
            Candidate best = cluster[0];
            for (int i = 1; i < cluster.Count; i++)
            {
                if (cluster[i].Item.Sort < best.Item.Sort)
                    best = cluster[i];
            }

            bool hasPhases = false;
            for (int i = 0; i < cluster.Count; i++)
            {
                if (cluster[i].Phase != AnimPhase.None)
                {
                    hasPhases = true;
                    break;
                }
            }

            if (hasPhases)
            {
                AnimGroupHeuristics.DetectPhase(best.Item.DisplayName, out string afterPhase);
                AnimGroupHeuristics.DetectGender(afterPhase, out string baseName);
                baseName = baseName.Trim();
                return baseName.Length > 0 ? baseName : best.Item.DisplayName;
            }

            AnimGroupHeuristics.DetectGender(best.Item.DisplayName, out string nameOnly);
            nameOnly = nameOnly.Trim();
            return nameOnly.Length > 0 ? nameOnly : best.Item.DisplayName;
        }

        private sealed class Candidate
        {
            public AnimGridItem Item = null!;
            public AnimPhase Phase;
            public AnimGender Gender;
            public int GenderOrdinal;
        }
    }
}
