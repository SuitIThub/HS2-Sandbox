namespace HS2SandboxPlugin
{
    /// <summary>Stable persistence keys for user-defined display name overrides.</summary>
    internal static class AnimDisplayNameKeys
    {
        public const string GroupPrefix = "group:";
        public const string CategoryPrefix = "category:";
        public const string AnimationPrefix = "anim:";
        public const string TreeNodePrefix = "node:";

        public static string Group(int groupId) => GroupPrefix + groupId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static string Category(int groupId, int categoryId) =>
            CategoryPrefix + groupId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." +
            categoryId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static string Animation(AnimGridItem item) => AnimationPrefix + item.CatalogKey;

        public static string Animation(AnimCatalogRef reference) => AnimationPrefix + reference.Key;

        public static string TreeNode(string nodeId) => TreeNodePrefix + nodeId;
    }
}
