namespace HS2SandboxPlugin
{
    /// <summary>How to walk the priority list when resolving characters.</summary>
    public enum StudioCharacterOrderMode
    {
        /// <summary>Every slot in list order. Unresolved slots are omitted from OCI results.</summary>
        AllInList = 0,

        /// <summary>Only slots that currently exist in the scene, in list order.</summary>
        InSceneOnly = 1,

        /// <summary>
        /// List order intersected with Studio selection. Use <see cref="StudioCharacterOrderQuery.AppendUnlistedSelected"/>
        /// to append selected characters not present in the list (Pose Browser untagged behavior).
        /// </summary>
        SelectedByPriority = 2
    }

    /// <summary>Parameters for <see cref="StudioCharacterPriorityResolver"/>.</summary>
    public struct StudioCharacterOrderQuery
    {
        public StudioCharacterOrderMode Mode;
        public StudioCharacterGenderFilter Gender;
        public bool AppendUnlistedSelected;

        public static StudioCharacterOrderQuery AllInList() =>
            new StudioCharacterOrderQuery
            {
                Mode = StudioCharacterOrderMode.AllInList,
                Gender = StudioCharacterGenderFilter.Any,
                AppendUnlistedSelected = false
            };

        public static StudioCharacterOrderQuery InSceneOnly() =>
            new StudioCharacterOrderQuery
            {
                Mode = StudioCharacterOrderMode.InSceneOnly,
                Gender = StudioCharacterGenderFilter.Any,
                AppendUnlistedSelected = false
            };

        public static StudioCharacterOrderQuery SelectedByPriority(
            StudioCharacterGenderFilter gender = StudioCharacterGenderFilter.Any,
            bool appendUnlistedSelected = false) =>
            new StudioCharacterOrderQuery
            {
                Mode = StudioCharacterOrderMode.SelectedByPriority,
                Gender = gender,
                AppendUnlistedSelected = appendUnlistedSelected
            };
    }
}
