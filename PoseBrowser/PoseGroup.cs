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
        /// World-space offsets from the first member's anchor character position, keyed by member relative path.
        /// The first member is the anchor and is not stored here.
        /// </summary>
        public Dictionary<string, Vector3> MemberRelativeOffsets { get; set; } =
            new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
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
