using UnityEngine;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    internal static class WorkspaceTreeLockBootstrap
    {
        private static bool _installed;

        internal static void Install(GameObject host)
        {
            if (_installed)
                return;

            _installed = true;
            WorkspaceTreeLockHarmony.Install();
            host.AddComponent<WorkspaceTreeLockInput>();
            WorkspaceTreeLockRestorer.EnsureInstance(host);
        }
    }
}
