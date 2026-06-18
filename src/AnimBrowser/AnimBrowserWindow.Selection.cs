using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private int _lastClickedVisibleIndex = -1;
        private const float GridCheckboxSize = 18f;

        private static void DrawCheckboxVisual(Rect rect, bool on)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            GUI.skin.toggle.Draw(rect, GUIContent.none, false, false, on, false);
        }

        private bool IsEntrySelected(AnimDisplayEntry entry)
        {
            if (entry.IsGroup)
                return IsGroupSelected(entry.Group!);
            return IsItemSelected(entry.Single!);
        }

        private void SetEntrySelected(AnimDisplayEntry entry, bool selected)
        {
            if (entry.IsGroup)
            {
                if (selected)
                    _selectedGroupIds.Add(entry.Group!.Id);
                else
                    _selectedGroupIds.Remove(entry.Group!.Id);
                return;
            }

            var reference = new AnimCatalogRef(entry.Single!.Group, entry.Single.Category, entry.Single.No);
            if (selected)
                _selectedItemRefs.Add(reference);
            else
                _selectedItemRefs.Remove(reference);
        }

        private void ToggleEntrySelection(AnimDisplayEntry entry)
        {
            if (entry.IsGroup)
                ToggleGroupSelection(entry.Group!);
            else
                ToggleItemSelection(entry.Single!);
        }

        private void SelectVisibleRangeAdditive(int fromIndex, int toIndex)
        {
            if (_visibleEntries.Count == 0)
                return;
            int start = Mathf.Clamp(Mathf.Min(fromIndex, toIndex), 0, _visibleEntries.Count - 1);
            int end = Mathf.Clamp(Mathf.Max(fromIndex, toIndex), 0, _visibleEntries.Count - 1);
            for (int i = start; i <= end; i++)
                SetEntrySelected(_visibleEntries[i], true);
        }

        /// <summary>Thumbnail / list-row click — mirrors Pose Browser card selection (Ctrl toggle, Shift range, plain select+apply).</summary>
        private void HandleEntryActivate(AnimDisplayEntry entry, int visibleIndex)
        {
            Event e = Event.current;
            if (e == null)
                return;

            if (e.button == 1)
            {
                ApplyEntry(entry);
                return;
            }

            if (e.control || e.command)
            {
                ToggleEntrySelection(entry);
                _lastClickedVisibleIndex = visibleIndex;
                return;
            }

            if (e.shift && _lastClickedVisibleIndex >= 0)
            {
                SelectVisibleRangeAdditive(_lastClickedVisibleIndex, visibleIndex);
                return;
            }

            ClearGridSelection();
            SetEntrySelected(entry, true);
            _lastClickedVisibleIndex = visibleIndex;
            ApplyEntry(entry);
        }

        private void ApplyEntry(AnimDisplayEntry entry)
        {
            if (entry.IsGroup)
                ApplyGroupPhase(entry.Group!, entry.Group!.MainPhase);
            else
                OnAnimationClicked(entry.Single!);
        }

        private bool TryHandleEntryCheckbox(AnimDisplayEntry entry, int visibleIndex, Rect cbRect, Event ev)
        {
            if (ev.type != EventType.MouseDown || ev.button != 0 || !cbRect.Contains(ev.mousePosition))
                return false;

            if (ev.shift && _lastClickedVisibleIndex >= 0)
                SelectVisibleRangeAdditive(_lastClickedVisibleIndex, visibleIndex);
            else
                ToggleEntrySelection(entry);

            _lastClickedVisibleIndex = visibleIndex;
            ev.Use();
            return true;
        }

        private Rect GridCheckboxRect(Rect thumbRect) =>
            new Rect(thumbRect.xMax - GridCheckboxSize - 3f, thumbRect.y + 3f, GridCheckboxSize, GridCheckboxSize);
    }
}
