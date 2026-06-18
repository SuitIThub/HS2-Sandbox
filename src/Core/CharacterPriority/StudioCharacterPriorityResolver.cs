using System.Collections.Generic;
using Studio;

namespace HS2SandboxPlugin
{
    /// <summary>Resolves live <see cref="OCIChar"/> instances from the shared priority list.</summary>
    public static class StudioCharacterPriorityResolver
    {
        public static List<OCIChar> ResolveCharacters(
            IList<StudioCharacterSlot> priority,
            StudioCharacterOrderQuery query,
            IList<OCIChar>? studioSelection = null)
        {
            var result = new List<OCIChar>();
            foreach (var entry in ResolveDetailed(priority, query, studioSelection))
            {
                if (entry.Oci != null)
                    result.Add(entry.Oci);
            }

            return result;
        }

        public static List<StudioCharacterResolution> ResolveDetailed(
            IList<StudioCharacterSlot> priority,
            StudioCharacterOrderQuery query,
            IList<OCIChar>? studioSelection = null)
        {
            var result = new List<StudioCharacterResolution>();
            if (priority == null || priority.Count == 0)
                return result;

            HashSet<OCIChar>? selectedSet = null;
            if (query.Mode == StudioCharacterOrderMode.SelectedByPriority && studioSelection != null)
            {
                selectedSet = new HashSet<OCIChar>();
                for (int i = 0; i < studioSelection.Count; i++)
                    selectedSet.Add(studioSelection[i]);
            }

            var used = new HashSet<OCIChar>();

            for (int i = 0; i < priority.Count; i++)
            {
                StudioCharacterSlot slot = priority[i];
                if (!PassesGenderFilter(slot, query.Gender))
                    continue;

                bool inScene = StudioCharacterSlot.TryResolveInScene(slot, out OCIChar oci);

                switch (query.Mode)
                {
                    case StudioCharacterOrderMode.AllInList:
                        result.Add(new StudioCharacterResolution { Slot = slot, Oci = inScene ? oci : null });
                        break;

                    case StudioCharacterOrderMode.InSceneOnly:
                        if (!inScene)
                            break;
                        if (used.Add(oci))
                            result.Add(new StudioCharacterResolution { Slot = slot, Oci = oci });
                        break;

                    case StudioCharacterOrderMode.SelectedByPriority:
                        if (!inScene || selectedSet == null || !selectedSet.Contains(oci))
                            break;
                        if (used.Add(oci))
                            result.Add(new StudioCharacterResolution { Slot = slot, Oci = oci });
                        break;
                }
            }

            if (query.Mode == StudioCharacterOrderMode.SelectedByPriority &&
                query.AppendUnlistedSelected &&
                studioSelection != null)
            {
                for (int i = 0; i < studioSelection.Count; i++)
                {
                    OCIChar oci = studioSelection[i];
                    if (!used.Add(oci))
                        continue;
                    result.Add(new StudioCharacterResolution
                    {
                        Slot = StudioCharacterSlot.FromScene(
                            oci,
                            StudioCharacterSelection.TryGetDicKey(oci, out int key) ? key : 0),
                        Oci = oci
                    });
                }
            }

            return result;
        }

        public static List<OCIChar> ResolveForApply(
            IStudioCharacterPriorityList list,
            IList<OCIChar> studioSelection,
            StudioCharacterGenderFilter gender = StudioCharacterGenderFilter.Any,
            bool appendUnlistedSelected = true)
        {
            if (list.Priority.Count == 0)
            {
                var fallback = new List<OCIChar>(studioSelection.Count);
                for (int i = 0; i < studioSelection.Count; i++)
                    fallback.Add(studioSelection[i]);
                return fallback;
            }

            return ResolveCharacters(
                list.Priority,
                StudioCharacterOrderQuery.SelectedByPriority(gender, appendUnlistedSelected),
                studioSelection);
        }

        private static bool PassesGenderFilter(StudioCharacterSlot slot, StudioCharacterGenderFilter gender)
        {
            if (gender == StudioCharacterGenderFilter.Male && slot.IsFemale)
                return false;
            if (gender == StudioCharacterGenderFilter.Female && !slot.IsFemale)
                return false;
            return true;
        }
    }
}
