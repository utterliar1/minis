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
                RowCount = 3,
                Dock = DockStyle.Fill
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _txtNasLibraryPath = new TextBox();
            _txtLocalMirrorPath = new TextBox();

            var libraryGroup = new GroupBox
            {
                Text = "图库位置",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 12),
                Margin = new Padding(0, 0, 0, 12)
            };
            var pathPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 2,
                Dock = DockStyle.Fill
            };
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pathPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            AddPathRow(pathPanel, 0, "NAS 主图库:", nasLibraryPath, _txtNasLibraryPath);
            AddPathRow(pathPanel, 1, "本地副本:", localMirrorPath, _txtLocalMirrorPath);
            libraryGroup.Controls.Add(pathPanel);

            var insertGroup = new GroupBox
            {
                Text = "插入设置",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 12),
                Margin = new Padding(0, 0, 0, 14)
            };
            var valuePanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 6,
                Dock = DockStyle.Fill
            };
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            _cmbLibraryMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 128,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 22, 0)
            };
            _cmbLibraryMode.Items.Add(LibraryMode.Local.ToString());
            _cmbLibraryMode.Items.Add(LibraryMode.Auto.ToString());
            _cmbLibraryMode.Items.Add(LibraryMode.Nas.ToString());
            _cmbLibraryMode.SelectedItem = currentLibraryMode.ToString();
            if (_cmbLibraryMode.SelectedIndex < 0) _cmbLibraryMode.SelectedItem = LibraryMode.Local.ToString();

            _numScale = new NumericUpDown
            {
                Width = 120,
                Minimum = 0.001m,
                Maximum = 10000,
                DecimalPlaces = 3,
                Value = ClampDecimal((decimal)insertScale, 0.001m, 10000m, 1m),
                Increment = 0.1m,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 22, 0)
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

            AddValueLabel(valuePanel, 0, "当前模式:");
            valuePanel.Controls.Add(_cmbLibraryMode, 1, 0);
            AddValueLabel(valuePanel, 2, "插入比例:");
            valuePanel.Controls.Add(_numScale, 3, 0);
            AddValueLabel(valuePanel, 4, "旋转角度:");
            valuePanel.Controls.Add(_numRotation, 5, 0);
            insertGroup.Controls.Add(valuePanel);

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

            layout.Controls.Add(libraryGroup, 0, 0);
            layout.Controls.Add(insertGroup, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);
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
                    : LibraryMode.Local;
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

        private static void AddPathRow(TableLayoutPanel pathPanel, int row, string labelText, string pathValue, TextBox textBox)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 8, 8)
            };
            textBox.Text = pathValue ?? "";
            textBox.Width = 520;
            textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            textBox.Margin = new Padding(0, 0, 8, 8);

            var btnBrowse = new Button
            {
                Text = "...",
                FlatStyle = FlatStyle.System,
                Width = 44,
                Height = textBox.PreferredHeight + 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.SelectedPath = textBox.Text;
                    if (dlg.ShowDialog() == DialogResult.OK) textBox.Text = dlg.SelectedPath;
                }
            };

            pathPanel.Controls.Add(label, 0, row);
            pathPanel.Controls.Add(textBox, 1, row);
            pathPanel.Controls.Add(btnBrowse, 2, row);
        }

        private static void AddValueLabel(TableLayoutPanel valuePanel, int column, string text)
        {
            valuePanel.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 8, 0)
            }, column, 0);
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max, decimal fallback)
        {
            if (value < min) return fallback;
            if (value > max) return max;
            return value;
        }
    }
}
