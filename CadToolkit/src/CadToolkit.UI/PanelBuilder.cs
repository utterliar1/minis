using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CadToolkit.Core;
using CommandGroup = CadToolkit.Core.Config.CommandGroup;

namespace CadToolkit.UI
{
    public class PanelAction
    {
        public string Kind;
        public string CommandName;
    }

    public static class PanelBuilder
    {
        static double _uiScaleFactor;

        static int UiScale(int value)
        {
            try
            {
                if (_uiScaleFactor <= 0)
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                        _uiScaleFactor = g.DpiX / 96.0;
                }
                return Math.Max(1, (int)Math.Round(value * _uiScaleFactor));
            }
            catch { return value; }
        }

        public static PanelAction Show(string title, string version, List<CommandGroup> groups)
        {
            int groupCols = groups.Count <= 4 ? 2 : (groups.Count <= 9 ? 3 : 4);
            int innerCols = 2;
            int bw = UiScale(104), bh = UiScale(30), gap = UiScale(4);
            int headerH = UiScale(24);
            int cellPad = UiScale(7);

            var groupHeights = new List<int>();
            foreach (var g in groups)
            {
                int rows = Math.Max(1, (int)Math.Ceiling(g.Commands.Count / (double)innerCols));
                groupHeights.Add(headerH + cellPad + rows * (bh + gap) - gap + cellPad);
            }

            int cellW = cellPad + innerCols * (bw + gap) - gap + cellPad;
            int groupGap = UiScale(6);
            int totalW = groupGap + groupCols * (cellW + groupGap);
            int barH = UiScale(34);
            int[] layoutHeights = new int[groupCols];
            for (int i = 0; i < layoutHeights.Length; i++) layoutHeights[i] = groupGap;
            foreach (int h in groupHeights)
            {
                int col = 0;
                for (int c = 1; c < groupCols; c++)
                    if (layoutHeights[c] < layoutHeights[col]) col = c;
                layoutHeights[col] += h + groupGap;
            }
            int contentH = groupGap;
            foreach (int h in layoutHeights)
                if (h > contentH) contentH = h;
            int totalH = contentH + barH;
            var work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int clientW = Math.Min(totalW, Math.Max(UiScale(360), work.Width - UiScale(80)));
            int clientH = Math.Min(totalH, Math.Max(UiScale(260), work.Height - UiScale(100)));

            var f = new Form();
            f.Text = title;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            f.TopMost = false;
            f.ShowInTaskbar = false;
            f.AutoScaleMode = AutoScaleMode.None;
            f.ClientSize = new Size(clientW, clientH);
            f.BackColor = Color.FromArgb(240, 240, 240);

            var content = new Panel();
            content.Dock = DockStyle.Fill;
            content.AutoScroll = true;
            content.BackColor = Color.FromArgb(240, 240, 240);
            content.AutoScrollMinSize = new Size(totalW, contentH);

            var bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = barH;
            bar.BackColor = Color.FromArgb(240, 240, 240);

            var btnCancel = new Button();
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Size = new Size(0, 0);
            btnCancel.Location = new Point(-100, -100);
            f.Controls.Add(btnCancel);
            f.CancelButton = btnCancel;

            PanelAction result = null;

            int[] columnHeights = new int[groupCols];
            for (int i = 0; i < columnHeights.Length; i++) columnHeights[i] = groupGap;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                int col = 0;
                for (int c = 1; c < groupCols; c++)
                    if (columnHeights[c] < columnHeights[col]) col = c;
                int cellH = groupHeights[gi];
                int gx = groupGap + col * (cellW + groupGap);
                int gy = columnHeights[col];
                columnHeights[col] += cellH + groupGap;

                var pnl = new Panel();
                pnl.Location = new Point(gx, gy);
                pnl.Size = new Size(cellW, cellH);
                pnl.BackColor = Color.White;
                pnl.BorderStyle = BorderStyle.FixedSingle;

                var lbl = new Label();
                lbl.Text = g.Name;
                lbl.Left = cellPad; lbl.Top = 2;
                lbl.AutoSize = false; lbl.Size = new Size(cellW - cellPad * 2, headerH);
                lbl.TextAlign = ContentAlignment.MiddleCenter;
                lbl.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
                lbl.ForeColor = Color.FromArgb(60, 60, 60);
                pnl.Controls.Add(lbl);

                int btnY0 = headerH + cellPad;
                for (int i = 0; i < g.Commands.Count; i++)
                {
                    string label = g.Commands[i].Key;
                    string cmd = g.Commands[i].Value;
                    var b = new Button();
                    b.Text = label;
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = Color.FromArgb(190, 190, 190);
                    b.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 232, 255);
                    b.BackColor = Color.White;
                    b.Font = new Font("Microsoft YaHei", 8.5f);
                    b.Size = new Size(bw, bh);
                    b.Location = new Point(cellPad + (i % innerCols) * (bw + gap), btnY0 + (i / innerCols) * (bh + gap));
                    string cmdName = cmd;
                    b.Click += delegate { result = new PanelAction { Kind = "CMD", CommandName = cmdName }; f.Close(); };
                    pnl.Controls.Add(b);
                }
                content.Controls.Add(pnl);
            }

            var btnAdd = new Button();
            btnAdd.Text = "+";
            btnAdd.FlatStyle = FlatStyle.Flat;
            btnAdd.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnAdd.BackColor = Color.White;
            btnAdd.Font = new Font("Microsoft YaHei", 10f);
            btnAdd.Size = new Size(UiScale(28), UiScale(24));
            btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAdd.Click += delegate { result = new PanelAction { Kind = "ADD" }; f.Close(); };

            var btnManage = new Button();
            btnManage.Text = "-";
            btnManage.FlatStyle = FlatStyle.Flat;
            btnManage.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnManage.BackColor = Color.White;
            btnManage.Font = new Font("Microsoft YaHei", 10f);
            btnManage.Size = new Size(UiScale(28), UiScale(24));
            btnManage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnManage.Click += delegate { result = new PanelAction { Kind = "MANAGE" }; f.Close(); };

            var btnConfigCheck = new Button();
            btnConfigCheck.Text = "⚙";
            btnConfigCheck.FlatStyle = FlatStyle.Flat;
            btnConfigCheck.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnConfigCheck.BackColor = Color.White;
            btnConfigCheck.Font = new Font("Microsoft YaHei", 10f);
            btnConfigCheck.Size = new Size(UiScale(28), UiScale(24));
            btnConfigCheck.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnConfigCheck.Click += delegate { result = new PanelAction { Kind = "STANDARDCENTER" }; f.Close(); };
            var configTip = new ToolTip();
            configTip.SetToolTip(btnConfigCheck, "规范中心");

            var lblAuthor = new Label();
            lblAuthor.Text = version;
            lblAuthor.AutoSize = false;
            lblAuthor.Size = new Size(UiScale(150), UiScale(24));
            lblAuthor.Location = new Point(groupGap, UiScale(5));
            lblAuthor.TextAlign = ContentAlignment.MiddleLeft;
            lblAuthor.ForeColor = Color.FromArgb(160, 160, 160);
            lblAuthor.Font = new Font("Microsoft YaHei", 7.5f);

            bar.Controls.Add(btnAdd);
            bar.Controls.Add(btnManage);
            bar.Controls.Add(btnConfigCheck);
            bar.Controls.Add(lblAuthor);
            bar.Resize += delegate
            {
                btnManage.Location = new Point(bar.ClientSize.Width - groupGap - btnManage.Width, UiScale(5));
                btnAdd.Location = new Point(btnManage.Left - UiScale(6) - btnAdd.Width, UiScale(5));
                btnConfigCheck.Location = new Point(btnAdd.Left - UiScale(6) - btnConfigCheck.Width, UiScale(5));
            };
            btnManage.Location = new Point(bar.ClientSize.Width - groupGap - btnManage.Width, UiScale(5));
            btnAdd.Location = new Point(btnManage.Left - UiScale(6) - btnAdd.Width, UiScale(5));
            btnConfigCheck.Location = new Point(btnAdd.Left - UiScale(6) - btnConfigCheck.Width, UiScale(5));
            f.Controls.Add(content);
            f.Controls.Add(bar);

            f.ShowDialog();
            f.Dispose();

            return result;
        }
    }
}
