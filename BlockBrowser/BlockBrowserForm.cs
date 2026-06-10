using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BlockBrowser
{
    // 禁止点击子控件时自动滚动的 FlowLayoutPanel
    class StableFlowPanel : FlowLayoutPanel
    {
        public StableFlowPanel() { DoubleBuffered = true; }
        public new void ScrollControlIntoView(Control activeControl)
        {
            // 不执行自动滚动，保持当前位置
        }
    }

    public class BlockBrowserForm : Form
    {
        private const int MinFormWidth = 700;
        private const int MinFormHeight = 450;

        // 模态模式：用户点了插入后返回OK
        private static string _lastCategory = "全部";

        public BlockInfo SelectedInsertBlock { get; private set; }
        public double InsertScale { get; private set; }
        public double InsertRotation { get; private set; }

        internal string PendingCommand { get; set; }
        private StableFlowPanel _flowBlocks;
        private Panel _catHost;
        private TableLayoutPanel _catLayout;
        private Panel _catViewport;
        private Panel _catActionPanel;
        private FlowLayoutPanel _catBar;
        private Button _btnCreateCategory;
        private HScrollBar _catScrollBar;
        private TextBox _txtSearch;
        private ToolStrip _toolbar;
        private ToolStripControlHost _txtSearchHost;
        private StatusStrip _statusBar;
        private ToolStripStatusLabel _lblStatus;
        private ToolStripStatusLabel _lblCount;

        private BlockInfo _selectedBlock;
        // _allBlocks removed
        private List<BlockThumbnailCard> _cards = new List<BlockThumbnailCard>();
        private string _currentCategory = "全部";
        private static int _savedThumbSize = BlockLibrary.ThumbSize;
        private int _thumbSize = _savedThumbSize;
        private System.Windows.Forms.Timer _searchTimer;
        private System.Windows.Forms.Timer _thumbTimer;
        private int _thumbIndex;
        private Dictionary<string, Image> _thumbCache = new Dictionary<string, Image>();
        
        private Dictionary<string, List<BlockThumbnailCard>> _categoryCards = new Dictionary<string, List<BlockThumbnailCard>>();


        public BlockBrowserForm()
        {
            _currentCategory = _lastCategory;
            InitializeComponent();
            Load += (s, e) => LoadData();
        }

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

            var btnDelete = new ToolStripButton("删除");
            btnDelete.Click += (s, e) => DoDelete();

            var btnRename = new ToolStripButton("重命名");
            btnRename.Click += (s, e) => DoRename();

            var btnRefresh = new ToolStripButton("刷新列表");
            btnRefresh.Click += (s, e) => LoadData();

            var btnAddToLib = new ToolStripButton("添加到库");
            btnAddToLib.Click += BtnAddToLib_Click;

            var btnExportBlock = new ToolStripButton("导出块");
            btnExportBlock.Click += BtnExportBlock_Click;

            var btnOpenFolder = new ToolStripButton("打开文件夹");
            btnOpenFolder.Click += (s, e) =>
            {
                if (Directory.Exists(BlockLibrary.LibraryPath))
                    System.Diagnostics.Process.Start("explorer.exe", BlockLibrary.LibraryPath);
            };

            var btnSettings = new ToolStripButton("设置");
            btnSettings.Click += (s, e) => ShowSettingsDialog();

            var btnPrebuildThumbnails = new ToolStripButton("预生成缩略图");
            btnPrebuildThumbnails.Click += (s, e) => PrebuildVisibleThumbnails();

            var btnRebuildThumbnails = new ToolStripButton("重建缩略图");
            btnRebuildThumbnails.Click += (s, e) => RebuildThumbnails();

            var btnUpdateMirror = new ToolStripButton("更新本地副本");
            btnUpdateMirror.Click += (s, e) =>
            {
                try
                {
                    BlockLibrary.UpdateLocalMirrorFromNas();
                    MessageBox.Show("本地副本已更新。", "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("更新失败: " + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var btnSync = new ToolStripButton("同步到NAS");
            btnSync.Click += (s, e) =>
            {
                try
                {
                    var preview = BlockLibrary.PreviewLocalSync();
                    var confirm = MessageBox.Show(
                        SyncSummaryMessageService.FormatPreviewDialog(preview),
                        "同步到 NAS",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) return;

                    var plan = BlockLibrary.SyncSafeUploadsToNas();
                    SyncSummaryMessageService.AppendLog(BlockLibrary.SyncLogPath, plan);
                    MessageBox.Show(
                        SyncSummaryMessageService.FormatDialog(plan),
                        "块浏览器",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _lblStatus.Text = GetActiveLibraryStatus();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("同步失败: " + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var btnSyncCenter = new ToolStripButton("同步中心");
            btnSyncCenter.Click += (s, e) => ShowSyncCenterDialog();

            var btnManage = new ToolStripDropDownButton("管理");
            btnManage.DropDownItems.AddRange(new ToolStripItem[]
            {
                btnRename,
                btnDelete,
                btnOpenFolder
            });

            var btnLibrary = new ToolStripDropDownButton("图库");
            btnLibrary.DropDownItems.AddRange(new ToolStripItem[]
            {
                btnUpdateMirror,
                btnSyncCenter,
                btnSync,
                new ToolStripSeparator(),
                btnPrebuildThumbnails,
                btnRebuildThumbnails,
                new ToolStripSeparator(),
                btnSettings
            });

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
                btnRefresh, new ToolStripSeparator(),
                btnManage, btnLibrary
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

        private void LoadData()
        {
            try
            {
                _thumbTimer.Stop();
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
                _cards.Clear();
                _flowBlocks.Controls.Clear();

                _lblStatus.Text = "加载中...";
                this.Refresh();

                RefreshCategories();

                LoadBlocks();
            }
            catch (System.Exception ex)
            {
                _lblStatus.Text = "错误: " + ex.Message;
            }
        }

        private void RefreshCategories()
        {
            var categories = BlockLibrary.GetBrowsableCategories();
            _catBar.SuspendLayout();
            _catBar.Controls.Clear();
            foreach (var cat in categories)
            {
                var btn = new Button
                {
                    Text = cat, FlatStyle = FlatStyle.System, AutoSize = true,
                    MinimumSize = new Size(0, 24), Padding = new Padding(8, 1, 8, 1),
                    Margin = new Padding(2, 0, 2, 0), Font = new Font("Microsoft YaHei", 9f),
                    Tag = cat, Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 1;
                SetCatBtnStyle(btn, cat == _currentCategory);
                btn.Click += (s, e) =>
                {
                    _currentCategory = (string)((Button)s).Tag;
                    _lastCategory = _currentCategory;
                    LoadBlocks();
                    UpdateCatHighlight();
                };
                _catBar.Controls.Add(btn);
            }
            _catBar.ResumeLayout();
            UpdateCategoryScroll();
        }

        private void SetCatBtnStyle(Button btn, bool active)
        {
            btn.FlatStyle = FlatStyle.System;
            // System style uses native rendering; use font bold for active highlight
            btn.Font = new Font("Microsoft YaHei", 9f, active ? FontStyle.Bold : FontStyle.Regular);
        }

        private void UpdateCatHighlight()
        {
            foreach (Control c in _catBar.Controls)
            {
                var btn = c as Button;
                if (btn != null) SetCatBtnStyle(btn, (string)btn.Tag == _currentCategory);
            }
        }

        private void UpdateCategoryScroll()
        {
            if (_catViewport == null || _catBar == null || _catScrollBar == null) return;

            _catBar.PerformLayout();
            int viewportWidth = Math.Max(0, _catViewport.ClientSize.Width);
            int viewportHeight = Math.Max(1, _catViewport.ClientSize.Height);
            int contentWidth = Math.Max(viewportWidth, _catBar.PreferredSize.Width);
            bool needsScroll = contentWidth > viewportWidth;

            _catBar.Height = viewportHeight;
            _catBar.Width = contentWidth;
            _catHost.Height = needsScroll ? 44 + 8 + SystemInformation.HorizontalScrollBarHeight : 44;
            _catLayout.RowStyles[1].Height = needsScroll ? 8 : 0;
            _catLayout.RowStyles[2].Height = needsScroll ? SystemInformation.HorizontalScrollBarHeight : 0;

            if (_catScrollBar.Visible != needsScroll)
            {
                _catScrollBar.Visible = needsScroll;
            }

            if (!needsScroll)
            {
                _catBar.Left = 0;
                _catScrollBar.Value = 0;
                _catScrollBar.Maximum = 0;
                return;
            }

            int maxValue = Math.Max(0, contentWidth - viewportWidth);
            _catScrollBar.SmallChange = 32;
            _catScrollBar.LargeChange = Math.Max(1, viewportWidth);
            _catScrollBar.Maximum = maxValue + _catScrollBar.LargeChange - 1;
            if (_catScrollBar.Value > maxValue) _catScrollBar.Value = maxValue;
            _catBar.Left = -_catScrollBar.Value;
        }

        private void LoadBlocks()
        {
            _searchTimer.Stop();
            if (_txtSearch.Text.Length > 0) _txtSearch.Text = "";
            ShowBlocks(BlockLibrary.GetBlocks(_currentCategory));
        }

        private void ShowBlocks(List<BlockInfo> blocks)
        {
            _thumbTimer.Stop();
            _selectedBlock = null;
            _flowBlocks.SuspendLayout();
            _flowBlocks.Controls.Clear();
            _cards.Clear();

            string catKey = _currentCategory ?? "全部";
            if (!_categoryCards.ContainsKey(catKey))
            {
                // 首次访问该分类：创建卡片并缓存
                var newCards = new List<BlockThumbnailCard>();
                foreach (var block in blocks)
                {
                    var card = new BlockThumbnailCard(block, _thumbSize);
                    card.BlockClicked += (s, b) =>
                    {
                        int sy = 0;
                        try { sy = _flowBlocks.VerticalScroll.Value; } catch { }
                        _flowBlocks.SuspendLayout();
                        _selectedBlock = b;
                        foreach (var c2 in _cards)
                        {
                            if (c2.IsSelected && c2 != s) { c2.IsSelected = false; break; }
                        }
                        ((BlockThumbnailCard)s).IsSelected = true;
                        ShowBlockInfo(b);
                        _flowBlocks.ResumeLayout(false);
                        try { if (sy > 0 && _flowBlocks.VerticalScroll.Visible) _flowBlocks.VerticalScroll.Value = Math.Min(sy, _flowBlocks.VerticalScroll.Maximum); } catch { }
                    };
                    card.BlockDoubleClicked += (s, b) => { _selectedBlock = b; DoInsert(); };
                    string ck = ThumbnailMemoryCacheService.GetKey(block.FilePath, _thumbSize);
                    if (_thumbCache.ContainsKey(ck) && _thumbCache[ck] != null)
                    {
                        try { card.LoadThumbnail(_thumbCache[ck]); }
                        catch { _thumbCache.Remove(ck); }
                    }
                    newCards.Add(card);
                }
                _categoryCards[catKey] = newCards;
            }

            // 从缓存取出卡片显示
            var cached = _categoryCards[catKey];
            _cards.AddRange(cached);
            foreach (var card in _cards) { card.IsSelected = false; _flowBlocks.Controls.Add(card); }
            _flowBlocks.ResumeLayout();

            _lblCount.Text = BlockFilterService.FormatCount(_cards.Count);
            _lblStatus.Text = GetActiveLibraryStatus();

            QueueVisibleMissingThumbnails();
        }

        private List<BlockThumbnailCard> _pendingThumbCards = new List<BlockThumbnailCard>();
        private int _failCount;

        private bool HasThumbnail(BlockThumbnailCard card)
        {
            return ThumbnailMemoryCacheService.HasValue(_thumbCache, card.Block.FilePath, _thumbSize);
        }

        private void QueueVisibleMissingThumbnails()
        {
            _thumbTimer.Stop();
            var needLoad = _cards.Where(c => c.Visible && !HasThumbnail(c)).ToList();
            _failCount = 0;
            _pendingThumbCards = needLoad;
            _thumbIndex = 0;
            if (needLoad.Count > 0) _thumbTimer.Start();
        }

        // Load thumbnails one by one on UI thread (safe for GstarCAD API)
        private void ThumbTimerTick(object sender, EventArgs e)
        {
            for (int i = 0; i < ThumbnailLoadProgressService.DefaultBatchSize; i++)
            {
                if (ThumbnailLoadProgressService.IsComplete(_thumbIndex, _pendingThumbCards.Count))
                {
                    _thumbTimer.Stop();
                    _lblStatus.Text = GetActiveLibraryStatus();
                    return;
                }
                var card = _pendingThumbCards[_thumbIndex];
                _thumbIndex++;
                try
                {
                    if (card.IsDisposed) continue;
                    var img = BlockLibrary.GetThumbnail(card.Block, _thumbSize);
                    if (img != null)
                    {
                        string ck = ThumbnailMemoryCacheService.GetKey(card.Block.FilePath, _thumbSize);
                        if (!_thumbCache.ContainsKey(ck))
                            _thumbCache[ck] = img;
                        else
                            img.Dispose();
                        card.LoadThumbnail(new Bitmap(_thumbCache[ck]));
                    }
                }
                catch { _failCount++; }
            }
            if (!ThumbnailLoadProgressService.IsComplete(_thumbIndex, _pendingThumbCards.Count))
                _lblStatus.Text = ThumbnailLoadProgressService.FormatLoadingStatus(_thumbIndex, _pendingThumbCards.Count);
            else if (_failCount > 0)
                _lblStatus.Text = ThumbnailLoadProgressService.FormatFailedReadyStatus(_failCount);
        }

        private void RefreshCards()
        {
            _thumbTimer.Stop();
            // 清除内存缓存，因为尺寸变了
            ResourceDisposalService.DisposeDictionaryValuesAndClear(_thumbCache);
            // 清除分类卡片缓存，重建卡片
            ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
            _cards.Clear();
            _flowBlocks.Controls.Clear();
            ShowBlocks(BlockLibrary.GetBlocks(_currentCategory));
        }

        private void PrebuildVisibleThumbnails()
        {
            var needLoad = _cards.Where(c => c.Visible && !HasThumbnail(c)).ToList();
            if (needLoad.Count == 0)
            {
                _lblStatus.Text = "当前列表缩略图已就绪。";
                return;
            }

            _failCount = 0;
            _pendingThumbCards = needLoad;
            _thumbIndex = 0;
            _thumbTimer.Start();
            _lblStatus.Text = ThumbnailLoadProgressService.FormatLoadingStatus(0, needLoad.Count);
        }

        private void RebuildThumbnails()
        {
            var dr = MessageBox.Show(
                "这会清空缩略图缓存，并重新生成当前列表的缩略图。是否继续？",
                "重建缩略图",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            try
            {
                _thumbTimer.Stop();
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_thumbCache);
                string cachePath = BlockLibrary.ThumbnailCachePath;
                if (Directory.Exists(cachePath)) Directory.Delete(cachePath, true);
                RefreshCards();
                _lblStatus.Text = "缩略图缓存已重建。";
            }
            catch (Exception ex)
            {
                MessageBox.Show("重建缩略图失败: " + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoFilter()
        {
            string kw = _txtSearch.Text;
            int visible = 0;
            int sy = 0;
            try { sy = _flowBlocks.VerticalScroll.Value; } catch { }
            _flowBlocks.SuspendLayout();
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.IsDisposed) continue;
                bool match = BlockFilterService.Matches(card.Block, kw);
                card.Visible = match;
                if (match) visible++;
            }
            _flowBlocks.ResumeLayout();
            try { if (sy > 0 && _flowBlocks.VerticalScroll.Visible) _flowBlocks.VerticalScroll.Value = Math.Min(sy, _flowBlocks.VerticalScroll.Maximum); } catch { }
            _lblCount.Text = BlockFilterService.FormatCount(visible);
            QueueVisibleMissingThumbnails();
        }

        private void ShowSyncCenterDialog()
        {
            using (var dlg = new SyncCenterDialog(
                () => BlockLibrary.PreviewLocalSync(),
                () => BlockLibrary.SyncSafeUploadsToNas(),
                BlockLibrary.SyncLogPath))
            {
                dlg.ShowDialog(this);
                _lblStatus.Text = GetActiveLibraryStatus();
            }
        }

        private void DoInsert()
        {
            if (_selectedBlock == null) { MessageBox.Show("请先选择一个块。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            SelectedInsertBlock = _selectedBlock;
            InsertScale = BlockLibrary.InsertScale;
            InsertRotation = BlockLibrary.InsertRotation;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void DoDelete()
        {
            if (_selectedBlock == null) { MessageBox.Show("请先选择一个块。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var dr = MessageBox.Show("确定删除此块文件？\n" + _selectedBlock.Name + "\n\n文件: " + _selectedBlock.FilePath, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;
            try
            {
                var plan = BlockDeletePlanService.CreatePlan(
                    _selectedBlock,
                    BlockLibrary.ActiveLibrary,
                    File.Exists,
                    BlockFileOperations.CanOpenForExclusiveWrite);
                string filePath = plan.FilePath;
                string name = plan.BlockName;
                if (plan.Action == BlockDeleteAction.MissingFile) { MessageBox.Show("文件不存在: " + filePath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (plan.Action == BlockDeleteAction.RecordLocalDeleteRequest)
                {
                    BlockLibrary.RecordLocalChange(LocalChangeAction.DeleteRequest, BlockLibrary.ToLibraryRelativePath(filePath), "", null);
                    MessageBox.Show("已记录删除请求。回到 NAS 后请在同步界面确认删除。", "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (plan.Action == BlockDeleteAction.FileLocked) { MessageBox.Show("文件被占用，请关闭CAD中打开的此文件后重试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                // Delete thumbnail cache
                BlockLibrary.RefreshThumbnail(_selectedBlock);
                // Delete the DWG file (移到回收站)
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(filePath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                catch
                {
                    File.Delete(filePath);
                }
                // Remove from UI
                BlockThumbnailCard cardToRemove = CardCacheService.FindByPath(_cards, filePath) as BlockThumbnailCard;
                if (cardToRemove != null)
                {
                    _flowBlocks.Controls.Remove(cardToRemove);
                    _cards.Remove(cardToRemove);
                    cardToRemove.Dispose();
                }
                // Remove from category cache
                BlockThumbnailCard cached = CardCacheService.RemoveFirstByPath(_categoryCards, filePath);
                if (cached != null) cached.Dispose();
                // Remove from memory cache (remove all size variants)
                var keysToRemove = ThumbnailMemoryCacheService.FindKeysForPath(_thumbCache.Keys, filePath);
                foreach (var k in keysToRemove) { ResourceDisposalService.DisposeQuietly(_thumbCache[k]); _thumbCache.Remove(k); }
                // 从最近列表中移除
                BlockLibrary.RemoveRecentBlock(filePath);
                _selectedBlock = null;
                _lblStatus.Text = "已删除: " + name;
                int visible = CardCacheService.CountVisible(_cards);
                _lblCount.Text = BlockFilterService.FormatCount(visible);
            }
            catch (System.Exception ex) { MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void DoRename()
        {
            if (_selectedBlock == null) { MessageBox.Show("请先选择一个块。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string oldName = _selectedBlock.Name;
            string newName = ShowInputDialog("输入新名称:", oldName);
            var plan = BlockRenamePlanService.CreatePlan(_selectedBlock, newName, File.Exists);
            if (plan.Action == BlockRenameAction.Cancel) return;
            if (plan.Action == BlockRenameAction.InvalidName)
            {
                MessageBox.Show("名称为空或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (plan.Action == BlockRenameAction.TargetExists) { MessageBox.Show("同名文件已存在: " + plan.NewName, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                // Rename via BlockLibrary (handles file move + cache rename)
                if (!BlockLibrary.RenameBlock(_selectedBlock, plan.NewName))
                {
                    MessageBox.Show("重命名失败。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                // Update memory cache key (oldPath_size -> newPath_size)
                ThumbnailMemoryCacheService.MovePathEntries(_thumbCache, plan.OldPath, plan.NewPath);
                _lblStatus.Text = "已重命名: " + plan.OldName + " -> " + plan.NewName;
                RefreshCards();
            }
            catch (System.Exception ex) { MessageBox.Show("重命名失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        internal string PendingCategory;
        internal string PendingBlockName;

        private void BtnCreateCategory_Click(object sender, EventArgs e)
        {
            string category = ShowInputDialog("输入新分类名称:");
            if (string.IsNullOrEmpty(category)) return;

            try
            {
                var result = BlockLibrary.CreateCategory(category);
                if (!result.IsValid)
                {
                    MessageBox.Show("分类名称为空或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                RefreshCategories();
                _lblStatus.Text = result.Created
                    ? "已创建分类: " + result.Category
                    : "分类已存在: " + result.Category;
            }
            catch (Exception ex)
            {
                MessageBox.Show("创建分类失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddToLib_Click(object sender, EventArgs e)
        {
            var categories = CategorySelectionService.GetUserCategories(BlockLibrary.GetCategories());
            string category = ShowCategoryDialog("选择分类", categories);
            if (category == null) return;
            string name = ShowInputDialog("输入块名称:");
            var plan = AddToLibraryRequestService.CreatePlan(category, name, BlockLibrary.IsSafeLibraryName);
            if (plan.Action == AddToLibraryRequestAction.Cancel) return;
            if (plan.Action == AddToLibraryRequestAction.InvalidName)
            {
                MessageBox.Show("分类或名称为空，或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            PendingCategory = plan.Category;
            PendingBlockName = plan.BlockName;
            PendingCommand = plan.PendingCommand;
            this.DialogResult = DialogResult.Abort;
            this.Close();
        }

        private void BtnExportBlock_Click(object sender, EventArgs e)
        {
            PendingCommand = "BBEXPORT";
            this.DialogResult = DialogResult.Abort;
            this.Close();
        }

        private void ShowBlockInfo(BlockInfo block)
        {
            _lblStatus.Text = BlockInfoStatusService.Format(block);
        }

        private string ShowInputDialog(string prompt)
        {
            return ShowInputDialog(prompt, "");
        }

        private string ShowInputDialog(string prompt, string defaultValue)
        {
            using (var form = TextPromptDialog.ForTextInput(prompt, defaultValue))
            {
                return form.ShowDialog(this) == DialogResult.OK ? form.Value : null;
            }
        }

        private string ShowCategoryDialog(string title, List<string> categories)
        {
            using (var form = TextPromptDialog.ForComboInput(title, categories))
            {
                return form.ShowDialog(this) == DialogResult.OK ? form.Value : null;
            }
        }

        private void ShowSettingsDialog()
        {
            using (var form = new SettingsDialog(
                BlockLibrary.NasLibraryPath,
                BlockLibrary.LocalMirrorPath,
                BlockLibrary.CurrentLibraryMode,
                BlockLibrary.InsertScale,
                BlockLibrary.InsertRotation * 180.0 / Math.PI))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    var plan = SettingsUpdateService.CreatePlan(
                        BlockLibrary.NasLibraryPath,
                        form.NasLibraryPathValue,
                        BlockLibrary.LocalMirrorPath,
                        form.LocalMirrorPathValue,
                        BlockLibrary.CurrentLibraryMode,
                        form.CurrentLibraryModeValue,
                        form.InsertScaleValue,
                        form.InsertRotationDegreesValue,
                        Directory.Exists);

                    if (!plan.IsValid) { MessageBox.Show("NAS 主图库路径和本地副本路径不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    if (plan.RequiresLocalMirrorDirectoryCreation)
                    {
                        var dr = MessageBox.Show("本地副本目录不存在，是否创建？\n" + plan.LocalMirrorPath, "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr == DialogResult.Yes) { try { Directory.CreateDirectory(plan.LocalMirrorPath); } catch (Exception ex) { MessageBox.Show("创建失败: " + ex.Message); return; } }
                        else return;
                    }
                    BlockLibrary.InsertScale = plan.InsertScale;
                    BlockLibrary.InsertRotation = plan.InsertRotationRadians;
                    if (plan.NasLibraryPathChanged || plan.LocalMirrorPathChanged || plan.CurrentLibraryModeChanged)
                    {
                        BlockLibrary.NasLibraryPath = plan.NasLibraryPath;
                        BlockLibrary.LocalMirrorPath = plan.LocalMirrorPath;
                        BlockLibrary.CurrentLibraryMode = plan.CurrentLibraryMode;
                        BlockLibrary.RefreshActiveLibrary();
                        ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
                        LoadData();
                    }
                    BlockLibrary.SaveConfig();
                }
            }
        }

        private void ShowInsertSettingsDialog()
        {
            using (var form = new InsertSettingsDialog(
                BlockLibrary.InsertScale,
                BlockLibrary.InsertRotation * 180.0 / Math.PI))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    BlockLibrary.InsertScale = form.InsertScaleValue;
                    BlockLibrary.InsertRotation = form.InsertRotationDegreesValue * Math.PI / 180.0;
                    BlockLibrary.SaveConfig();
                    _lblStatus.Text = "插入设置已更新。";
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_searchTimer != null) _searchTimer.Dispose();
                if (_thumbTimer != null) _thumbTimer.Dispose();
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_thumbCache);
                ResourceDisposalService.DisposeAll(_cards);
            }
            if (this.WindowState == FormWindowState.Normal)
            {
                BlockLibrary.FormWidth = this.Width;
                BlockLibrary.FormHeight = this.Height;
                BlockLibrary.SaveConfig();
            }
            base.Dispose(disposing);
        }
    }

}
