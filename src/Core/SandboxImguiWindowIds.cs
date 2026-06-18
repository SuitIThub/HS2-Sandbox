namespace HS2SandboxPlugin
{
    /// <summary>
    /// Global IMGUI window IDs for all Sandbox modules. Unity treats these as unique across every
    /// <see cref="UnityEngine.GUILayout.Window"/> in the process — allocate new IDs here only.
    /// </summary>
    internal static class SandboxImguiWindowIds
    {
        internal static class CopyScript
        {
            public const int Main = 2001;
            public const int ListEditor = 2009;
        }

        internal static class Timeline
        {
            public const int Main = 2002;
            public const int Variables = 2004;
            public const int ListEditor = 2005;
            public const int Category = 2006;
            public const int DictEditor = 2007;
            public const int PersistentVars = 2008;
        }

        internal static class Notebook
        {
            public const int Main = 2010;
        }

        internal static class SonScale
        {
            public const int Main = 2012;
        }

        internal static class PoseBrowser
        {
            public const int Main = 2020;
            public const int Options = 2021;
            public const int Help = 2022;
            public const int Tag = 2024;
            public const int Sort = 2025;
            public const int Characters = 2026;
            public const int History = 2027;
            public const int ItemAssociation = 2028;
            public const int StashDocked = 2029;
            public const int StashUndocked = 2030;
        }

        internal static class AnimBrowser
        {
            public const int Main = 2050;
            public const int Controls = 2051;
            public const int Characters = 2052;
            public const int GroupReview = 2053;
            public const int Options = 2054;
            public const int ControlsUndocked = 2055;
        }

        internal static class HeelzControl
        {
            public const int Main = 204900;
            public const int TagPicker = 204901;
        }
    }
}
