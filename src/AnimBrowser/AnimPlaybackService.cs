using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class AnimPlaybackService
    {
        public static void ApplyAnimation(AnimGridItem item, IEnumerable<OCIChar> characters)
        {
            foreach (var oci in characters)
            {
                if (oci == null)
                    continue;
                try
                {
                    oci.LoadAnime(item.Group, item.Category, item.No, 0f);
                }
                catch
                {
                    // ignored per character
                }
            }
        }

        public static void SetSpeed(IEnumerable<OCIChar> characters, float speed)
        {
            if (speed < 0f)
                return;
            if (speed > 0f && speed <= AnimBrowserConfig.MinPlaybackSpeed)
                return;

            foreach (var oci in characters)
            {
                if (oci == null)
                    continue;
                try
                {
                    oci.animeSpeed = speed;
                }
                catch
                {
                    // ignored
                }
            }

            AnimBrowserAnimationUiBridge.TryRefreshFirstSelected(characters, refreshSpeed: true);
        }

        public static void SetPattern(IEnumerable<OCIChar> characters, float pattern)
        {
            foreach (var oci in characters)
            {
                if (oci == null)
                    continue;

                AnimControlCapabilities caps = AnimControlCapabilityService.Probe(oci);
                if (!caps.HasPattern)
                    continue;

                try
                {
                    oci.animePattern = pattern;
                }
                catch
                {
                    // ignored
                }
            }

            AnimBrowserAnimationUiBridge.TryRefreshFirstSelected(characters, refreshPattern: true);
        }

        public static void SetExtraParam(OCIChar? oci, int index, float value)
        {
            if (oci == null)
                return;

            AnimControlCapabilities caps = AnimControlCapabilityService.Probe(oci);
            if (index == 0 && !caps.HasExtra1)
                return;
            if (index == 1 && !caps.HasExtra2)
                return;

            try
            {
                if (index == 0)
                    oci.animeOptionParam1 = value;
                else if (index == 1)
                    oci.animeOptionParam2 = value;
            }
            catch
            {
                // ignored
            }
        }

        public static void SetOptionVisible(IEnumerable<OCIChar> characters, bool visible)
        {
            foreach (var oci in characters)
            {
                if (oci == null)
                    continue;
                try
                {
                    if (oci.oiCharInfo != null)
                        oci.oiCharInfo.animeOptionVisible = visible;
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static void SetForceLoop(IEnumerable<OCIChar> characters, bool forceLoop)
        {
            foreach (var oci in characters)
            {
                if (oci == null)
                    continue;
                try
                {
                    if (oci.oiCharInfo != null)
                        oci.oiCharInfo.isAnimeForceLoop = forceLoop;
                    if (oci.charAnimeCtrl != null)
                        oci.charAnimeCtrl.isForceLoop = forceLoop;
                }
                catch
                {
                    // ignored
                }
            }

            AnimBrowserAnimationUiBridge.TryRefreshFirstSelected(characters, refreshLoop: true);
        }

        public static bool IsFinitePlaybackTime(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        public static float SanitizeNormalizedTime(float normalizedTime)
        {
            if (!IsFinitePlaybackTime(normalizedTime))
                return 0f;
            return Mathf.Clamp01(normalizedTime);
        }

        public static bool TryGetNormalizedTime(OCIChar? oci, out float normalizedTime)
        {
            normalizedTime = 0f;
            if (oci == null)
                return false;

            try
            {
                if (TryReadRawNormalizedTime(oci, out float rawTime))
                {
                    if (!IsFinitePlaybackTime(rawTime))
                        return false;

                    normalizedTime = IsAnimationLooping(oci)
                        ? Mathf.Repeat(rawTime, 1f)
                        : Mathf.Clamp01(rawTime);
                    return IsFinitePlaybackTime(normalizedTime);
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private static bool TryReadRawNormalizedTime(OCIChar oci, out float rawTime)
        {
            rawTime = 0f;
            Animator? animator = oci.charAnimeCtrl?.animator ?? oci.charInfo?.animBody;
            if (animator != null)
            {
                rawTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                return IsFinitePlaybackTime(rawTime);
            }

            if (oci.charAnimeCtrl != null)
            {
                rawTime = oci.charAnimeCtrl.normalizedTime;
                return IsFinitePlaybackTime(rawTime);
            }

            return false;
        }

        private static bool IsAnimationLooping(OCIChar oci)
        {
            try
            {
                if (oci.oiCharInfo != null && oci.oiCharInfo.isAnimeForceLoop)
                    return true;
                if (oci.charAnimeCtrl != null && oci.charAnimeCtrl.isForceLoop)
                    return true;
                if (oci.isHAnime)
                    return true;

                Animator? animator = oci.charAnimeCtrl?.animator ?? oci.charInfo?.animBody;
                if (animator != null)
                    return animator.GetCurrentAnimatorStateInfo(0).loop;
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public static bool TryGetGroupNormalizedTime(IList<OCIChar> characters, out float normalizedTime)
        {
            normalizedTime = 0f;
            if (characters == null || characters.Count == 0)
                return false;

            for (int i = 0; i < characters.Count; i++)
            {
                if (TryGetNormalizedTime(characters[i], out normalizedTime))
                    return true;
            }

            return false;
        }

        public static bool TryGetClipLengthSeconds(OCIChar? oci, out float clipLengthSeconds)
        {
            clipLengthSeconds = 0f;
            if (oci == null)
                return false;

            try
            {
                Animator? animator = oci.charAnimeCtrl?.animator ?? oci.charInfo?.animBody;
                if (animator == null)
                    return false;

                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.length > 0f)
                {
                    clipLengthSeconds = state.length;
                    return true;
                }

                AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
                if (clips != null && clips.Length > 0 && clips[0].clip != null && clips[0].clip.length > 0f)
                {
                    clipLengthSeconds = clips[0].clip.length;
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public static bool TryGetGroupClipLengthSeconds(IList<OCIChar> characters, out float clipLengthSeconds)
        {
            clipLengthSeconds = 0f;
            if (characters == null || characters.Count == 0)
                return false;

            for (int i = 0; i < characters.Count; i++)
            {
                if (TryGetClipLengthSeconds(characters[i], out clipLengthSeconds))
                    return true;
            }

            return false;
        }

        public static bool TryGetGroupPlaybackSeconds(
            IList<OCIChar> characters,
            out float playbackSeconds,
            out float clipLengthSeconds)
        {
            playbackSeconds = 0f;
            clipLengthSeconds = 0f;
            if (!TryGetGroupNormalizedTime(characters, out float normalizedTime))
                return false;
            if (!TryGetGroupClipLengthSeconds(characters, out clipLengthSeconds) || clipLengthSeconds <= 0f)
                return false;

            playbackSeconds = normalizedTime * clipLengthSeconds;
            return IsFinitePlaybackTime(playbackSeconds);
        }

        public static void SetPlaybackSeconds(IEnumerable<OCIChar> characters, float playbackSeconds, float clipLengthSeconds)
        {
            if (!IsFinitePlaybackTime(playbackSeconds) || clipLengthSeconds <= 0f)
                return;

            float normalizedTime = SanitizeNormalizedTime(playbackSeconds / clipLengthSeconds);
            SetNormalizedTime(characters, normalizedTime);
        }

        public static string FormatDurationSeconds(float seconds)
        {
            if (!IsFinitePlaybackTime(seconds) || seconds < 0f)
                seconds = 0f;
            if (seconds >= 60f)
            {
                int minutes = (int)(seconds / 60f);
                float remainder = seconds - minutes * 60f;
                return minutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":"
                    + remainder.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture)
                    + " s";
            }

            return seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s";
        }

        public static void SetNormalizedTime(OCIChar? oci, float normalizedTime)
        {
            if (oci == null)
                return;

            normalizedTime = SanitizeNormalizedTime(normalizedTime);
            try
            {
                var animeInfo = oci.oiCharInfo?.animeInfo;
                if (animeInfo != null && animeInfo.exist)
                {
                    oci.LoadAnime(animeInfo.group, animeInfo.category, animeInfo.no, normalizedTime);
                    return;
                }

                if (oci.charAnimeCtrl?.animator != null)
                {
                    var animator = oci.charAnimeCtrl.animator;
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    animator.Play(state.shortNameHash, 0, normalizedTime);
                    animator.Update(0f);
                }
            }
            catch
            {
                // ignored
            }
        }

        public static void SetNormalizedTime(IEnumerable<OCIChar> characters, float normalizedTime)
        {
            foreach (var oci in characters)
                SetNormalizedTime(oci, normalizedTime);
        }

        public static void RestartSelected(IEnumerable<OCIChar> characters)
        {
            foreach (var oci in characters)
            {
                if (oci == null)
                    continue;
                try
                {
                    oci.RestartAnime();
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static void RestartAllInScene()
        {
            RestartSelected(StudioCharacterSelection.GetAllSceneCharacters());
        }

        public static bool TryReadControlsFromCharacter(OCIChar? oci, out AnimPlaybackControlsState state)
        {
            state = new AnimPlaybackControlsState();
            if (oci == null)
                return false;

            try
            {
                state.Speed = oci.animeSpeed;
                state.Pattern = oci.animePattern;
                state.Extra1 = oci.animeOptionParam1;
                state.Extra2 = oci.animeOptionParam2;
                state.OptionVisible = oci.oiCharInfo != null && oci.oiCharInfo.animeOptionVisible;
                state.ForceLoop = oci.oiCharInfo != null && oci.oiCharInfo.isAnimeForceLoop;
                if (!state.ForceLoop && oci.charAnimeCtrl != null)
                    state.ForceLoop = oci.charAnimeCtrl.isForceLoop;
                state.Capabilities = AnimControlCapabilityService.Probe(oci);
                TryGetNormalizedTime(oci, out state.NormalizedTime);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal struct AnimPlaybackControlsState
    {
        public float Speed;
        public float Pattern;
        public float Extra1;
        public float Extra2;
        public float NormalizedTime;
        public bool OptionVisible;
        public bool ForceLoop;
        public AnimControlCapabilities Capabilities;
    }
}
