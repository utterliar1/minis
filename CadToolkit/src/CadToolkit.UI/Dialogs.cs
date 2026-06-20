using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CadToolkit.Core;

namespace CadToolkit.UI
{
    internal static class DpiUtil
    {
        static int Scale(int value, float factor) { return Math.Max(1, (int)Math.Round(value * factor)); }

        public static void Apply(Form form)
        {
            float factor = 1f;
            try
            {
                using (var g = form.CreateGraphics()) factor = g.DpiX / 96f;
            }
            catch { }
            if (factor <= 1.05f) return;

            form.SuspendLayout();
            form.AutoScaleMode = AutoScaleMode.None;
            form.AutoScroll = true;
            form.ClientSize = new Size(Scale(form.ClientSize.Width, factor), Scale(form.ClientSize.Height, factor));
            ScaleControls(form.Controls, factor);
            form.ResumeLayout(false);
        }

        static void ScaleControls(Control.ControlCollection controls, float factor)
        {
            foreach (Control c in controls)
            {
                c.SetBounds(Scale(c.Left, factor), Scale(c.Top, factor), Scale(c.Width, factor), Scale(c.Height, factor));
                if (c.Controls.Count > 0) ScaleControls(c.Controls, factor);
            }
        }
    }

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
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(360, 178);

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

