using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>A node in the displayed (post-merge) category tree. Mirrors the raw catalog tree
    /// but can represent virtual merged nodes spanning several raw groups/categories.</summary>
    internal sealed class AnimViewNode
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public int Depth;
        public bool IsGroup;
        public bool IsExpanded = false;
        public bool IsMerged;

        /// <summary>Raw catalog group id for a plain (non-merged) group node, else -1.</summary>
        public int RawGroupId = -1;

        public readonly List<AnimViewNode> Children = new List<AnimViewNode>();

        /// <summary>Real (group, category) buckets this node draws its items from. Group nodes leave
        /// this empty and aggregate their children.</summary>
        public readonly List<AnimCatalogRef> SourceCategories = new List<AnimCatalogRef>();

        /// <summary>Display path from tree root to this node (segment names).</summary>
        public readonly List<string> DisplayPathSegments = new List<string>();

        public AnimNodePlacementKind PlacementKind = AnimNodePlacementKind.Normal;

        /// <summary>Merge rule that owns this node when <see cref="IsMerged"/>; empty otherwise.</summary>
        public string MergeRuleId = string.Empty;

        public string? CachedTruncatedName;
        public float CachedTruncatedWidth = -1f;
        public string? CachedTruncatedLabelSource;

        public string GetDisplayLabel() => StudioAutoTranslation.Resolve(Name);

        public void InvalidateDisplayCaches()
        {
            CachedTruncatedName = null;
            CachedTruncatedWidth = -1f;
            CachedTruncatedLabelSource = null;
        }
    }

    /// <summary>One animation cell inside a grouped card, with its resolved role.</summary>
    internal sealed class AnimGroupSlot
    {
        public AnimGridItem Item = null!;
        public AnimPhase Phase;
        public AnimGender Gender;
        public int GenderOrdinal;
        public string GenderLabel = string.Empty;
    }

    /// <summary>A grouped card: several catalog animations sharing one tile, with phase and gender buttons.</summary>
    internal sealed class AnimDisplayGroup
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public readonly List<AnimGroupSlot> Slots = new List<AnimGroupSlot>();
        public readonly List<AnimPhase> Phases = new List<AnimPhase>();

        /// <summary>One representative slot per distinct gender participant (m, f, f2…), in display order.</summary>
        public readonly List<AnimGroupSlot> GenderParticipants = new List<AnimGroupSlot>();

        public bool HasPhases;
        public bool HasGenders;
        /// <summary>Plain numbered slot buttons when the group has no inferred phase or gender roles.</summary>
        public bool HasSlotIndexButtons;
        public AnimPhase MainPhase = AnimPhase.None;
        public int Sort;

        private GUIContent[]? _phaseContents;
        private GUIContent[]? _genderContents;
        private GUIContent[]? _slotIndexContents;
        private GUIContent? _listContent;
        private string _listLabel = string.Empty;

        public AnimGridItem ThumbnailItem => Slots.Count > 0 ? Slots[0].Item : null!;

        public GUIContent GetListContent(string tooltip)
        {
            string label = GetDisplayLabel();
            if (_listContent == null || !string.Equals(_listLabel, label, System.StringComparison.Ordinal))
            {
                _listLabel = label;
                _listContent = new GUIContent(label, tooltip);
            }
            return _listContent;
        }

        public string GetDisplayLabel() => StudioAutoTranslation.Resolve(Name);

        /// <summary>Computes phases, gender participants and main phase from the resolved slots.</summary>
        public void Recompute()
        {
            Phases.Clear();
            GenderParticipants.Clear();
            _phaseContents = null;
            _genderContents = null;
            _slotIndexContents = null;

            bool hasIn = false, hasLoop = false, hasOut = false;
            int maleCount = 0, femaleCount = 0;
            HasGenders = false;

            for (int i = 0; i < Slots.Count; i++)
            {
                AnimGroupSlot slot = Slots[i];
                switch (slot.Phase)
                {
                    case AnimPhase.In: hasIn = true; break;
                    case AnimPhase.Loop: hasLoop = true; break;
                    case AnimPhase.Out: hasOut = true; break;
                }
                if (slot.Gender == AnimGender.Male)
                {
                    HasGenders = true;
                    if (slot.GenderOrdinal + 1 > maleCount) maleCount = slot.GenderOrdinal + 1;
                }
                else if (slot.Gender == AnimGender.Female)
                {
                    HasGenders = true;
                    if (slot.GenderOrdinal + 1 > femaleCount) femaleCount = slot.GenderOrdinal + 1;
                }
            }

            if (hasIn) Phases.Add(AnimPhase.In);
            if (hasLoop) Phases.Add(AnimPhase.Loop);
            if (hasOut) Phases.Add(AnimPhase.Out);

            // Single-phase groups behave like plain gender groups (no phase button row).
            if (Phases.Count <= 1)
            {
                MainPhase = Phases.Count == 1 ? Phases[0] : AnimPhase.None;
                Phases.Clear();
                HasPhases = false;
            }
            else
            {
                HasPhases = true;
                MainPhase = hasLoop ? AnimPhase.Loop : Phases[0];
            }

            for (int i = 0; i < Slots.Count; i++)
                Slots[i].GenderLabel = AnimRoleText.GenderButtonLabel(Slots[i].Gender, Slots[i].GenderOrdinal, maleCount, femaleCount);

            // Distinct gender participants in stable order, one representative slot each.
            var seen = new HashSet<int>();
            for (int i = 0; i < Slots.Count; i++)
            {
                AnimGroupSlot slot = Slots[i];
                if (slot.Gender == AnimGender.Unknown)
                    continue;
                int key = ((int)slot.Gender << 8) | slot.GenderOrdinal;
                if (seen.Add(key))
                    GenderParticipants.Add(slot);
            }

            HasSlotIndexButtons = !HasPhases && !HasGenders && Slots.Count >= 2;
        }

        public GUIContent[] SlotIndexContents
        {
            get
            {
                if (_slotIndexContents == null)
                {
                    _slotIndexContents = new GUIContent[Slots.Count];
                    for (int i = 0; i < Slots.Count; i++)
                    {
                        string num = AnimRoleText.SlotIndexLabel(i);
                        string original = StudioAutoTranslation.Resolve(Slots[i].Item.DisplayName);
                        _slotIndexContents[i] = new GUIContent(num, original);
                    }
                }
                return _slotIndexContents;
            }
        }

        public GUIContent[] PhaseContents
        {
            get
            {
                if (_phaseContents == null)
                {
                    _phaseContents = new GUIContent[Phases.Count];
                    for (int i = 0; i < Phases.Count; i++)
                        _phaseContents[i] = new GUIContent(
                            AnimRoleText.PhaseButtonLabel(Phases[i]),
                            AnimRoleText.PhaseName(Phases[i]));
                }
                return _phaseContents;
            }
        }

        public GUIContent[] GenderContents
        {
            get
            {
                if (_genderContents == null)
                {
                    _genderContents = new GUIContent[GenderParticipants.Count];
                    for (int i = 0; i < GenderParticipants.Count; i++)
                    {
                        AnimGroupSlot slot = GenderParticipants[i];
                        string original = StudioAutoTranslation.Resolve(slot.Item.DisplayName);
                        _genderContents[i] = new GUIContent(slot.GenderLabel, original);
                    }
                }
                return _genderContents;
            }
        }

        /// <summary>Finds the slot matching a phase + gender participant, or null.</summary>
        public AnimGroupSlot? FindSlot(AnimPhase phase, AnimGender gender, int ordinal)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                AnimGroupSlot slot = Slots[i];
                if (slot.Gender == gender && slot.GenderOrdinal == ordinal &&
                    (!HasPhases || slot.Phase == phase))
                    return slot;
            }
            return null;
        }

        public void InvalidateDisplayCaches()
        {
            _phaseContents = null;
            _genderContents = null;
            _slotIndexContents = null;
            _listContent = null;
            for (int i = 0; i < Slots.Count; i++)
                Slots[i].Item.InvalidateDisplayCaches();
        }
    }

    /// <summary>Display strings for animation roles (phase symbols + names).</summary>
    internal static class AnimRoleText
    {
        /// <summary>Short ASCII label for IMGUI role buttons (Unity default font often lacks Unicode symbols).</summary>
        public static string PhaseButtonLabel(AnimPhase phase)
        {
            switch (phase)
            {
                case AnimPhase.In: return "in";
                case AnimPhase.Loop: return "loop";
                case AnimPhase.Out: return "out";
                default: return "·";
            }
        }

        public static string PhaseSymbol(AnimPhase phase) => PhaseButtonLabel(phase);

        public static string PhaseName(AnimPhase phase)
        {
            switch (phase)
            {
                case AnimPhase.In: return "Intro";
                case AnimPhase.Loop: return "Loop";
                case AnimPhase.Out: return "Outro";
                default: return "Single";
            }
        }

        public static string GenderButtonLabel(AnimGender gender, int ordinal, int maleCount, int femaleCount)
        {
            switch (gender)
            {
                case AnimGender.Male:
                    return maleCount > 1 ? "m" + (ordinal + 1) : "m";
                case AnimGender.Female:
                    return femaleCount > 1 ? "f" + (ordinal + 1) : "f";
                default:
                    return "any";
            }
        }

        public static string SlotIndexLabel(int zeroBasedIndex) =>
            (zeroBasedIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool GroupUsesSlotIndexLabels(IList<AnimGroupMemberData> members)
        {
            if (members == null || members.Count < 2)
                return false;
            for (int i = 0; i < members.Count; i++)
            {
                AnimGroupMemberData member = members[i];
                if (member.Gender != AnimGender.Unknown || member.Phase != AnimPhase.None)
                    return false;
            }
            return true;
        }

        public static bool MemberUsesSlotIndexLabel(AnimGroupMemberData member) =>
            member.Gender == AnimGender.Unknown && member.Phase == AnimPhase.None;

        public static string ReviewGenderRoleLabel(
            AnimGroupMemberData member,
            int maleCount,
            int femaleCount)
        {
            if (member.Gender == AnimGender.Male || member.Gender == AnimGender.Female)
                return GenderButtonLabel(member.Gender, member.GenderOrdinal, maleCount, femaleCount);
            if (member.Phase == AnimPhase.None)
                return SlotIndexLabel(member.GenderOrdinal);
            return "·";
        }

        public static string ReviewPhaseRoleLabel(AnimPhase phase) =>
            phase == AnimPhase.None ? "—" : PhaseButtonLabel(phase);

        public static string GenderName(AnimGender gender)
        {
            switch (gender)
            {
                case AnimGender.Male: return "Male";
                case AnimGender.Female: return "Female";
                default: return "Unknown";
            }
        }
    }

    /// <summary>A single entry in the content view: either one ungrouped animation or one display group.</summary>
    internal sealed class AnimDisplayEntry
    {
        public AnimGridItem? Single;
        public AnimDisplayGroup? Group;

        public bool IsGroup => Group != null;

        public int Sort => Group?.Sort ?? (Single?.Sort ?? 0);

        public static AnimDisplayEntry ForSingle(AnimGridItem item) => new AnimDisplayEntry { Single = item };

        public static AnimDisplayEntry ForGroup(AnimDisplayGroup group) => new AnimDisplayEntry { Group = group };
    }
}
