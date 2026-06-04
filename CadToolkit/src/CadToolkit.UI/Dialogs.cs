using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CadToolkit.Core;

namespace CadToolkit.UI
{
    public class AlignDialog : Form
    {
        public AlignChoice Choice;
        public AlignDialog()
        {
            Choice = new AlignChoice { Horizontal = HAlign.Left, Vertical = VAlign.Bottom };
            Text = "\u6587\u5B57\u5BF9\u9F50";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(300, 220);

            var hint = new Label();
            hint.Text = "\u70B9\u51FB\u4E5D\u5BAB\u683C\u9009\u62E9\u5BF9\u9F50\u65B9\u5411";
            hint.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            hint.Dock = DockStyle.Top;
            hint.Height = 26;
            hint.TextAlign = ContentAlignment.MiddleCenter;

            var grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Top;
            grid.Height = 150;
            grid.ColumnCount = 3;
            grid.RowCount = 3;
            grid.Padding = new Padding(8);
            for (int i = 0; i < 3; i++) { grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33)); }

            string[] labels = { "\u5DE6\u4E0A", "\u4E2D\u4E0A", "\u53F3\u4E0A", "\u5DE6\u4E2D", "\u6B63\u4E2D", "\u53F3\u4E2D", "\u5DE6\u4E0B", "\u4E2D\u4E0B", "\u53F3\u4E0B" };
            HAlign[] ha = { HAlign.Left, HAlign.Center, HAlign.Right, HAlign.Left, HAlign.Center, HAlign.Right, HAlign.Left, HAlign.Center, HAlign.Right };
            VAlign[] va = { VAlign.Top, VAlign.Top, VAlign.Top, VAlign.Middle, VAlign.Middle, VAlign.Middle, VAlign.Bottom, VAlign.Bottom, VAlign.Bottom };

            for (int i = 0; i < 9; i++)
            {
                int idx = i;
                var btn = new Button();
                btn.Text = labels[i];
                btn.Dock = DockStyle.Fill;
                btn.Margin = new Padding(2);
                btn.FlatStyle = FlatStyle.System;
                btn.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
                btn.Click += delegate { Choice = new AlignChoice { Horizontal = ha[idx], Vertical = va[idx] }; DialogResult = DialogResult.OK; Close(); };
                grid.Controls.Add(btn, i % 3, i / 3);
            }

            var close = new Button();
            close.Text = "\u53D6\u6D88";
            close.DialogResult = DialogResult.Cancel;
            close.FlatStyle = FlatStyle.System;
            close.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            close.Size = new Size(70, 28);
            close.Location = new Point(ClientSize.Width - 82, ClientSize.Height - 36);

