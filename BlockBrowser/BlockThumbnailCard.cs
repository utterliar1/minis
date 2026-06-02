using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class BlockThumbnailCard : UserControl
    {
        public BlockInfo Block { get; private set; }
        public bool IsSelected { get; set; }
        public event EventHandler<BlockInfo> BlockDoubleClicked;
        public event EventHandler<BlockInfo> BlockClicked;

        private PictureBox _pic;
        private Label _lbl;
        private ToolTip _tip;
        private bool _hover;

        public BlockThumbnailCard(BlockInfo block, int thumbSize)
        {
            Block = block;
            int cw = thumbSize + 12;
            int ch = thumbSize + 30;
            Size = new Size(cw, ch);
            Margin = new Padding(4);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            _tip = new ToolTip();
            _tip.SetToolTip(this, block.Name + "\n" + block.Category);

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(3), BackColor = Color.FromArgb(245, 247, 250) };

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

            panel.Controls.Add(_lbl);
            panel.Controls.Add(_pic);
            Controls.Add(panel);

            EventHandler onClick = (s, e) => { if (BlockClicked != null) BlockClicked(this, Block); };
            EventHandler onDbl = (s, e) => { if (BlockDoubleClicked != null) BlockDoubleClicked(this, Block); };
            _pic.Click += onClick; _pic.DoubleClick += onDbl;
            _lbl.Click += onClick; _lbl.DoubleClick += onDbl;
            Click += onClick; DoubleClick += onDbl;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Color c = IsSelected ? Color.FromArgb(60, 120, 200) : _hover ? Color.FromArgb(130, 170, 220) : Color.FromArgb(210, 215, 220);
            int w = IsSelected ? 2 : 1;
            using (var p = new Pen(c, w))
                e.Graphics.DrawRectangle(p, w / 2, w / 2, Width - w, Height - w);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

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
