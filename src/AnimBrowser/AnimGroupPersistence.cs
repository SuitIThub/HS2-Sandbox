using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;

namespace HS2SandboxPlugin
{
    /// <summary>Reads/writes <c>anim_browser_groups.json</c> (tree merges + display groups).
    /// Hand-written JSON via <see cref="AnimGroupJson"/> per project policy.</summary>
    internal static class AnimGroupPersistence
    {
        public const int JsonVersion = 2;

        private static string GroupsPath =>
            PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "anim_browser_groups.json");

        public static void Load(
            List<AnimTreeMergeRule> treeMerges,
            List<AnimDisplayGroupData> displayGroups,
            Dictionary<string, string> displayNameOverrides)
        {
            try
            {
                string path = GroupsPath;
                if (!File.Exists(path))
                    return;

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!(AnimGroupJson.Parse(json) is Dictionary<string, object?> root))
                {
                    SandboxServices.Log.LogWarning("AnimBrowser: Could not parse anim_browser_groups.json");
                    return;
                }

                foreach (object? entry in AnimGroupJson.AsArray(root.TryGetValue("treeMerges", out var tm) ? tm : null))
                {
                    var obj = AnimGroupJson.AsObject(entry);
                    var rule = new AnimTreeMergeRule
                    {
                        Id = AnimGroupJson.AsString(obj.TryGetValue("id", out var id) ? id : null),
                        Name = AnimGroupJson.AsString(obj.TryGetValue("name", out var nm) ? nm : null),
                        Kind = (AnimTreeMergeKind)AnimGroupJson.AsInt(obj.TryGetValue("kind", out var kd) ? kd : null)
                    };
                    foreach (object? src in AnimGroupJson.AsArray(obj.TryGetValue("sources", out var ss) ? ss : null))
                    {
                        if (AnimCatalogRef.TryParse(AnimGroupJson.AsString(src), out AnimCatalogRef refValue))
                            rule.Sources.Add(refValue);
                    }
                    LoadRefArray(obj, "excludedSources", rule.ExcludedSources);
                    LoadRefArray(obj, "excludedAnimations", rule.ExcludedAnimationRefs);
                    LoadStringMap(obj, "subcategoryBucketAliases", rule.SubcategoryBucketAliases);
                    if (!string.IsNullOrEmpty(rule.Id) && rule.Sources.Count >= 2)
                        treeMerges.Add(rule);
                }

                foreach (object? entry in AnimGroupJson.AsArray(root.TryGetValue("displayGroups", out var dg) ? dg : null))
                {
                    var obj = AnimGroupJson.AsObject(entry);
                    var group = new AnimDisplayGroupData
                    {
                        Id = AnimGroupJson.AsString(obj.TryGetValue("id", out var id) ? id : null),
                        Name = AnimGroupJson.AsString(obj.TryGetValue("name", out var nm) ? nm : null)
                    };
                    foreach (object? mem in AnimGroupJson.AsArray(obj.TryGetValue("members", out var ms) ? ms : null))
                    {
                        var mobj = AnimGroupJson.AsObject(mem);
                        if (!AnimCatalogRef.TryParse(AnimGroupJson.AsString(mobj.TryGetValue("ref", out var rf) ? rf : null), out AnimCatalogRef refValue))
                            continue;
                        group.Members.Add(new AnimGroupMemberData
                        {
                            Ref = refValue,
                            Phase = (AnimPhase)AnimGroupJson.AsInt(mobj.TryGetValue("phase", out var ph) ? ph : null),
                            Gender = (AnimGender)AnimGroupJson.AsInt(mobj.TryGetValue("gender", out var gn) ? gn : null),
                            GenderOrdinal = AnimGroupJson.AsInt(mobj.TryGetValue("ord", out var od) ? od : null)
                        });
                    }
                    if (!string.IsNullOrEmpty(group.Id) && group.Members.Count >= 2)
                        displayGroups.Add(group);
                }

                foreach (object? entry in AnimGroupJson.AsArray(root.TryGetValue("displayNames", out var dn) ? dn : null))
                {
                    var obj = AnimGroupJson.AsObject(entry);
                    string key = AnimGroupJson.AsString(obj.TryGetValue("key", out var ky) ? ky : null);
                    string name = AnimGroupJson.AsString(obj.TryGetValue("name", out var nm) ? nm : null);
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(name))
                        continue;
                    displayNameOverrides[key] = name;
                }
            }
            catch (System.Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimBrowser: Could not load anim_browser_groups.json: " + ex.Message);
            }
        }

        private static void LoadRefArray(Dictionary<string, object?> obj, string key, List<AnimCatalogRef> target)
        {
            foreach (object? entry in AnimGroupJson.AsArray(obj.TryGetValue(key, out var value) ? value : null))
            {
                if (AnimCatalogRef.TryParse(AnimGroupJson.AsString(entry), out AnimCatalogRef refValue))
                    target.Add(refValue);
            }
        }

        private static void LoadStringMap(Dictionary<string, object?> obj, string key, Dictionary<string, string> target)
        {
            foreach (var kvp in AnimGroupJson.AsObject(obj.TryGetValue(key, out var value) ? value : null))
            {
                string mapped = AnimGroupJson.AsString(kvp.Value);
                if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(mapped))
                    target[kvp.Key] = mapped;
            }
        }

        public static void Save(
            List<AnimTreeMergeRule> treeMerges,
            List<AnimDisplayGroupData> displayGroups,
            Dictionary<string, string> displayNameOverrides)
        {
            try
            {
                string path = GroupsPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                FileEx.WriteAllTextAtomic(path, BuildJson(treeMerges, displayGroups, displayNameOverrides), Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimBrowser: Could not save anim_browser_groups.json: " + ex.Message);
            }
        }

        private static string BuildJson(
            List<AnimTreeMergeRule> treeMerges,
            List<AnimDisplayGroupData> displayGroups,
            Dictionary<string, string> displayNameOverrides)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            sb.Append("\"version\":").Append(JsonVersion);

            sb.Append(",\"treeMerges\":[");
            for (int i = 0; i < treeMerges.Count; i++)
            {
                var rule = treeMerges[i];
                if (i > 0)
                    sb.Append(',');
                sb.Append("{\"id\":\"").Append(AnimGroupJson.Escape(rule.Id))
                    .Append("\",\"name\":\"").Append(AnimGroupJson.Escape(rule.Name))
                    .Append("\",\"kind\":").Append((int)rule.Kind)
                    .Append(",\"sources\":[");
                for (int s = 0; s < rule.Sources.Count; s++)
                {
                    if (s > 0)
                        sb.Append(',');
                    sb.Append('"').Append(rule.Sources[s].Key).Append('"');
                }
                sb.Append("]");
                AppendRefArray(sb, "excludedSources", rule.ExcludedSources);
                AppendRefArray(sb, "excludedAnimations", rule.ExcludedAnimationRefs);
                AppendStringMap(sb, "subcategoryBucketAliases", rule.SubcategoryBucketAliases);
                sb.Append('}');
            }
            sb.Append(']');

            sb.Append(",\"displayGroups\":[");
            for (int i = 0; i < displayGroups.Count; i++)
            {
                var group = displayGroups[i];
                if (i > 0)
                    sb.Append(',');
                sb.Append("{\"id\":\"").Append(AnimGroupJson.Escape(group.Id))
                    .Append("\",\"name\":\"").Append(AnimGroupJson.Escape(group.Name))
                    .Append("\",\"members\":[");
                for (int m = 0; m < group.Members.Count; m++)
                {
                    var member = group.Members[m];
                    if (m > 0)
                        sb.Append(',');
                    sb.Append("{\"ref\":\"").Append(member.Ref.Key)
                        .Append("\",\"phase\":").Append((int)member.Phase)
                        .Append(",\"gender\":").Append((int)member.Gender)
                        .Append(",\"ord\":").Append(member.GenderOrdinal.ToString(CultureInfo.InvariantCulture))
                        .Append('}');
                }
                sb.Append("]}");
            }
            sb.Append(']');

            sb.Append(",\"displayNames\":[");
            bool firstName = true;
            foreach (var kvp in displayNameOverrides)
            {
                if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value))
                    continue;
                if (!firstName)
                    sb.Append(',');
                firstName = false;
                sb.Append("{\"key\":\"").Append(AnimGroupJson.Escape(kvp.Key))
                    .Append("\",\"name\":\"").Append(AnimGroupJson.Escape(kvp.Value))
                    .Append("\"}");
            }
            sb.Append(']');

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendRefArray(StringBuilder sb, string key, List<AnimCatalogRef> refs)
        {
            if (refs.Count == 0)
                return;
            sb.Append(",\"").Append(key).Append("\":[");
            for (int i = 0; i < refs.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append('"').Append(refs[i].Key).Append('"');
            }
            sb.Append(']');
        }

        private static void AppendStringMap(StringBuilder sb, string key, Dictionary<string, string> map)
        {
            if (map.Count == 0)
                return;
            sb.Append(",\"").Append(key).Append("\":{");
            bool first = true;
            foreach (var kvp in map)
            {
                if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value))
                    continue;
                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append('"').Append(AnimGroupJson.Escape(kvp.Key))
                    .Append("\":\"").Append(AnimGroupJson.Escape(kvp.Value)).Append('"');
            }
            sb.Append('}');
        }
    }
}
