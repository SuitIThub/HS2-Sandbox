using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Preview rig: embedded skeleton + clip sampling (no OCIChar).</summary>
    internal sealed class AnimPreviewRig
    {
        private static readonly Color DefaultColor = new Color(0.85f, 0.92f, 1f, 1f);

        private readonly AnimPreviewEmbeddedRig _skeleton = new AnimPreviewEmbeddedRig();
        private readonly Vector3[] _jointBuffer = new Vector3[AnimPreviewBoneSet.JointCount];
        private readonly bool[] _jointValid = new bool[AnimPreviewBoneSet.JointCount];

        public Color LineColor = DefaultColor;
        public int PreferredSex = 1;
        public Vector3 StageAnchor = AnimPreviewRigPool.OffScreenPosition + new Vector3(0f, 1f, 0f);

        private AnimationClip? _activeClip;
        private float _sampleTime;

        public void EnsureSkeleton(int sex)
        {
            PreferredSex = sex;
            _skeleton.EnsureBuilt(sex);
            _skeleton.SetWorldAnchor(StageAnchor);
        }

        public void Detach()
        {
            _activeClip = null;
            _sampleTime = 0f;
            _skeleton.Dispose();
        }

        public void ApplyAnimation(AnimGridItem item, AnimationClip? clip, float normalizedTime)
        {
            if (clip == null)
            {
                _skeleton.ApplyRestPose();
                return;
            }

            _activeClip = clip;
            _sampleTime = Mathf.Clamp01(normalizedTime) * clip.length;
            _skeleton.SampleClip(clip, _sampleTime);
        }

        public void AdvanceSample(float deltaTime, float speed)
        {
            if (_activeClip == null)
                return;

            _sampleTime += deltaTime * Mathf.Max(0.01f, speed);
            if (_activeClip.length > 0.01f)
            {
                if (_sampleTime > _activeClip.length)
                    _sampleTime %= _activeClip.length;
            }

            _skeleton.SampleClip(_activeClip, _sampleTime);
        }

        public bool TrySampleJoints(out Vector3[] joints, out bool[] valid)
        {
            joints = _jointBuffer;
            valid = _jointValid;
            if (_skeleton.Root == null)
                return false;

            _skeleton.SetWorldAnchor(StageAnchor);
            return _skeleton.TryReadJoints(_jointBuffer, _jointValid);
        }
    }
}
