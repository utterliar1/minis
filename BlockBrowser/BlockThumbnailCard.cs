using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class BlockThumbnailCard : UserControl
    {
        public BlockInfo Block { get; private set; }
        public event EventHandler<BlockInfo> BlockDoubleClicked;
        public event EventHandler<BlockInfo> BlockClicked;

        private PictureBox _pic;
        private Label _lbl;
        private Panel _panel;
        private ToolTip _tip;
        private bool _hover;
        private bool _selected;

        public bool IsSelected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                UpdateVisual();
            }
        }

        public BlockThumbnailCard(BlockInfo block, int thumbSize)
        {
            Block = block;
            int cw = thumbSize + 12;
            int ch = thumbSize + 30;
            Size = new Size(cw, ch);
            Margin = new Padding(4);
            Padding = new Padding(2);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(210, 215, 220);

            _tip = new ToolTip();
            _tip.SetToolTip(this, block.Name + "\n" + block.Category);

            _panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(3), BackColor = Color.FromArgb(245, 247, 250) };

            _pic = new PictureBox
            {
                Dock = DockStyle.Top, Height = thumbSize,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle
            };
            _pic.Image = BlockLibrary.GeneratePlaceholder(block.Name, thumbSize);

            _lbl = new Label
            {
                Dock = DockStyle.Fill, Text = block.Name,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei", 8f),
                ForeColor = Color.FromArgb(50, 50, 50),
                Padding = new Padding(0, 2, 0, 0)
            };

            _panel.Controls.Add(_lbl);
            _panel.Controls.Add(_pic);
            Controls.Add(_panel);

            EventHandler onClick = (s, e) => { if (BlockClicked != null) BlockClicked(this, Block); };
            EventHandler onDbl = (s, e) => { if (BlockDoubleClicked != null) BlockDoubleClicked(this, Block); };
            _pic.Click += onClick; _pic.DoubleClick += onDbl;
            _lbl.Click += onClick; _lbl.DoubleClick += onDbl;
            Click += onClick; DoubleClick += onDbl;
        }

        private void UpdateVisual()
        {
            if (_selected)
            {
                BackColor = Color.FromArgb(50, 110, 190);
                _panel.BackColor = Color.FromArgb(215, 228, 248);
            }
            else if (_hover)
            {
                BackColor = Color.FromArgb(130, 170, 220);
                _panel.BackColor = Color.FromArgb(230, 238, 248);
            }
            else
            {
                BackColor = Color.FromArgb(210, 215, 220);
                _panel.BackColor = Color.FromArgb(245, 247, 250);
            }
        }

        public void LoadThumbnail(Image img)
        {
            if (_pic.IsDisposed) return;
            if (_pic.InvokeRequired) { try { _pic.Invoke(new Action(() => LoadThumbnail(img))); } catch { } return; }
            var old = _pic.Image;
            _pic.Image = img;
            if (old != null) old.Dispose();
        }

        public void SetPlaceholder(int thumbSize)
        {
            _pic.Height = thumbSize;
            Size = new Size(thumbSize + 12, thumbSize + 30);
            var old = _pic.Image;
            _pic.Image = BlockLibrary.GeneratePlaceholder(Block.Name, thumbSize);
            if (old != null) old.Dispose();
        }

        public void UpdateLabel(string newName)
        {
            _lbl.Text = newName;
            _tip.SetToolTip(this, newName + "\n" + Block.Category);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; UpdateVisual(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; UpdateVisual(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_tip != null) _tip.Dispose();
                if (_pic != null && _pic.Image != null) _pic.Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}