using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class MirrorPreviewDialog : Form
    {
        private readonly TreeView _treeView;

        public MirrorPreviewDialog(MirrorDirectoryResult result)
        {
            Text = "\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u9884\u89C8";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(820, 600);
            MinimumSize = new Size(680, 460);
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 3,
                Dock = DockStyle.Fill
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var summaryLabel = new Label
            {
                Text = BuildSummary(result),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8)
            };

            _treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowNodeToolTips = true,
                Margin = new Padding(0, 0, 0, 12)
            };
            MirrorPreviewTreeBuilder.Populate(_treeView, result);

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            var cancelButton = new Button
            {
                Text = "\u53D6\u6D88",
                DialogResult = DialogResult.Cancel,
                AutoSize = true
            };
            var okButton = new Button
            {
                Text = "\u7EE7\u7EED\u66F4\u65B0",
                DialogResult = DialogResult.OK,
                AutoSize = true
            };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            layout.Controls.Add(summaryLabel, 0, 0);
            layout.Controls.Add(_treeView, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);
            Controls.Add(layout);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private static string BuildSummary(MirrorDirectoryResult result)
        {
            var counts = result ?? new MirrorDirectoryResult();
            return string.Format(
                "\u65B0\u589E: {0}    \u8986\u76D6: {1}    \u5220\u9664: {2}    \u4FDD\u62A4\u8DF3\u8FC7: {3}",
                counts.CopiedNewCount,
                counts.OverwrittenCount,
                counts.DeletedCount,
                counts.ProtectedSkipCount);
        }
    }
}