            var rbFirst = new RadioButton(); rbFirst.Text = "\u81EA\u52A8"; rbFirst.Left = 94; rbFirst.Top = 5; rbFirst.AutoSize = true; rbFirst.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbFirst.Checked = UseFirstBase;
            var rbPick = new RadioButton(); rbPick.Text = "\u624B\u52A8"; rbPick.Left = 170; rbPick.Top = 5; rbPick.AutoSize = true; rbPick.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f); rbPick.Checked = !UseFirstBase;
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
            DpiUtil.Apply(this);
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
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(400, 180);

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
            DpiUtil.Apply(this);
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
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(400, 150);

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
            DpiUtil.Apply(this);
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
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(400, 150);

            var l1 = new Label(); l1.Text = "\u663E\u793A\u540D\u79F0\uFF1A"; l1.Left = 16; l1.Top = 18; l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t1 = new TextBox(); t1.Left = 100; t1.Top = 14; t1.Width = 270; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var l2 = new Label(); l2.Text = "CAD\u547D\u4EE4\uFF1A"; l2.Left = 16; l2.Top = 52; l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t2 = new TextBox(); t2.Left = 100; t2.Top = 48; t2.Width = 270; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var ok = new Button(); ok.Text = "\u786E\u5B9A"; ok.DialogResult = DialogResult.OK; ok.Left = 210; ok.Top = 96; ok.Width = 80; ok.FlatStyle = FlatStyle.System; ok.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var cancel = new Button(); cancel.Text = "\u53D6\u6D88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 300; cancel.Top = 96; cancel.Width = 76; cancel.FlatStyle = FlatStyle.System; cancel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            ok.Click += delegate { CmdLabel = t1.Text.Trim(); CmdName = t2.Text.Trim(); };
            Controls.AddRange(new Control[] { l1, t1, l2, t2, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            DpiUtil.Apply(this);
        }
    }

    public enum TextNumberMode
    {
        Prefix,
        Suffix,
        Replace
    }

    public class TextNumberDialog : Form
    {
        public int StartNumber;
        public TextNumberMode Mode;

        public TextNumberDialog()
        {
            StartNumber = 1;
            Mode = TextNumberMode.Suffix;
            Text = "\u6587\u5B57\u7F16\u53F7";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None; AutoScroll = true; ClientSize = new Size(340, 112);

            var l3 = new Label(); l3.Text = "\u8D77\u59CB\u53F7\uFF1A"; l3.Left = 16; l3.Top = 16; l3.AutoSize = true; l3.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t3 = new TextBox(); t3.Left = 86; t3.Top = 12; t3.Width = 90; t3.Text = "1"; t3.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var lMode = new Label(); lMode.Text = "\u7F16\u53F7\u4F4D\u7F6E\uFF1A"; lMode.Left = 16; lMode.Top = 48; lMode.AutoSize = true; lMode.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var rbPrefix = new RadioButton(); rbPrefix.Text = "\u524D\u7F00"; rbPrefix.Left = 96; rbPrefix.Top = 46; rbPrefix.Width = 64; rbPrefix.Height = 24; rbPrefix.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var rbSuffix = new RadioButton(); rbSuffix.Text = "\u540E\u7F00"; rbSuffix.Left = 164; rbSuffix.Top = 46; rbSuffix.Width = 64; rbSuffix.Height = 24; rbSuffix.Checked = true; rbSuffix.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var rbReplace = new RadioButton(); rbReplace.Text = "\u66FF\u6362"; rbReplace.Left = 232; rbReplace.Top = 46; rbReplace.Width = 64; rbReplace.Height = 24; rbReplace.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var ok = new Button(); ok.Text = "\u786E\u5B9A"; ok.DialogResult = DialogResult.OK; ok.Left = 156; ok.Top = 78; ok.Width = 78; ok.Height = 26; ok.FlatStyle = FlatStyle.System;
            var cancel = new Button(); cancel.Text = "\u53D6\u6D88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 246; cancel.Top = 78; cancel.Width = 78; cancel.Height = 26; cancel.FlatStyle = FlatStyle.System;

            ok.Click += delegate
            {
                int n;
                StartNumber = int.TryParse(t3.Text.Trim(), out n) ? n : 1;
                Mode = rbReplace.Checked ? TextNumberMode.Replace : (rbPrefix.Checked ? TextNumberMode.Prefix : TextNumberMode.Suffix);
            };

            Controls.AddRange(new Control[] { l3, t3, lMode, rbPrefix, rbSuffix, rbReplace, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            Shown += delegate { t3.Focus(); t3.SelectAll(); };
            DpiUtil.Apply(this);
        }
    }

    public class BatchPlotPreflightRow
    {
        public string Index;
        public string SheetNumber;
        public string SheetName;
        public string Size;
        public string Orientation;
        public string Target;
        public string Status;
        public bool SizeMismatched;
        public int PositionOrder;
        public int SelectionOrder;
    }

    public class BatchPlotDialog : Form
    {
        public string DeviceName;
        public string PaperName;
        public string PlotStyle;
        public bool AutoRotate;
        public bool CenterPlot;
        public double MarginPercent;
        public double MarginMm;
        public string FileNameMode;
        public string SortMode;
        public bool ReverseOrder;
        readonly ToolTip outputDirectoryToolTip = new ToolTip();

        public BatchPlotDialog(int frameCount, string frameBlockName, List<BatchPlotPreflightRow> preflightRows, string outputDirectory, string drawingName)
        {
            DeviceName = Config.BatchPlotDevice;
            PaperName = Config.BatchPlotPaper;
            PlotStyle = Config.BatchPlotStyle;
            AutoRotate = Config.BatchPlotAutoRotate;
            CenterPlot = Config.BatchPlotCenter;
            MarginPercent = Config.BatchPlotMarginPercent;
            MarginMm = Config.BatchPlotMarginMm;
            FileNameMode = Config.BatchPlotFileNameMode;
            SortMode = Config.BatchPlotSortMode;
            ReverseOrder = Config.BatchPlotSortReverse;

            Text = "\u6279\u91CF\u6253\u5370";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(680, 560);

            var lblInfo = new Label();
            lblInfo.Text = string.Format("\u56FE\u6846\u5757\uFF1A{0}\uFF1B\u6570\u91CF\uFF1A{1}", string.IsNullOrEmpty(frameBlockName) ? "\u672A\u77E5" : frameBlockName, frameCount);
            lblInfo.Left = 16; lblInfo.Top = 14; lblInfo.Width = 648; lblInfo.Height = 24;
            lblInfo.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var lblDir = new Label();
            lblDir.Text = "\u8F93\u51FA\u76EE\u5F55\uFF1A";
            lblDir.Left = 16; lblDir.Top = 48; lblDir.AutoSize = true;
            lblDir.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var txtDir = new TextBox();
            txtDir.Left = 96; txtDir.Top = 44; txtDir.Width = 230; txtDir.ReadOnly = true;
            txtDir.Text = outputDirectory;
            txtDir.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            outputDirectoryToolTip.SetToolTip(txtDir, outputDirectory);

            var lblDevice = new Label();
            lblDevice.Text = "\u6253\u5370\u8BBE\u5907\uFF1A";
            lblDevice.Left = 16; lblDevice.Top = 82; lblDevice.AutoSize = true;
            lblDevice.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var cmbDevice = new ComboBox();
            cmbDevice.Left = 96; cmbDevice.Top = 78; cmbDevice.Width = 230;
            cmbDevice.DropDownStyle = ComboBoxStyle.DropDown;
            cmbDevice.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            AddPrinterName(cmbDevice, "DWG To PDF.pc3");
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                AddPrinterName(cmbDevice, printer);
            }
            cmbDevice.Text = DeviceName;

            var lblPaper = new Label();
            lblPaper.Text = "\u56FE\u7EB8\uFF1A";
            lblPaper.Left = 360; lblPaper.Top = 48; lblPaper.AutoSize = true;
            lblPaper.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var cmbPaper = new ComboBox();
            cmbPaper.Left = 430; cmbPaper.Top = 44; cmbPaper.Width = 78;
            cmbPaper.DropDownStyle = ComboBoxStyle.DropDown;
            cmbPaper.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            cmbPaper.Items.Add("A4");
            cmbPaper.Items.Add("A3");
            cmbPaper.Items.Add("A2");
            cmbPaper.Items.Add("A1");
            cmbPaper.Items.Add("A0");
            cmbPaper.Text = PaperName;

            var lblStyle = new Label();
            lblStyle.Text = "\u6837\u5F0F\uFF1A";
            lblStyle.Left = 16; lblStyle.Top = 116; lblStyle.AutoSize = true;
            lblStyle.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var cmbStyle = new ComboBox();
            cmbStyle.Left = 96; cmbStyle.Top = 112; cmbStyle.Width = 230;
            cmbStyle.DropDownStyle = ComboBoxStyle.DropDown;
            cmbStyle.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            foreach (string style in GetPlotStyleNames())
            {
                cmbStyle.Items.Add(style);
            }
            SelectAvailablePlotStyle(cmbStyle, PlotStyle);

            var lblMargin = new Label();
            lblMargin.Text = "\u9875\u8FB9\u8DDD\uFF1A";
            lblMargin.Left = 520; lblMargin.Top = 48; lblMargin.AutoSize = true;
            lblMargin.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var txtMargin = new TextBox();
            txtMargin.Left = 590; txtMargin.Top = 44; txtMargin.Width = 32;
            txtMargin.Text = MarginMm.ToString();
            txtMargin.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var lblMarginUnit = new Label();
            lblMarginUnit.Text = "mm";
            lblMarginUnit.Left = 626; lblMarginUnit.Top = 48; lblMarginUnit.AutoSize = true;
            lblMarginUnit.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var lblFileName = new Label();
            lblFileName.Text = "\u6587\u4EF6\u540D\uFF1A";
            lblFileName.Left = 360; lblFileName.Top = 82; lblFileName.AutoSize = true;
            lblFileName.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var cmbFileNameMode = new ComboBox();
            cmbFileNameMode.Left = 430; cmbFileNameMode.Top = 78; cmbFileNameMode.Width = 194;
            cmbFileNameMode.DropDownStyle = ComboBoxStyle.DropDown;
            cmbFileNameMode.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            AddFileNameMode(cmbFileNameMode, "DrawingDashIndex", "\u56FE\u540D-001");
            AddFileNameMode(cmbFileNameMode, "DrawingUnderscoreIndex", "\u56FE\u540D_001");
            AddFileNameMode(cmbFileNameMode, "SheetNumberName", "\u56FE\u53F7 \u56FE\u540D");
            AddFileNameMode(cmbFileNameMode, "IndexOnly", "001");
            SelectFileNameMode(cmbFileNameMode, FileNameMode);

            var lblSortMode = new Label();
            lblSortMode.Text = "\u6392\u5E8F\uFF1A";
            lblSortMode.Left = 360; lblSortMode.Top = 116; lblSortMode.AutoSize = true;
            lblSortMode.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var cmbSortMode = new ComboBox();
            cmbSortMode.Left = 430; cmbSortMode.Top = 112; cmbSortMode.Width = 194;
            cmbSortMode.DropDownStyle = ComboBoxStyle.DropDown;
            cmbSortMode.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            AddFileNameMode(cmbSortMode, "Position", "\u4F4D\u7F6E\u6392\u5E8F");
            AddFileNameMode(cmbSortMode, "SheetNumber", "\u56FE\u53F7\u6392\u5E8F");
            AddFileNameMode(cmbSortMode, "SelectionOrder", "\u9009\u62E9\u987A\u5E8F");
            SelectFileNameMode(cmbSortMode, SortMode);

            var chkRotate = new CheckBox();
            chkRotate.Text = "\u81EA\u52A8\u65CB\u8F6C";
            chkRotate.Left = 430; chkRotate.Top = 146; chkRotate.Width = 96;
            chkRotate.Checked = AutoRotate;
            chkRotate.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var chkCenter = new CheckBox();
            chkCenter.Text = "\u5C45\u4E2D\u6253\u5370";
            chkCenter.Left = 540; chkCenter.Top = 146; chkCenter.Width = 94;
            chkCenter.Checked = CenterPlot;
            chkCenter.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var chkSortForward = new CheckBox();
            chkSortForward.Text = "\u987A\u5E8F";
            chkSortForward.Left = 430; chkSortForward.Top = 170; chkSortForward.Width = 96;
            chkSortForward.Checked = !ReverseOrder;
            chkSortForward.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var chkSortReverse = new CheckBox();
            chkSortReverse.Text = "\u5012\u5E8F";
            chkSortReverse.Left = 540; chkSortReverse.Top = 170; chkSortReverse.Width = 94;
            chkSortReverse.Checked = ReverseOrder;
            chkSortReverse.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

            var lblSortRule = new Label();
            lblSortRule.Left = 16; lblSortRule.Top = 146; lblSortRule.Width = 388; lblSortRule.Height = 20;
            lblSortRule.ForeColor = Color.FromArgb(90, 90, 90);
            lblSortRule.Font = new System.Drawing.Font("Microsoft YaHei", 8.5f);

            var lblNote = new Label();
            lblNote.Text = "\u9884\u68C0\uFF1A\u8BF7\u6838\u5BF9\u56FE\u53F7\u3001\u56FE\u540D\u3001\u76EE\u6807\u548C\u72B6\u6001\u540E\u518D\u6253\u5370\u3002";
            lblNote.Left = 16; lblNote.Top = 170; lblNote.Width = 388; lblNote.Height = 22;
            lblNote.ForeColor = Color.FromArgb(90, 90, 90);
            lblNote.Font = new System.Drawing.Font("Microsoft YaHei", 8.5f);

            var preflightList = new ListView();
            preflightList.Left = 16; preflightList.Top = 194; preflightList.Width = 648; preflightList.Height = 298;
            preflightList.View = View.Details;
            preflightList.FullRowSelect = true;
            preflightList.GridLines = true;
            preflightList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            preflightList.Font = new System.Drawing.Font("Microsoft YaHei", 8.5f);
            preflightList.Columns.Add("\u5E8F\u53F7", 42);
            preflightList.Columns.Add("\u56FE\u53F7", 80);
            preflightList.Columns.Add("\u56FE\u540D", 170);
            preflightList.Columns.Add("\u5C3A\u5BF8", 95);
            preflightList.Columns.Add("\u65B9\u5411", 55);
            preflightList.Columns.Add("\u76EE\u6807", 120);
            preflightList.Columns.Add("\u72B6\u6001", 80);
            RebuildDialogBatchPlotPreflightList(preflightList, preflightRows);

            var lblWarning = new Label();
            lblWarning.Text = "\u68C0\u6D4B\u5230\u56FE\u6846\u5C3A\u5BF8\u4E0D\u4E00\u81F4\uFF0C\u8BF7\u786E\u8BA4\u662F\u5426\u6DF7\u9009\u3002";
            lblWarning.Left = 16; lblWarning.Top = 498; lblWarning.Width = 648; lblWarning.Height = 20;
            lblWarning.ForeColor = Color.FromArgb(170, 90, 0);
            lblWarning.Font = new System.Drawing.Font("Microsoft YaHei", 8.5f);
            lblWarning.Visible = HasMismatchedPreflightSize(preflightRows);

            var copyPreflight = new Button();
            copyPreflight.Text = "\u590D\u5236\u5217\u8868";
            copyPreflight.Left = 16; copyPreflight.Top = 524; copyPreflight.Width = 88; copyPreflight.Height = 28;
            copyPreflight.FlatStyle = FlatStyle.System;
            copyPreflight.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            copyPreflight.Enabled = preflightRows != null && preflightRows.Count > 0;
            copyPreflight.Click += delegate
            {
                string text = FormatPreflightRows(preflightRows);
                if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
            };
            outputDirectoryToolTip.SetToolTip(copyPreflight, "\u590D\u5236\u9884\u68C0\u5217\u8868\u5230\u526A\u8D34\u677F");

            bool updatingSortDirection = false;
            EventHandler refreshPreflight = delegate { RefreshBatchPlotPreflight(lblInfo, lblSortRule, preflightList, preflightRows, frameBlockName, frameCount, drawingName, cmbDevice.Text, GetDialogSelectedFileNameMode(cmbFileNameMode), GetDialogSelectedFileNameMode(cmbSortMode), chkSortReverse.Checked); };
            EventHandler syncSortDirection = delegate(object sender, EventArgs e)
            {
                if (updatingSortDirection) return;
                updatingSortDirection = true;
                if (sender == chkSortReverse)
                {
                    if (chkSortReverse.Checked) chkSortForward.Checked = false;
                    else if (!chkSortForward.Checked) chkSortForward.Checked = true;
                }
                else
                {
                    if (chkSortForward.Checked) chkSortReverse.Checked = false;
                    else if (!chkSortReverse.Checked) chkSortReverse.Checked = true;
                }
                updatingSortDirection = false;
                refreshPreflight(sender, e);
            };
            cmbDevice.TextChanged += refreshPreflight;
            cmbFileNameMode.SelectedIndexChanged += refreshPreflight;
            cmbSortMode.SelectedIndexChanged += refreshPreflight;
            chkSortForward.CheckedChanged += syncSortDirection;
            chkSortReverse.CheckedChanged += syncSortDirection;
            RefreshBatchPlotPreflight(lblInfo, lblSortRule, preflightList, preflightRows, frameBlockName, frameCount, drawingName, cmbDevice.Text, GetDialogSelectedFileNameMode(cmbFileNameMode), GetDialogSelectedFileNameMode(cmbSortMode), chkSortReverse.Checked);

            var ok = new Button();
            ok.Text = "\u786E\u5B9A";
            ok.DialogResult = DialogResult.OK;
            ok.Left = 496; ok.Top = 524; ok.Width = 78; ok.Height = 28;
            ok.FlatStyle = FlatStyle.System;
            ok.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var cancel = new Button();
            cancel.Text = "\u53D6\u6D88";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Left = 586; cancel.Top = 524; cancel.Width = 78; cancel.Height = 28;
            cancel.FlatStyle = FlatStyle.System;
            cancel.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            ok.Click += delegate
            {
                DeviceName = cmbDevice.Text.Trim();
                PaperName = cmbPaper.Text.Trim();
                PlotStyle = cmbStyle.Text.Trim();
                AutoRotate = chkRotate.Checked;
                CenterPlot = chkCenter.Checked;
                double margin;
                MarginMm = double.TryParse(txtMargin.Text.Trim(), out margin) ? Math.Max(0, margin) : 0;
                MarginPercent = MarginMm;
                FileNameMode = GetSelectedFileNameMode(cmbFileNameMode);
                SortMode = GetSelectedFileNameMode(cmbSortMode);
                ReverseOrder = chkSortReverse.Checked;
                Config.BatchPlotDevice = DeviceName;
                Config.BatchPlotPaper = PaperName;
                Config.BatchPlotStyle = PlotStyle;
                Config.BatchPlotAutoRotate = AutoRotate;
                Config.BatchPlotCenter = CenterPlot;
                Config.BatchPlotMarginPercent = MarginPercent;
                Config.BatchPlotMarginMm = MarginMm;
                Config.BatchPlotFileNameMode = FileNameMode;
                Config.BatchPlotSortMode = SortMode;
                Config.BatchPlotSortReverse = ReverseOrder;
            };

            Controls.AddRange(new Control[] { lblInfo, lblDir, txtDir, lblDevice, cmbDevice, lblPaper, cmbPaper, lblStyle, cmbStyle, lblMargin, txtMargin, lblMarginUnit, lblFileName, cmbFileNameMode, lblSortMode, cmbSortMode, chkRotate, chkCenter, chkSortForward, chkSortReverse, lblSortRule, lblNote, preflightList, lblWarning, copyPreflight, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            Shown += delegate { cmbDevice.Focus(); };
            DpiUtil.Apply(this);
            AutoScroll = false;
        }

        static string FormatPreflightRows(List<BatchPlotPreflightRow> rows)
        {
            if (rows == null || rows.Count == 0) return "";
            var lines = new List<string>();
            lines.Add("\u5E8F\u53F7\t\u56FE\u53F7\t\u56FE\u540D\t\u5C3A\u5BF8\t\u65B9\u5411\t\u76EE\u6807\t\u72B6\u6001");
            foreach (BatchPlotPreflightRow row in rows)
            {
                lines.Add(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", row.Index, row.SheetNumber, row.SheetName, row.Size, row.Orientation, row.Target, row.Status));
            }
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        static void RefreshBatchPlotPreflight(Label summary, Label sortRule, ListView list, List<BatchPlotPreflightRow> rows, string frameBlockName, int frameCount, string drawingName, string deviceName, string fileNameMode, string sortMode, bool reverseOrder)
        {
            bool outputToFile = IsDialogPdfPlotDevice(deviceName);
            string outputMode = outputToFile ? "\u8F93\u51FA\uFF1APDF" : "\u8F93\u51FA\uFF1A\u6253\u5370\u673A";
            summary.Text = string.Format("\u56FE\u6846\u5757\uFF1A{0}\uFF1B\u6570\u91CF\uFF1A{1}\uFF1B{2}", string.IsNullOrEmpty(frameBlockName) ? "\u672A\u77E5" : frameBlockName, frameCount, outputMode);
            if (sortRule != null) sortRule.Text = GetDialogBatchPlotSortRule(sortMode);

            if (rows == null || list == null) return;
            SortDialogBatchPlotPreflightRows(rows, sortMode);
            if (reverseOrder) rows.Reverse();
            ResetDialogDuplicateStatuses(rows);
            for (int i = 0; i < rows.Count; i++)
            {
                BatchPlotPreflightRow row = rows[i];
                row.Index = (i + 1).ToString("D3");
                row.Target = outputToFile ? BuildDialogBatchPlotOutputFileName(drawingName, i + 1, fileNameMode, row) : "\u53D1\u9001\u5230\u6253\u5370\u673A";
            }
            MarkDialogDuplicateTargets(rows, outputToFile);
            RebuildDialogBatchPlotPreflightList(list, rows);
        }

        static string GetDialogBatchPlotSortRule(string sortMode)
        {
            if (string.Equals(sortMode, "SelectionOrder", StringComparison.OrdinalIgnoreCase))
                return "\u6392\u5E8F\u89C4\u5219\uFF1A\u6309\u9009\u62E9\u56FE\u6846\u7684\u5148\u540E\u987A\u5E8F\u6392\u5E8F\u3002";
            if (string.Equals(sortMode, "SheetNumber", StringComparison.OrdinalIgnoreCase))
                return "\u6392\u5E8F\u89C4\u5219\uFF1A\u6309\u56FE\u53F7\u3001\u56FE\u540D\u6392\u5E8F\uFF1B\u7F3A\u5931\u65F6\u6309\u4F4D\u7F6E\u515C\u5E95\u3002";
            return "\u6392\u5E8F\u89C4\u5219\uFF1A\u6309\u4F4D\u7F6E\u9010\u5217\u6392\u5E8F\uFF0C\u540C\u4E00\u5217\u4ECE\u4E0A\u5230\u4E0B\uFF0C\u518D\u4ECE\u5DE6\u5230\u53F3\u3002";
        }

        static void SortDialogBatchPlotPreflightRows(List<BatchPlotPreflightRow> rows, string sortMode)
        {
            if (rows == null) return;
            if (string.Equals(sortMode, "SelectionOrder", StringComparison.OrdinalIgnoreCase))
            {
                rows.Sort(CompareDialogSelectionOrder);
                return;
            }
            if (string.Equals(sortMode, "SheetNumber", StringComparison.OrdinalIgnoreCase))
            {
                rows.Sort(delegate(BatchPlotPreflightRow a, BatchPlotPreflightRow b)
                {
                    int bySheetNumber = string.Compare(SafeDialogString(a == null ? null : a.SheetNumber), SafeDialogString(b == null ? null : b.SheetNumber), StringComparison.OrdinalIgnoreCase);
                    if (bySheetNumber != 0) return bySheetNumber;
                    int bySheetName = string.Compare(SafeDialogString(a == null ? null : a.SheetName), SafeDialogString(b == null ? null : b.SheetName), StringComparison.OrdinalIgnoreCase);
                    if (bySheetName != 0) return bySheetName;
                    return CompareDialogPositionOrder(a, b);
                });
                return;
            }
            rows.Sort(CompareDialogPositionOrder);
        }

        static int CompareDialogSelectionOrder(BatchPlotPreflightRow a, BatchPlotPreflightRow b)
        {
            int left = a == null ? int.MaxValue : a.SelectionOrder;
            int right = b == null ? int.MaxValue : b.SelectionOrder;
            return left.CompareTo(right);
        }

        static int CompareDialogPositionOrder(BatchPlotPreflightRow a, BatchPlotPreflightRow b)
        {
            int left = a == null ? int.MaxValue : a.PositionOrder;
            int right = b == null ? int.MaxValue : b.PositionOrder;
            return left.CompareTo(right);
        }

        static void RebuildDialogBatchPlotPreflightList(ListView list, List<BatchPlotPreflightRow> rows)
        {
            if (list == null) return;
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                foreach (BatchPlotPreflightRow row in rows ?? new List<BatchPlotPreflightRow>())
                {
                    var item = new ListViewItem(row.Index ?? "");
                    item.SubItems.Add(row.SheetNumber ?? "");
                    item.SubItems.Add(row.SheetName ?? "");
                    item.SubItems.Add(row.Size ?? "");
                    item.SubItems.Add(row.Orientation ?? "");
                    item.SubItems.Add(row.Target ?? "");
                    item.SubItems.Add(row.Status ?? "");
                    list.Items.Add(item);
                }
            }
            finally
            {
                list.EndUpdate();
            }
        }

        static string SafeDialogString(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Trim();
        }

        static void ResetDialogDuplicateStatuses(List<BatchPlotPreflightRow> rows)
        {
            if (rows == null) return;
            foreach (BatchPlotPreflightRow row in rows)
            {
                row.Status = RemoveDialogStatus(row.Status, "\u6587\u4EF6\u540D\u91CD\u590D");
            }
        }

        static void MarkDialogDuplicateTargets(List<BatchPlotPreflightRow> rows, bool outputToFile)
        {
            if (!outputToFile || rows == null) return;
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (BatchPlotPreflightRow row in rows)
            {
                string key = string.IsNullOrEmpty(row.Target) ? "" : row.Target.Trim();
                if (string.IsNullOrEmpty(key)) continue;
                counts[key] = counts.ContainsKey(key) ? counts[key] + 1 : 1;
            }
            foreach (BatchPlotPreflightRow row in rows)
            {
                string key = string.IsNullOrEmpty(row.Target) ? "" : row.Target.Trim();
                if (!string.IsNullOrEmpty(key) && counts.ContainsKey(key) && counts[key] > 1)
                    row.Status = AppendDialogStatus(row.Status, "\u6587\u4EF6\u540D\u91CD\u590D");
            }
        }

        static string AppendDialogStatus(string status, string addition)
        {
            if (string.IsNullOrEmpty(status) || status == "\u6B63\u5E38") return addition;
            if (status.IndexOf(addition, StringComparison.OrdinalIgnoreCase) >= 0) return status;
            return status + "\uFF1B" + addition;
        }

        static string RemoveDialogStatus(string status, string target)
        {
            if (string.IsNullOrEmpty(status)) return "\u6B63\u5E38";
            var kept = new List<string>();
            foreach (string part in status.Split(new char[] { '\uFF1B' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!part.Equals(target, StringComparison.OrdinalIgnoreCase) && !part.Equals("\u6B63\u5E38", StringComparison.OrdinalIgnoreCase))
                    kept.Add(part);
            }
            return kept.Count == 0 ? "\u6B63\u5E38" : string.Join("\uFF1B", kept.ToArray());
        }

        static bool IsDialogPdfPlotDevice(string deviceName)
        {
            string rawName = string.IsNullOrEmpty(deviceName) ? "" : deviceName.Trim();
            string name = rawName.ToUpperInvariant();
            if (name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (name.IndexOf("ADOBE PDF", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("PDF24", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("MICROSOFT PRINT TO PDF", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return rawName.IndexOf("DWG To PDF", StringComparison.OrdinalIgnoreCase) >= 0
                || name.EndsWith(".PC3", StringComparison.OrdinalIgnoreCase);
        }

        static string BuildDialogBatchPlotOutputFileName(string drawingName, int index, string fileNameMode, BatchPlotPreflightRow row)
        {
            string name = string.IsNullOrEmpty(drawingName) ? "Drawing" : drawingName;
            string serial = index.ToString("D3");
            string mode = string.IsNullOrEmpty(fileNameMode) ? "DrawingDashIndex" : fileNameMode;
            if (mode.Equals("IndexOnly", StringComparison.OrdinalIgnoreCase)) return serial + ".pdf";
            if (mode.Equals("DrawingUnderscoreIndex", StringComparison.OrdinalIgnoreCase)) return name + "_" + serial + ".pdf";
            if (mode.Equals("SheetNumberName", StringComparison.OrdinalIgnoreCase))
            {
                string sheetStem = ((row == null ? "" : row.SheetNumber) + " " + (row == null ? "" : row.SheetName)).Trim();
                return (string.IsNullOrEmpty(sheetStem) ? serial : sheetStem) + ".pdf";
            }
            return name + "-" + serial + ".pdf";
        }

        static string GetDialogSelectedFileNameMode(ComboBox combo)
        {
            return GetSelectedFileNameMode(combo);
        }

        static bool HasMismatchedPreflightSize(List<BatchPlotPreflightRow> rows)
        {
            if (rows == null) return false;
            foreach (BatchPlotPreflightRow row in rows)
                if (row != null && row.SizeMismatched) return true;
            return false;
        }

        class FileNameModeItem
        {
            public string Value;
            public string Text;
            public override string ToString() { return Text; }
        }

        static void AddFileNameMode(ComboBox combo, string value, string text)
        {
            combo.Items.Add(new FileNameModeItem { Value = value, Text = text });
        }

        static void SelectFileNameMode(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as FileNameModeItem;
                if (item != null && string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        static string GetSelectedFileNameMode(ComboBox combo)
        {
            var item = combo.SelectedItem as FileNameModeItem;
            return item == null ? "DrawingDashIndex" : item.Value;
        }

        static List<string> GetPlotStyleNames()
        {
            var names = new List<string>();
            AddCadPlotStyleNames(names);

            AddPlotStyleName(names, "monochrome.ctb");
            AddPlotStyleName(names, "acad.ctb");
            AddPlotStyleName(names, "grayscale.ctb");

            SortPlotStyleNames(names, Config.BatchPlotStyle);
            return names;
        }

        static void SortPlotStyleNames(List<string> names, string currentStyle)
        {
            names.Sort(delegate(string a, string b)
            {
                int byPriority = GetPlotStyleSortPriority(a, currentStyle).CompareTo(GetPlotStyleSortPriority(b, currentStyle));
                if (byPriority != 0) return byPriority;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
        }

        static int GetPlotStyleSortPriority(string name, string currentStyle)
        {
            if (!string.IsNullOrEmpty(currentStyle) && string.Equals(name, currentStyle, StringComparison.OrdinalIgnoreCase)) return 0;
            if (ContainsCjk(name) && !IsBuiltInPlotStyle(name)) return 1;
            if (!IsBuiltInPlotStyle(name)) return 2;
            return 3;
        }

        static void AddCadPlotStyleNames(List<string> names)
        {
            try
            {
                Type validatorType = null;
                foreach (string typeName in GetCadPlotSettingsValidatorTypeNames())
                {
                    validatorType = FindCadType(typeName);
                    if (validatorType != null) break;
                }
                if (validatorType == null) return;

                object validator = validatorType.InvokeMember("Current", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Static, null, null, null);
                if (validator == null) return;

                object list = validator.GetType().InvokeMember("GetPlotStyleSheetList", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, validator, null);
                var enumerable = list as System.Collections.IEnumerable;
                if (enumerable == null) return;

                foreach (object item in enumerable)
                {
                    AddPlotStyleName(names, Convert.ToString(item));
                }
            }
            catch { }
        }

        static IEnumerable<string> GetCadPlotSettingsValidatorTypeNames()
        {
            string host = GetCurrentCadHostKey();
            if (host == "ZWCAD")
            {
                yield return "ZwSoft.ZwCAD.DatabaseServices.PlotSettingsValidator";
                yield return "GrxCAD.DatabaseServices.PlotSettingsValidator";
                yield return "Autodesk.AutoCAD.DatabaseServices.PlotSettingsValidator";
            }
            else if (host == "GSTARCAD")
            {
                yield return "GrxCAD.DatabaseServices.PlotSettingsValidator";
                yield return "ZwSoft.ZwCAD.DatabaseServices.PlotSettingsValidator";
                yield return "Autodesk.AutoCAD.DatabaseServices.PlotSettingsValidator";
            }
            else
            {
                yield return "Autodesk.AutoCAD.DatabaseServices.PlotSettingsValidator";
                yield return "ZwSoft.ZwCAD.DatabaseServices.PlotSettingsValidator";
                yield return "GrxCAD.DatabaseServices.PlotSettingsValidator";
            }
            yield return "GcDb.PlotSettingsValidator";
            yield return "OdDbPlotSettingsValidator";
        }

        static string GetCurrentCadHostKey()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name;
                if (string.Equals(name, "ZwManaged", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "ZwDatabaseMgd", StringComparison.OrdinalIgnoreCase)) return "ZWCAD";
                if (string.Equals(name, "gmap", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "gmdb", StringComparison.OrdinalIgnoreCase)) return "GSTARCAD";
                if (string.Equals(name, "acmgd", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "acdbmgd", StringComparison.OrdinalIgnoreCase)) return "AUTOCAD";
            }
            return "";
        }

        static void SelectAvailablePlotStyle(ComboBox combo, string configuredStyle)
        {
            if (combo == null) return;
            if (!string.IsNullOrEmpty(configuredStyle))
            {
                foreach (object item in combo.Items)
                {
                    string text = Convert.ToString(item);
                    if (string.Equals(text, configuredStyle, StringComparison.OrdinalIgnoreCase))
                    {
                        combo.Text = text;
                        return;
                    }
                }
            }
            if (combo.Items.Count > 0) combo.Text = Convert.ToString(combo.Items[0]);
            else combo.Text = configuredStyle ?? "";
        }

        static Type FindCadType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = asm.GetType(fullName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return Type.GetType(fullName, false);
        }

        static void AddPlotStyleName(List<string> names, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!IsCtbPlotStyleName(name)) return;
            foreach (string existing in names)
            {
                if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) return;
            }
            names.Add(name);
        }

        static bool IsCtbPlotStyleName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.Trim().EndsWith(".ctb", StringComparison.OrdinalIgnoreCase);
        }

        static bool ContainsCjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char ch in text)
            {
                if (ch >= '\u4E00' && ch <= '\u9FFF') return true;
            }
            return false;
        }

        static bool IsBuiltInPlotStyle(string name)
        {
            string normalized = SafeStyleName(name);
            return normalized == "ACAD.CTB"
                || normalized == "ACAD.STB"
                || normalized == "MONOCHROME.CTB"
                || normalized == "GRAYSCALE.CTB"
                || normalized == "COLOR.STB"
                || normalized == "MONO.STB"
                || normalized == "GCAD.CTB"
                || normalized == "GCAD.STB"
                || normalized.StartsWith("AUTODESK-", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("DWF", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("FILL PATTERNS", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("SCREENING", StringComparison.OrdinalIgnoreCase);
        }

        static string SafeStyleName(string name)
        {
            return string.IsNullOrEmpty(name) ? "" : name.Trim().ToUpperInvariant();
        }

        static void AddPrinterName(ComboBox combo, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!ContainsPrinterName(combo, name)) combo.Items.Add(name);
        }

        static bool ContainsPrinterName(ComboBox combo, string name)
        {
            foreach (object item in combo.Items)
            {
                string existing = Convert.ToString(item);
                if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
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
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(400, 300);

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
            DpiUtil.Apply(this);
        }
    }
}

