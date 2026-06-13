using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class BlockThumbnailCard : UserControl, IBlockCardState
    {
        public BlockInfo Block { get; private set; }
        public string FilePath { get { return Block == null ? "" : Block.FilePath; } }
        public event EventHandler<BlockInfo> BlockDoubleClicked;
        public event EventHandler<BlockInfo> BlockClicked;

        private PictureBox _pic;
        private Label _lbl;
        private Panel _panel;
        private Panel _imageFrame;
        private Label _statusBadge;
        private ToolTip _tip;
        private bool _hover;
        private bool _selected;
        private bool _thumbnailFailed;
        private int _labelHeight;

        private const int CardExtraWidth = 16;
        private const int CardExtraHeightWithoutLabel = 14;
        private const int ContentPadding = 4;
        private const int LabelMinHeight = 26;
        private const int ImageFrameExtra = 2;

        private static readonly Color BorderNormal = Color.FromArgb(218, 224, 232);
        private static readonly Color BorderHover = Color.FromArgb(142, 170, 205);
        private static readonly Color BorderSelected = Color.FromArgb(45, 105, 185);
        private static readonly Color SurfaceNormal = Color.FromArgb(248, 250, 252);
        private static readonly Color SurfaceHover = Color.FromArgb(240, 246, 253);
        private static readonly Color SurfaceSelected = Color.FromArgb(226, 239, 255);
        private static readonly Color ImageBorderNormal = Color.FromArgb(232, 236, 242);
        private static readonly Color ImageBorderHover = Color.FromArgb(196, 211, 230);
        private static readonly Color ImageBorderSelected = Color.FromArgb(132, 172, 226);
        private static readonly Color LabelNormal = Color.FromArgb(45, 55, 72);
        private static readonly Color LabelSelected = Color.FromArgb(25, 75, 130);
        private static readonly Color LabelBackNormal = SurfaceNormal;
        private static readonly Color LabelBackHover = Color.FromArgb(234, 243, 253);
        private static readonly Color LabelBackSelected = Color.FromArgb(207, 226, 250);

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
            var labelFont = new Font("Microsoft YaHei", 8f);
            _labelHeight = GetLabelHeight(labelFont);
            Size = GetCardSize(thumbSize, _labelHeight);
            MinimumSize = Size;
            Margin = new Padding(4);
            Padding = new Padding(2);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            BackColor = BorderNormal;

            _tip = new ToolTip();

            _panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(ContentPadding),
                BackColor = SurfaceNormal,
                Cursor = Cursors.Hand
            };

            _imageFrame = new Panel
            {
                Dock = DockStyle.Top,
                Height = thumbSize + ImageFrameExtra,
                Padding = new Padding(1),
                BackColor = ImageBorderNormal,
                Cursor = Cursors.Hand
            };

            _pic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Cursor = Cursors.Hand
            };
            _pic.Image = BlockLibrary.GeneratePlaceholder(block.Name ?? "", thumbSize);
            _imageFrame.Controls.Add(_pic);

            _statusBadge = new Label
            {
                AutoSize = false,
                Size = new Size(18, 18),
                Text = "!",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei", 7f, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 248, 220),
                ForeColor = Color.FromArgb(150, 82, 20),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _imageFrame.Controls.Add(_statusBadge);
            _statusBadge.BringToFront();
            _imageFrame.Resize += (s, e) => PositionStatusBadge();

            _lbl = new Label
            {
                Dock = DockStyle.Bottom,
                Height = _labelHeight,
                Text = block.Name,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                UseMnemonic = false,
                Font = labelFont,
                ForeColor = LabelNormal,
                Padding = new Padding(2, 3, 2, 0),
                Cursor = Cursors.Hand
            };

            _panel.Controls.Add(_lbl);
            _panel.Controls.Add(_imageFrame);
            Controls.Add(_panel);

            EventHandler onClick = (s, e) => { if (BlockClicked != null) BlockClicked(this, Block); };
            EventHandler onDbl = (s, e) => { if (BlockDoubleClicked != null) BlockDoubleClicked(this, Block); };
            _panel.Click += onClick; _panel.DoubleClick += onDbl;
            _imageFrame.Click += onClick; _imageFrame.DoubleClick += onDbl;
            _pic.Click += onClick; _pic.DoubleClick += onDbl;
            _statusBadge.Click += onClick; _statusBadge.DoubleClick += onDbl;
            _lbl.Click += onClick; _lbl.DoubleClick += onDbl;
            Click += onClick; DoubleClick += onDbl;

            WireHoverEvents(this);
            ApplyToolTip(block.Name);
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_selected)
            {
                BackColor = BorderSelected;
                _panel.BackColor = SurfaceSelected;
                _imageFrame.BackColor = ImageBorderSelected;
                _lbl.ForeColor = LabelSelected;
                _lbl.BackColor = LabelBackSelected;
            }
            else if (_hover)
            {
                BackColor = BorderHover;
                _panel.BackColor = SurfaceHover;
                _imageFrame.BackColor = ImageBorderHover;
                _lbl.ForeColor = LabelNormal;
                _lbl.BackColor = LabelBackHover;
            }
            else
            {
                BackColor = BorderNormal;
                _panel.BackColor = SurfaceNormal;
                _imageFrame.BackColor = ImageBorderNormal;
                _lbl.ForeColor = LabelNormal;
                _lbl.BackColor = LabelBackNormal;
            }
        }

        public void LoadThumbnail(Image img)
        {
            if (_pic.IsDisposed) return;
            if (_pic.InvokeRequired) { try { _pic.Invoke(new Action(() => LoadThumbnail(img))); } catch { if (img != null) img.Dispose(); } return; }
            var old = _pic.Image;
            _pic.Image = img;
            if (old != null) old.Dispose();
            SetThumbnailFailed(false);
        }

        public void SetPlaceholder(int thumbSize)
        {
            _labelHeight = GetLabelHeight(_lbl.Font);
            _lbl.Height = _labelHeight;
            _imageFrame.Height = thumbSize + ImageFrameExtra;
            Size = GetCardSize(thumbSize, _labelHeight);
            MinimumSize = Size;
            var old = _pic.Image;
            _pic.Image = BlockLibrary.GeneratePlaceholder(Block.Name, thumbSize);
            if (old != null) old.Dispose();
            SetThumbnailFailed(false);
        }

        public void SetThumbnailFailed(bool failed)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { Invoke(new Action(() => SetThumbnailFailed(failed))); } catch { } return; }
            if (_thumbnailFailed == failed) return;
            _thumbnailFailed = failed;
            if (_statusBadge != null)
            {
                _statusBadge.Visible = failed;
                if (failed) _statusBadge.BringToFront();
            }
            ApplyToolTip(_lbl == null ? (Block == null ? "" : Block.Name) : _lbl.Text);
        }

        public void UpdateLabel(string newName)
        {
            _lbl.Text = newName;
            ApplyToolTip(newName);
        }

        private static int GetLabelHeight(Font font)
        {
            return Math.Max(LabelMinHeight, font == null ? LabelMinHeight : font.Height + 8);
        }

        private static Size GetCardSize(int thumbSize, int labelHeight)
        {
            return new Size(thumbSize + CardExtraWidth, thumbSize + CardExtraHeightWithoutLabel + labelHeight);
        }

        private void ApplyToolTip(string displayName)
        {
            string text = BuildToolTipText(Block, displayName, _thumbnailFailed);
            _tip.SetToolTip(this, text);
            _tip.SetToolTip(_panel, text);
            _tip.SetToolTip(_imageFrame, text);
            _tip.SetToolTip(_pic, text);
            _tip.SetToolTip(_statusBadge, text);
            _tip.SetToolTip(_lbl, text);
        }

        private static string BuildToolTipText(BlockInfo block, string displayName, bool thumbnailFailed)
        {
            if (block == null) return thumbnailFailed ? "缩略图生成失败" : "";
            string text = displayName ?? "";
            if (!string.IsNullOrEmpty(block.Category))
                text += "\n" + block.Category;
            if (!string.IsNullOrEmpty(block.FilePath))
                text += "\n" + block.FilePath;
            if (thumbnailFailed)
                text += (text.Length > 0 ? "\n" : "") + "缩略图生成失败";
            return text;
        }

        private void PositionStatusBadge()
        {
            if (_statusBadge == null || _imageFrame == null) return;
            int x = Math.Max(2, _imageFrame.ClientSize.Width - _statusBadge.Width - 4);
            _statusBadge.Location = new Point(x, 4);
        }

        private void WireHoverEvents(Control control)
        {
            control.MouseEnter += CardMouseEnter;
            control.MouseLeave += CardMouseLeave;
            foreach (Control child in control.Controls)
                WireHoverEvents(child);
        }

        private void CardMouseEnter(object sender, EventArgs e)
        {
            SetHover(true);
        }

        private void CardMouseLeave(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            try { BeginInvoke(new Action(UpdateHoverFromMousePosition)); }
            catch { UpdateHoverFromMousePosition(); }
        }

        private void UpdateHoverFromMousePosition()
        {
            if (IsDisposed) return;
            SetHover(ClientRectangle.Contains(PointToClient(Control.MousePosition)));
        }

        private void SetHover(bool hover)
        {
            if (_hover == hover) return;
            _hover = hover;
            UpdateVisual();
        }

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
