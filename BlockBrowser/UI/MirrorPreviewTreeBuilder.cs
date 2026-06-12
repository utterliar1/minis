using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace BlockBrowser
{
    public static class MirrorPreviewTreeBuilder
    {
        private static readonly MirrorDirectoryAction[] ActionOrder = new[]
        {
            MirrorDirectoryAction.Delete,
            MirrorDirectoryAction.Overwrite,
            MirrorDirectoryAction.CopyNew,
            MirrorDirectoryAction.ProtectedSkip
        };

        public static TreeNode[] Build(MirrorDirectoryResult result)
        {
            var roots = new List<TreeNode>();
            var entries = result == null ? new List<MirrorDirectoryEntry>() : result.Entries;
            foreach (MirrorDirectoryAction action in ActionOrder)
            {
                var actionEntries = GetEntriesForAction(entries, action);
                if (actionEntries.Count == 0) continue;

                var root = new TreeNode(GetActionLabel(action) + " (" + actionEntries.Count + ")");
                root.Tag = action;
                foreach (MirrorDirectoryEntry entry in actionEntries)
                {
                    AddPath(root, entry);
                }
                root.Expand();
                roots.Add(root);
            }

            if (roots.Count == 0)
            {
                roots.Add(new TreeNode("\u6682\u65E0\u9700\u8981\u66F4\u65B0\u7684\u6587\u4EF6"));
            }

            return roots.ToArray();
        }

        public static void Populate(TreeView treeView, MirrorDirectoryResult result)
        {
            if (treeView == null) return;
            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();
                treeView.Nodes.AddRange(Build(result));
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        private static List<MirrorDirectoryEntry> GetEntriesForAction(IEnumerable<MirrorDirectoryEntry> entries, MirrorDirectoryAction action)
        {
            var list = new List<MirrorDirectoryEntry>();
            foreach (MirrorDirectoryEntry entry in entries ?? new MirrorDirectoryEntry[0])
            {
                if (entry == null || entry.Action != action) continue;
                list.Add(entry);
            }
            list.Sort(delegate(MirrorDirectoryEntry left, MirrorDirectoryEntry right)
            {
                return string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private static void AddPath(TreeNode root, MirrorDirectoryEntry entry)
        {
            string relativePath = NormalizePath(entry.RelativePath);
            if (string.IsNullOrEmpty(relativePath)) relativePath = "\u672A\u547D\u540D";
            string[] parts = relativePath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) parts = new[] { relativePath };

            TreeNodeCollection children = root.Nodes;
            for (int i = 0; i < parts.Length; i++)
            {
                bool isLeaf = i == parts.Length - 1;
                TreeNode node = isLeaf ? null : FindChild(children, parts[i]);
                if (node == null)
                {
                    node = new TreeNode(parts[i]);
                    children.Add(node);
                }

                if (isLeaf)
                {
                    node.Tag = entry;
                    node.ToolTipText = relativePath;
                }

                children = node.Nodes;
            }
        }

        private static TreeNode FindChild(TreeNodeCollection nodes, string text)
        {
            foreach (TreeNode node in nodes)
            {
                if (string.Equals(node.Text, text, StringComparison.OrdinalIgnoreCase)) return node;
            }
            return null;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? "").Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();
        }

        private static string GetActionLabel(MirrorDirectoryAction action)
        {
            switch (action)
            {
                case MirrorDirectoryAction.Delete:
                    return "\u5220\u9664";
                case MirrorDirectoryAction.Overwrite:
                    return "\u8986\u76D6";
                case MirrorDirectoryAction.CopyNew:
                    return "\u65B0\u589E";
                case MirrorDirectoryAction.ProtectedSkip:
                    return "\u4FDD\u62A4\u8DF3\u8FC7";
                default:
                    return "\u5176\u4ED6";
            }
        }
    }
}
