using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class SettingsDialog : Form
    {
        private readonly TextBox _txtNasLibraryPath;
        private readonly TextBox _txtLocalMirrorPath;
        private readonly TextBox _txtProtectedLocalCategories;
        private readonly ComboBox _cmbLibraryMode;

        public SettingsDialog(
            string nasLibraryPath,
            string localMirrorPath,
            string protectedLocalCategories,
            LibraryMode currentLibraryMode)
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
                RowCount = 2,
                Dock = DockStyle.Fill
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _txtNasLibraryPath = new TextBox();
            _txtLocalMirrorPath = new TextBox();
            _txtProtectedLocalCategories = new TextBox();

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
                RowCount = 4,
                Dock = DockStyle.Fill
            };
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pathPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pathPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pathPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            AddPathRow(pathPanel, 0, "NAS 主图库:", nasLibraryPath, _txtNasLibraryPath);
            AddPathRow(pathPanel, 1, "本地图库:", localMirrorPath, _txtLocalMirrorPath);
            AddTextRow(pathPanel, 2, "保护分类:", protectedLocalCategories, _txtProtectedLocalCategories);

            _cmbLibraryMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 128,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 8, 8)
            };
            _cmbLibraryMode.Items.Add(LibraryMode.Local.ToString());
            _cmbLibraryMode.Items.Add(LibraryMode.Auto.ToString());
            _cmbLibraryMode.Items.Add(LibraryMode.Nas.ToString());
            _cmbLibraryMode.SelectedItem = currentLibraryMode.ToString();
            if (_cmbLibraryMode.SelectedIndex < 0) _cmbLibraryMode.SelectedItem = LibraryMode.Local.ToString();

            AddModeRow(pathPanel, 3, "当前模式:", _cmbLibraryMode);
            libraryGroup.Controls.Add(pathPanel);

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
            layout.Controls.Add(buttonPanel, 0, 1);
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

        public string ProtectedLocalCategoriesValue
        {
            get { return _txtProtectedLocalCategories.Text; }
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

        private static void AddTextRow(TableLayoutPanel pathPanel, int row, string labelText, string textValue, TextBox textBox)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 8, 8)
            };
            textBox.Text = textValue ?? "";
            textBox.Width = 520;
            textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            textBox.Margin = new Padding(0, 0, 8, 8);

            pathPanel.Controls.Add(label, 0, row);
            pathPanel.Controls.Add(textBox, 1, row);
        }

        private static void AddModeRow(TableLayoutPanel pathPanel, int row, string labelText, ComboBox comboBox)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 8, 8)
            };
            pathPanel.Controls.Add(label, 0, row);
            pathPanel.Controls.Add(comboBox, 1, row);
        }
    }
}
