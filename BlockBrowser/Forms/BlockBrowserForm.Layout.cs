using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BlockBrowser
{
    public partial class BlockBrowserForm
    {
        private void InitializeComponent()
        {
            Text = "块浏览器 - " + BlockLibrary.PlatformName;
            Size = GetInitialFormSize();
            MinimumSize = new Size(MinFormWidth, MinFormHeight);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 245, 248);
            AutoScaleMode = AutoScaleMode.Dpi;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei", 9f);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
            Resize += (s, e) => UpdateSearchBoxWidth();

            // Timers
            _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); DoFilter(); };
            _thumbTimer = new System.Windows.Forms.Timer { Interval = 10 };
            _thumbTimer.Tick += ThumbTimerTick;

            // Toolbar
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4, 2, 4, 2) };

            var btnInsert = new ToolStripButton("插入");
            btnInsert.Click += (s, e) => DoInsert();

            var btnInsertSettings = new ToolStripButton("插入设置");
            btnInsertSettings.Click += (s, e) => ShowInsertSettingsDialog();

            var btnRefresh = new ToolStripButton("刷新列表");
            btnRefresh.Click += (s, e) => LoadData();

            var btnAddToLib = new ToolStripButton("添加到库");
            btnAddToLib.Click += BtnAddToLib_Click;

            var btnExportBlock = new ToolStripButton("导出块");
            btnExportBlock.Click += BtnExportBlock_Click;

            var btnOpenFolder = new ToolStripMenuItem("打开文件夹");
            btnOpenFolder.Click += (s, e) =>
            {
                if (Directory.Exists(BlockLibrary.LibraryPath))
                    System.Diagnostics.Process.Start("explorer.exe", BlockLibrary.LibraryPath);
            };

            var btnSettings = new ToolStripMenuItem("设置");
            btnSettings.Click += (s, e) => ShowSettingsDialog();

            var btnPrebuildThumbnails = new ToolStripMenuItem("补全缩略图");
            btnPrebuildThumbnails.Click += (s, e) => PrebuildVisibleThumbnails();

            var btnRebuildThumbnails = new ToolStripMenuItem("重建缩略图");
            btnRebuildThumbnails.Click += (s, e) => RebuildThumbnails();

            var btnStatusDiagnostics = new ToolStripMenuItem("状态诊断");
            btnStatusDiagnostics.Click += (s, e) => ShowStatusDiagnosticsDialog();

            var btnUpdateLocalLibrary = new ToolStripButton("更新本地图库");
            btnUpdateLocalLibrary.Click += (s, e) =>
            {
                try
                {
                    var preview = BlockLibrary.PreviewLocalMirrorFromNas();
                    using (var previewDialog = new MirrorPreviewDialog(preview))
                    {
                        if (previewDialog.ShowDialog(this) != DialogResult.OK)
                            return;
                    }
                    var result = BlockLibrary.UpdateLocalMirrorFromNas();
                    MessageBox.Show(MirrorSummaryMessageService.FormatDialog(result), "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("更新本地图库失败: " + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var btnSyncCenter = new ToolStripMenuItem("同步中心");
            btnSyncCenter.Click += (s, e) => ShowSyncCenterDialog();

            var btnLibrary = new ToolStripDropDownButton("图库");
            if (BlockLibrary.AllowNasSync)
            {
                btnLibrary.DropDownItems.Add(btnSyncCenter);
                btnLibrary.DropDownItems.Add(new ToolStripSeparator());
            }
            btnLibrary.DropDownItems.Add(btnPrebuildThumbnails);
            btnLibrary.DropDownItems.Add(btnRebuildThumbnails);
            btnLibrary.DropDownItems.Add(new ToolStripSeparator());
            btnLibrary.DropDownItems.Add(btnStatusDiagnostics);
            btnLibrary.DropDownItems.Add(btnOpenFolder);
            btnLibrary.DropDownItems.Add(btnSettings);

            // Search box - wide, with explicit MinimumSize
            _txtSearch = new TextBox { Width = 140, BorderStyle = BorderStyle.FixedSingle };
            _txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { _txtSearch.Text = ""; e.SuppressKeyPress = true; } };
            _txtSearchHost = new ToolStripControlHost(_txtSearch) { AutoSize = false, Width = GetSearchBoxWidth() };

            var lblSearch = new ToolStripLabel("搜索:");

            var lblSize = new ToolStripLabel("大小:");
            var cmbThumbSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 65 };
            cmbThumbSize.Items.AddRange(new object[] { "小", "中", "大", "特大" });
            int[] sizes = { 80, 128, 180, 256 };
            int savedIdx = Array.IndexOf(sizes, _savedThumbSize);
            cmbThumbSize.SelectedIndex = savedIdx >= 0 ? savedIdx : 1;
            cmbThumbSize.SelectedIndexChanged += (s, e) =>
            {
                _thumbSize = sizes[cmbThumbSize.SelectedIndex];
                _savedThumbSize = _thumbSize; BlockLibrary.ThumbSize = _thumbSize; BlockLibrary.SaveConfig();
                // 释放旧尺寸的缩略图缓存
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_thumbCache);
                RefreshCards();
            };
            var cmbHost = new ToolStripControlHost(cmbThumbSize);

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                lblSearch, _txtSearchHost, new ToolStripSeparator(),
                lblSize, cmbHost, new ToolStripSeparator(),
                btnInsert, btnInsertSettings, new ToolStripSeparator(),
                btnAddToLib, btnExportBlock, new ToolStripSeparator(),
                btnRefresh, btnUpdateLocalLibrary, new ToolStripSeparator(),
                btnLibrary
            });

            // Category bar - compact top panel
            _catHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(235, 238, 242),
                Padding = new Padding(0)
            };

            _catLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _catLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _catLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            _catLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            _catLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            _catLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

            _btnCreateCategory = new Button
            {
                Text = "+",
                Dock = DockStyle.None,
                Size = new Size(28, 24),
                Location = new Point(4, 10),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = Color.FromArgb(235, 238, 242),
                ForeColor = Color.FromArgb(35, 42, 52),
                Font = new Font("Microsoft YaHei", 11f, FontStyle.Bold),
                Margin = new Padding(0),
                Padding = new Padding(0),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _btnCreateCategory.FlatAppearance.BorderSize = 0;
            _btnCreateCategory.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 229, 236);
            _btnCreateCategory.FlatAppearance.MouseDownBackColor = Color.FromArgb(212, 218, 226);
            _btnCreateCategory.Click += BtnCreateCategory_Click;

            _catActionPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _catActionPanel.Controls.Add(_btnCreateCategory);

            _catViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _catViewport.Resize += (s, e) => UpdateCategoryScroll();

            _catBar = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                Location = new Point(0, 0),
                Height = 44,
                Padding = new Padding(10, 4, 10, 8),
                BackColor = Color.FromArgb(235, 238, 242),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, AutoScroll = false
            };
            _catScrollBar = new HScrollBar
            {
                Dock = DockStyle.Fill,
                Visible = false,
                SmallChange = 24,
                LargeChange = 120,
                Margin = new Padding(0)
            };
            _catScrollBar.ValueChanged += (s, e) => { _catBar.Left = -_catScrollBar.Value; };

            var scrollGap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            var actionGap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            var scrollSpacer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(235, 238, 242),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _catViewport.Controls.Add(_catBar);
            _catLayout.Controls.Add(_catViewport, 0, 0);
            _catLayout.Controls.Add(scrollGap, 0, 1);
            _catLayout.Controls.Add(_catScrollBar, 0, 2);
            _catLayout.Controls.Add(_catActionPanel, 1, 0);
            _catLayout.Controls.Add(actionGap, 1, 1);
            _catLayout.Controls.Add(scrollSpacer, 1, 2);
            _catHost.Controls.Add(_catLayout);

            // Thumbnail grid - fills remaining space
            _flowBlocks = new StableFlowPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(248, 249, 252)
            };

            // Status bar
            _statusBar = new StatusStrip();
            var lblAuthor = new ToolStripLabel("v" + BlockLibrary.AppVersion + " | WLUP") { ForeColor = Color.FromArgb(130, 130, 140) };
            _lblStatus = new ToolStripStatusLabel(GetActiveLibraryStatus()) { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _lblCount = new ToolStripStatusLabel("0") { TextAlign = ContentAlignment.MiddleRight };
            _statusBar.Items.AddRange(new ToolStripItem[] { lblAuthor, _lblStatus, _lblCount });

            // Context menu
            var ctx = new ContextMenuStrip();
            ctx.Items.Add("插入", null, (s, e) => DoInsert());
            ctx.Items.Add("复制名称", null, (s, e) => { if (_selectedBlock != null) Clipboard.SetText(_selectedBlock.Name); });
            ctx.Items.Add("删除", null, (s, e) => DoDelete());
            ctx.Items.Add("重命名", null, (s, e) => DoRename());
            _flowBlocks.ContextMenuStrip = ctx;

            Controls.Add(_flowBlocks);
            Controls.Add(_catHost);
            Controls.Add(_toolbar);
            Controls.Add(_statusBar);
        }

        private Size GetInitialFormSize()
        {
            int width = BlockLibrary.FormWidth;
            int height = BlockLibrary.FormHeight;
            try
            {
                Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
                width = Math.Min(width, Math.Max(MinFormWidth, work.Width - 40));
                height = Math.Min(height, Math.Max(MinFormHeight, work.Height - 60));
            }
            catch { }

            return new Size(Math.Max(MinFormWidth, width), Math.Max(MinFormHeight, height));
        }

        private int GetSearchBoxWidth()
        {
            int width = ClientSize.Width > 0 ? ClientSize.Width : BlockLibrary.FormWidth;
            if (width <= 760) return 100;
            if (width <= 900) return 120;
            return 140;
        }

        private void UpdateSearchBoxWidth()
        {
            if (_txtSearch == null || _txtSearchHost == null) return;

            int width = GetSearchBoxWidth();
            _txtSearch.Width = width;
            _txtSearchHost.Width = width;
        }

        private string GetActiveLibraryStatus()
        {
            return ActiveLibraryStatusService.Format(BlockLibrary.ActiveLibrary);
        }
    }
}
