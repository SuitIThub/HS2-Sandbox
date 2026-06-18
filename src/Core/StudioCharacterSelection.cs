using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Shared Studio character selection helpers (tree + guide objects).</summary>
    internal static class StudioCharacterSelection
    {
        public static IEnumerable<OCIChar> GetSelectedCharacters()
        {
            var list = new List<OCIChar>();
            try
            {
                var gom = Singleton<GuideObjectManager>.Instance;
                if (gom == null)
                    return list;

                foreach (var key in gom.selectObjectKey)
                {
                    try
                    {
                        if (Studio.Studio.GetCtrlInfo(key) is OCIChar oci)
                            AddUnique(list, oci);
                    }
                    catch
                    {
                        // ignore bad key
                    }
                }

                try
                {
                    if (gom.selectObject != null)
                        AddUnique(list, TryGetFromGuideObject(gom.selectObject));
                }
                catch
                {
                    // ignored
                }

                AddTreeSelected(list);
                return list;
            }
            catch
            {
                return list;
            }
        }

        public static void AddUnique(List<OCIChar> list, OCIChar? oci)
        {
            if (oci == null)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], oci))
                    return;
            }
            list.Add(oci);
        }

        public static TreeNodeCtrl? TryGetTreeNodeCtrl()
        {
            try
            {
                return Singleton<Studio.Studio>.Instance.treeNodeCtrl;
            }
            catch
            {
                return null;
            }
        }

        private static void AddTreeSelected(List<OCIChar> list)
        {
            try
            {
                var selectedNodes = TryGetTreeNodeCtrl()?.selectNodes;
                if (selectedNodes == null)
                    return;

                var studio = Singleton<Studio.Studio>.Instance;
                foreach (var node in selectedNodes)
                {
                    if (node == null)
                        continue;
                    if (studio.dicInfo.TryGetValue(node, out ObjectCtrlInfo info) && info is OCIChar oci)
                        AddUnique(list, oci);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static OCIChar? TryGetFromGuideObject(GuideObject guide)
        {
            if (guide == null)
                return null;
            Transform? t = guide.transformTarget;
            return t != null ? FindFromTransform(t) : null;
        }

        private static OCIChar? FindFromTransform(Transform t)
        {
            if (t == null)
                return null;

            try
            {
                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    var oci = kvp.Value as OCIChar;
                    if (oci == null || oci.charInfo == null)
                        continue;
                    Transform charTransform = oci.charInfo.transform;
                    if (charTransform == null)
                        continue;
                    if (t == charTransform || t.IsChildOf(charTransform))
                        return oci;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static string GetDisplayName(OCIChar oci)
        {
            try
            {
                var tn = oci.treeNodeObject;
                if (tn != null && !StringEx.IsNullOrWhiteSpace(tn.textName))
                    return tn.textName.Trim();
            }
            catch
            {
                // ignore
            }

            try
            {
                var param = oci.oiCharInfo?.charFile?.parameter;
                if (param != null && !StringEx.IsNullOrWhiteSpace(param.fullname))
                    return param.fullname.Trim();
            }
            catch
            {
                // ignore
            }

            return "Character";
        }

        public static IEnumerable<OCIChar> GetAllSceneCharacters()
        {
            var list = new List<OCIChar>();
            try
            {
                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar oci)
                        AddUnique(list, oci);
                }
            }
            catch
            {
                // ignored
            }

            return list;
        }

        public static List<OciDicKeyPair> GetSceneCharacters()
        {
            var list = new List<OciDicKeyPair>();
            try
            {
                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar oci)
                        list.Add(new OciDicKeyPair(oci, kvp.Key));
                }
            }
            catch
            {
                // ignored
            }

            return list;
        }

        public static bool TryGetDicKey(OCIChar oci, out int dicKey)
        {
            dicKey = 0;
            try
            {
                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (ReferenceEquals(kvp.Value, oci))
                    {
                        dicKey = kvp.Key;
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public static bool IsFemaleCharacter(OCIChar oci)
        {
            if (oci is OCICharFemale)
                return true;
            try
            {
                return oci.oiCharInfo != null && oci.oiCharInfo.sex != 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsMaleCharacter(OCIChar oci) => !IsFemaleCharacter(oci);
    }
}
