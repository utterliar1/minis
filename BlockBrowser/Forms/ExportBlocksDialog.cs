using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class ExportBlocksDialog : Form
    {
        private readonly List<string> _allBlockNames;
        private readonly TextBox _searchBox;
        private readonly ListBox _blockList;
        private readonly Label _countLabel;
        private readonly ComboBox _categoryBox;

        public IList<string> SelectedBlocks { get; private set; }
        public string SelectedCategory { get; private set; }

        public ExportBlocksDialog(IEnumerable<string> blockNames, IEnumerable<string> categories)
        {
            _allBlockNames = (blockNames ?? new string[0]).OrderBy(n => n).ToList();
            SelectedBlocks = new List<string>();
            SelectedCategory = "";

            Text = "导出块到库";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 7,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var searchLabel = new Label { Text = "搜索:", AutoSize = true, Anchor = AnchorStyles.Left };
            _searchBox = new TextBox { Dock = DockStyle.Fill };

            var blockLabel = new Label { Text = "选择块 (Ctrl/Shift 多选):", AutoSize = true, Anchor = AnchorStyles.Left };
            _blockList = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended,
                IntegralHeight = false
            };

            _countLabel = new Label
            {
                Text = "已选: 0",
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 80, 100),
                Anchor = AnchorStyles.Right
            };

            var categoryLabel = new Label { Text = "分类:", AutoSize = true, Anchor = AnchorStyles.Left };
            _categoryBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            foreach (string category in categories ?? new string[0])
            {
                _categoryBox.Items.Add(category);
            }
            if (_categoryBox.Items.Count > 0) _categoryBox.SelectedIndex = 0;

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Dock = DockStyle.Fill
            };
            var okButton = new Button { Text = "导出", DialogResult = DialogResult.OK, AutoSize = true };
            var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            layout.Controls.Add(searchLabel, 0, 0);
            layout.Controls.Add(_searchBox, 1, 0);
            layout.Controls.Add(blockLabel, 0, 1);
            layout.SetColumnSpan(blockLabel, 2);
            layout.Controls.Add(_blockList, 0, 2);
            layout.SetColumnSpan(_blockList, 2);
            layout.Controls.Add(_countLabel, 0, 3);
            layout.SetColumnSpan(_countLabel, 2);
            layout.Controls.Add(categoryLabel, 0, 4);
            layout.SetColumnSpan(categoryLabel, 2);
            layout.Controls.Add(_categoryBox, 0, 5);
            layout.SetColumnSpan(_categoryBox, 2);
            layout.Controls.Add(buttonPanel, 0, 6);
            layout.SetColumnSpan(buttonPanel, 2);

            Controls.Add(layout);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            _searchBox.TextChanged += (s, e) => ApplyFilter();
            _blockList.SelectedIndexChanged += (s, e) => UpdateSelectedCount();
            FormClosing += OnFormClosing;

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string keyword = (_searchBox.Text ?? "").Trim().ToLowerInvariant();
            _blockList.BeginUpdate();
            try
            {
                _blockList.Items.Clear();
                foreach (string name in _allBlockNames)
                {
                    if (string.IsNullOrEmpty(keyword) || name.ToLowerInvariant().Contains(keyword))
                        _blockList.Items.Add(name);
                }
                if (_blockList.Items.Count > 0) _blockList.SelectedIndex = 0;
            }
            finally
            {
                _blockList.EndUpdate();
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            _countLabel.Text = "已选: " + _blockList.SelectedIndices.Count;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK) return;

            var selected = new List<string>();
            foreach (var item in _blockList.SelectedItems)
            {
                if (item != null) selected.Add(item.ToString());
            }
            SelectedBlocks = selected;
            SelectedCategory = (_categoryBox.Text ?? "").Trim();
        }
    }
}
