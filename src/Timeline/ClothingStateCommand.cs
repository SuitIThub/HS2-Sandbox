using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets a clothing part's state (On / Half / Off) by pressing the corresponding Studio UI button.
    /// Uses cached parent GameObjects fetched when the timeline window is first opened.
    /// </summary>
    public class ClothingStateCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        private static readonly string[] StateLabels = { "On", "Half", "Off" };

        public override string TypeId => "clothing_state";
        public override string GetDisplayLabel() => $"Clothing";

        private string _partKey = ClothingStateCache.PartKeyAllClothing;
        private int _stateIndex; // 0=On, 1=Half, 2=Off

        private string GetStateLabel()
        {
            if (_stateIndex >= 0 && _stateIndex < StateLabels.Length)
                return StateLabels[_stateIndex];
            return "?";
        }

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();

            IReadOnlyList<string>? partNames = ClothingStateCache.GetPartNames();
            if (partNames == null) partNames = Array.Empty<string>();
            int partCount = partNames.Count;
            int currentPartIndex = -1;
            if (partCount > 0)
            {
                for (int i = 0; i < partCount; i++)
                {
                    if (string.Equals(partNames[i], _partKey, StringComparison.OrdinalIgnoreCase))
                    {
                        currentPartIndex = i;
                        break;
                    }
                }
                if (currentPartIndex < 0)
                {
                    _partKey = partNames[0];
                    currentPartIndex = 0;
                }
            }

            GUILayout.Label("Part", GUILayout.Width(28));
            if (partCount > 0)
            {
                if (GUILayout.Button("<", GUILayout.Width(20)))
                {
                    currentPartIndex = (currentPartIndex - 1 + partCount) % partCount;
                    _partKey = partNames[currentPartIndex];
                }
                GUILayout.Label(_partKey, GUILayout.MinWidth(70), GUILayout.ExpandWidth(true));
                if (GUILayout.Button(">", GUILayout.Width(20)))
                {
                    currentPartIndex = (currentPartIndex + 1) % partCount;
                    _partKey = partNames[currentPartIndex];
                }
            }
            else
            {
                _partKey = GUILayout.TextField(_partKey ?? ClothingStateCache.PartKeyAllClothing, GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            }

            int stateCount = ClothingStateCache.GetStateCount(_partKey);
            GUILayout.Space(8);
            GUILayout.Label("State", GUILayout.Width(36));
            if (stateCount == 3)
            {
                _stateIndex = GUILayout.SelectionGrid(_stateIndex, StateLabels, 3, GUILayout.MinWidth(120));
                _stateIndex = Math.Max(0, Math.Min(2, _stateIndex));
            }
            else
            {
                int twoState = Math.Max(0, Math.Min(1, _stateIndex));
                twoState = GUILayout.SelectionGrid(twoState, new[] { "On", "Off" }, 2, GUILayout.MinWidth(80));
                _stateIndex = twoState;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ClothingStateCache.IsFetched)
            {
                SandboxServices.Log.LogWarning("Clothing state cache not ready. Open the timeline window first so the UI is loaded.");
                onComplete();
                return;
            }
            int stateCount = ClothingStateCache.GetStateCount(_partKey);
            int indexToPress = _stateIndex;
            if (stateCount == 2 && _stateIndex == 2)
                indexToPress = 1; // Off in 3-state terms = index 1 for 2-state parts
            bool ok = ClothingStateCache.PressState(_partKey, indexToPress);
            if (!ok)
                SandboxServices.Log.LogWarning($"Clothing state: could not press part '{_partKey}' state {_stateIndex}.");
            onComplete();
        }

        public override string SerializePayload()
        {
            int stateCount = ClothingStateCache.GetStateCount(_partKey);
            string stateLabel;
            if (stateCount == 2 && _stateIndex == 1)
                stateLabel = "Off"; // 2-state: index 1 = Off
            else if (_stateIndex >= 0 && _stateIndex < StateLabels.Length)
                stateLabel = StateLabels[_stateIndex];
            else
                stateLabel = StateLabels[0];
            return (_partKey ?? "").Replace(PayloadSeparator.ToString(), "") + PayloadSeparator + stateLabel;
        }

        public override void DeserializePayload(string payload)
        {
            _partKey = ClothingStateCache.PartKeyAllClothing;
            _stateIndex = 0;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1 && !string.IsNullOrEmpty(p[0]))
                _partKey = p[0];
            if (p.Length >= 2 && !string.IsNullOrEmpty(p[1]))
            {
                string label = p[1].Trim();
                for (int i = 0; i < StateLabels.Length; i++)
                {
                    if (string.Equals(StateLabels[i], label, StringComparison.OrdinalIgnoreCase))
                    {
                        _stateIndex = i;
                        break;
                    }
                }
                // Backward compatibility: if second part is a digit, treat as old index payload
                if (p[1].Length == 1 && int.TryParse(p[1], out int idx))
                    _stateIndex = Math.Max(0, Math.Min(2, idx));
            }
        }
    }
}
