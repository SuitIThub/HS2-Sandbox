namespace HS2SandboxPlugin
{
    /// <summary>Runtime values driven by the injected Manipulate UI (and read by <see cref="SonScaleApplier"/>).</summary>
    internal static class SonScaleSettings
    {
        internal static bool Enabled = true;
        /// <summary>
        /// Overall size along the shaft (first hijacked slider). On multi-segment rigs this multiplies chain spacing with
        /// <see cref="Length"/>; root Z is not scaled so Better Penetration / IK stay stable. Single-bone rigs use root Z (<c>master×length</c>).
        /// </summary>
        internal static float Master = 1f;
        internal static float Length = 1f;
        internal static float Girth = 1f;
        /// <summary>Uniform scale on <see cref="SonBoneResolver.BallsRootBoneName"/> when that bone exists (folded into dan root scale if it is the same transform).</summary>
        internal static float Balls = 1f;
    }
}
