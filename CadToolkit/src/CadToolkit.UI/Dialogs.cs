using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CadToolkit.Core;

namespace CadToolkit.UI
{
    public class AlignDialog : Form
    {
        public int HorzIndex = 0;
        public bool UseFirstBase = true;
        public double LineSpacing = 0;
        public AlignDialog()
        {
            HorzIndex = Config.AlignHorizontal;
            UseFirstBase = Config.AlignUseFirstBase;
            LineSpacing = Config.AlignLineSpacing;

            Text = "\u6587\u5B57\u5BF9\u9F50";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(360, 178);

            // Group 1: horizontal alignment
            var pnlH = new Panel();
            pnlH.Left = 12; pnlH.Top = 8; pnlH.Size = new Size(336, 30);
            pnlH.BorderStyle = BorderStyle.None;

            var lblH = new Label();
            lblH.Text = "\u6C34\u5E73\u5BF9\u9F50\uFF1A";
            lblH.Left = 4; lblH.Top = 7; lblH.AutoSize = true;
            lblH.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var rbL = new RadioButton(); rbL.Text = "\u5DE6"; rbL.Left = 94; rbL.Top = 5; rbL.AutoSize = true; rbL.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbL.Checked = (HorzIndex == 0);
            var rbC = new RadioButton(); rbC.Text = "\u4E2D"; rbC.Left = 154; rbC.Top = 5; rbC.AutoSize = true; rbC.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbC.Checked = (HorzIndex == 1);
            var rbR = new RadioButton(); rbR.Text = "\u53F3"; rbR.Left = 214; rbR.Top = 5; rbR.AutoSize = true; rbR.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbR.Checked = (HorzIndex == 2);
            rbL.CheckedChanged += delegate { if (rbL.Checked) HorzIndex = 0; };
            rbC.CheckedChanged += delegate { if (rbC.Checked) HorzIndex = 1; };
            rbR.CheckedChanged += delegate { if (rbR.Checked) HorzIndex = 2; };
            pnlH.Controls.AddRange(new Control[] { lblH, rbL, rbC, rbR });

            // Group 2: base point
            var pnlB = new Panel();
            pnlB.Left = 12; pnlB.Top = 40; pnlB.Size = new Size(336, 30);
            pnlB.BorderStyle = BorderStyle.None;

            var lblBase = new Label();
            lblBase.Text = "\u57FA\u70B9\uFF1A";
            lblBase.Left = 4; lblBase.Top = 7; lblBase.AutoSize = true;
            lblBase.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var rbFirst = new RadioButton(); rbFirst.Text = "\u7B2C\u4E00\u4E2A\u9009\u4E2D\u6587\u5B57"; rbFirst.Left = 94; rbFirst.Top = 5; rbFirst.AutoSize = true; rbFirst.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbFirst.Checked = UseFirstBase;
            var rbPick = new RadioButton(); rbPick.Text = "\u624B\u52A8\u6307\u5B9A"; rbPick.Left = 244; rbPick.Top = 5; rbPick.AutoSize = true; rbPick.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbPick.Checked = !UseFirstBase;
            rbFirst.CheckedChanged += delegate { if (rbFirst.Checked) UseFirstBase = true; };
            rbPick.CheckedChanged += delegate { if (rbPick.Checked) UseFirstBase = false; };
            pnlB.Controls.AddRange(new Control[] { lblBase, rbFirst, rbPick });

            // Group 3: line spacing
            var pnlS = new Panel();
            pnlS.Left = 12; pnlS.Top = 74; pnlS.Size = new Size(336, 52);
            pnlS.BorderStyle = BorderStyle.None;

            var lblSpace = new Label();
            lblSpace.Text = "\u884C\u8DDD\uFF1A";
            lblSpace.Left = 4; lblSpace.Top = 7; lblSpace.AutoSize = true;
            lblSpace.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var rbAuto = new RadioButton(); rbAuto.Text = "\u81EA\u52A8"; rbAuto.Left = 94; rbAuto.Top = 5; rbAuto.AutoSize = true; rbAuto.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbAuto.Checked = (LineSpacing <= 0);
            var rbManual = new RadioButton(); rbManual.Text = "\u6307\u5B9A"; rbManual.Left = 174; rbManual.Top = 5; rbManual.AutoSize = true; rbManual.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbManual.Checked = (LineSpacing > 0);

            var txtSpace = new TextBox();
            txtSpace.Text = LineSpacing > 0 ? LineSpacing.ToString() : "";
            txtSpace.Left = 234; txtSpace.Top = 3; txtSpace.Width = 60;
            txtSpace.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            txtSpace.Enabled = rbManual.Checked;
            rbAuto.CheckedChanged += delegate { if (rbAuto.Checked) { txtSpace.Enabled = false; LineSpacing = 0; } };
            rbManual.CheckedChanged += delegate { if (rbManual.Checked) { txtSpace.Enabled = true; double.TryParse(txtSpace.Text, out LineSpacing); } };
            txtSpace.TextChanged += delegate { if (txtSpace.Enabled) double.TryParse(txtSpace.Text, out LineSpacing); };
            pnlS.Controls.AddRange(new Control[] { lblSpace, rbAuto, rbManual, txtSpace });

            // Buttons
            var ok = new Button(); ok.Text = "\u786E\u5B9A"; ok.DialogResult = DialogResult.OK; ok.Left = 170; ok.Top = 134; ok.Width = 80; ok.Height = 28; ok.FlatStyle = FlatStyle.System; ok.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var cancel = new Button(); cancel.Text = "\u53D6\u6D88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 262; cancel.Top = 134; cancel.Width = 80; cancel.Height = 28; cancel.FlatStyle = FlatStyle.System; cancel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            ok.Click += delegate
            {
                Config.AlignHorizontal = HorzIndex;
                Config.AlignUseFirstBase = UseFirstBase;
                Config.AlignLineSpacing = LineSpacing;
            };

            Controls.AddRange(new Control[] { pnlH, pnlB, pnlS, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
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
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(400, 180);

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
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(400, 150);

            var l1 = new Label(); l1.Text = "\u65E7\u540D\u79F0\uFF1A"; l1.Left = 16; l1.Top = 18; l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t1 = new TextBox(); t1.Left = 90; t1.Top = 14; t1.Width = 280; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f); t1.Text = OldName; if (OldName.Length > 0) t1.ReadOnly = true;
            var l2 = new Label(); l2.Text = "\u65B0\u540D\u79F0\uFF1A"; l2.Left = 16; l2.Top = 52; l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t2 = new TextBox(); t2.Left = 90; t2.Top = 48; t2.Width = 280; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var ok = new Button(); ok.Text = "\u786E\u5B9A"; ok.DialogResult = DialogResult.OK; ok.Left = 210; ok.Top = 96; ok.Width = 80; ok.FlatStyle = FlatStyle.System; ok.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var cancel = new Button(); cancel.Text = "\u53D6\u6D88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 300; cancel.Top = 96; cancel.Width = 76; cancel.FlatStyle = FlatStyle.System; cancel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            ok.Click += delegate { OldName = t1.Text.Trim(); NewName = t2.Text.Trim(); };
            Controls.AddRange(new Control[] { l1, t1, l2, t2, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            Shown += delegate { t2.Focus(); };
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
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(400, 150);

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
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(400, 300);

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

