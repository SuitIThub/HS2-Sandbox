using Studio;

namespace HS2SandboxPlugin
{
    /// <summary>One priority-list slot paired with its live scene character when resolved.</summary>
    public struct StudioCharacterResolution
    {
        public StudioCharacterSlot Slot;
        public OCIChar? Oci;

        public bool InScene => Oci != null;
    }
}
