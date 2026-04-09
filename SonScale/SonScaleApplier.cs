using System.Collections;
using System.Collections.Generic;
using AIChara;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// When Studio Better Penetration drives the shaft (<see cref="SonScaleBpIntegration"/> hooks active + valid BP
    /// targets), girth is a single dan-root <c>(girth,girth,1)</c> multiply so thickness stays uniform down the chain.
    /// Per-segment XY scale would compound parent×child and look conical. Length/overall hooks <c>m_baseDanLength</c>.
    /// Non-BP multi-segment uses localPosition scaling; single-bone uses root <c>(girth,girth,master×length)</c>.
    /// <see cref="SonScaleSettings.Balls"/> applies uniform scale to <c>cm_J_dan_f_top</c> when distinct from the dan root, or multiplies the dan root when they coincide.
    /// Runs late after BP and at end of frame.
    /// </summary>
    [DefaultExecutionOrder(500000)]
    public sealed class SonScaleApplier : MonoBehaviour
    {
        private sealed class MulCache
        {
            public Vector3 LastDanScaleMul = Vector3.one;
            public Vector3 LastChainPosMul = Vector3.one;
            public Vector3 LastOutputScale;
            public bool HasWritten;
            public bool HasVanillaAnchorSample;
            public Vector3 LastVanillaForAnchor;
            /// <summary>Uniform balls multiplier when balls root equals dan root; otherwise 1.</summary>
            public float LastBallsMul = 1f;
        }

        private sealed class BallsOnlyMulCache
        {
            public float LastMul = 1f;
            public Vector3 LastOutputScale;
            public bool HasWritten;
        }

        private sealed class BoneLpCache
        {
            public Vector3 LastOut;
            public bool HasWritten;
            /// <summary>0=X, 1=Y, 2=Z — which local axis carries segment spacing for strip/re-apply.</summary>
            public int LengthAxis = 2;
        }

        private readonly Dictionary<int, MulCache> _mulCache = new Dictionary<int, MulCache>();
        private readonly Dictionary<int, BallsOnlyMulCache> _ballsOnlyMulCache = new Dictionary<int, BallsOnlyMulCache>();
        private readonly Dictionary<int, Vector3> _baseLocalByChaId = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Dictionary<int, BoneLpCache>> _chainLpByChaId = new Dictionary<int, Dictionary<int, BoneLpCache>>();
        private readonly List<Transform> _chainBuffer = new List<Transform>();

        private void OnEnable()
        {
            StartCoroutine(EndOfFrameApplyLoop());
        }

        private IEnumerator EndOfFrameApplyLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                if (SonScaleSettings.Enabled && StudioAPI.StudioLoaded)
                    ApplyToAllSelected();
            }
        }

        private void LateUpdate()
        {
            if (!SonScaleSettings.Enabled)
            {
                _mulCache.Clear();
                _ballsOnlyMulCache.Clear();
                _baseLocalByChaId.Clear();
                _chainLpByChaId.Clear();
                return;
            }

            if (!StudioAPI.StudioLoaded)
                return;

            ApplyToAllSelected();
        }

        private void ApplyToAllSelected()
        {
            IEnumerable<OCIChar>? selected;
            try
            {
                selected = StudioAPI.GetSelectedCharacters();
            }
            catch
            {
                return;
            }

            if (selected == null)
                return;

            float master = SonScaleSettings.Master;
            float len = SonScaleSettings.Length;
            float gir = SonScaleSettings.Girth;
            float ballsMul = SonScaleSettings.Balls;
            var activeIds = new HashSet<int>();

            foreach (OCIChar? oci in selected)
            {
                if (oci == null)
                    continue;

                ChaControl? cha = oci.charInfo;
                if (cha == null)
                    continue;

                Transform? dan = SonBoneResolver.FindDanTransform(cha);
                Transform? ballsTf = SonBoneResolver.FindBallsTransform(cha);
                if (dan == null && ballsTf == null)
                    continue;

                int id = cha.GetInstanceID();
                activeIds.Add(id);

                bool ballsFoldedIntoDan = dan != null && ballsTf != null && ReferenceEquals(ballsTf, dan);

                if (dan != null)
                {
                    if (!_mulCache.TryGetValue(id, out MulCache? cache))
                    {
                        cache = new MulCache();
                        _mulCache[id] = cache;
                    }

                    SonBoneResolver.CollectSonShaftDescendants(dan, _chainBuffer);
                    SortChainByDepth(dan, _chainBuffer);
                    bool useChainForLength = _chainBuffer.Count > 0;
                    bool bpOwnsShaft = useChainForLength
                        && SonScaleBpIntegration.LengthHooksInstalled
                        && SonScaleBpIntegration.IsBpDrivingShaft(cha);

                    Vector3 curScale = dan.localScale;
                    Vector3 vanillaScale;
                    Vector3 prevDanMul = cache.LastDanScaleMul;
                    float prevBalls = cache.LastBallsMul;

                    if (!cache.HasWritten)
                    {
                        vanillaScale = curScale;
                    }
                    else
                    {
                        // Loose match: float noise, BP, or Studio can nudge scale slightly; false "external" writes doubled mul.
                        const float eps = 0.02f;
                        bool stillOursScale =
                            Mathf.Abs(curScale.x - cache.LastOutputScale.x) <= eps &&
                            Mathf.Abs(curScale.y - cache.LastOutputScale.y) <= eps &&
                            Mathf.Abs(curScale.z - cache.LastOutputScale.z) <= eps;

                        if (stillOursScale)
                        {
                            if (ballsFoldedIntoDan)
                            {
                                float bx = prevDanMul.x * prevBalls;
                                float by = prevDanMul.y * prevBalls;
                                float bz = prevDanMul.z * prevBalls;
                                vanillaScale = new Vector3(
                                    bx > 1e-6f ? curScale.x / bx : curScale.x,
                                    by > 1e-6f ? curScale.y / by : curScale.y,
                                    bz > 1e-6f ? curScale.z / bz : curScale.z);
                            }
                            else
                            {
                                vanillaScale = new Vector3(
                                    prevDanMul.x > 1e-6f ? curScale.x / prevDanMul.x : curScale.x,
                                    prevDanMul.y > 1e-6f ? curScale.y / prevDanMul.y : curScale.y,
                                    prevDanMul.z > 1e-6f ? curScale.z / prevDanMul.z : curScale.z);
                            }
                        }
                        else
                        {
                            // BP / IK rewrites the dan root scale (or full pose) each frame; it is not "vanilla + partial game delta".
                            // Treat current scale as the baseline and re-apply our multipliers on top.
                            vanillaScale = curScale;
                        }
                    }

                if (cache.HasVanillaAnchorSample && (vanillaScale - cache.LastVanillaForAnchor).sqrMagnitude > 1e-4f)
                    _baseLocalByChaId.Remove(id);

                cache.LastVanillaForAnchor = vanillaScale;
                cache.HasVanillaAnchorSample = true;

                // Chain: do not scale root Z — fold master into chain length mul with Length slider.
                // BP path: same root XY girth as legacy chain; per-segment girth in Harmony was removed (hierarchy compound).
                Vector3 danScaleMul = useChainForLength
                    ? new Vector3(gir, gir, 1f)
                    : new Vector3(gir, gir, master * len);

                float ballsOnDan = ballsFoldedIntoDan ? ballsMul : 1f;
                Vector3 newOutScale = new Vector3(
                    vanillaScale.x * danScaleMul.x * ballsOnDan,
                    vanillaScale.y * danScaleMul.y * ballsOnDan,
                    vanillaScale.z * danScaleMul.z * ballsOnDan);

                Vector3 prevChainMul = cache.HasWritten ? cache.LastChainPosMul : Vector3.one;
                Vector3 chainPosMul = useChainForLength
                    ? new Vector3(gir, gir, len * master)
                    : new Vector3(gir, gir, len);

                Vector3 pRead = dan.localPosition;
                Quaternion r = dan.localRotation;

                // Parent scale first so descendant localPosition edits use the intended hierarchy.
                dan.localScale = newOutScale;

                if (bpOwnsShaft)
                    _chainLpByChaId.Remove(id);

                if (useChainForLength && !bpOwnsShaft)
                {
                    if (!_chainLpByChaId.TryGetValue(id, out Dictionary<int, BoneLpCache>? boneDict))
                    {
                        boneDict = new Dictionary<int, BoneLpCache>();
                        _chainLpByChaId[id] = boneDict;
                    }

                    const float epsP = 0.02f;
                    for (int i = 0; i < _chainBuffer.Count; i++)
                    {
                        Transform bone = _chainBuffer[i];
                        int bid = bone.GetInstanceID();

                        if (!boneDict.TryGetValue(bid, out BoneLpCache? be))
                        {
                            be = new BoneLpCache();
                            boneDict[bid] = be;
                        }

                        Vector3 lp = bone.localPosition;
                        Vector3 vanillaLp;
                        if (!be.HasWritten)
                        {
                            vanillaLp = lp;
                            be.LengthAxis = PickShaftLengthAxis(lp);
                        }
                        else
                        {
                            bool stillBone =
                                Mathf.Abs(lp.x - be.LastOut.x) <= epsP &&
                                Mathf.Abs(lp.y - be.LastOut.y) <= epsP &&
                                Mathf.Abs(lp.z - be.LastOut.z) <= epsP;

                            if (stillBone)
                            {
                                vanillaLp = DivShaftLocal(
                                    lp,
                                    prevChainMul.x,
                                    prevChainMul.z,
                                    be.LengthAxis);
                            }
                            else
                            {
                                // BP overwrote locals; treat as fresh baseline and re-detect length axis for this bone.
                                vanillaLp = lp;
                                be.LengthAxis = PickShaftLengthAxis(lp);
                            }
                        }

                        float lenAlong = chainPosMul.z;
                        float gChain = chainPosMul.x;
                        Vector3 newLp = MulShaftLocal(vanillaLp, gChain, lenAlong, be.LengthAxis);

                        bone.localPosition = newLp;
                        be.LastOut = newLp;
                        be.HasWritten = true;
                    }
                }

                // Single-bone: nudge root when scale changes so the body attachment stays stable. Multi-segment: girth
                // changes root X/Y and chain locals; the anchor formula slides the whole shaft off the body (BP + IK).
                if (!useChainForLength)
                {
                    Vector3 scaleStep = newOutScale - curScale;
                    if (scaleStep.sqrMagnitude > 1e-8f)
                    {
                        Vector3 pBase = GetCachedBaseLocal(id, dan);
                        Vector3 delta = r * Vector3.Scale(pBase, vanillaScale - newOutScale);
                        dan.localPosition = pRead + delta;
                    }
                }

                cache.LastDanScaleMul = danScaleMul;
                cache.LastBallsMul = ballsFoldedIntoDan ? ballsMul : 1f;
                cache.LastChainPosMul = bpOwnsShaft ? Vector3.one : chainPosMul;
                cache.LastOutputScale = newOutScale;
                cache.HasWritten = true;
                }

                if (ballsTf != null && !ballsFoldedIntoDan)
                {
                    if (!_ballsOnlyMulCache.TryGetValue(id, out BallsOnlyMulCache? bCache))
                    {
                        bCache = new BallsOnlyMulCache();
                        _ballsOnlyMulCache[id] = bCache;
                    }

                    Vector3 curB = ballsTf.localScale;
                    Vector3 vanillaB;
                    if (!bCache.HasWritten)
                    {
                        vanillaB = curB;
                    }
                    else
                    {
                        const float epsB = 0.02f;
                        bool stillBalls =
                            Mathf.Abs(curB.x - bCache.LastOutputScale.x) <= epsB &&
                            Mathf.Abs(curB.y - bCache.LastOutputScale.y) <= epsB &&
                            Mathf.Abs(curB.z - bCache.LastOutputScale.z) <= epsB;

                        if (stillBalls && bCache.LastMul > 1e-6f)
                        {
                            float inv = 1f / bCache.LastMul;
                            vanillaB = new Vector3(curB.x * inv, curB.y * inv, curB.z * inv);
                        }
                        else
                        {
                            vanillaB = curB;
                        }
                    }

                    Vector3 newBallsScale = new Vector3(
                        vanillaB.x * ballsMul,
                        vanillaB.y * ballsMul,
                        vanillaB.z * ballsMul);

                    ballsTf.localScale = newBallsScale;
                    bCache.LastMul = ballsMul;
                    bCache.LastOutputScale = newBallsScale;
                    bCache.HasWritten = true;
                }
            }

            if (_mulCache.Count > activeIds.Count)
            {
                var stale = new List<int>();
                foreach (int key in _mulCache.Keys)
                {
                    if (!activeIds.Contains(key))
                        stale.Add(key);
                }

                for (int i = 0; i < stale.Count; i++)
                {
                    int k = stale[i];
                    _mulCache.Remove(k);
                    _baseLocalByChaId.Remove(k);
                    _chainLpByChaId.Remove(k);
                }
            }

            if (_ballsOnlyMulCache.Count > activeIds.Count)
            {
                var staleB = new List<int>();
                foreach (int key in _ballsOnlyMulCache.Keys)
                {
                    if (!activeIds.Contains(key))
                        staleB.Add(key);
                }

                for (int i = 0; i < staleB.Count; i++)
                    _ballsOnlyMulCache.Remove(staleB[i]);
            }
        }

        private Vector3 GetCachedBaseLocal(int chaInstanceId, Transform dan)
        {
            if (_baseLocalByChaId.TryGetValue(chaInstanceId, out Vector3 cached))
                return cached;

            Vector3 est = SonBoneResolver.EstimateDanBaseLocalPoint(dan);
            _baseLocalByChaId[chaInstanceId] = est;
            return est;
        }

        private static void SortChainByDepth(Transform danRoot, List<Transform> chain)
        {
            if (chain.Count <= 1)
                return;

            chain.Sort((a, b) => GetDepthBelow(danRoot, a).CompareTo(GetDepthBelow(danRoot, b)));
        }

        private static int GetDepthBelow(Transform root, Transform t)
        {
            int d = 0;
            Transform? x = t;
            while (x != null && x != root)
            {
                d++;
                x = x.parent;
            }

            return d;
        }

        /// <summary>Prefer Z (Illusion default), then Y, then X when choosing which local axis is the segment offset.</summary>
        private static int PickShaftLengthAxis(Vector3 lp)
        {
            float ax = Mathf.Abs(lp.x);
            float ay = Mathf.Abs(lp.y);
            float az = Mathf.Abs(lp.z);
            if (ax < 1e-10f && ay < 1e-10f && az < 1e-10f)
                return 2;

            if (az >= ax && az >= ay)
                return 2;
            if (ay >= ax)
                return 1;
            return 0;
        }

        private static Vector3 MulShaftLocal(Vector3 lp, float girthMul, float lengthMul, int lengthAxis)
        {
            Vector3 r = lp;
            switch (lengthAxis)
            {
                case 0:
                    r.x *= lengthMul;
                    r.y *= girthMul;
                    r.z *= girthMul;
                    break;
                case 1:
                    r.x *= girthMul;
                    r.y *= lengthMul;
                    r.z *= girthMul;
                    break;
                default:
                    r.x *= girthMul;
                    r.y *= girthMul;
                    r.z *= lengthMul;
                    break;
            }

            return r;
        }

        private static Vector3 DivShaftLocal(Vector3 lp, float girthMul, float lengthMul, int lengthAxis)
        {
            float ig = girthMul > 1e-8f ? 1f / girthMul : 1f;
            float il = lengthMul > 1e-8f ? 1f / lengthMul : 1f;
            Vector3 r = lp;
            switch (lengthAxis)
            {
                case 0:
                    r.x *= il;
                    r.y *= ig;
                    r.z *= ig;
                    break;
                case 1:
                    r.x *= ig;
                    r.y *= il;
                    r.z *= ig;
                    break;
                default:
                    r.x *= ig;
                    r.y *= ig;
                    r.z *= il;
                    break;
            }

            return r;
        }
    }
}
