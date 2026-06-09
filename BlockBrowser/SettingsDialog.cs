using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class SettingsDialog : Form
    {
        private readonly TextBox _txtNasLibraryPath;
        private readonly TextBox _txtLocalMirrorPath;
        private readonly ComboBox _cmbLibraryMode;
        private readonly NumericUpDown _numScale;
        private readonly NumericUpDown _numRotation;

        public SettingsDialog(
            string nasLibraryPath,
            string localMirrorPath,
            LibraryMode currentLibraryMode,
            double insertScale,
            double insertRotationDegrees)
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
                RowCount = 4,
                Dock = DockStyle.Fill
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _txtNasLibraryPath = new TextBox();
            var nasPathPanel = CreatePathPanel("NAS 主图库路径:", nasLibraryPath, _txtNasLibraryPath);

            _txtLocalMirrorPath = new TextBox();
            var localPathPanel = CreatePathPanel("本地副本路径:", localMirrorPath, _txtLocalMirrorPath);

            var valuePanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 6,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 18)
            };
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            var lblMode = new Label
            {
                Text = "当前模式:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 6, 0)
            };
            _cmbLibraryMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 100,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 18, 0)
            };
            _cmbLibraryMode.Items.Add(LibraryMode.Auto.ToString());
            _cmbLibraryMode.Items.Add(LibraryMode.Nas.ToString());
            _cmbLibraryMode.Items.Add(LibraryMode.Local.ToString());
            _cmbLibraryMode.SelectedItem = currentLibraryMode.ToString();
            if (_cmbLibraryMode.SelectedIndex < 0) _cmbLibraryMode.SelectedItem = LibraryMode.Auto.ToString();

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
            valuePanel.Controls.Add(lblMode, 0, 0);
            valuePanel.Controls.Add(_cmbLibraryMode, 1, 0);
            valuePanel.Controls.Add(lblScale, 2, 0);
            valuePanel.Controls.Add(_numScale, 3, 0);
            valuePanel.Controls.Add(lblRot, 4, 0);
            valuePanel.Controls.Add(_numRotation, 5, 0);

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

            layout.Controls.Add(nasPathPanel, 0, 0);
            layout.Controls.Add(localPathPanel, 0, 1);
            layout.Controls.Add(valuePanel, 0, 2);
            layout.Controls.Add(buttonPanel, 0, 3);
            Controls.Add(layout);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public string NasLibraryPathValue
        {
            get { return _txtNasLibraryPath.Text; }
        }

        public string LocalMirrorPathValue
        {
            get { return _txtLocalMirrorPath.Text; }
        }

        public LibraryMode CurrentLibraryModeValue
        {
            get
            {
                LibraryMode mode;
                return Enum.TryParse(_cmbLibraryMode.SelectedItem as string, true, out mode)
                    ? mode
                    : LibraryMode.Auto;
            }
        }

        public double InsertScaleValue
        {
            get { return (double)_numScale.Value; }
        }

        public double InsertRotationDegreesValue
        {
            get { return (double)_numRotation.Value; }
        }

        private static TableLayoutPanel CreatePathPanel(string labelText, string pathValue, TextBox textBox)
        {
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

            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 4)
            };
            textBox.Text = pathValue ?? "";
            textBox.Width = 360;
            textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            textBox.Margin = new Padding(0, 0, 8, 0);

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
                    dlg.SelectedPath = textBox.Text;
                    if (dlg.ShowDialog() == DialogResult.OK) textBox.Text = dlg.SelectedPath;
                }
            };

            pathPanel.Controls.Add(label, 0, 0);
            pathPanel.SetColumnSpan(label, 2);
            pathPanel.Controls.Add(textBox, 0, 1);
            pathPanel.Controls.Add(btnBrowse, 1, 1);
            return pathPanel;
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max, decimal fallback)
        {
            if (value < min) return fallback;
            if (value > max) return max;
            return value;
        }
    }
}
