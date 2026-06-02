using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class BlockBrowserForm : Form
    {
        // 模态模式：用户点了插入后返回OK
        private static string _lastCategory = "全部";

        public BlockInfo SelectedInsertBlock { get; private set; }
        public double InsertScale { get; private set; }
        public double InsertRotation { get; private set; }

        internal string _pendingCommand;
        private FlowLayoutPanel _flowBlocks;
        private FlowLayoutPanel _catBar;
        private TextBox _txtSearch;
        private ToolStrip _toolbar;
        private StatusStrip _statusBar;
        private ToolStripStatusLabel _lblStatus;
        private ToolStripStatusLabel _lblCount;

        private BlockInfo _selectedBlock;
        // _allBlocks removed
        private List<BlockThumbnailCard> _cards = new List<BlockThumbnailCard>();
        private string _currentCategory = "全部";
        private static int _savedThumbSize = 128;
        private int _thumbSize = _savedThumbSize;
        private System.Windows.Forms.Timer _searchTimer;
        private System.Windows.Forms.Timer _thumbTimer;
        private int _thumbIndex;
        private Dictionary<string, Image> _thumbCache = new Dictionary<string, Image>();
        private static Dictionary<string, Image> _placeholderCache = new Dictionary<string, Image>();
        private Dictionary<string, List<BlockThumbnailCard>> _categoryCards = new Dictionary<string, List<BlockThumbnailCard>>();

        public BlockBrowserForm()
        {
            _currentCategory = _lastCategory;
            InitializeComponent();
            Load += (s, e) => LoadData();
        }

        private void InitializeComponent()
        {
            Text = "块浏览器 v1.0 - " + BlockLibrary.PlatformName;
            Size = new Size(1000, 650);
            MinimumSize = new Size(700, 450);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 245, 248);
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

            // Search box - wide, with explicit MinimumSize
            _txtSearch = new TextBox { Width = 220, BorderStyle = BorderStyle.FixedSingle };
            _txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { _txtSearch.Text = ""; } };
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
                _savedThumbSize = _thumbSize;
                RefreshCards();
            };
            var cmbHost = new ToolStripControlHost(cmbThumbSize);

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                btnInsert, new ToolStripSeparator(),
                btnAddToLib, btnExportBlock, new ToolStripSeparator(),
                btnRefresh, btnOpenFolder, btnSettings, new ToolStripSeparator(),
                lblSearch, txtSearchHost, new ToolStripSeparator(),
                lblSize, cmbHost
            });

            // Category bar - compact top panel
            _catBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 56,
                Padding = new Padding(8, 6, 8, 10),
                BackColor = Color.FromArgb(235, 238, 242),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, AutoScroll = true
            };

            // Thumbnail grid - fills remaining space
            _flowBlocks = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(248, 249, 252)
            };

            // Status bar
            _statusBar = new StatusStrip();
            var lblAuthor = new ToolStripLabel("v1.0 | 制作人：WLUP") { ForeColor = Color.FromArgb(130, 130, 140) };
            _lblStatus = new ToolStripStatusLabel("就绪") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _lblCount = new ToolStripStatusLabel("0") { TextAlign = ContentAlignment.MiddleRight };
            _statusBar.Items.AddRange(new ToolStripItem[] { lblAuthor, _lblStatus, _lblCount });

            // Context menu
            var ctx = new ContextMenuStrip();
            ctx.Items.Add("插入", null, (s, e) => DoInsert());
            ctx.Items.Add("复制名称", null, (s, e) => { if (_selectedBlock != null) Clipboard.SetText(_selectedBlock.Name); });
            _flowBlocks.ContextMenuStrip = ctx;

            Controls.Add(_flowBlocks);
            Controls.Add(_catBar);
            Controls.Add(_toolbar);
            Controls.Add(_statusBar);
        }

        private void LoadData()
        {
            try
            {
                _lblStatus.Text = "加载中...";
                Application.DoEvents();

                // Load categories
                var categories = BlockLibrary.GetCategories();
                _catBar.SuspendLayout();
                _catBar.Controls.Clear();
                foreach (var cat in categories)
                {
                    var btn = new Button
                    {
                        Text = cat, FlatStyle = FlatStyle.Flat, AutoSize = true,
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

                LoadBlocks();
            }
            catch (System.Exception ex)
            {
                _lblStatus.Text = "错误: " + ex.Message;
            }
        }

        private void SetCatBtnStyle(Button btn, bool active)
        {
            if (active)
            {
                btn.BackColor = Color.FromArgb(60, 120, 200);
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(40, 100, 180);
            }
            else
            {
                btn.BackColor = Color.FromArgb(248, 250, 252);
                btn.ForeColor = Color.FromArgb(50, 55, 65);
                btn.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 200);
            }
        }

        private void UpdateCatHighlight()
        {
            foreach (Control c in _catBar.Controls)
            {
                var btn = c as Button;
                if (btn != null) SetCatBtnStyle(btn, (string)btn.Tag == _currentCategory);
            }
        }

        private void LoadBlocks()
        {
            _txtSearch.Text = "";
            ShowBlocks(BlockLibrary.GetBlocks(_currentCategory));
        }

        private void CreateCard(BlockInfo block)
        {
            var card = new BlockThumbnailCard(block, _thumbSize);
            card.BlockClicked += (s, b) =>
            {
                _selectedBlock = b;
                foreach (var c2 in _cards) c2.IsSelected = (c2 == s);
                _flowBlocks.Invalidate(true);
            };
            card.BlockDoubleClicked += (s, b) => { _selectedBlock = b; DoInsert(); };
            // 内存缓存命中则直接显示
            string ck = block.FilePath ?? "";
            if (_thumbCache.ContainsKey(ck) && _thumbCache[ck] != null)
            {
                try { card.LoadThumbnail(new Bitmap(_thumbCache[ck], _thumbSize, _thumbSize)); }
                catch { _thumbCache.Remove(ck); }
            }
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
                        _selectedBlock = b;
                        foreach (var c2 in _cards) c2.IsSelected = (c2 == s);
                        _flowBlocks.Invalidate(true);
                    };
                    card.BlockDoubleClicked += (s, b) => { _selectedBlock = b; DoInsert(); };
                    string ck = block.FilePath ?? "";
                    if (_thumbCache.ContainsKey(ck) && _thumbCache[ck] != null)
                    {
                        try { card.LoadThumbnail(new Bitmap(_thumbCache[ck], _thumbSize, _thumbSize)); }
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

            _lblCount.Text = _cards.Count + " 个";
            _lblStatus.Text = "就绪";

            // 只加载还没有缩略图的卡片
            var needLoad = _cards.Where(c => !HasThumbnail(c)).ToList();
            if (needLoad.Count > 0)
            {
                _pendingThumbCards = needLoad;
                _thumbIndex = 0;
                _thumbTimer.Start();
            }
        }

        private List<BlockThumbnailCard> _pendingThumbCards = new List<BlockThumbnailCard>();

        private bool HasThumbnail(BlockThumbnailCard card)
        {
            string ck = card.Block.FilePath ?? "";
            return _thumbCache.ContainsKey(ck) && _thumbCache[ck] != null;
        }

        // Load thumbnails one by one on UI thread (safe for GstarCAD API)
        private int _thumbFailCount;

                        private void ThumbTimerTick(object sender, EventArgs e)
        {
            for (int i = 0; i < 5; i++)
            {
                if (_thumbIndex >= _pendingThumbCards.Count)
                {
                    _thumbTimer.Stop();
                    _lblStatus.Text = "就绪";
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
                        card.LoadThumbnail(img);
                        string ck = card.Block.FilePath ?? "";
                        if (!_thumbCache.ContainsKey(ck))
                            _thumbCache[ck] = new Bitmap(img);
                    }
                }
                catch { }
            }
            if (_thumbIndex < _pendingThumbCards.Count)
                _lblStatus.Text = string.Format("加载中... {0}/{1}", _thumbIndex, _pendingThumbCards.Count);
        }

        private void RefreshCards()
        {
            _thumbTimer.Stop();
            // 清除内存缓存，因为尺寸变了
            foreach (var kv in _thumbCache) { try { kv.Value.Dispose(); } catch { } }
            _thumbCache.Clear();
            // 清除分类卡片缓存，重建卡片
            foreach (var kv in _categoryCards) { foreach (var c in kv.Value) { try { c.Dispose(); } catch { } } }
            _categoryCards.Clear();
            _cards.Clear();
            _flowBlocks.Controls.Clear();
            ShowBlocks(BlockLibrary.GetBlocks(_currentCategory));
        }

        private void DoFilter()
        {
            string kw = _txtSearch.Text.Trim().ToLowerInvariant();
            bool showAll = string.IsNullOrEmpty(kw);
            _flowBlocks.SuspendLayout();
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.IsDisposed) continue;
                bool match = showAll ||
                    card.Block.Name.ToLowerInvariant().Contains(kw) ||
                    card.Block.Category.ToLowerInvariant().Contains(kw);
                card.Visible = match;
            }
            _flowBlocks.ResumeLayout();
            int visible = _cards.Count(c => !c.IsDisposed && c.Visible);
            _lblCount.Text = visible + " 个";
        }

        private bool _inserting;

                                        private void DoInsert()
        {
            if (_selectedBlock == null) { MessageBox.Show("请先选择一个块。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var opts = new InsertOptionsForm(_selectedBlock.Name))
            {
                if (opts.ShowDialog(this) != DialogResult.OK) return;
                SelectedInsertBlock = _selectedBlock;
                InsertScale = opts.InsertScale;
                InsertRotation = opts.Rotation;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        internal string PendingCategory;
        internal string PendingBlockName;

        private void BtnAddToLib_Click(object sender, EventArgs e)
        {
            var categories = BlockLibrary.GetCategories().Where(c => c != "全部").ToList();
            string category = ShowCategoryDialog("选择分类", categories);
            if (category == null) return;
            string name = ShowInputDialog("输入块名称:");
            if (string.IsNullOrEmpty(name)) return;
            PendingCategory = category;
            PendingBlockName = name.Trim();
            _pendingCommand = "BBADD";
            this.DialogResult = DialogResult.Abort;
            this.Close();
        }

        private void BtnExportBlock_Click(object sender, EventArgs e)
        {
            _pendingCommand = "BBEXPORT";
            this.DialogResult = DialogResult.Abort;
            this.Close();
        }

        private string ShowInputDialog(string prompt)
        {
            using (var form = new Form())
            {
                form.Text = prompt;
                form.Size = new Size(350, 150);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false; form.MinimizeBox = false;
                var txt = new TextBox { Location = new Point(15, 20), Width = 300 };
                var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(160, 70) };
                var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(250, 70) };
                form.Controls.AddRange(new Control[] { txt, btnOk, btnCancel });
                form.AcceptButton = btnOk; form.CancelButton = btnCancel;
                return form.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
            }
        }

        private string ShowCategoryDialog(string title, List<string> categories)
        {
            using (var form = new Form())
            {
                form.Text = title; form.Size = new Size(350, 180);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog; form.MaximizeBox = false;
                var lbl = new Label { Text = "选择或输入分类:", Location = new Point(15, 15), AutoSize = true };
                var cmb = new ComboBox { Location = new Point(15, 40), Width = 300, DropDownStyle = ComboBoxStyle.DropDown };
                cmb.Items.AddRange(categories.ToArray());
                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
                var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(160, 100) };
                var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(250, 100) };
                form.Controls.AddRange(new Control[] { lbl, cmb, btnOk, btnCancel });
                form.AcceptButton = btnOk; form.CancelButton = btnCancel;
                return form.ShowDialog(this) == DialogResult.OK ? cmb.Text.Trim() : null;
            }
        }

        private void ShowSettingsDialog()
        {
            using (var form = new Form())
            {
                form.Text = "块浏览器设置";
                form.Size = new Size(450, 180);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false; form.MinimizeBox = false;

                var lbl = new Label { Text = "块库路径:", Location = new Point(15, 20), AutoSize = true };
                var txt = new TextBox { Text = BlockLibrary.LibraryPath, Location = new Point(15, 45), Width = 350 };
                var btnBrowse = new Button { Text = "...", Location = new Point(370, 43), Width = 40 };
                btnBrowse.Click += (s2, e2) =>
                {
                    using (var dlg = new FolderBrowserDialog())
                    {
                        dlg.SelectedPath = txt.Text;
                        if (dlg.ShowDialog() == DialogResult.OK) txt.Text = dlg.SelectedPath;
                    }
                };
                var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(240, 100) };
                var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(330, 100) };
                form.Controls.AddRange(new Control[] { lbl, txt, btnBrowse, btnOk, btnCancel });
                form.AcceptButton = btnOk; form.CancelButton = btnCancel;

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    string newPath = txt.Text.Trim();
                    if (!string.IsNullOrEmpty(newPath) && newPath != BlockLibrary.LibraryPath)
                    {
                        BlockLibrary.LibraryPath = newPath;
                        BlockLibrary.SaveConfig();
                        LoadData();
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_searchTimer != null) _searchTimer.Dispose();
                if (_thumbTimer != null) _thumbTimer.Dispose();
                foreach (var kv in _categoryCards) { foreach (var c in kv.Value) { try { c.Dispose(); } catch { } } }
                _categoryCards.Clear();
                foreach (var kv in _thumbCache) { try { kv.Value.Dispose(); } catch { } }
                _thumbCache.Clear();
                foreach (var card in _cards) { try { card.Dispose(); } catch { } }
            }
            base.Dispose(disposing);
        }
    }

    public class InsertOptionsForm : Form
    {
        public double InsertScale { get; private set; }
        public double Rotation { get; private set; }

        public InsertOptionsForm(string blockName)
        {
            Text = string.Format("插入 - {0}", blockName);
            Size = new Size(320, 200);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            var lblScale = new Label { Text = "比例:", Location = new Point(20, 25), AutoSize = true };
            var numScale = new NumericUpDown { Location = new Point(100, 22), Width = 170, Minimum = 0.001m, Maximum = 10000, DecimalPlaces = 3, Value = 1.0m, Increment = 0.1m };
            var lblRot = new Label { Text = "角度(度):", Location = new Point(20, 60), AutoSize = true };
            var numRot = new NumericUpDown { Location = new Point(100, 57), Width = 170, Minimum = -360, Maximum = 360, DecimalPlaces = 1, Value = 0, Increment = 5 };
            var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(120, 110) };
            var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(210, 110) };
            btnOk.Click += (s, e) => { InsertScale = (double)numScale.Value; Rotation = (double)numRot.Value * Math.PI / 180.0; };
            Controls.AddRange(new Control[] { lblScale, numScale, lblRot, numRot, btnOk, btnCancel });
            AcceptButton = btnOk; CancelButton = btnCancel;
        }
    }
}
