using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    internal sealed class AnimPreviewPhaseSequencer
    {
        /// <summary>The Loop phase plays whole loops until at least this many seconds have elapsed.</summary>
        private const float MinLoopSeconds = 3f;

        private AnimDisplayEntry? _entry;
        private readonly List<AnimPhase> _phaseOrder = new List<AnimPhase>();
        private int _phaseIndex;
        private float _phaseElapsed;
        private AnimPhase _activePhase = AnimPhase.None;
        private float _activePhaseClipLength = 2f;

        public AnimPhase ActivePhase => _activePhase;

        public void SetTarget(AnimDisplayEntry? entry)
        {
            _entry = entry;
            _phaseOrder.Clear();
            _phaseIndex = 0;
            _phaseElapsed = 0f;
            _activePhase = AnimPhase.None;
            _activePhaseClipLength = 2f;

            if (entry == null)
                return;

            if (entry.IsGroup && entry.Group != null)
            {
                AnimDisplayGroup group = entry.Group;
                if (group.HasPhases && group.Phases.Count > 0)
                {
                    for (int i = 0; i < group.Phases.Count; i++)
                        _phaseOrder.Add(group.Phases[i]);
                    _activePhase = _phaseOrder[0];
                }
                else
                {
                    _activePhase = group.MainPhase != AnimPhase.None ? group.MainPhase : AnimPhase.Loop;
                }
            }
            else
            {
                _activePhase = AnimPhase.Loop;
            }
        }

        public void Update(float deltaTime, out bool phaseChanged)
        {
            phaseChanged = false;
            if (_entry == null)
                return;

            _phaseElapsed += deltaTime;
            if (_phaseOrder.Count == 0)
                return;

            float duration = GetPhaseDuration(_activePhase);
            if (_phaseElapsed < duration)
                return;

            _phaseElapsed = 0f;
            int next = _phaseIndex + 1;
            if (next >= _phaseOrder.Count)
                next = 0;
            _phaseIndex = next;
            _activePhase = _phaseOrder[_phaseIndex];
            phaseChanged = true;
        }

        /// <summary>Set by the stage when a phase's clips are applied — the longest participant clip.</summary>
        public void SetActivePhaseClipLength(float seconds)
        {
            if (seconds > 0.05f)
                _activePhaseClipLength = seconds;
        }

        private float GetPhaseDuration(AnimPhase phase)
        {
            float length = _activePhaseClipLength > 0.1f ? _activePhaseClipLength : 0.1f;
            if (phase != AnimPhase.Loop)
                return length;

            // Loop plays whole loops until >= MinLoopSeconds (e.g. a 2 s loop runs twice = 4 s).
            int repeats = (int)System.Math.Ceiling(MinLoopSeconds / length);
            if (repeats < 1)
                repeats = 1;
            return repeats * length;
        }
    }
}
