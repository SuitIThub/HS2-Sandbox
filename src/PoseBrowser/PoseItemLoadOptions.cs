namespace HS2SandboxPlugin
{
    /// <summary>Per-load options from the Items pane (session toggles).</summary>
    internal sealed class PoseItemLoadOptions
    {
        public bool LoadPosition { get; set; } = true;
        public bool LoadRotation { get; set; } = true;
        public bool LoadScale { get; set; } = true;
        public bool ForceFreePlacement { get; set; }

        public bool AppliesAnyTransform => LoadPosition || LoadRotation || LoadScale;
    }
}
