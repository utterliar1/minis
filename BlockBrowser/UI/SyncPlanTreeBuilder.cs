using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace BlockBrowser
{
    public static class SyncPlanTreeBuilder
    {
        private static readonly SyncDecisionKind[] KindOrder = new[]
        {
            SyncDecisionKind.Upload,
            SyncDecisionKind.VersionCopy,
            SyncDecisionKind.SkipDuplicate,
            SyncDecisionKind.ProtectedCategorySkip,
            SyncDecisionKind.Conflict,
            SyncDecisionKind.DeleteReview,
            SyncDecisionKind.RenameReview,
            SyncDecisionKind.Error,
            SyncDecisionKind.NoOp
        };

        public static TreeNode[] Build(SyncPlan plan)
        {
            var roots = new List<TreeNode>();
            var decisions = plan == null ? new List<SyncDecision>() : plan.Decisions;

            foreach (SyncDecisionKind kind in KindOrder)
            {
                var kindDecisions = GetDecisionsForKind(decisions, kind);
                if (kindDecisions.Count == 0) continue;

                var root = new TreeNode(GetKindLabel(kind) + " (" + kindDecisions.Count + ")");
                root.Tag = kind;
                foreach (SyncDecision decision in kindDecisions)
                {
                    AddPath(root, decision);
                }
                root.Expand();
                roots.Add(root);
            }

            if (roots.Count == 0)
            {
                roots.Add(new TreeNode("\u6682\u65E0\u540C\u6B65\u9879"));
            }

            return roots.ToArray();
        }

        public static void Populate(TreeView treeView, SyncPlan plan)
        {
            if (treeView == null) return;
            treeView.BeginUpdate();
            try
            {
                treeView.Nodes.Clear();
                treeView.Nodes.AddRange(Build(plan));
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        private static List<SyncDecision> GetDecisionsForKind(IEnumerable<SyncDecision> decisions, SyncDecisionKind kind)
        {
            var list = new List<SyncDecision>();
            foreach (SyncDecision decision in decisions ?? new SyncDecision[0])
            {
                if (decision == null || decision.Kind != kind) continue;
                list.Add(decision);
            }
            list.Sort(delegate(SyncDecision left, SyncDecision right)
            {
                return string.Compare(GetPath(left), GetPath(right), StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private static void AddPath(TreeNode root, SyncDecision decision)
        {
            string relativePath = NormalizePath(GetPath(decision));
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
                    node.Tag = decision;
                    node.ToolTipText = BuildToolTip(decision, relativePath);
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

        private static string GetPath(SyncDecision decision)
        {
            if (decision == null) return "";
            return string.IsNullOrEmpty(decision.Path) ? decision.TargetPath : decision.Path;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? "").Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();
        }

        private static string BuildToolTip(SyncDecision decision, string relativePath)
        {
            if (decision == null || string.IsNullOrEmpty(decision.Message)) return relativePath;
            return relativePath + Environment.NewLine + decision.Message;
        }

        private static string GetKindLabel(SyncDecisionKind kind)
        {
            switch (kind)
            {
                case SyncDecisionKind.Upload:
                case SyncDecisionKind.VersionCopy:
                    return "\u4E0A\u4F20";
                case SyncDecisionKind.SkipDuplicate:
                    return "\u91CD\u590D\u8DF3\u8FC7";
                case SyncDecisionKind.ProtectedCategorySkip:
                    return "\u767D\u540D\u5355\u8DF3\u8FC7";
                case SyncDecisionKind.Conflict:
                    return "\u51B2\u7A81";
                case SyncDecisionKind.DeleteReview:
                    return "\u5220\u9664\u786E\u8BA4";
                case SyncDecisionKind.RenameReview:
                    return "\u91CD\u547D\u540D\u786E\u8BA4";
                case SyncDecisionKind.Error:
                    return "\u5931\u8D25";
                default:
                    return "\u65E0\u64CD\u4F5C";
            }
        }
    }
}
