using HarmonyLib;
using Studio;

namespace HS2SandboxPlugin.WorkspaceTreeLock
{
    /// <summary>
    /// Prevents Studio from hiding pinned <see cref="TreeNodeObject"/> rows when ancestors collapse.
    /// Direct <see cref="TreeNodeObject.SetVisible"/> (e.g. eye toggle) must still run when propagation depth is zero.
    /// </summary>
    internal static class WorkspaceTreeLockHarmony
    {
        internal const string HarmonyId = "com.hs2.sandbox.workspacetreelock";

        /// <summary>Non-zero while <see cref="SetVisibleChild"/> / <see cref="SetVisibleLoop"/> are applying a hide from the tree.</summary>
        private static int _propagateHideDepth;

        internal static void Install()
        {
            var harmony = new Harmony(HarmonyId);
            var t = typeof(TreeNodeObject);

            harmony.Patch(
                AccessTools.Method(t, nameof(TreeNodeObject.SetVisible), new[] { typeof(bool) }),
                prefix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SetVisible_Prefix)));

            harmony.Patch(
                AccessTools.Method(t, "SetVisibleChild", new[] { typeof(TreeNodeObject), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SetVisibleChild_Prefix)),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SetVisibleChild_Postfix)));

            harmony.Patch(
                AccessTools.Method(t, "SetVisibleLoop", new[] { typeof(TreeNodeObject), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SetVisibleLoop_Prefix)),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(SetVisibleLoop_Postfix)));

            harmony.Patch(
                AccessTools.Method(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.DeleteNode), new[] { typeof(TreeNodeObject) }),
                postfix: new HarmonyMethod(typeof(WorkspaceTreeLockHarmony), nameof(DeleteNode_Postfix)));
        }

        private static void DeleteNode_Postfix(TreeNodeObject _node)
        {
            if (_node != null)
                WorkspaceTreeLockRegistry.ForgetNode(_node);
        }

        private static bool SetVisible_Prefix(TreeNodeObject __instance, bool _visible)
        {
            if (!_visible
                && WorkspaceTreeLockRegistry.IsLocked(__instance)
                && _propagateHideDepth > 0)
                return false;
            return true;
        }

        private static bool SetVisibleChild_Prefix(
            TreeNodeObject __instance,
            TreeNodeObject _source,
            bool _visible,
            ref bool __state)
        {
            __state = false;
            if (!_visible && WorkspaceTreeLockRegistry.IsLocked(_source))
                return false;

            if (!_visible)
            {
                _propagateHideDepth++;
                __state = true;
            }

            return true;
        }

        private static void SetVisibleChild_Postfix(bool __state)
        {
            if (__state)
                _propagateHideDepth--;
        }

        private static bool SetVisibleLoop_Prefix(
            TreeNodeObject __instance,
            TreeNodeObject _source,
            bool _visible,
            ref bool __state)
        {
            __state = false;
            if (!_visible && WorkspaceTreeLockRegistry.IsLocked(_source))
                return false;

            if (!_visible)
            {
                _propagateHideDepth++;
                __state = true;
            }

            return true;
        }

        private static void SetVisibleLoop_Postfix(bool __state)
        {
            if (__state)
                _propagateHideDepth--;
        }
    }
}
