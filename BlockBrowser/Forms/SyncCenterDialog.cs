using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class SyncCenterDialog : Form
    {
        private readonly Func<SyncPlan> _previewProvider;
        private readonly Func<SyncPlan> _syncRunner;
        private readonly string _logPath;
        private readonly TreeView _treeDetails;
        private readonly Label _lblLogPath;
        private SyncPlan _lastPlan;

        public SyncCenterDialog(Func<SyncPlan> previewProvider, Func<SyncPlan> syncRunner, string logPath)
        {
            _previewProvider = previewProvider;
            _syncRunner = syncRunner;
            _logPath = logPath ?? "";

            Text = "\u540C\u6B65\u4E2D\u5FC3";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei", 9f);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Size = new Size(620, 480);
            MinimumSize = new Size(520, 360);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            _lblLogPath = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _treeDetails = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowNodeToolTips = true
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            var btnClose = new Button { Text = "\u5173\u95ED", Width = 82, Height = 28, DialogResult = DialogResult.Cancel };
            btnClose.Click += (s, e) => Close();

            var btnCopy = new Button { Text = "\u590D\u5236", Width = 82, Height = 28 };
            btnCopy.Click += (s, e) =>
            {
                string report = SyncSummaryMessageService.FormatDetailedReport(_lastPlan);
                if (!string.IsNullOrEmpty(report))
                    Clipboard.SetText(report);
            };

            var btnOpenLog = new Button { Text = "\u6253\u5F00\u65E5\u5FD7", Width = 92, Height = 28 };
            btnOpenLog.Click += (s, e) =>
            {
                if (!File.Exists(_logPath))
                {
                    MessageBox.Show("\u6682\u65E0\u540C\u6B65\u65E5\u5FD7\u3002", "\u5757\u6D4F\u89C8\u5668", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                System.Diagnostics.Process.Start("notepad.exe", _logPath);
            };

            var btnSync = new Button { Text = "\u6267\u884C\u540C\u6B65", Width = 92, Height = 28 };
            btnSync.Click += (s, e) => RunSync();

            var btnRefresh = new Button { Text = "\u5237\u65B0\u9884\u89C8", Width = 92, Height = 28 };
            btnRefresh.Click += (s, e) => RefreshPreview();

            buttons.Controls.Add(btnClose);
            buttons.Controls.Add(btnCopy);
            buttons.Controls.Add(btnOpenLog);
            buttons.Controls.Add(btnSync);
            buttons.Controls.Add(btnRefresh);

            layout.Controls.Add(_lblLogPath, 0, 0);
            layout.Controls.Add(_treeDetails, 0, 1);
            layout.Controls.Add(buttons, 0, 2);
            Controls.Add(layout);

            CancelButton = btnClose;
            Load += (s, e) => RefreshPreview();
        }

        private void RefreshPreview()
        {
            try
            {
                _lblLogPath.Text = "\u65E5\u5FD7: " + _logPath;
                var plan = _previewProvider == null ? null : _previewProvider();
                _lastPlan = plan;
                SyncPlanTreeBuilder.Populate(_treeDetails, plan);
            }
            catch (Exception ex)
            {
                _lastPlan = null;
                _treeDetails.Nodes.Clear();
                _treeDetails.Nodes.Add(new TreeNode("\u540C\u6B65\u9884\u89C8\u5931\u8D25: " + ex.Message));
            }
        }

        private void RunSync()
        {
            try
            {
                var preview = _previewProvider == null ? null : _previewProvider();
                _lastPlan = preview;
                SyncPlanTreeBuilder.Populate(_treeDetails, preview);

                var confirm = MessageBox.Show(
                    "确定按当前同步中心预览执行同步到 NAS？",
                    "\u540C\u6B65\u5230 NAS",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;

                var plan = _syncRunner == null ? null : _syncRunner();
                SyncSummaryMessageService.AppendLog(_logPath, plan);
                _lastPlan = plan;
                SyncPlanTreeBuilder.Populate(_treeDetails, plan);
            }
            catch (Exception ex)
            {
                MessageBox.Show("\u540C\u6B65\u5931\u8D25: " + ex.Message, "\u5757\u6D4F\u89C8\u5668", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
