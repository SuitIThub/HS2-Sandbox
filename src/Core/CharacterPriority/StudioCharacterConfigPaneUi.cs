using System;
using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Shared IMGUI for the Studio character priority list (Pose / Anim browsers).</summary>
    internal static class StudioCharacterConfigPaneUi
    {
        public struct Layout
        {
            public Func<float, GUILayoutOption> W;
            public Func<float, GUILayoutOption> H;
            public float DragHeaderHeight;
        }

        public static void DrawBody(
            ref Vector2 scroll,
            ref int selectedSlotIndex,
            IStudioCharacterPriorityList config,
            IEnumerable<OCIChar> studioSelected,
            Layout layout,
            string description,
            Action? onClose)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label(description, layout.H(32f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load characters", layout.H(26f)))
            {
                config.LoadFromScene(StudioCharacterSelection.GetSceneCharacters());
                selectedSlotIndex = -1;
            }

            if (GUILayout.Button("Remove missing", layout.H(26f)))
            {
                int removed = config.RemoveSlotsNotInScene();
                if (removed > 0)
                    selectedSlotIndex = -1;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            var slots = config.Priority;
            var selectedInStudio = new HashSet<OCIChar>();
            foreach (var oci in studioSelected)
                selectedInStudio.Add(oci);

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                int slotIndex = i;
                bool inScene = slot.TryResolveInScene(out var oci);
                bool isStudioSelected = inScene && selectedInStudio.Contains(oci);
                bool rowOn = selectedSlotIndex == i;
                Color prev = GUI.color;
                if (!inScene)
                    GUI.color = new Color(1f, 0.75f, 0.55f, 1f);
                else if (isStudioSelected)
                    GUI.color = new Color(0.55f, 1f, 0.65f, 1f);

                GUILayout.BeginHorizontal();
                string genderLabel = slot.IsFemale ? "f" : "m";
                if (GUILayout.Button(genderLabel, layout.W(24f), layout.H(22f)))
                    config.ToggleSlotGender(i);

                string label = inScene
                    ? (i + 1) + ". " + slot.DisplayName
                    : (i + 1) + ". " + slot.DisplayName + " (missing)";
                if (GUILayout.Toggle(rowOn, label, GUI.skin.button, layout.H(22f), GUILayout.ExpandWidth(true)))
                    selectedSlotIndex = i;
                else if (rowOn)
                    selectedSlotIndex = -1;

                if (GUILayout.Button(
                        new GUIContent("✕", "Remove this character from the priority list."),
                        layout.W(28f),
                        layout.H(22f)))
                {
                    config.RemoveSlot(slotIndex);
                    if (selectedSlotIndex == slotIndex)
                        selectedSlotIndex = -1;
                    else if (selectedSlotIndex > slotIndex)
                        selectedSlotIndex--;
                }

                GUILayout.EndHorizontal();
                GUI.color = prev;
            }

            GUILayout.EndScrollView();

            GUI.enabled = selectedSlotIndex >= 0 && selectedSlotIndex < slots.Count;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("↑", layout.W(28f), layout.H(22f)))
            {
                config.MoveSlot(selectedSlotIndex, -1);
                selectedSlotIndex = Math.Max(0, selectedSlotIndex - 1);
            }

            if (GUILayout.Button("↓", layout.W(28f), layout.H(22f)))
            {
                config.MoveSlot(selectedSlotIndex, 1);
                selectedSlotIndex = Math.Min(slots.Count - 1, selectedSlotIndex + 1);
            }

            if (GUILayout.Button("✕", layout.W(28f), layout.H(22f)))
            {
                config.RemoveSlot(selectedSlotIndex);
                selectedSlotIndex = -1;
            }

            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (onClose != null)
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("Close", layout.H(26f)))
                    onClose();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, layout.DragHeaderHeight));
        }
    }
}
