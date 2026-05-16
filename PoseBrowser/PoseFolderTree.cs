using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HS2SandboxPlugin
{
    public class PoseFolderNode
    {
        public string FullPath { get; }
        public string Name { get; }
        public int Depth { get; }
        public List<PoseFolderNode> Children { get; } = new List<PoseFolderNode>();
        public bool IsExpanded { get; set; }
        public bool HasChildren => Children.Count > 0;

        public PoseFolderNode(string fullPath, int depth)
        {
            FullPath = fullPath;
            Name = Path.GetFileName(fullPath);
            Depth = depth;
        }
    }

    public class PoseFolderTree
    {
        private const string BackupFolder = "!_AutoBackup";

        public string RootPath { get; }
        public List<PoseFolderNode> RootNodes { get; private set; } = new List<PoseFolderNode>();
        public PoseFolderNode? SelectedNode { get; set; }

        public string? CurrentFolderPath => SelectedNode?.FullPath;

        public PoseFolderTree(string rootPath)
        {
            RootPath = rootPath;
        }

        public void Refresh()
        {
            RootNodes.Clear();
            if (!Directory.Exists(RootPath)) return;

            var rootDir = new DirectoryInfo(RootPath);
            foreach (var dir in rootDir.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(dir.Name, BackupFolder, StringComparison.OrdinalIgnoreCase))
                    continue;
                var node = BuildNode(dir, 0);
                RootNodes.Add(node);
            }
        }

        private PoseFolderNode BuildNode(DirectoryInfo dirInfo, int depth)
        {
            var node = new PoseFolderNode(dirInfo.FullName, depth);
            try
            {
                foreach (var subDir in dirInfo.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(subDir.Name, BackupFolder, StringComparison.OrdinalIgnoreCase))
                        continue;
                    node.Children.Add(BuildNode(subDir, depth + 1));
                }
            }
            catch
            {
                // Permission denied or similar
            }
            return node;
        }

        public IEnumerable<PoseFolderNode> GetVisibleNodes()
        {
            foreach (var root in RootNodes)
            {
                foreach (var node in EnumerateVisible(root))
                    yield return node;
            }
        }

        private IEnumerable<PoseFolderNode> EnumerateVisible(PoseFolderNode node)
        {
            yield return node;
            if (node.IsExpanded)
            {
                foreach (var child in node.Children)
                {
                    foreach (var visible in EnumerateVisible(child))
                        yield return visible;
                }
            }
        }

        public void SelectNode(PoseFolderNode node)
        {
            SelectedNode = node;
        }

        public void ToggleExpand(PoseFolderNode node)
        {
            node.IsExpanded = !node.IsExpanded;
        }

        public PoseFolderNode? FindNodeByFullPath(string fullPath)
        {
            string target = Path.GetFullPath(fullPath);
            foreach (var root in RootNodes)
            {
                var hit = FindRecursive(root, target);
                if (hit != null) return hit;
            }
            return null;
        }

        private static PoseFolderNode? FindRecursive(PoseFolderNode node, string targetFull)
        {
            if (Path.GetFullPath(node.FullPath).Equals(targetFull, StringComparison.OrdinalIgnoreCase))
                return node;
            foreach (var c in node.Children)
            {
                var r = FindRecursive(c, targetFull);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Expands ancestors so <paramref name="target"/> is reachable in the tree list.</summary>
        public void EnsureExpandedToShow(PoseFolderNode target)
        {
            string targetF = Path.GetFullPath(target.FullPath);
            foreach (var root in RootNodes)
            {
                if (TryExpandPathTo(root, targetF))
                    return;
            }
        }

        private static bool TryExpandPathTo(PoseFolderNode node, string targetFull)
        {
            string nf = Path.GetFullPath(node.FullPath);
            if (targetFull.Equals(nf, StringComparison.OrdinalIgnoreCase))
                return true;
            string sep = Path.DirectorySeparatorChar.ToString();
            string prefix = nf.EndsWith(sep) ? nf : nf + sep;
            if (!targetFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            node.IsExpanded = true;
            foreach (var c in node.Children)
            {
                if (TryExpandPathTo(c, targetFull))
                    return true;
            }
            return false;
        }
    }
}
