using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BlockBrowser
{
    // 禁止点击子控件时自动滚动的 FlowLayoutPanel
    class StableFlowPanel : FlowLayoutPanel
    {
        private const int WM_SETREDRAW = 0x000B;
        private const int WM_HSCROLL = 0x0114;
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private int _redrawLockCount;

        public event EventHandler ViewportChanged;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public StableFlowPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }

        public void BeginBulkUpdate()
        {
            if (!IsHandleCreated) return;
            if (_redrawLockCount++ == 0)
                SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        public void EndBulkUpdate()
        {
            if (_redrawLockCount <= 0) return;
            _redrawLockCount--;
            if (_redrawLockCount > 0 || !IsHandleCreated) return;

            SendMessage(Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            Invalidate(true);
            Update();
        }

        protected override Point ScrollToControl(Control activeControl)
        {
            return DisplayRectangle.Location;
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            OnViewportChanged();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            OnViewportChanged();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            OnViewportChanged();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL || m.Msg == WM_MOUSEWHEEL)
                OnViewportChanged();
        }

        private void OnViewportChanged()
        {
            var handler = ViewportChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    public partial class BlockBrowserForm : Form
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
        private System.Windows.Forms.Timer _cardTimer;
        private System.Windows.Forms.Timer _viewportThumbTimer;
        private int _thumbIndex;
        private Dictionary<string, Image> _thumbCache = new Dictionary<string, Image>();
        
        private Dictionary<string, List<BlockThumbnailCard>> _categoryCards = new Dictionary<string, List<BlockThumbnailCard>>();
        private bool _initialLoadQueued;
        private List<BlockInfo> _pendingBlocks = new List<BlockInfo>();
        private List<BlockThumbnailCard> _pendingBuiltCards = new List<BlockThumbnailCard>();
        private string _pendingCategoryKey = "";
        private int _pendingCardIndex;
        private int _cardLoadVersion;


        public BlockBrowserForm()
        {
            _currentCategory = _lastCategory;
            InitializeComponent();
            Shown += async (s, e) => await LoadDataAsync();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_searchTimer != null) _searchTimer.Dispose();
                if (_thumbTimer != null) _thumbTimer.Dispose();
                if (_cardTimer != null) _cardTimer.Dispose();
                if (_viewportThumbTimer != null) _viewportThumbTimer.Dispose();
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
