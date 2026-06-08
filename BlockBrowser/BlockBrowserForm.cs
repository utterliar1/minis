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
            Size = new Size(BlockLibrary.FormWidth, BlockLibrary.FormHeight);
            MinimumSize = new Size(700, 450);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 245, 248);
            AutoScaleMode = AutoScaleMode.Dpi;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei", 9f);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            // Timers
            _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); DoFilter(); };
            _thumbTimer = new System.Windows.Forms.Timer { Interval = 10 };
            _thumbTimer.Tick += ThumbTimerTick;

            // Toolbar
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4, 2, 4, 2) };

            var btnInsert = new ToolStripButton("插入");
            btnInsert.Click += (s, e) => DoInsert();

            var btnDelete = new ToolStripButton("删除");
            btnDelete.Click += (s, e) => DoDelete();

            var btnRename = new ToolStripButton("重命名");
            btnRename.Click += (s, e) => DoRename();

            var btnRefresh = new ToolStripButton("刷新");
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
                    var plan = BlockLibrary.SyncSafeUploadsToNas();
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

            // Search box - wide, with explicit MinimumSize
            _txtSearch = new TextBox { Width = 140, BorderStyle = BorderStyle.FixedSingle };
            _txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { _txtSearch.Text = ""; e.SuppressKeyPress = true; } };
            var txtSearchHost = new ToolStripControlHost(_txtSearch) { AutoSize = false };

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
                lblSearch, txtSearchHost, new ToolStripSeparator(),
                lblSize, cmbHost, new ToolStripSeparator(),
                btnInsert, new ToolStripSeparator(),
                btnAddToLib, btnExportBlock, new ToolStripSeparator(),
                btnRename, btnDelete, new ToolStripSeparator(),
                btnRefresh, btnOpenFolder, new ToolStripSeparator(),
                btnUpdateMirror, btnSync, new ToolStripSeparator(),
                btnSettings
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

                // Load categories
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

                LoadBlocks();
            }
            catch (System.Exception ex)
            {
                _lblStatus.Text = "错误: " + ex.Message;
            }
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

            // 只加载还没有缩略图的卡片
            var needLoad = _cards.Where(c => !HasThumbnail(c)).ToList();
            if (needLoad.Count > 0)
            {
                _failCount = 0;
                _pendingThumbCards = needLoad;
                _thumbIndex = 0;
                _thumbTimer.Start();
            }
        }

        private List<BlockThumbnailCard> _pendingThumbCards = new List<BlockThumbnailCard>();
        private int _failCount;

        private bool HasThumbnail(BlockThumbnailCard card)
        {
            return ThumbnailMemoryCacheService.HasValue(_thumbCache, card.Block.FilePath, _thumbSize);
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
                string filePath = _selectedBlock.FilePath;
                string name = _selectedBlock.Name;
                if (!File.Exists(filePath)) { MessageBox.Show("文件不存在: " + filePath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (BlockLibrary.ActiveLibrary != null && BlockLibrary.ActiveLibrary.Kind == ActiveLibraryKind.LocalMirror)
                {
                    BlockLibrary.RecordLocalChange(LocalChangeAction.DeleteRequest, BlockLibrary.ToLibraryRelativePath(filePath), "", null);
                    MessageBox.Show("已记录删除请求。回到 NAS 后请在同步界面确认删除。", "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                // Check file is not locked
                if (!BlockFileOperations.CanOpenForExclusiveWrite(filePath)) { MessageBox.Show("文件被占用，请关闭CAD中打开的此文件后重试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
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
            if (string.IsNullOrEmpty(newName) || newName.Trim() == oldName) return;
            newName = newName.Trim();
            if (!BlockFileOperations.CanRenameBlock(_selectedBlock, newName, false))
            {
                MessageBox.Show("名称为空或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string oldPath = _selectedBlock.FilePath;
            string newPath = BlockFileOperations.GetRenameTargetPath(_selectedBlock, newName);
            if (File.Exists(newPath)) { MessageBox.Show("同名文件已存在: " + newName, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                // Rename via BlockLibrary (handles file move + cache rename)
                if (!BlockLibrary.RenameBlock(_selectedBlock, newName))
                {
                    MessageBox.Show("重命名失败。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                // Update memory cache key (oldPath_size -> newPath_size)
                ThumbnailMemoryCacheService.MovePathEntries(_thumbCache, oldPath, newPath);
                _lblStatus.Text = "已重命名: " + oldName + " -> " + newName;
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

                LoadData();
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
            if (string.IsNullOrEmpty(name)) return;
            if (!BlockLibrary.IsSafeLibraryName(category) || !BlockLibrary.IsSafeLibraryName(name))
            {
                MessageBox.Show("分类或名称为空，或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            PendingCategory = category;
            PendingBlockName = name.Trim();
            PendingCommand = "BBADD";
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
            if (block == null || !File.Exists(block.FilePath))
            {
                _lblStatus.Text = "就绪";
                return;
            }
            try
            {
                var fi = new System.IO.FileInfo(block.FilePath);
                string sizeStr = fi.Length < 1024 ? fi.Length + " B"
                    : fi.Length < 1024 * 1024 ? (fi.Length / 1024.0).ToString("F1") + " KB"
                    : (fi.Length / 1024.0 / 1024.0).ToString("F1") + " MB";
                _lblStatus.Text = string.Format("{0}  |  {1}  |  修改: {2}",
                    block.Name, sizeStr, fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
            }
            catch { _lblStatus.Text = block.Name; }
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
                BlockLibrary.LibraryPath,
                BlockLibrary.InsertScale,
                BlockLibrary.InsertRotation * 180.0 / Math.PI))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    string newPath = form.LibraryPathValue.Trim();
                    if (string.IsNullOrEmpty(newPath)) { MessageBox.Show("路径不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    if (!Directory.Exists(newPath))
                    {
                        var dr = MessageBox.Show("目录不存在，是否创建？\n" + newPath, "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr == DialogResult.Yes) { try { Directory.CreateDirectory(newPath); } catch (Exception ex) { MessageBox.Show("创建失败: " + ex.Message); return; } }
                        else return;
                    }
                    BlockLibrary.InsertScale = form.InsertScaleValue;
                    BlockLibrary.InsertRotation = form.InsertRotationDegreesValue * Math.PI / 180.0;
                    if (newPath != BlockLibrary.LibraryPath)
                    {
                        BlockLibrary.LibraryPath = newPath;
                        BlockLibrary.NasLibraryPath = newPath;
                        ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
                        LoadData();
                    }
                    BlockLibrary.SaveConfig();
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
