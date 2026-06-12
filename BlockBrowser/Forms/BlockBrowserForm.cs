using System;
using System.Collections.Generic;
using System.Drawing;
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
        private int _thumbIndex;
        private Dictionary<string, Image> _thumbCache = new Dictionary<string, Image>();
        
        private Dictionary<string, List<BlockThumbnailCard>> _categoryCards = new Dictionary<string, List<BlockThumbnailCard>>();


        public BlockBrowserForm()
        {
            _currentCategory = _lastCategory;
            InitializeComponent();
            Load += (s, e) => LoadData();
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