            Controls.Add(grid);
            Controls.Add(hint);
            Controls.Add(close);
            CancelButton = close;
        }
    }

    public class FindReplaceDialog : Form
    {
        public string FindText;
        public string ReplaceText;
        public bool IgnoreCase;
        public FindReplaceDialog()
        {
            FindText = ""; ReplaceText = ""; IgnoreCase = true;
            Text = "\u67E5\u627E\u66FF\u6362";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(400, 180);

            var l1 = new Label(); l1.Text = "\u67E5\u627E\uFF1A"; l1.Left = 16; l1.Top = 18; l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t1 = new TextBox(); t1.Left = 76; t1.Top = 14; t1.Width = 300; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var l2 = new Label(); l2.Text = "\u66FF\u6362\uFF1A"; l2.Left = 16; l2.Top = 52; l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t2 = new TextBox(); t2.Left = 76; t2.Top = 48; t2.Width = 300; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var chk = new CheckBox(); chk.Text = "\u5FFD\u7565\u5927\u5C0F\u5199"; chk.Left = 76; chk.Top = 84; chk.Checked = true; chk.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var ok = new Button(); ok.Text = "\u786E\u5B9A"; ok.DialogResult = DialogResult.OK; ok.Left = 210; ok.Top = 120; ok.Width = 80; ok.FlatStyle = FlatStyle.System; ok.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var cancel = new Button(); cancel.Text = "\u53D6\u6D88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 300; cancel.Top = 120; cancel.Width = 76; cancel.FlatStyle = FlatStyle.System; cancel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            ok.Click += delegate { FindText = t1.Text; ReplaceText = t2.Text; IgnoreCase = chk.Checked; };
            Controls.AddRange(new Control[] { l1, t1, l2, t2, chk, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
        }
    }

    public class RenameBlockDialog : Form
    {
        public string OldName;
        public string NewName;
        public RenameBlockDialog(string prefillOld)
        {
            OldName = prefillOld ?? "";
            NewName = "";
            Text = "\u91CD\u547D\u540D\u5757";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(400, 150);

            var l1 = new Label(); l1.Text = "\u65E7\u540D\u79F0\uFF1A"; l1.Left = 16; l1.Top = 18; l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t1 = new TextBox(); t1.Left = 90; t1.Top = 14; t1.Width = 280; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f); t1.Text = OldName; if (OldName.Length > 0) t1.ReadOnly = true;
            var l2 = new Label(); l2.Text = "\u65B0\u540D\u79F0\uFF1A"; l2.Left = 16; l2.Top = 52; l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t2 = new TextBox(); t2.Left = 90; t2.Top = 48; t2.Width = 280; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var ok = new Button(); ok.Text = "\u786E\u5B9A"; ok.DialogResult = DialogResult.OK; ok.Left = 210; ok.Top = 96; ok.Width = 80; ok.FlatStyle = FlatStyle.System; ok.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var cancel = new Button(); cancel.Text = "\u53D6\u6D88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 300; cancel.Top = 96; cancel.Width = 76; cancel.FlatStyle = FlatStyle.System; cancel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            ok.Click += delegate { OldName = t1.Text.Trim(); NewName = t2.Text.Trim(); };
            Controls.AddRange(new Control[] { l1, t1, l2, t2, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
        }
    }

    public class AddCommandDialog : Form
    {
        public string CmdLabel;
        public string CmdName;
        public AddCommandDialog()
        {
            CmdLabel = ""; CmdName = "";
            Text = "\u6DFB\u52A0\u547D\u4EE4";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(400, 150);

            var l1 = new Label(); l1.Text = "\u663E\u793A\u540D\u79F0\uFF1A"; l1.Left = 16; l1.Top = 18; l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t1 = new TextBox(); t1.Left = 100; t1.Top = 14; t1.Width = 270; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var l2 = new Label(); l2.Text = "CAD\u547D\u4EE4\uFF1A"; l2.Left = 16; l2.Top = 52; l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t2 = new TextBox(); t2.Left = 100; t2.Top = 48; t2.Width = 270; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var ok = new Button(); ok.Text = "\u786E\u5B9A"; ok.DialogResult = DialogResult.OK; ok.Left = 210; ok.Top = 96; ok.Width = 80; ok.FlatStyle = FlatStyle.System; ok.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var cancel = new Button(); cancel.Text = "\u53D6\u6D88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 300; cancel.Top = 96; cancel.Width = 76; cancel.FlatStyle = FlatStyle.System; cancel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            ok.Click += delegate { CmdLabel = t1.Text.Trim(); CmdName = t2.Text.Trim(); };
            Controls.AddRange(new Control[] { l1, t1, l2, t2, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
        }
    }

    public class ManageCommandsDialog : Form
    {
        public ManageCommandsDialog()
        {
            Text = "\u7BA1\u7406\u547D\u4EE4";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(400, 300);

            var stored = new List<KeyValuePair<string, string>>(Config.GetCommands());

            var list = new ListBox();
            list.Left = 12; list.Top = 12; list.Width = 376; list.Height = 230;
            list.Font = new System.Drawing.Font("Microsoft YaHei", 10f);

            for (int i = 0; i < stored.Count; i++)
            {
                list.Items.Add(stored[i].Key + "  =  " + stored[i].Value);
            }

            var btnDel = new Button();
            btnDel.Text = "\u5220\u9664\u9009\u4E2D";
            btnDel.FlatStyle = FlatStyle.System;
            btnDel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            btnDel.Size = new Size(90, 28);
            btnDel.Location = new Point(12, 252);
            btnDel.Click += delegate
            {
                if (list.SelectedIndex < 0) return;
                Config.RemoveCommand(stored[list.SelectedIndex].Key);
                stored.RemoveAt(list.SelectedIndex);
                list.Items.RemoveAt(list.SelectedIndex);
            };

            var btnClose = new Button();
            btnClose.Text = "\u5173\u95ED";
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.FlatStyle = FlatStyle.System;
            btnClose.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            btnClose.Size = new Size(70, 28);
            btnClose.Location = new Point(318, 252);

            Controls.Add(list);
            Controls.Add(btnDel);
            Controls.Add(btnClose);
            CancelButton = btnClose;
        }
    }
}
