using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace CadToolkit
{
    public partial class CadCommands
    {
        class StandardPreviewFilterControls
        {
            public RadioButton All;
            public RadioButton Unknown;
            public RadioButton Migration;
            public RadioButton Whitelist;
            public Label SearchLabel;
            public TextBox Search;
        }

        static Form CreateStandardPreviewForm(string title)
        {
            var f = new Form();
            f.Text = SafeStr(title);
            f.StartPosition = FormStartPosition.CenterScreen;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
            f.AutoScaleMode = AutoScaleMode.None; f.AutoScroll = true; f.ClientSize = new Size(UiScale(620), UiScale(540));
            return f;
        }

        static StandardPreviewFilterControls CreateStandardPreviewFilterControls(string allText, string unknownText, string migrationText, string whitelistText, string searchText)
        {
            var controls = new StandardPreviewFilterControls();

            controls.All = new RadioButton();
            controls.All.Text = SafeStr(allText); controls.All.Left = UiScale(12); controls.All.Top = UiScale(12); controls.All.Width = UiScale(70); controls.All.Height = UiScale(24); controls.All.Checked = true;
            controls.All.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            controls.Unknown = new RadioButton();
            controls.Unknown.Text = SafeStr(unknownText); controls.Unknown.Left = UiScale(88); controls.Unknown.Top = UiScale(12); controls.Unknown.Width = UiScale(86); controls.Unknown.Height = UiScale(24);
            controls.Unknown.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            controls.Migration = new RadioButton();
            controls.Migration.Text = SafeStr(migrationText); controls.Migration.Left = UiScale(180); controls.Migration.Top = UiScale(12); controls.Migration.Width = UiScale(86); controls.Migration.Height = UiScale(24);
            controls.Migration.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            controls.Whitelist = new RadioButton();
            controls.Whitelist.Text = SafeStr(whitelistText); controls.Whitelist.Left = UiScale(272); controls.Whitelist.Top = UiScale(12); controls.Whitelist.Width = UiScale(86); controls.Whitelist.Height = UiScale(24);
            controls.Whitelist.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            controls.SearchLabel = new Label();
            controls.SearchLabel.Text = SafeStr(searchText); controls.SearchLabel.Left = UiScale(382); controls.SearchLabel.Top = UiScale(15); controls.SearchLabel.Width = UiScale(40); controls.SearchLabel.Height = UiScale(20);
            controls.SearchLabel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            controls.Search = new TextBox();
            controls.Search.Left = UiScale(428); controls.Search.Top = UiScale(12); controls.Search.Width = UiScale(180); controls.Search.Height = UiScale(24);
            controls.Search.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            return controls;
        }

        static TreeView CreateStandardPreviewTree(int height)
        {
            var tree = new TreeView();
            tree.HideSelection = false;
            tree.FullRowSelect = true;
            tree.ShowNodeToolTips = true;
            tree.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            tree.Left = UiScale(12); tree.Top = UiScale(42); tree.Width = UiScale(596); tree.Height = UiScale(height);
            return tree;
        }

        static Button CreateStandardPreviewButton(string text, int left, int width, DialogResult dialogResult)
        {
            var button = new Button();
            button.Text = SafeStr(text);
            button.Left = UiScale(left); button.Top = UiScale(500);
            button.Width = UiScale(width); button.Height = UiScale(28);
            button.FlatStyle = FlatStyle.System;
            button.DialogResult = dialogResult;
            return button;
        }

        static TreeNode[] FilterStandardPreviewNodes(TreeNode[] filtered, string searchText)
        {
            string needle = SafeStr(searchText).Trim();
            if (needle.Length == 0) return filtered;

            var nodes = new List<TreeNode>();
            if (filtered != null && filtered.Length > 0) nodes.Add((TreeNode)filtered[0].Clone());
            if (filtered != null)
            {
                for (int i = 1; i < filtered.Length; i++)
                {
                    var matched = CloneStandardPreviewNodeMatches(filtered[i], needle);
                    if (matched != null) nodes.Add(matched);
                }
            }
            if (nodes.Count == 1) nodes.Add(new TreeNode("无匹配结果"));
            return nodes.ToArray();
        }

        static string FormatStandardPreviewTreeReport(TreeNode[] nodes)
        {
            var sb = new StringBuilder();
            if (nodes == null) return "";
            foreach (TreeNode node in nodes)
                AppendStandardPreviewTreeReportNode(sb, node, 0);
            return sb.ToString();
        }

        static void UpdateStandardPreviewTree(TreeView tree, TreeNode[] nodes, bool expand)
        {
            if (tree == null) return;
            tree.BeginUpdate();
            try
            {
                tree.Nodes.Clear();
                if (nodes != null) tree.Nodes.AddRange(nodes);
                if (expand) tree.ExpandAll();
            }
            finally
            {
                tree.EndUpdate();
            }
        }

        static void AppendStandardPreviewTreeReportNode(StringBuilder sb, TreeNode node, int depth)
        {
            if (sb == null || node == null) return;
            if (depth > 0) sb.Append(new string(' ', depth * 2));
            sb.AppendLine(SafeStr(node.Text));
            foreach (TreeNode child in node.Nodes)
                AppendStandardPreviewTreeReportNode(sb, child, depth + 1);
        }

        static TreeNode CloneStandardPreviewNodeMatches(TreeNode node, string needle)
        {
            bool selfMatches = NodeTextContains(node, needle);
            var clone = new TreeNode(node.Text);
            clone.ToolTipText = node.ToolTipText;
            foreach (TreeNode child in node.Nodes)
            {
                var childClone = CloneStandardPreviewNodeMatches(child, needle);
                if (childClone != null) clone.Nodes.Add(childClone);
            }
            if (selfMatches || clone.Nodes.Count > 0) return clone;
            return null;
        }
    }
}
