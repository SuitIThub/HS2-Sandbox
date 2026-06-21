using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    internal sealed class AnimPreviewFigureSlot
    {
        public AnimGridItem Item = null!;
        public AnimGender Gender;
        public int GenderOrdinal;
        public int PreferredSex = 1;
    }

    internal static class AnimPreviewSlotResolver
    {
        public static void ResolveSingle(AnimGridItem item, List<AnimPreviewFigureSlot> output)
        {
            output.Clear();
            if (item == null)
                return;

            output.Add(new AnimPreviewFigureSlot
            {
                Item = item,
                Gender = AnimGender.Unknown,
                GenderOrdinal = 0,
                PreferredSex = -1,
            });
        }

        public static void ResolveGroup(AnimDisplayGroup group, AnimPhase phase, List<AnimPreviewFigureSlot> output)
        {
            output.Clear();
            if (group == null)
                return;

            if (group.GenderParticipants.Count > 0)
            {
                for (int i = 0; i < group.GenderParticipants.Count; i++)
                {
                    AnimGroupSlot participant = group.GenderParticipants[i];
                    AnimGroupSlot? slot = group.FindSlot(phase, participant.Gender, participant.GenderOrdinal);
                    if (slot == null)
                        continue;

                    output.Add(new AnimPreviewFigureSlot
                    {
                        Item = slot.Item,
                        Gender = slot.Gender,
                        GenderOrdinal = slot.GenderOrdinal,
                        PreferredSex = GenderToSex(slot.Gender),
                    });
                }
                return;
            }

            for (int i = 0; i < group.Slots.Count; i++)
            {
                AnimGroupSlot slot = group.Slots[i];
                if (group.HasPhases && slot.Phase != phase)
                    continue;

                output.Add(new AnimPreviewFigureSlot
                {
                    Item = slot.Item,
                    Gender = slot.Gender,
                    GenderOrdinal = slot.GenderOrdinal,
                    PreferredSex = GenderToSex(slot.Gender),
                });
            }
        }

        private static int GenderToSex(AnimGender gender)
        {
            switch (gender)
            {
                case AnimGender.Male: return 0;
                case AnimGender.Female: return 1;
                default: return -1;
            }
        }
    }
}
