using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    /// <summary>Mutable priority list of Studio characters for multi-character apply.</summary>
    public interface IStudioCharacterPriorityList
    {
        IList<StudioCharacterSlot> Priority { get; }

        void LoadFromScene(IEnumerable<OciDicKeyPair> sceneCharacters);

        int RemoveSlotsNotInScene();

        void MoveSlot(int index, int delta);

        void ToggleSlotGender(int index);

        void RemoveSlot(int index);

        void ReloadFromDisk();
    }
}
