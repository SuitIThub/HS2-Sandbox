using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Plays a short notification-style sound (two-tone chime) when executed. Generated at runtime.
    /// </summary>
    public class SoundCommand : TimelineCommand
    {
        public override string TypeId => "sound";

        private static AudioClip? _cachedBeep;

        private static AudioClip GetBeepClip()
        {
            if (_cachedBeep != null) return _cachedBeep;
            const int sampleRate = 44100;
            float[] part1 = Tone(sampleRate, 880f, 0.07f, 0.25f);
            float[] gap = new float[Mathf.RoundToInt(sampleRate * 0.02f)];
            float[] part2 = Tone(sampleRate, 660f, 0.09f, 0.28f);
            int total = part1.Length + gap.Length + part2.Length;
            float[] data = new float[total];
            Array.Copy(part1, 0, data, 0, part1.Length);
            Array.Copy(part2, 0, data, part1.Length + gap.Length, part2.Length);
            _cachedBeep = AudioClip.Create("TimelineNotify", total, 1, sampleRate, false);
            _cachedBeep.SetData(data, 0);
            return _cachedBeep;
        }

        private static float[] Tone(int sampleRate, float frequency, float duration, float volume)
        {
            int n = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (float)i / n;
                env = env * env;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * env;
            }
            return data;
        }

        public override string GetDisplayLabel() => "Sound";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label("Notification chime", GUILayout.ExpandWidth(true));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            AudioClip clip = GetBeepClip();
            if (clip != null)
            {
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                AudioSource.PlayClipAtPoint(clip, pos);
            }
            onComplete();
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}
