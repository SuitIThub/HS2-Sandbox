using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal readonly struct AnimCatalogKey
    {
        public readonly int Group;
        public readonly int Category;
        public readonly int No;

        public AnimCatalogKey(int group, int category, int no)
        {
            Group = group;
            Category = category;
            No = no;
        }

        public bool IsValid => Group >= 0 && Category >= 0 && No >= 0;
    }

    internal struct AnimControlCapabilities
    {
        public bool HasPattern;
        public bool HasExtra1;
        public bool HasExtra2;
        public string Extra1ParamName;
        public string Extra2ParamName;

        public bool HasAnyExtra => HasExtra1 || HasExtra2;
    }

    /// <summary>
    /// Mirrors OCIChar.LoadAnime + animeOptionParam setters: extras apply when isHAnime and
    /// animeParam[i] is non-empty (LoadAnime clears the slot via CheckAnimeParam when unused).
    /// Pattern applies when isAnimeMotion and the motion float exists on the body animator.
    /// </summary>
    internal static class AnimControlCapabilityService
    {
        private const string DefaultExtra1Param = "height";
        private const string DefaultExtra2Param = "Breast";

        public static AnimControlCapabilities Probe(OCIChar? oci) =>
            oci == null ? default : ProbeFromCharacterState(oci);

        public static string FormatParamLabel(string paramName, int fallbackIndex)
        {
            if (string.IsNullOrEmpty(paramName))
                return fallbackIndex == 0 ? "Extra 1" : "Extra 2";

            string lower = paramName.ToLowerInvariant();
            if (lower.Contains("height"))
                return "Height";
            if (lower.Contains("breast"))
                return "Breast";
            if (lower.Contains("motion"))
                return "Motion";

            return paramName;
        }

        private static AnimControlCapabilities ProbeFromCharacterState(OCIChar oci)
        {
            var caps = new AnimControlCapabilities();
            try
            {
                var animeInfo = oci.oiCharInfo?.animeInfo;
                if (animeInfo == null || !animeInfo.exist)
                    return caps;

                if (oci.isAnimeMotion && HasAnimatorFloatParam(oci, "motion"))
                    caps.HasPattern = true;

                if (!oci.isHAnime || HasUninitializedAnimeParamDefaults(oci.animeParam))
                    return caps;

                string[] animeParam = oci.animeParam;
                if (animeParam == null || animeParam.Length == 0)
                    return caps;

                if (animeParam.Length > 0 && !string.IsNullOrEmpty(animeParam[0]))
                {
                    caps.HasExtra1 = true;
                    caps.Extra1ParamName = animeParam[0];
                }

                if (animeParam.Length > 1 && !string.IsNullOrEmpty(animeParam[1]))
                {
                    caps.HasExtra2 = true;
                    caps.Extra2ParamName = animeParam[1];
                }
            }
            catch
            {
                // ignored
            }

            return caps;
        }

        /// <summary>
        /// OCIChar field initializer before the first LoadAnime; not a reliable capability signal.
        /// </summary>
        private static bool HasUninitializedAnimeParamDefaults(string[]? animeParam)
        {
            return animeParam != null
                && animeParam.Length > 1
                && animeParam[0] == DefaultExtra1Param
                && animeParam[1] == DefaultExtra2Param;
        }

        private static bool HasAnimatorFloatParam(OCIChar oci, string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
                return false;

            try
            {
                AnimatorControllerParameter[]? parameters = oci.charInfo?.animBody?.parameters;
                if (parameters == null || parameters.Length == 0)
                    return false;

                for (int i = 0; i < parameters.Length; i++)
                {
                    AnimatorControllerParameter p = parameters[i];
                    if (p.type == AnimatorControllerParameterType.Float &&
                        p.name == paramName)
                        return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }
    }
}
