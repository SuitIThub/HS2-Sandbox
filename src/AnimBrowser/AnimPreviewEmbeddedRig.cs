using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Plugin-owned, path-faithful bone hierarchy for clip sampling — no scene OCIChar.
    /// The hierarchy reproduces the real character's bone names/parents/local TRS (baked in
    /// <see cref="AnimPreviewSkeletonData"/>) so generic clips bind via transform path when fed
    /// through <see cref="AnimationClip.SampleAnimation"/>. The 19 stick joints are references
    /// into this tree; only their world positions are read for drawing.
    /// See [[animbrowser-clip-sampling-runtime]].
    /// </summary>
    internal sealed class AnimPreviewEmbeddedRig
    {
        private readonly Transform[] _bones = new Transform[AnimPreviewSkeletonData.BoneCount];
        private readonly Transform[] _stickJoints = new Transform[AnimPreviewBoneSet.JointCount];
        private Transform? _samplingRoot;
        private Animator? _animator;
        private RuntimeAnimatorController? _controller;
        private Vector3 _worldAnchor = Vector3.zero;
        private int _sex = 1;
        private bool _built;

        /// <summary>The GameObject clips are sampled onto (= the animator object; clip paths start at cf_J_Root).</summary>
        public Transform? Root => _samplingRoot;
        public int Sex => _sex;

        public void SetWorldAnchor(Vector3 anchor)
        {
            _worldAnchor = anchor;
            if (_samplingRoot != null)
            {
                _samplingRoot.position = _worldAnchor;
                _samplingRoot.rotation = Quaternion.identity;
            }
        }

        public void EnsureBuilt(int sex)
        {
            if (_built && _sex == sex)
                return;

            Dispose();
            _sex = sex;
            BuildHierarchy();
            SetWorldAnchor(_worldAnchor);
        }

        public void Dispose()
        {
            if (_samplingRoot != null)
                UnityEngine.Object.Destroy(_samplingRoot.gameObject);

            _samplingRoot = null;
            _animator = null;
            for (int i = 0; i < _bones.Length; i++)
                _bones[i] = null!;
            for (int i = 0; i < _stickJoints.Length; i++)
                _stickJoints[i] = null!;
            _built = false;
        }

        /// <summary>Reset every bone to its baked rest TRS (clears the previous sampled frame).</summary>
        public void ApplyRestPose()
        {
            if (!_built)
                return;

            Vector3[] pos = AnimPreviewSkeletonData.LocalPositions(_sex);
            Quaternion[] rot = AnimPreviewSkeletonData.LocalRotations(_sex);
            Vector3[] scl = AnimPreviewSkeletonData.LocalScales(_sex);

            for (int i = 0; i < _bones.Length; i++)
            {
                Transform t = _bones[i];
                if (t == null)
                    continue;
                t.localPosition = pos[i];
                t.localRotation = rot[i];
                t.localScale = scl[i];
            }

            if (_samplingRoot != null)
            {
                _samplingRoot.position = _worldAnchor;
                _samplingRoot.rotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Apply the clip at the given time via SampleAnimation. HS2/KKS bind a generic clip onto the
        /// bare Animator directly. KK (Unity 5.6) additionally requires the Animator to carry a
        /// Controller that references the clip — otherwise SampleAnimation writes nothing — so the
        /// clip's source controller is assigned (kept disabled so it doesn't auto-drive the rig).
        /// The controller is sticky: pass it once (on clip change); later scrub calls may omit it.
        /// </summary>
        public void SampleClip(AnimationClip? clip, float timeSeconds, RuntimeAnimatorController? controller = null)
        {
            if (!_built || _samplingRoot == null)
                return;

            if (controller != null)
                _controller = controller;

            ApplyRestPose();
            if (clip == null)
                return;

#if KK || AI
            // Unity 5.6 (KK) and Unity 2018.2 (AI): AnimationClip.SampleAnimation only writes when the
            // Animator carries a Controller that references the clip (on a bare Animator it logs
            // "Non-Legacy animations cannot be sampled ... without an Animator" and writes nothing).
            // Assign the clip's source controller, kept disabled so its own state machine doesn't
            // overwrite our explicit sample during the animation update. HS2/KKS (2018.4/2019.4) sample
            // a bare Animator fine. See [[animbrowser-clip-sampling-runtime]].
            if (_animator != null && _controller != null)
            {
                if (_animator.runtimeAnimatorController != _controller)
                    _animator.runtimeAnimatorController = _controller;
                _animator.enabled = false;
            }
#endif
            try
            {
                clip.SampleAnimation(_samplingRoot.gameObject, WrapClipTime(clip, timeSeconds));
            }
            catch
            {
                ApplyRestPose();
            }

            // Sampling may translate the rig via root motion curves; keep the sampling root pinned
            // so the figure stays at the stage anchor (camera reframes on bounds anyway).
            _samplingRoot.position = _worldAnchor;
            _samplingRoot.rotation = Quaternion.identity;
        }

        /// <summary>World positions of the 19 stick joints, in stick-joint order.</summary>
        public bool TryReadJoints(Vector3[] buffer, bool[] valid)
        {
            if (!_built || buffer == null || valid == null ||
                buffer.Length < AnimPreviewBoneSet.JointCount || valid.Length < AnimPreviewBoneSet.JointCount)
                return false;

            int found = 0;
            for (int i = 0; i < AnimPreviewBoneSet.JointCount; i++)
            {
                Transform t = _stickJoints[i];
                if (t == null)
                {
                    buffer[i] = _worldAnchor;
                    valid[i] = false;
                    continue;
                }

                buffer[i] = t.position;
                valid[i] = true;
                found++;
            }

            return found >= 6;
        }

        private void BuildHierarchy()
        {
            var rootGo = new GameObject("AnimPreviewSampleRoot") { hideFlags = HideFlags.HideAndDontSave };
            _samplingRoot = rootGo.transform;
            _samplingRoot.position = _worldAnchor;
            _samplingRoot.rotation = Quaternion.identity;

            // AnimationClip.SampleAnimation only writes to the hierarchy when the root GameObject
            // carries an Animator (it provides the binding context for generic Mecanim clips).
            // HS2/KKS need no controller; KK (Unity 5.6) gets the clip's source controller assigned
            // in SampleClip. cullingMode=AlwaysAnimate so an off-screen, renderer-less rig is never
            // culled out of sampling.
            _animator = rootGo.AddComponent<Animator>();
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.enabled = true;

            Vector3[] pos = AnimPreviewSkeletonData.LocalPositions(_sex);
            Quaternion[] rot = AnimPreviewSkeletonData.LocalRotations(_sex);
            Vector3[] scl = AnimPreviewSkeletonData.LocalScales(_sex);

            for (int i = 0; i < AnimPreviewSkeletonData.BoneCount; i++)
            {
                var boneGo = new GameObject(AnimPreviewSkeletonData.BoneNames[i]) { hideFlags = HideFlags.HideAndDontSave };
                Transform t = boneGo.transform;

                int parent = AnimPreviewSkeletonData.BoneParents[i];
                Transform parentT = parent >= 0 && _bones[parent] != null ? _bones[parent] : _samplingRoot;
                t.SetParent(parentT, false);
                t.localPosition = pos[i];
                t.localRotation = rot[i];
                t.localScale = scl[i];
                _bones[i] = t;
            }

            for (int s = 0; s < AnimPreviewBoneSet.JointCount; s++)
            {
                int boneIndex = AnimPreviewSkeletonData.StickJointBone[s];
                _stickJoints[s] = boneIndex >= 0 && boneIndex < _bones.Length ? _bones[boneIndex] : null!;
            }

            _built = true;
            ApplyUniformScale();
        }

        // All figures are normalized to this rest-pose height so male/female (and custom-height)
        // rigs render at the same size; the shared anchor + this uniform scale keep grouped figures
        // comparable. Camera framing handles absolute size, so the exact value is arbitrary.
        private const float UniformHeight = 10f;

        private void ApplyUniformScale()
        {
            if (_samplingRoot == null)
                return;

            _samplingRoot.localScale = Vector3.one;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < _stickJoints.Length; i++)
            {
                Transform t = _stickJoints[i];
                if (t == null)
                    continue;
                float y = t.position.y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float height = maxY - minY;
            if (height > 0.001f)
                _samplingRoot.localScale = Vector3.one * (UniformHeight / height);
        }

        private static float WrapClipTime(AnimationClip clip, float timeSeconds)
        {
            if (clip.length <= 0.01f)
                return 0f;
            if (timeSeconds < 0f)
                timeSeconds = 0f;
            if (timeSeconds > clip.length)
                timeSeconds %= clip.length;
            return timeSeconds;
        }
    }
}
