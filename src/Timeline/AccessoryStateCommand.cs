using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets an accessory slot's state (On / Off) by pressing the corresponding Studio UI button.
    /// Uses cached parent GameObjects from StudioScene/.../01_State/Viewport/Content/Slot.
    /// </summary>
    public class AccessoryStateCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        private static readonly string[] StateLabels = { "On", "Off" };

        /// <summary>Display label for the Slot01(Clone) slot that controls all accessories.</summary>
        private const string DisplayLabelAllSlots = "All Slots";

        public override string TypeId => "accessory_state";
        public override string GetDisplayLabel() => "Accessory";

        private string _slotKey = AccessoryStateCache.SlotNameAllSlots;
        private int _stateIndex; // 0=On, 1=Off

        private string GetStateLabel()
        {
            if (_stateIndex >= 0 && _stateIndex < StateLabels.Length)
                return StateLabels[_stateIndex];
            return "?";
        }

        private static List<string> BuildSlotList()
        {
            var real = AccessoryStateCache.GetSlotNames();
            var list = new List<string>();
            if (real != null && real.Count > 0)
            {
                // Put "All Slots" (Slot01(Clone)) first if present
                if (real.Any(s => string.Equals(s, AccessoryStateCache.SlotNameAllSlots, StringComparison.OrdinalIgnoreCase)))
                    list.Add(AccessoryStateCache.SlotNameAllSlots);
                foreach (string name in real)
                {
                    if (!string.Equals(name, AccessoryStateCache.SlotNameAllSlots, StringComparison.OrdinalIgnoreCase))
                        list.Add(name);
                }
            }
            return list;
        }

        private static int IndexOf(List<string> list, string? key)
        {
            if (string.IsNullOrEmpty(key) || list.Count == 0) return 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Slot", GUILayout.Width(24));
            if (GUILayout.Button("<", GUILayout.Width(20)))
            {
                var list = BuildSlotList();
                if (list.Count > 0)
                {
                    int idx = IndexOf(list, _slotKey);
                    idx = (idx - 1 + list.Count) % list.Count;
                    _slotKey = list[idx];
                }
            }
            string slotDisplay = string.Equals(_slotKey, AccessoryStateCache.SlotNameAllSlots, StringComparison.OrdinalIgnoreCase)
                ? DisplayLabelAllSlots
                : (_slotKey ?? DisplayLabelAllSlots);
            GUILayout.Label(slotDisplay, GUILayout.MinWidth(70), GUILayout.ExpandWidth(true));
            if (GUILayout.Button(">", GUILayout.Width(20)))
            {
                var list = BuildSlotList();
                if (list.Count > 0)
                {
                    int idx = IndexOf(list, _slotKey);
                    idx = (idx + 1) % list.Count;
                    _slotKey = list[idx];
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("State", GUILayout.Width(36));
            _stateIndex = GUILayout.SelectionGrid(_stateIndex, StateLabels, 2, GUILayout.MinWidth(80));
            _stateIndex = Math.Max(0, Math.Min(1, _stateIndex));

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!AccessoryStateCache.IsFetched)
            {
                SandboxServices.Log.LogWarning("Accessory state cache not ready. Open the timeline window first so the UI is loaded.");
                onComplete();
                return;
            }
            bool ok = AccessoryStateCache.PressState(_slotKey, _stateIndex);
            if (!ok)
                SandboxServices.Log.LogWarning($"Accessory state: could not press slot '{_slotKey}' state {_stateIndex}.");
            onComplete();
        }

        public override string SerializePayload()
        {
            return (_slotKey ?? "") + PayloadSeparator + _stateIndex;
        }

        public override void DeserializePayload(string payload)
        {
            _slotKey = AccessoryStateCache.SlotNameAllSlots;
            _stateIndex = 0;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1 && !string.IsNullOrEmpty(p[0]))
            {
                _slotKey = p[0];
                // Backwards compatibility: old "All Accessories" key maps to the real all-slots GameObject name
                if (string.Equals(_slotKey, "All Accessories", StringComparison.OrdinalIgnoreCase))
                    _slotKey = AccessoryStateCache.SlotNameAllSlots;
            }
            if (p.Length >= 2 && int.TryParse(p[1], out int idx))
                _stateIndex = Math.Max(0, Math.Min(1, idx));
        }
    }
}
