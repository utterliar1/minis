using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class StatusDiagnosticsDialog : Form
    {
        private readonly TextBox _txtReport;

        public StatusDiagnosticsDialog(string report)
        {
            Text = "状态诊断";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei", 9f);
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            Size = new Size(680, 520);
            MinimumSize = new Size(560, 380);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            _txtReport = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Text = report ?? ""
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            var btnClose = new Button { Text = "关闭", Width = 82, Height = 28 };
            btnClose.Click += (s, e) => Close();

            var btnCopy = new Button { Text = "复制", Width = 82, Height = 28 };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_txtReport.Text))
                    Clipboard.SetText(_txtReport.Text);
            };

            buttons.Controls.Add(btnClose);
            buttons.Controls.Add(btnCopy);
            layout.Controls.Add(_txtReport, 0, 0);
            layout.Controls.Add(buttons, 0, 1);
            Controls.Add(layout);
        }
    }
}
