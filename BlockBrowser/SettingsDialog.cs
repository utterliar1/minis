using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class SettingsDialog : Form
    {
        private readonly TextBox _txtLibraryPath;
        private readonly NumericUpDown _numScale;
        private readonly NumericUpDown _numRotation;

        public SettingsDialog(string libraryPath, double insertScale, double insertRotationDegrees)
        {
            Text = "块浏览器设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                Dock = DockStyle.Fill
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var pathPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12)
            };
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblPath = new Label
            {
                Text = "块库路径:",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 4)
            };
            _txtLibraryPath = new TextBox
            {
                Text = libraryPath ?? "",
                Width = 360,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 8, 0)
            };
            var btnBrowse = new Button
            {
                Text = "...",
                FlatStyle = FlatStyle.System,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.SelectedPath = _txtLibraryPath.Text;
                    if (dlg.ShowDialog() == DialogResult.OK) _txtLibraryPath.Text = dlg.SelectedPath;
                }
            };
            pathPanel.Controls.Add(lblPath, 0, 0);
            pathPanel.SetColumnSpan(lblPath, 2);
            pathPanel.Controls.Add(_txtLibraryPath, 0, 1);
            pathPanel.Controls.Add(btnBrowse, 1, 1);

            var valuePanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 18)
            };
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            var lblScale = new Label
            {
                Text = "插入比例:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 6, 0)
            };
            _numScale = new NumericUpDown
            {
                Width = 120,
                Minimum = 0.001m,
                Maximum = 10000,
                DecimalPlaces = 3,
                Value = ClampDecimal((decimal)insertScale, 0.001m, 10000m, 1m),
                Increment = 0.1m,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 18, 0)
            };

            var lblRot = new Label
            {
                Text = "旋转角度:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 6, 0)
            };
            _numRotation = new NumericUpDown
            {
                Width = 100,
                Minimum = -360,
                Maximum = 360,
                DecimalPlaces = 1,
                Value = ClampDecimal((decimal)insertRotationDegrees, -360m, 360m, 0m),
                Increment = 5,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0)
            };
            valuePanel.Controls.Add(lblScale, 0, 0);
            valuePanel.Controls.Add(_numScale, 1, 0);
            valuePanel.Controls.Add(lblRot, 2, 0);
            valuePanel.Controls.Add(_numRotation, 3, 0);

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            var btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.System,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(8, 0, 0, 0)
            };
            var btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.System,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(8, 0, 0, 0)
            };
            buttonPanel.Controls.Add(btnCancel);
            buttonPanel.Controls.Add(btnOk);

            layout.Controls.Add(pathPanel, 0, 0);
            layout.Controls.Add(valuePanel, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);
            Controls.Add(layout);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public string LibraryPathValue
        {
            get { return _txtLibraryPath.Text; }
        }

        public double InsertScaleValue
        {
            get { return (double)_numScale.Value; }
        }

        public double InsertRotationDegreesValue
        {
            get { return (double)_numRotation.Value; }
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max, decimal fallback)
        {
            if (value < min) return fallback;
            if (value > max) return max;
            return value;
        }
    }
}
