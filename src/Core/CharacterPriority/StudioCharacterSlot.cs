using System;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>One entry in the shared Studio character priority list.</summary>
    public sealed class StudioCharacterSlot
    {
        public int DicKey { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsFemale { get; set; }

        public bool TryResolveInScene(out OCIChar oci) => TryResolveInScene(this, out oci);

        public static bool TryResolveInScene(StudioCharacterSlot slot, out OCIChar oci)
        {
            oci = null!;
            if (StringEx.IsNullOrWhiteSpace(slot.DisplayName) && slot.DicKey == 0)
                return false;

            try
            {
                if (Singleton<Studio.Studio>.Instance.dicObjectCtrl.TryGetValue(slot.DicKey, out var info) &&
                    info is OCIChar byKey &&
                    NamesMatch(slot, byKey))
                {
                    oci = byKey;
                    return true;
                }

                if (StringEx.IsNullOrWhiteSpace(slot.DisplayName))
                    return false;

                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar c && NamesMatch(slot, c))
                    {
                        oci = c;
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private static bool NamesMatch(StudioCharacterSlot slot, OCIChar oci) =>
            string.Equals(
                slot.DisplayName,
                StudioCharacterSelection.GetDisplayName(oci),
                StringComparison.OrdinalIgnoreCase);

        public static bool RefreshIdentityFromScene(StudioCharacterSlot slot, OCIChar oci, int dicKey)
        {
            string name = StudioCharacterSelection.GetDisplayName(oci);
            bool changed = slot.DicKey != dicKey ||
                           !string.Equals(slot.DisplayName, name, StringComparison.OrdinalIgnoreCase);
            if (!changed)
                return false;

            slot.DicKey = dicKey;
            slot.DisplayName = name;
            return true;
        }

        public static StudioCharacterSlot FromScene(OCIChar oci, int dicKey) =>
            new StudioCharacterSlot
            {
                DicKey = dicKey,
                DisplayName = StudioCharacterSelection.GetDisplayName(oci),
                IsFemale = StudioCharacterSelection.IsFemaleCharacter(oci)
            };

        internal StudioCharacterSlotPersisted ToPersisted() =>
            new StudioCharacterSlotPersisted
            {
                dicKey = DicKey,
                displayName = DisplayName ?? string.Empty,
                isFemale = IsFemale
            };

        internal static StudioCharacterSlot FromPersisted(StudioCharacterSlotPersisted p) =>
            new StudioCharacterSlot
            {
                DicKey = p.dicKey,
                DisplayName = p.displayName ?? string.Empty,
                IsFemale = p.isFemale
            };
    }

    [Serializable]
    internal sealed class StudioCharacterSlotPersisted
    {
        public int dicKey;
        public string displayName = string.Empty;
        public bool isFemale;
    }

    /// <summary>Legacy v2 on-disk shape (JsonUtility import only).</summary>
    [Serializable]
    internal sealed class StudioCharacterConfigFileV2
    {
        public int version = 2;
        public StudioCharacterSlotPersisted[] male = new StudioCharacterSlotPersisted[0];
        public StudioCharacterSlotPersisted[] female = new StudioCharacterSlotPersisted[0];
        public bool untaggedInterleaveFemaleFirst;
    }
}
