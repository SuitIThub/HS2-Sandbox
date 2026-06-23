using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal enum AnimPreviewStageState
    {
        Idle,
        Loading,
        Ready,
        Unavailable,
    }

    /// <summary>Preview camera framing modes (persisted as int in options).</summary>
    internal enum AnimPreviewCameraMode
    {
        FrontalFull = 0,   // 0° — straight-on (default)
        Front45 = 1,       // 45° front-side
        Side90 = 2,        // 90° side
        Rotate = 3,        // continuous orbit
        RotateDwell = 4,   // continuous orbit, pausing 2s at 0°/45°/90°
    }

    /// <summary>Off-screen stick-figure preview (embedded skeleton + clip sampling).</summary>
    internal sealed class AnimPreviewStage : MonoBehaviour
    {
        public const int RenderSize = 192;
        private const int MaxFigures = 8;

        /// <summary>Figure colour by index (wraps); used by the grid to tint role buttons on hover.</summary>
        public static Color FigureColorAt(int index) => FigureColors[((index % FigureColors.Length) + FigureColors.Length) % FigureColors.Length];

        // Vivid, well-separated hues so multiple figures are easy to tell apart on the dark backdrop.
        private static readonly Color[] FigureColors =
        {
            new Color(0.40f, 0.75f, 1.00f, 1f), // blue
            new Color(1.00f, 0.42f, 0.45f, 1f), // red
            new Color(0.45f, 1.00f, 0.52f, 1f), // green
            new Color(1.00f, 0.84f, 0.25f, 1f), // gold
            new Color(0.80f, 0.52f, 1.00f, 1f), // purple
            new Color(0.25f, 1.00f, 0.90f, 1f), // cyan
            new Color(1.00f, 0.60f, 0.25f, 1f), // orange
            new Color(1.00f, 0.95f, 0.95f, 1f), // white
        };

        private Camera? _camera;
        private RenderTexture? _renderTexture;
        private AnimPreviewGlCameraHook? _glHook;
        private readonly AnimPreviewPhaseSequencer _sequencer = new AnimPreviewPhaseSequencer();
        private readonly List<AnimPreviewFigureSlot> _figureSlots = new List<AnimPreviewFigureSlot>();
        private readonly AnimPreviewRig[] _activeRigs = new AnimPreviewRig[MaxFigures];
        private readonly AnimationClip?[] _activeClips = new AnimationClip[MaxFigures];
        private readonly AnimPreviewFigureDraw[] _figureDraws = new AnimPreviewFigureDraw[MaxFigures];
        private readonly Vector3[][] _jointBuffers = new Vector3[MaxFigures][];
        private readonly bool[][] _figureValid = new bool[MaxFigures][];

        // Camera orbit (yaw) settings for the rotating modes.
        public const float CamRotateSpeedDefault = 30f;
        public const float CamRotateSpeedMin = 10f;
        public const float CamRotateSpeedMax = 240f;
        private const float CamDwellSeconds = 2f;
        private static readonly float[] CamDwellAngles = { 0f, 45f, 90f };

        public const float CamPitchDefault = 10f;
        public const float CamPitchMin = -90f;
        public const float CamPitchMax = 90f;

        // Iteration groups (plain numbered slots 1,2,3…): figures are independent single-character
        // clips that all root at the same spot, so they're spread out along X and the camera slowly
        // pans between the outermost two while it orbits, framing one figure at a time.
        public const float IterationPanSecondsDefault = 4f; // one-way sweep duration (ping-pongs)
        public const float IterationPanSecondsMin = 1f;     // faster pan
        public const float IterationPanSecondsMax = 15f;    // slower pan
        private const float IterationFigureGap = 0.3f;      // clear space between adjacent figures (m)
        private const float IterationFigureMinSpacing = 0.8f;

        private int _cameraMode;
        private int _iterationCameraMode;
        private float _cameraRotateSpeed = CamRotateSpeedDefault;
        private float _cameraPitch = CamPitchDefault;
        private float _iterationPanSeconds = IterationPanSecondsDefault;

        /// <summary>Active camera mode (see <see cref="AnimPreviewCameraMode"/>); set from options.</summary>
        public int CameraMode
        {
            get => _cameraMode;
            set => _cameraMode = value;
        }

        /// <summary>Camera mode used for iteration-group (numbered-slot) previews, where the camera also
        /// pans between figures. Kept separate from <see cref="CameraMode"/> so the orbit behaviour for
        /// the pan can be chosen independently. Set from options.</summary>
        public int IterationCameraMode
        {
            get => _iterationCameraMode;
            set => _iterationCameraMode = value;
        }

        /// <summary>Orbit speed (degrees/second) for the rotating camera modes; set from options.</summary>
        public float CameraRotateSpeed
        {
            get => _cameraRotateSpeed;
            set => _cameraRotateSpeed = Mathf.Clamp(value, CamRotateSpeedMin, CamRotateSpeedMax);
        }

        /// <summary>Camera pitch (degrees): 0 = level, +90 = straight down (top view), -90 = straight up.</summary>
        public float CameraPitch
        {
            get => _cameraPitch;
            set => _cameraPitch = Mathf.Clamp(value, CamPitchMin, CamPitchMax);
        }

        /// <summary>Seconds the camera takes to sweep from the first to the last figure in iteration-group
        /// previews (one-way; it ping-pongs). Higher = slower pan. Set from options.</summary>
        public float IterationPanSeconds
        {
            get => _iterationPanSeconds;
            set => _iterationPanSeconds = Mathf.Clamp(value, IterationPanSecondsMin, IterationPanSecondsMax);
        }

        private AnimDisplayEntry? _targetEntry;
        private AnimPreviewStageState _state = AnimPreviewStageState.Idle;
        private string _statusMessage = string.Empty;
        private bool _active;
        private Coroutine? _loadCoroutine;
        private int _activeFigureCount;

        /// <summary>True when the target is an iteration group (numbered slots): figures get spread
        /// along X and the camera pans between the outermost ones (see <see cref="FrameCameraOnFiguresSpread"/>).</summary>
        private bool _spreadFigures;

        public Texture? OutputTexture => _renderTexture;
        public AnimPreviewStageState State => _state;
        public string StatusMessage => _statusMessage;
        public bool IsActive => _active;

        private void Awake()
        {
            for (int i = 0; i < MaxFigures; i++)
            {
                _jointBuffers[i] = new Vector3[AnimPreviewBoneSet.JointCount];
                _figureValid[i] = new bool[AnimPreviewBoneSet.JointCount];
            }

            var camGo = new GameObject("AnimPreviewCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = AnimPreviewRigPool.OffScreenPosition + new Vector3(0f, 1f, -2.5f);
            camGo.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            _camera.cullingMask = 0;
            _camera.depth = 100f;
            _camera.orthographic = true;
            _camera.fieldOfView = 35f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.enabled = false;

            _renderTexture = new RenderTexture(RenderSize, RenderSize, 16, RenderTextureFormat.ARGB32);
            _renderTexture.name = "AnimPreviewRT";
            _camera.targetTexture = _renderTexture;

            _glHook = camGo.AddComponent<AnimPreviewGlCameraHook>();
            _glHook.Initialize(this);
        }

        private void OnDestroy()
        {
            ClearTarget();
            AnimPreviewRigPool.DisposeOwned();
            AnimClipLoader.ClearCache();
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        public void SetTarget(AnimDisplayEntry? entry)
        {
            if (entry == null)
            {
                ClearTarget();
                return;
            }

            if (_targetEntry != null && SameTarget(_targetEntry, entry) && _state == AnimPreviewStageState.Ready)
                return;

            _targetEntry = entry;
            _sequencer.SetTarget(entry);
            _active = true;
            _state = AnimPreviewStageState.Loading;
            _statusMessage = "Loading…";

            if (_loadCoroutine != null)
            {
                StopCoroutine(_loadCoroutine);
                _loadCoroutine = null;
            }

            _loadCoroutine = StartCoroutine(BeginTargetCoroutine(entry));
        }

        public void ClearTarget()
        {
            _active = false;
            _targetEntry = null;
            _activeFigureCount = 0;
            _spreadFigures = false;
            _state = AnimPreviewStageState.Idle;
            _statusMessage = string.Empty;

            if (_loadCoroutine != null)
            {
                StopCoroutine(_loadCoroutine);
                _loadCoroutine = null;
            }

            if (_camera != null)
                _camera.enabled = false;

            for (int i = 0; i < _activeClips.Length; i++)
                _activeClips[i] = null;
        }

        private void Update()
        {
            if (!_active || _targetEntry == null || _state != AnimPreviewStageState.Ready)
                return;

            bool phaseChanged;
            _sequencer.Update(Time.unscaledDeltaTime, out phaseChanged);
            if (phaseChanged)
                ApplyCurrentPhase();

            for (int i = 0; i < _activeFigureCount; i++)
            {
                AnimPreviewRig? rig = _activeRigs[i];
                if (rig != null)
                    rig.AdvanceSample(Time.unscaledDeltaTime, 1f);
            }

            FrameCamera();
            RenderFrame();
        }

        private IEnumerator BeginTargetCoroutine(AnimDisplayEntry entry)
        {
            yield return null;

            ResolveFigureSlots(entry, _sequencer.ActivePhase);
            if (_figureSlots.Count == 0)
            {
                _state = AnimPreviewStageState.Unavailable;
                _statusMessage = "No preview slots";
                yield break;
            }

            int figureCount = Mathf.Min(_figureSlots.Count, MaxFigures);
            yield return AnimPreviewRigPool.EnsureFigureCountCoroutine(figureCount, _figureSlots, _activeRigs);

            _activeFigureCount = figureCount;
            _spreadFigures = entry.IsGroup && entry.Group != null &&
                             entry.Group.HasSlotIndexButtons && figureCount >= 2;
            bool anyReady = false;

            for (int i = 0; i < _activeFigureCount; i++)
            {
                AnimPreviewRig rig = _activeRigs[i];
                rig.LineColor = FigureColors[i % FigureColors.Length];
                rig.EnsureSkeleton(_figureSlots[i].PreferredSex);
                _activeClips[i] = null;
                if (rig.TrySampleJoints(out _, out _))
                    anyReady = true;
            }

            if (anyReady)
            {
                _state = AnimPreviewStageState.Ready;
                if (_camera != null)
                {
                    FrameCamera();
                    _camera.enabled = true;
                }
                RenderFrame();
            }

            if (!anyReady)
            {
                _state = AnimPreviewStageState.Unavailable;
                if (string.IsNullOrEmpty(_statusMessage))
                    _statusMessage = "Skeleton unavailable";
                yield break;
            }

            // Preload every clip this entry can show (all phases × participants) so phase rotation
            // can swap to already-cached clips each change instead of loading async mid-cycle.
            yield return PreloadClipsCoroutine(entry);

            // Apply the initial phase's clips to each figure (figures stay in T-pose if none load).
            ApplyPhaseClips(_sequencer.ActivePhase);

            // Iteration groups: now that the figures are posed, spread them out along X so they sit
            // side by side instead of overlapping at a shared root.
            if (_spreadFigures)
                LayoutSpreadFigures();

            _statusMessage = string.Empty;
            FrameCamera();
            RenderFrame();
            _loadCoroutine = null;
        }

        /// <summary>Loads (and caches) every clip the entry can display across all phases/participants.</summary>
        private IEnumerator PreloadClipsCoroutine(AnimDisplayEntry entry)
        {
            if (entry.IsGroup && entry.Group != null)
            {
                List<AnimGroupSlot> slots = entry.Group.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    AnimGroupSlot slot = slots[i];
                    AnimGridItem item = slot.Item;
                    if (item == null || AnimClipLoader.TryGetCached(item, out _, out _))
                        continue;
                    yield return AnimClipLoader.LoadClipCoroutine(item, slot.Gender, slot.GenderOrdinal,
                        (clip, humanoid, error) => { });
                }
            }
            else if (entry.Single != null && !AnimClipLoader.TryGetCached(entry.Single, out _, out _))
            {
                yield return AnimClipLoader.LoadClipCoroutine(entry.Single, AnimGender.Unknown, 0, (clip, humanoid, error) => { });
            }
        }

        private void ApplyCurrentPhase() => ApplyPhaseClips(_sequencer.ActivePhase);

        /// <summary>
        /// Binds each figure's clip for the given phase (from cache) and tells the sequencer how long
        /// the phase runs (the longest participant clip — Loop repeats that, In/Out play it once).
        /// </summary>
        private void ApplyPhaseClips(AnimPhase phase)
        {
            if (_targetEntry == null)
                return;

            ResolveFigureSlots(_targetEntry, phase);
            float maxLength = 0f;
            for (int i = 0; i < _activeFigureCount && i < _figureSlots.Count; i++)
            {
                AnimPreviewRig? rig = _activeRigs[i];
                AnimGridItem? item = _figureSlots[i].Item;
                if (rig == null || item == null)
                    continue;

                if (AnimClipLoader.TryGetCached(item, out AnimationClip? clip, out _) && clip != null)
                {
                    _activeClips[i] = clip;
                    RuntimeAnimatorController? controller = null;
#if KK || AI
                    // KK (Unity 5.6) and AI (Unity 2018.2): SampleAnimation needs the rig's Animator
                    // to carry a controller that references the clip, else it logs "Non-Legacy
                    // animations cannot be sampled ... without an Animator" and writes nothing
                    // (→ T-pose). HS2/KKS (2018.4/2019.4) leave this null and sample on a bare Animator.
                    AnimClipLoader.TryGetCachedController(item, out controller);
#endif
                    rig.ApplyAnimation(item, clip, controller, 0f);
                    if (clip.length > maxLength)
                        maxLength = clip.length;
                }
            }

            if (maxLength > 0f)
                _sequencer.SetActivePhaseClipLength(maxLength);
        }

        private void ResolveFigureSlots(AnimDisplayEntry entry, AnimPhase phase)
        {
            if (entry.IsGroup && entry.Group != null)
                AnimPreviewSlotResolver.ResolveGroup(entry.Group, phase, _figureSlots);
            else if (entry.Single != null)
                AnimPreviewSlotResolver.ResolveSingle(entry.Single, _figureSlots);
            else
                _figureSlots.Clear();
        }

        internal void DrawGlStickFigures(Camera camera)
        {
            if (!_active || _state != AnimPreviewStageState.Ready)
                return;

            int count = 0;
            for (int i = 0; i < _activeFigureCount; i++)
            {
                AnimPreviewRig? rig = _activeRigs[i];
                if (rig == null || !rig.TrySampleJoints(out Vector3[] joints, out bool[] valid))
                    continue;

                System.Array.Copy(joints, _jointBuffers[i], AnimPreviewBoneSet.JointCount);
                System.Array.Copy(valid, _figureValid[i], AnimPreviewBoneSet.JointCount);
                _figureDraws[count] = new AnimPreviewFigureDraw
                {
                    Joints = _jointBuffers[i],
                    Valid = _figureValid[i],
                    JointCount = AnimPreviewBoneSet.JointCount,
                    Color = rig.LineColor,
                };
                count++;
            }

            AnimPreviewGlRenderer.DrawStickFigures(camera, _figureDraws, count);
        }

        /// <summary>Camera yaw (degrees) for the given mode — constant angles, or time-driven orbit.</summary>
        private float ComputeCameraYaw(int mode)
        {
            switch ((AnimPreviewCameraMode)mode)
            {
                case AnimPreviewCameraMode.Front45:
                    return 45f;
                case AnimPreviewCameraMode.Side90:
                    return 90f;
                case AnimPreviewCameraMode.Rotate:
                    return Mathf.Repeat(Time.unscaledTime * _cameraRotateSpeed, 360f);
                case AnimPreviewCameraMode.RotateDwell:
                    return ComputeDwellYaw(Time.unscaledTime);
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Continuous orbit that pauses <see cref="CamDwellSeconds"/> at each of <see cref="CamDwellAngles"/>
        /// (0°/45°/90°), then sweeps the rest of the circle back to 0° before repeating.
        /// </summary>
        private float ComputeDwellYaw(float time)
        {
            float speed = _cameraRotateSpeed;
            int n = CamDwellAngles.Length;

            // One cycle: for each dwell angle, hold, then rotate to the next dwell angle (the last
            // segment sweeps from the final angle all the way round to 360° == 0°).
            float cycle = 0f;
            for (int i = 0; i < n; i++)
            {
                cycle += CamDwellSeconds;
                float from = CamDwellAngles[i];
                float to = i + 1 < n ? CamDwellAngles[i + 1] : 360f;
                cycle += (to - from) / speed;
            }

            float t = Mathf.Repeat(time, cycle);
            for (int i = 0; i < n; i++)
            {
                if (t < CamDwellSeconds)
                    return CamDwellAngles[i];
                t -= CamDwellSeconds;

                float from = CamDwellAngles[i];
                float to = i + 1 < n ? CamDwellAngles[i + 1] : 360f;
                float rotTime = (to - from) / speed;
                if (t < rotTime)
                    return Mathf.Lerp(from, to, t / rotTime);
                t -= rotTime;
            }

            return CamDwellAngles[0];
        }

        private void FrameCamera()
        {
            if (_spreadFigures && _activeFigureCount >= 2)
                FrameCameraOnFiguresSpread();
            else
                FrameCameraOnFigures();
        }

        /// <summary>Spreads iteration-group figures evenly along X so they no longer overlap. Spacing is
        /// derived from the widest single figure (measured from its own root) plus a fixed gap, and the
        /// row is centred on the shared stage anchor. Called once after the clips are first applied.</summary>
        private void LayoutSpreadFigures()
        {
            if (_activeFigureCount < 2)
                return;

            float maxHalfX = 0.25f;
            for (int i = 0; i < _activeFigureCount; i++)
            {
                AnimPreviewRig? rig = _activeRigs[i];
                if (rig == null || !rig.TrySampleJoints(out Vector3[] joints, out bool[] valid))
                    continue;
                float rootX = rig.StageAnchor.x;
                for (int j = 0; j < AnimPreviewBoneSet.JointCount; j++)
                {
                    if (!valid[j] || !IsFinite(joints[j]))
                        continue;
                    maxHalfX = Mathf.Max(maxHalfX, Mathf.Abs(joints[j].x - rootX));
                }
            }

            float spacing = Mathf.Max(2f * maxHalfX + IterationFigureGap, IterationFigureMinSpacing);
            Vector3 baseAnchor = AnimPreviewRigPool.OffScreenPosition + new Vector3(0f, 1f, 0f);
            float mid = (_activeFigureCount - 1) * 0.5f;
            for (int i = 0; i < _activeFigureCount; i++)
            {
                AnimPreviewRig? rig = _activeRigs[i];
                if (rig == null)
                    continue;
                // Mirror the X order (mid - i instead of i - mid): the camera is yawed a flat 180° so it
                // faces the figures' front, which flips world-X on screen. Placing figure 0 at +X puts the
                // first animation on the viewer's LEFT, so the on-screen order matches the slot order 1,2,3.
                rig.StageAnchor = baseAnchor + new Vector3((mid - i) * spacing, 0f, 0f);
                // Re-pin the root at the new anchor so subsequent samples place the figure there.
                rig.TrySampleJoints(out _, out _);
            }
        }

        /// <summary>
        /// Framing for iteration groups: the orthographic size fits the single largest figure (measured
        /// per-character from its own root, NOT the combined bounds of all figures), and the camera
        /// centre ping-pongs between the outermost figures' roots so it slowly wanders across the row
        /// while the orbit yaw keeps rotating.
        /// </summary>
        private void FrameCameraOnFiguresSpread()
        {
            if (_camera == null || _activeFigureCount < 2)
                return;

            float yaw = ComputeCameraYaw(_iterationCameraMode) + 180f;
            Quaternion rot = Quaternion.Euler(_cameraPitch, yaw, 0f);
            Vector3 camRight = rot * Vector3.right;
            Vector3 camUp = rot * Vector3.up;

            // Per-figure metrics: each figure's own joint-bounds centre (so the camera frames the body
            // mass, not the ground-level root) and its own half-extents. Zoom uses the largest SINGLE
            // figure — never the combined span of all figures — so a row of N iterations frames like one.
            float halfWidth = 0.15f;
            float halfHeight = 0.25f;
            Vector3 firstCenter = Vector3.zero;
            Vector3 lastCenter = Vector3.zero;
            bool haveFirst = false;
            bool haveLast = false;

            for (int i = 0; i < _activeFigureCount; i++)
            {
                AnimPreviewRig? rig = _activeRigs[i];
                if (rig == null || !rig.TrySampleJoints(out Vector3[] joints, out bool[] valid))
                    continue;

                bool haveBounds = false;
                Vector3 fMin = Vector3.zero;
                Vector3 fMax = Vector3.zero;
                for (int j = 0; j < AnimPreviewBoneSet.JointCount; j++)
                {
                    if (!valid[j] || !IsFinite(joints[j]))
                        continue;
                    if (!haveBounds) { fMin = joints[j]; fMax = joints[j]; haveBounds = true; }
                    else { fMin = Vector3.Min(fMin, joints[j]); fMax = Vector3.Max(fMax, joints[j]); }
                }
                if (!haveBounds)
                    continue;

                Vector3 figureCenter = (fMin + fMax) * 0.5f;
                for (int j = 0; j < AnimPreviewBoneSet.JointCount; j++)
                {
                    if (!valid[j] || !IsFinite(joints[j]))
                        continue;
                    Vector3 off = joints[j] - figureCenter;
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(off, camRight)));
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(off, camUp)));
                }

                if (i == 0) { firstCenter = figureCenter; haveFirst = true; }
                if (i == _activeFigureCount - 1) { lastCenter = figureCenter; haveLast = true; }
            }

            // Pan between the outermost figures' centres (figure 2 of 1-2-3 lies in between, so it's
            // naturally covered). Smoothstep eases the dwell at each end.
            if (!haveFirst)
                firstCenter = _activeRigs[0]?.StageAnchor ?? AnimPreviewRigPool.OffScreenPosition + Vector3.up;
            if (!haveLast)
                lastCenter = firstCenter;
            float t = Mathf.PingPong(Time.unscaledTime / _iterationPanSeconds, 1f);
            t = t * t * (3f - 2f * t);
            Vector3 center = Vector3.Lerp(firstCenter, lastCenter, t);
            if (!IsFinite(center))
                center = firstCenter;

            const float camDistance = 50f;
            Transform camT = _camera.transform;
            camT.position = center + rot * new Vector3(0f, 0f, -camDistance);
            camT.rotation = rot;
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = camDistance * 2f;
            float aspect = _renderTexture != null && _renderTexture.height > 0
                ? (float)_renderTexture.width / _renderTexture.height
                : 1f;
            float orthoSize = Mathf.Max(halfHeight, halfWidth / aspect) * 1.2f;
            _camera.orthographicSize = (float.IsNaN(orthoSize) || float.IsInfinity(orthoSize) || orthoSize <= 0f)
                ? 1f
                : Mathf.Clamp(orthoSize, 0.05f, 1000f);
        }

        private void FrameCameraOnFigures()
        {
            if (_camera == null || _activeFigureCount <= 0)
                return;

            bool haveBounds = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            for (int i = 0; i < _activeFigureCount; i++)
            {
                AnimPreviewRig? rig = _activeRigs[i];
                if (rig == null || !rig.TrySampleJoints(out Vector3[] joints, out bool[] valid))
                    continue;

                for (int j = 0; j < AnimPreviewBoneSet.JointCount; j++)
                {
                    if (!valid[j] || !IsFinite(joints[j]))
                        continue;
                    if (!haveBounds)
                    {
                        min = joints[j];
                        max = joints[j];
                        haveBounds = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, joints[j]);
                        max = Vector3.Max(max, joints[j]);
                    }
                }
            }

            Vector3 center = haveBounds
                ? (min + max) * 0.5f
                : (_activeRigs[0]?.StageAnchor ?? AnimPreviewRigPool.OffScreenPosition + Vector3.up);
            if (!IsFinite(center))
                center = AnimPreviewRigPool.OffScreenPosition + Vector3.up;

            // Orthographic, so the standoff distance doesn't affect framing — keep it large with a
            // deep near/far slab so limbs swung toward the camera never cross the near plane (the
            // "figure clips through the background" artefact). Yaw orbits the camera around the
            // figures' centre for the configurable view angles / rotation modes.
            const float camDistance = 50f;
            // +180° so a yaw of 0° looks at the character's front (the rig faces -Z; without the offset
            // the camera sits behind it). Applied uniformly so every mode/angle is front-relative.
            float yaw = ComputeCameraYaw(_cameraMode) + 180f;
            Quaternion rot = Quaternion.Euler(_cameraPitch, yaw, 0f);
            Vector3 camRight = rot * Vector3.right;
            Vector3 camUp = rot * Vector3.up;

            // Tight framing for THIS view angle: project every joint onto the camera's right/up axes
            // (relative to centre) and take the furthest extents, instead of a yaw-independent box.
            float halfWidth = 0.15f;
            float halfHeight = 0.25f;
            if (haveBounds)
            {
                for (int i = 0; i < _activeFigureCount; i++)
                {
                    AnimPreviewRig? rig = _activeRigs[i];
                    if (rig == null || !rig.TrySampleJoints(out Vector3[] joints, out bool[] valid))
                        continue;
                    for (int j = 0; j < AnimPreviewBoneSet.JointCount; j++)
                    {
                        if (!valid[j] || !IsFinite(joints[j]))
                            continue;
                        Vector3 off = joints[j] - center;
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(off, camRight)));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(off, camUp)));
                    }
                }
            }
            else
            {
                halfWidth = 0.35f;
                halfHeight = 0.55f;
            }

            Transform camT = _camera.transform;
            camT.position = center + rot * new Vector3(0f, 0f, -camDistance);
            camT.rotation = rot;
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = camDistance * 2f;
            float aspect = _renderTexture != null && _renderTexture.height > 0
                ? (float)_renderTexture.width / _renderTexture.height
                : 1f;
            float orthoSize = Mathf.Max(halfHeight, halfWidth / aspect) * 1.15f;
            // Final guard: never feed a non-finite size to the camera (a degenerate projection matrix
            // corrupts GL state and surfaces as IMGUI "Mismatched LayoutGroup" errors elsewhere).
            _camera.orthographicSize = (float.IsNaN(orthoSize) || float.IsInfinity(orthoSize) || orthoSize <= 0f)
                ? 1f
                : Mathf.Clamp(orthoSize, 0.05f, 1000f);
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        private void RenderFrame()
        {
            if (_camera == null || !_camera.enabled)
                return;
            _camera.Render();
        }

        private static bool SameTarget(AnimDisplayEntry a, AnimDisplayEntry b)
        {
            if (a.IsGroup != b.IsGroup)
                return false;
            if (a.IsGroup)
                return a.Group != null && b.Group != null && string.Equals(a.Group.Id, b.Group.Id, System.StringComparison.Ordinal);
            return a.Single != null && b.Single != null &&
                   string.Equals(a.Single.CatalogKey, b.Single.CatalogKey, System.StringComparison.Ordinal);
        }
    }

    internal sealed class AnimPreviewGlCameraHook : MonoBehaviour
    {
        private AnimPreviewStage? _stage;

        public void Initialize(AnimPreviewStage stage)
        {
            _stage = stage;
        }

        private void OnPostRender()
        {
            if (_stage == null)
                return;
            Camera cam = GetComponent<Camera>();
            if (cam == null)
                return;
            _stage.DrawGlStickFigures(cam);
        }
    }
}
