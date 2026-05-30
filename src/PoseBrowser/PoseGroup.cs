using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public sealed class PoseGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public HashSet<string> Tags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Member pose paths relative to the pose library root.</summary>
        public List<string> MemberRelativePaths { get; set; } = new List<string>();
        /// <summary>
        /// Position offset from the anchor in the anchor's local frame (<c>Inverse(anchorRot) * (memberPos - anchorPos)</c>),
        /// keyed by member relative path. On apply: <c>anchorPos + anchorRot * offset</c> (orbits with anchor rotation).
        /// </summary>
        public Dictionary<string, Vector3> MemberRelativeOffsets { get; set; } =
            new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Maker body-height slider per member (including anchor), keyed by member relative path.
        /// Used with <see cref="MemberRelativeOffsets"/> to adjust world Y on apply when characters differ in height.
        /// </summary>
        public Dictionary<string, float> MemberBodyHeights { get; set; } =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Studio guide object scale per member (including anchor), keyed by member relative path.
        /// Used with <see cref="MemberRelativeOffsets"/> to scale saved anchor-local offsets on apply when object scale differs.
        /// </summary>
        public Dictionary<string, Vector3> MemberObjectScales { get; set; } =
            new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Rotation relative to the anchor (first member), keyed by member relative path.
        /// <c>Quaternion.Inverse(anchorRot) * memberRot</c>; anchor is not stored here.
        /// </summary>
        public Dictionary<string, Quaternion> MemberRelativeRotations { get; set; } =
            new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
    }

    [Serializable]
    internal sealed class PoseGroupsPersistedFile
    {
        public int version = 1;
        public PoseGroupPersistedEntry[] groups = Array.Empty<PoseGroupPersistedEntry>();
    }

    [Serializable]
    internal sealed class PoseGroupPersistedEntry
    {
        public string id = "";
        public string name = "";
        public string[] tags = Array.Empty<string>();
        public string[] members = Array.Empty<string>();
    }
}
