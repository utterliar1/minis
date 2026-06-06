using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections;
using CadToolkit.Core;
using CadToolkit.UI;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using GrxCAD.Runtime;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

#if GSTARCAD
[assembly: CommandClass(typeof(CadToolkit.CadCommands))]
[assembly: ExtensionApplication(typeof(CadToolkit.CadPlugin))]
#else
[assembly: CommandClass(typeof(CadToolkit.CadCommands))]
#endif

namespace CadToolkit
{
#if GSTARCAD
    public class CadPlugin : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(CadPlugin).Assembly.Location);
                System.AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, System.ResolveEventArgs e)
                {
                    string name = new System.Reflection.AssemblyName(e.Name).Name + ".dll";
                    string p2 = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(p2)) return System.Reflection.Assembly.LoadFrom(p2);
                    return null;
                };
                Config.Init(typeof(CadPlugin).Assembly.Location);
            }
            catch { }
        }
        public void Terminate() { }
    }
#endif

    public class CadCommands
    {
        static bool _initDone;
        static ObjectId[] _pendingSelection;
        static Dictionary<string, ObjectId[]> _isoState = new Dictionary<string, ObjectId[]>();

        static void EnsureInit()
        {
            if (_initDone) return;
            _initDone = true;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(CadCommands).Assembly.Location);
                System.AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, System.ResolveEventArgs e)
                {
                    string name = new System.Reflection.AssemblyName(e.Name).Name + ".dll";
                    string p2 = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(p2)) return System.Reflection.Assembly.LoadFrom(p2);
                    return null;
                };
                Config.Init(typeof(CadCommands).Assembly.Location);
            }
            catch { }
        }

        static Editor Ed { get { return CadApp.DocumentManager.MdiActiveDocument.Editor; } }
        static Database Db { get { return CadApp.DocumentManager.MdiActiveDocument.Database; } }
        static string DocKey { get { var d = CadApp.DocumentManager.MdiActiveDocument; if (d == null) return ""; try { return d.Name; } catch { try { return d.Database.Filename; } catch { return d.GetHashCode().ToString(); } } } }
        static string SafeStr(string s) { return s == null ? "" : s; }
        static string PlatformName
        {
            get
            {
#if AUTOCAD
                return "AutoCAD";
#elif ZWCAD
                return "ZWCAD";
#elif GSTARCAD
                return "GstarCAD";
#else
                return "CAD";
#endif
            }
        }

        static ObjectId GetTextStyleId(Transaction tr, string name)
        {
            var tst = (TextStyleTable)tr.GetObject(Db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has(name)) return tst[name];
            return Db.Textstyle;
        }

        static string ReplaceEx(string src, string oldStr, string rep, StringComparison comp)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(oldStr)) return src;
            int idx = 0; var sb = new StringBuilder();
            while (idx <= src.Length)
            {
                int pos = src.IndexOf(oldStr, idx, comp);
                if (pos < 0) { sb.Append(src, idx, src.Length - idx); break; }
                sb.Append(src, idx, pos - idx).Append(rep);
                idx = pos + oldStr.Length;
            }
            return sb.ToString();
        }

        static bool CheckDoc()
        {
            if (CadApp.DocumentManager.MdiActiveDocument == null)
            {
                MessageBox.Show("\u8bf7\u5148\u6253\u5f00\u4e00\u4e2a\u56fe\u7ebf\u6587\u4ef6\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        static PromptSelectionResult GetPendingOrSelection()
        {
            if (_pendingSelection != null && _pendingSelection.Length > 0)
            {
                var ids = _pendingSelection;
                _pendingSelection = null;
                Ed.SetImpliedSelection(ids);
                var result = Ed.SelectImplied();
                if (result.Status == PromptStatus.OK && result.Value != null && result.Value.Count > 0)
                    return result;
            }
            var psr = Ed.SelectImplied();
            if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
                return psr;
            return Ed.GetSelection();
        }

        static void Log(string msg)
        {
            try
            {
                string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CadToolkit.log");
                System.IO.File.AppendAllText(logPath, string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg));
            }
            catch { }
        }
        [CommandMethod("CT_PANEL")]
        public void ShowPanel()
        {
            EnsureInit();
            var groups = Config.GetCommandGroups();
            int totalCmds = 0;
            foreach (var g in groups) totalCmds += g.Commands.Count;
            if (totalCmds == 0)
            {
                MessageBox.Show("\u6ca1\u6709\u914d\u7f6e\u4efb\u4f55\u547d\u4ee4\u3002\n\u8bf7\u7f16\u8f91 CadToolkit.ini \u7684 [Commands] \u6bb5\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int groupCols = groups.Count <= 4 ? 2 : (groups.Count <= 9 ? 3 : 4);
            int innerCols = 2;
            int bw = 96, bh = 28, gap = 3;
            int headerH = 22;
            int cellPad = 6;

            int maxRows = 0;
            foreach (var g in groups)
            {
                int rows = (int)Math.Ceiling(g.Commands.Count / (double)innerCols);
                if (rows > maxRows) maxRows = rows;
            }

            int cellW = cellPad + innerCols * (bw + gap) - gap + cellPad;
            int cellH = headerH + cellPad + maxRows * (bh + gap) - gap + cellPad;
            int groupGap = 6;
            int totalW = groupGap + groupCols * (cellW + groupGap);
            int groupRows = (int)Math.Ceiling(groups.Count / (double)groupCols);
            int totalH = groupGap + groupRows * (cellH + groupGap) + 32;

            var f = new Form();
            f.Text = "CadToolkit - " + PlatformName;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            f.TopMost = true;
            f.ShowInTaskbar = false;
            f.AutoScaleDimensions = new System.Drawing.SizeF(96f, 96f);
            f.AutoScaleMode = AutoScaleMode.Dpi;
            f.ClientSize = new Size(totalW, totalH);
            f.BackColor = Color.FromArgb(240, 240, 240);

            var btnCancel = new Button();
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Size = new Size(0, 0);
            btnCancel.Location = new Point(-100, -100);
            f.Controls.Add(btnCancel);
            f.CancelButton = btnCancel;

            string action = null;

            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                int gx = groupGap + (gi % groupCols) * (cellW + groupGap);
                int gy = groupGap + (gi / groupCols) * (cellH + groupGap);

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
                lbl.Font = new System.Drawing.Font("Microsoft YaHei", 9f, FontStyle.Bold);
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
                    b.Font = new System.Drawing.Font("Microsoft YaHei", 8.5f);
                    b.Size = new Size(bw, bh);
                    b.Location = new Point(cellPad + (i % innerCols) * (bw + gap), btnY0 + (i / innerCols) * (bh + gap));
                    string cmdName = cmd;
                    b.Click += delegate { action = "CMD:" + cmdName; f.Close(); };
                    pnl.Controls.Add(b);
                }
                f.Controls.Add(pnl);
            }

            var bar = new Panel();
            bar.Location = new Point(0, totalH - 30);
            bar.Size = new Size(totalW, 30);
            bar.BackColor = Color.FromArgb(240, 240, 240);

            var btnAdd = new Button();
            btnAdd.Text = "+";
            btnAdd.FlatStyle = FlatStyle.Flat;
            btnAdd.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnAdd.BackColor = Color.White;
            btnAdd.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            btnAdd.Size = new Size(24, 22);
            btnAdd.Location = new Point(totalW - 56, 4);
            btnAdd.Click += delegate { action = "ADD"; f.Close(); };

            var btnManage = new Button();
            btnManage.Text = "-";
            btnManage.FlatStyle = FlatStyle.Flat;
            btnManage.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnManage.BackColor = Color.White;
            btnManage.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            btnManage.Size = new Size(24, 22);
            btnManage.Location = new Point(totalW - 26, 4);
            btnManage.Click += delegate { action = "MANAGE"; f.Close(); };

            var lblAuthor = new Label();
            lblAuthor.Text = Config.Version + " | WLUP";
            lblAuthor.AutoSize = false;
            lblAuthor.Size = new Size(120, 22);
            lblAuthor.Location = new Point(groupGap, 4);
            lblAuthor.TextAlign = ContentAlignment.MiddleLeft;
            lblAuthor.ForeColor = Color.FromArgb(160, 160, 160);
            lblAuthor.Font = new System.Drawing.Font("Microsoft YaHei", 7.5f);

            bar.Controls.Add(btnAdd);
            bar.Controls.Add(btnManage);
            bar.Controls.Add(lblAuthor);
            f.Controls.Add(bar);

            if (!CheckDoc()) return;

            _pendingSelection = null;
            try
            {
                string pfPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(CadCommands).Assembly.Location), "..", "pickfirst.txt");
                if (System.IO.File.Exists(pfPath))
                {
                    string handleStr = System.IO.File.ReadAllText(pfPath).Trim();
                    try { System.IO.File.Delete(pfPath); } catch {}
                    if (!string.IsNullOrEmpty(handleStr))
                    {
                        string[] handles = handleStr.Split(new char[] { ',' });
                        var ids = new List<ObjectId>();
                        foreach (string h in handles)
                        {
                            try
                            {
                                long val = long.Parse(h.Trim(), System.Globalization.NumberStyles.HexNumber);
                                ObjectId oid = Db.GetObjectId(false, new Handle(val), 0);
                                if (oid.IsValid) ids.Add(oid);
                            }
                            catch { }
                        }
                        if (ids.Count > 0) _pendingSelection = ids.ToArray();
                    }
                }
            }
            catch { }

            f.ShowDialog();
            f.Dispose();

            if (action == null) return;
            if (action.StartsWith("CMD:"))
            {
                string cmdName = action.Substring(4);
                System.EventHandler idle = null;
                idle = delegate(object sender, System.EventArgs ea)
                {
                    try { CadApp.Idle -= idle; } catch {}
                    CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute(cmdName + " ", true, false, true);
                };
                CadApp.Idle += idle;
            }
            else if (action == "ADD")
            {
                using (var dlg = new AddCommandDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK && dlg.CmdLabel != null && dlg.CmdLabel.Length > 0 && dlg.CmdName != null && dlg.CmdName.Length > 0)
                        Config.SaveCommand(dlg.CmdLabel, dlg.CmdName);
                }
            }
            else if (action == "MANAGE")
            {
                using (var dlg = new ManageCommandsDialog()) { dlg.ShowDialog(); }
            }
        }        [CommandMethod("CT_FINDREPLACE")]
        public void FindReplace()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            using (var dlg = new FindReplaceDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (string.IsNullOrEmpty(dlg.FindText)) { Ed.WriteMessage("\n\u67e5\u627e\u5185\u5bb9\u4e3a\u7a7a\u3002"); return; }
                int count = 0;
                StringComparison cmp = dlg.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForRead);
                    count += ReplaceInBlock(tr, msBtr, dlg.FindText, dlg.ReplaceText, cmp);
                    var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        if (btr.Name.StartsWith("*") || btr.Name.Equals("Model_Space") || btr.Name.StartsWith("Paper_Space")) continue;
                        count += ReplaceInBlock(tr, btr, dlg.FindText, dlg.ReplaceText, cmp);
                    }
                    tr.Commit();
                }
                Ed.WriteMessage(string.Format("\n\u5df2\u66ff\u6362 {0} \u5904\u3002", count));
            }
        }
        static int ReplaceInBlock(Transaction tr, BlockTableRecord btr, string findText, string replaceText, StringComparison cmp)
        {
            int count = 0;
            foreach (ObjectId id in btr)
            {
                var obj = tr.GetObject(id, OpenMode.ForRead);
                if (obj is DBText)
                {
                    var dt = (DBText)obj;
                    string txt = SafeStr(dt.TextString);
                    string ntxt = ReplaceEx(txt, findText, replaceText, cmp);
                    if (ntxt != txt) { dt.UpgradeOpen(); dt.TextString = ntxt; count++; }
                }
                else if (obj is MText)
                {
                    var mt = (MText)obj;
                    string txt = SafeStr(mt.Contents);
                    string ntxt = ReplaceEx(txt, findText, replaceText, cmp);
                    if (ntxt != txt) { mt.UpgradeOpen(); mt.Contents = ntxt; count++; }
                }
                else if (obj is BlockReference)
                {
                    var br = (BlockReference)obj;
                    if (br.AttributeCollection.Count > 0)
                    {
                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                            if (att == null) continue;
                            string txt = SafeStr(att.TextString);
                            string ntxt = ReplaceEx(txt, findText, replaceText, cmp);
                            if (ntxt != txt) { att.UpgradeOpen(); att.TextString = ntxt; count++; }
                        }
                    }
                }
            }
            return count;
        }
        [CommandMethod("CT_ALIGN")]
        public void AlignText()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            using (var dlg = new AlignDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                int h = dlg.HorzIndex;
                bool useFirst = dlg.UseFirstBase;
                double spacing = dlg.LineSpacing;
                var texts = new List<DBText>();
                double baseX = 0;
                double baseY = 0;
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead);
                        if (ent is DBText) texts.Add((DBText)ent);
                    }
                    if (texts.Count == 0) { Ed.WriteMessage("\n\u672a\u627e\u5230\u5355\u884c\u6587\u5b57\u3002"); return; }
                    texts.Sort(delegate(DBText a, DBText b) { return b.Position.Y.CompareTo(a.Position.Y); });
                    if (spacing <= 0)
                    {
                        double maxH = 0;
                        foreach (var t in texts) { if (t.Height > maxH) maxH = t.Height; }
                        spacing = maxH * 1.8;
                    }
                    if (useFirst)
                    {
                        var t0 = texts[0];
                        double tw = 0;
                        try { tw = t0.WidthFactor * t0.TextString.Length * t0.Height * 0.6; } catch { }
                        if (h == 0) baseX = t0.Position.X;
                        else if (h == 1) baseX = t0.Position.X + tw / 2.0;
                        else baseX = t0.Position.X + tw;
                        baseY = t0.Position.Y;
                    }
                    else
                    {
                        var ppr = Ed.GetPoint("\n\u6307\u5b9a\u5bf9\u9f50\u57fa\u70b9\uff1a");
                        if (ppr.Status != PromptStatus.OK) return;
                        baseX = ppr.Value.X;
                        baseY = ppr.Value.Y;
                    }
                    for (int i = 0; i < texts.Count; i++)
                    {
                        var dt = texts[i];
                        double tw = 0;
                        try { tw = dt.WidthFactor * dt.TextString.Length * dt.Height * 0.6; } catch { }
                        double nx = baseX;
                        if (h == 1) nx = baseX - tw / 2.0;
                        else if (h == 2) nx = baseX - tw;
                        double ny = baseY - i * spacing;
                        dt.UpgradeOpen();
                        dt.Position = new Point3d(nx, ny, 0);
                    }
                    tr.Commit();
                }
                Ed.WriteMessage(string.Format("\n\u5df2\u5bf9\u9f50 {0} \u4e2a\u6587\u5b57\u3002", texts.Count));
            }
        }        [CommandMethod("CT_UNDERLINE")]
        public void UnderlineText()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            bool keep = Config.KeepOriginal;
            int count = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    if (!(ent is DBText)) continue;
                    var dt = (DBText)ent;
                    var mt = new MText();
                    mt.Contents = "\\L" + SafeStr(dt.TextString) + "\\l";
                    mt.Location = dt.Position;
                    mt.TextHeight = dt.Height;
                    mt.Rotation = dt.Rotation;
                    mt.Layer = dt.Layer;
                    mt.Color = dt.Color;
                    mt.TextStyleId = dt.TextStyleId;
                    msBtr.AppendEntity(mt);
                    tr.AddNewlyCreatedDBObject(mt, true);
                    if (!keep) { dt.UpgradeOpen(); dt.Erase(); }
                    count++;
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u8f6c\u6362 {0} \u4e2a\u6587\u5b57\u4e3a\u5e26\u4e0b\u5212\u7ebf MText\u3002", count));
        }
        [CommandMethod("CT_RENAMEBLOCK")]
        public void RenameBlock()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            ObjectId pickedId = default(ObjectId);
            if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead);
                        if (ent is BlockReference) { pickedId = id; break; }
                    }
                    tr.Commit();
                }
            }
            if (!pickedId.IsValid)
            {
                var peo = new PromptEntityOptions("\n\u9009\u62e9\u8981\u91cd\u547d\u540d\u7684\u5757\uff1a");
                peo.SetRejectMessage("\n\u53ea\u80fd\u9009\u62e9\u5757\u53c2\u7167\u3002");
                peo.AddAllowedClass(typeof(BlockReference), true);
                var per = Ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;
                pickedId = per.ObjectId;
            }
            string oldName = "";
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var br = (BlockReference)tr.GetObject(pickedId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                oldName = btr.Name;
                tr.Commit();
            }
            using (var dlg = new RenameBlockDialog(oldName))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (string.IsNullOrEmpty(dlg.NewName))
                {
                    Ed.WriteMessage("\n\u65b0\u540d\u79f0\u4e0d\u80fd\u4e3a\u7a7a\u3002");
                    return;
                }
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                    if (bt.Has(dlg.NewName))
                    {
                        Ed.WriteMessage(string.Format("\n\u5757 \"{0}\" \u5df2\u5b58\u5728\u3002", dlg.NewName));
                        return;
                    }
                    var btr = (BlockTableRecord)tr.GetObject(bt[oldName], OpenMode.ForWrite);
                    btr.Name = dlg.NewName;
                    tr.Commit();
                    Ed.WriteMessage(string.Format("\n\u5df2\u5c06\u5757 \"{0}\" \u91cd\u547d\u540d\u4e3a \"{1}\"\u3002", oldName, dlg.NewName));
                }
            }
        }
        [CommandMethod("CT_QUICKBLOCK")]
        public void QuickBlock()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            var ppr = Ed.GetPoint("\n\u6307\u5b9a\u5757\u57fa\u70b9\uff1a");
            if (ppr.Status != PromptStatus.OK) return;
            string prefix = Config.Prefix;
            bool del = Config.DeleteOriginal;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForWrite);
                int idx = 1;
                string name;
                do { name = string.Format("{0}{1:D3}", prefix, idx++); } while (bt.Has(name));
                var btr = new BlockTableRecord();
                btr.Name = name;
                btr.Origin = ppr.Value;
                bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);
                var ids = new ObjectIdCollection(psr.Value.GetObjectIds());
                var mapping = new IdMapping();
                Db.DeepCloneObjects(ids, btr.Id, mapping, false);
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                var br = new BlockReference(ppr.Value, btr.Id);
                msBtr.AppendEntity(br);
                tr.AddNewlyCreatedDBObject(br, true);
                if (del)
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        tr.GetObject(id, OpenMode.ForWrite).Erase();
                    }
                }
                tr.Commit();
                Ed.WriteMessage(string.Format("\n\u5df2\u521b\u5efa\u5757 \"{0}\" \uff0c\u5305\u542b {1} \u4e2a\u5bf9\u8c61\u3002", name, ids.Count));
            }
        }        [CommandMethod("CT_SETLAYER0")]
        public void SetLayer0()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            int count = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(Db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has("0"))
                {
                    lt.UpgradeOpen();
                    var ltr = new LayerTableRecord();
                    ltr.Name = "0";
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    if (ent is BlockReference)
                    {
                        var br = (BlockReference)ent;
                        br.UpgradeOpen();
                        br.Layer = "0";
                        br.ColorIndex = 256;
                        count++;
                        if (br.BlockTableRecord.IsValid)
                            count += SetBlockLayer0(tr, br.BlockTableRecord);
                    }
                    else
                    {
                        ent.UpgradeOpen();
                        ent.Layer = "0";
                        ent.ColorIndex = 256;
                        count++;
                    }
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u5c06 {0} \u4e2a\u5bf9\u8c61\u6539\u5230 0 \u5c42\u3002", count));
        }
        static int SetBlockLayer0(Transaction tr, ObjectId btrId)
        {
            int count = 0;
            var btr = tr.GetObject(btrId, OpenMode.ForWrite) as BlockTableRecord;
            if (btr == null) return 0;
            foreach (ObjectId entId in btr)
            {
                var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (ent is BlockReference)
                {
                    var innerBr = (BlockReference)ent;
                    innerBr.UpgradeOpen();
                    innerBr.Layer = "0";
                    innerBr.ColorIndex = 256;
                    count++;
                    if (innerBr.BlockTableRecord.IsValid)
                        count += SetBlockLayer0(tr, innerBr.BlockTableRecord);
                }
                else
                {
                    ent.UpgradeOpen();
                    ent.Layer = "0";
                    ent.ColorIndex = 256;
                    count++;
                }
            }
            return count;
        }
        [CommandMethod("CT_CENTERLINE")]
        public void DrawCenterLine()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            string ltName = "Continuous";
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var lt = (LinetypeTable)tr.GetObject(Db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has("CENTER")) { ltName = "CENTER"; }
                else
                {
                    try
                    {
#if AUTOCAD
                        Db.LoadLineTypeFile("CENTER", "acad.lin");
#elif ZWCAD
                        Db.LoadLineTypeFile("CENTER", "zwcad.lin");
#elif GSTARCAD
                        Db.LoadLineTypeFile("CENTER", "gcad.lin");
#endif
                        ltName = "CENTER";
                    }
                    catch { Ed.WriteMessage("\n\u8b66\u544a\uff1a\u65e0\u6cd5\u52a0\u8f7d CENTER \u7ebf\u578b\uff0c\u5c06\u4f7f\u7528 Continuous\u3002"); }
                }
                tr.Commit();
            }
            int count = 0;
            double extRatio = 0.25;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    Point3d center = Point3d.Origin;
                    double halfX = 0, halfY = 0;
                    bool ok = false;
                    if (ent is Circle)
                    {
                        var ci = (Circle)ent;
                        center = ci.Center;
                        double r = ci.Radius;
                        double ext = r * extRatio;
                        halfX = r + ext; halfY = r + ext;
                        ok = true;
                    }
                    else if (ent is Polyline)
                    {
                        var pl = (Polyline)ent;
                        if (pl.Closed && pl.NumberOfVertices == 4)
                        {
                            double mnx = double.MaxValue, mny = double.MaxValue;
                            double mxx = double.MinValue, mxy = double.MinValue;
                            for (int i = 0; i < 4; i++)
                            {
                                Point3d pt = pl.GetPoint3dAt(i);
                                if (pt.X < mnx) mnx = pt.X; if (pt.Y < mny) mny = pt.Y;
                                if (pt.X > mxx) mxx = pt.X; if (pt.Y > mxy) mxy = pt.Y;
                            }
                            center = new Point3d((mnx + mxx) / 2.0, (mny + mxy) / 2.0, 0);
                            halfX = (mxx - mnx) / 2.0 * (1.0 + extRatio);
                            halfY = (mxy - mny) / 2.0 * (1.0 + extRatio);
                            ok = true;
                        }
                    }
                    else if (ent is Hatch || ent is Solid3d)
                    {
                        Extents3d ext3d;
                        var e2 = (Entity)ent; try { ext3d = e2.GeometricExtents; } catch { continue; }
                        center = new Point3d((ext3d.MinPoint.X + ext3d.MaxPoint.X) / 2.0, (ext3d.MinPoint.Y + ext3d.MaxPoint.Y) / 2.0, 0);
                        halfX = (ext3d.MaxPoint.X - ext3d.MinPoint.X) / 2.0 * (1.0 + extRatio);
                        halfY = (ext3d.MaxPoint.Y - ext3d.MinPoint.Y) / 2.0 * (1.0 + extRatio);
                        ok = true;
                    }
                    if (!ok) continue;
                    var l1 = new Line(new Point3d(center.X - halfX, center.Y, 0), new Point3d(center.X + halfX, center.Y, 0));
                    l1.Layer = "0"; l1.ColorIndex = 1;
                    try { l1.Linetype = ltName; } catch { }
                    l1.LinetypeScale = 1.0;
                    msBtr.AppendEntity(l1); tr.AddNewlyCreatedDBObject(l1, true);
                    var l2 = new Line(new Point3d(center.X, center.Y - halfY, 0), new Point3d(center.X, center.Y + halfY, 0));
                    l2.Layer = "0"; l2.ColorIndex = 1;
                    try { l2.Linetype = ltName; } catch { }
                    l2.LinetypeScale = 1.0;
                    msBtr.AppendEntity(l2); tr.AddNewlyCreatedDBObject(l2, true);
                    count++;
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u4e3a {0} \u4e2a\u5bf9\u8c61\u7ed8\u5236\u4e2d\u5fc3\u7ebf\u3002", count));
        }        [CommandMethod("CT_SELECTBYLAYER")]
        public void SelectByLayer()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var peo = new PromptEntityOptions("\n\u9009\u62e9\u4e00\u4e2a\u5bf9\u8c61\u4ee5\u6309\u56fe\u5c42\u9009\u62e9\uff1a");
            var per = Ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            string layerName;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                layerName = ent.Layer;
                tr.Commit();
            }
            var filter = new TypedValue[] { new TypedValue(8, layerName) };
            var sf = new SelectionFilter(filter);
            var psr = Ed.GetSelection(sf);
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            Ed.SetImpliedSelection(psr.Value.GetObjectIds());
            Ed.WriteMessage(string.Format("\n\u5df2\u9009\u62e9\u56fe\u5c42 \"{0}\" \u4e0a\u7684 {1} \u4e2a\u5bf9\u8c61\u3002", layerName, psr.Value.Count));
        }
        [CommandMethod("CT_SELECTBYCOLOR")]
        public void SelectByColor()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var peo = new PromptEntityOptions("\n\u9009\u62e9\u4e00\u4e2a\u5bf9\u8c61\u4ee5\u6309\u989c\u8272\u9009\u62e9\uff1a");
            var per = Ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            int colorIndex;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                colorIndex = ent.ColorIndex;
                tr.Commit();
            }
            var filter = new TypedValue[] { new TypedValue(62, colorIndex) };
            var sf = new SelectionFilter(filter);
            var psr = Ed.GetSelection(sf);
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            Ed.SetImpliedSelection(psr.Value.GetObjectIds());
            string colorName = colorIndex == 256 ? "ByLayer" : (colorIndex == 0 ? "ByBlock" : colorIndex.ToString());
            Ed.WriteMessage(string.Format("\n\u5df2\u9009\u62e9\u989c\u8272 {0} \u7684 {1} \u4e2a\u5bf9\u8c61\u3002", colorName, psr.Value.Count));
        }
        [CommandMethod("CT_SELECTBYBLOCK")]
        public void SelectByBlock()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var peo = new PromptEntityOptions("\n\u9009\u62e9\u4e00\u4e2a\u5757\u53c2\u7167\u4ee5\u6309\u5757\u540d\u9009\u62e9\uff1a");
            peo.SetRejectMessage("\n\u53ea\u80fd\u9009\u62e9\u5757\u53c2\u7167\u3002");
            peo.AddAllowedClass(typeof(BlockReference), true);
            var per = Ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            string blockName;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var br = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                blockName = btr.Name;
                tr.Commit();
            }
            var filter = new TypedValue[] { new TypedValue(0, "INSERT"), new TypedValue(2, blockName) };
            var sf = new SelectionFilter(filter);
            var psr = Ed.GetSelection(sf);
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            Ed.SetImpliedSelection(psr.Value.GetObjectIds());
            Ed.WriteMessage(string.Format("\n\u5df2\u9009\u62e9\u5757 \"{0}\" \u7684 {1} \u4e2a\u53c2\u7167\u3002", blockName, psr.Value.Count));
        }        [CommandMethod("CT_TEXTBRUSH")]
        public void TextBrush()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var peo1 = new PromptEntityOptions("\n\u9009\u62e9\u6e90\u6587\u5b57\uff08\u5355\u884c/\u591a\u884c\uff09\uff1a");
            peo1.SetRejectMessage("\n\u53ea\u80fd\u9009\u62e9\u6587\u5b57\u5bf9\u8c61\u3002");
            peo1.AddAllowedClass(typeof(DBText), true);
            peo1.AddAllowedClass(typeof(MText), true);
            var per1 = Ed.GetEntity(peo1);
            if (per1.Status != PromptStatus.OK) return;
            string srcLayer; int srcColor; double srcHeight; string srcStyle;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(per1.ObjectId, OpenMode.ForRead);
                if (ent is DBText) { var dt = (DBText)ent; srcLayer = dt.Layer; srcColor = dt.ColorIndex; srcHeight = dt.Height; srcStyle = dt.TextStyleName; }
                else { var mt = (MText)ent; srcLayer = mt.Layer; srcColor = mt.ColorIndex; srcHeight = mt.TextHeight; srcStyle = mt.TextStyleName; }
                tr.Commit();
            }
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u76ee\u6807\u6587\u5b57\u3002"); return; }
            int count = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForWrite);
                    if (ent is DBText) { var dt = (DBText)ent; dt.Layer = srcLayer; dt.ColorIndex = srcColor; dt.Height = srcHeight; dt.TextStyleId = GetTextStyleId(tr, srcStyle); count++; }
                    else if (ent is MText) { var mt = (MText)ent; mt.Layer = srcLayer; mt.ColorIndex = srcColor; mt.TextHeight = srcHeight; mt.TextStyleId = GetTextStyleId(tr, srcStyle); count++; }
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u590d\u5236\u683c\u5f0f\u5230 {0} \u4e2a\u6587\u5b57\u3002", count));
        }
        [CommandMethod("CT_TEXTMERGE")]
        public void TextMerge()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            var texts = new List<KeyValuePair<double, string>>();
            double maxHeight = 0;
            double posX = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    string txt = null; double y = 0; double h = 0;
                    if (ent is DBText) { var dt = (DBText)ent; txt = SafeStr(dt.TextString); y = dt.Position.Y; h = dt.Height; posX = dt.Position.X; }
                    else if (ent is MText) { var mt = (MText)ent; txt = SafeStr(mt.Contents); y = mt.Location.Y; h = mt.TextHeight; posX = mt.Location.X; }
                    if (txt != null) { texts.Add(new KeyValuePair<double, string>(y, txt)); if (h > maxHeight) maxHeight = h; }
                }
                tr.Commit();
            }
            if (texts.Count < 2) { Ed.WriteMessage("\n\u9700\u8981\u81f3\u5c11\u4e24\u4e2a\u6587\u5b57\u5bf9\u8c61\u3002"); return; }
            texts.Sort(delegate(KeyValuePair<double, string> a, KeyValuePair<double, string> b) { return b.Key.CompareTo(a.Key); });
            var sb = new StringBuilder();
            for (int i = 0; i < texts.Count; i++) { if (i > 0) sb.Append("\\P"); sb.Append(texts[i].Value); }
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                var mt = new MText();
                mt.Contents = sb.ToString();
                mt.Location = new Point3d(posX, texts[0].Key, 0);
                mt.TextHeight = maxHeight > 0 ? maxHeight : 3.0;
                msBtr.AppendEntity(mt);
                tr.AddNewlyCreatedDBObject(mt, true);
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u5408\u5e76 {0} \u4e2a\u6587\u5b57\u4e3a\u591a\u884c\u6587\u5b57\u3002", texts.Count));
        }        [CommandMethod("CT_TEXTNUMBER")]
        public void TextNumber()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            var f = new Form();
            f.Text = "\u6587\u5b57\u7f16\u53f7";
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
            f.ClientSize = new Size(300, 120);
            var l1 = new Label(); l1.Text = "\u524d\u7f00\uff1a"; l1.Left = 16; l1.Top = 16; l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t1 = new TextBox(); t1.Left = 76; t1.Top = 12; t1.Width = 60; t1.Text = ""; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var l2 = new Label(); l2.Text = "\u540e\u7f00\uff1a"; l2.Left = 150; l2.Top = 16; l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t2 = new TextBox(); t2.Left = 210; t2.Top = 12; t2.Width = 60; t2.Text = ""; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var l3 = new Label(); l3.Text = "\u8d77\u59cb\u53f7\uff1a"; l3.Left = 16; l3.Top = 52; l3.AutoSize = true; l3.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t3 = new TextBox(); t3.Left = 76; t3.Top = 48; t3.Width = 60; t3.Text = "1"; t3.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var chkReplace = new CheckBox(); chkReplace.Text = "\u66ff\u6362\uff08\u7528\u7f16\u53f7\u66ff\u6362\u539f\u6587\uff09"; chkReplace.Left = 150; chkReplace.Top = 50; chkReplace.AutoSize = true; chkReplace.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var ok = new Button(); ok.Text = "\u786e\u5b9a"; ok.DialogResult = DialogResult.OK; ok.Left = 120; ok.Top = 82; ok.Width = 80; ok.FlatStyle = FlatStyle.System;
            var cancel = new Button(); cancel.Text = "\u53d6\u6d88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 210; cancel.Top = 82; cancel.Width = 76; cancel.FlatStyle = FlatStyle.System;
            f.Controls.AddRange(new Control[] { l1, t1, l2, t2, l3, t3, chkReplace, ok, cancel });
            f.AcceptButton = ok; f.CancelButton = cancel;
            f.Shown += delegate { t1.Focus(); };
            if (f.ShowDialog() != DialogResult.OK) return;
            string prefix = t1.Text.Trim();
            string suffix = t2.Text.Trim();
            int startNum = 1; int.TryParse(t3.Text.Trim(), out startNum);
            var items = new List<KeyValuePair<Point3d, ObjectId>>();
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    Point3d pos = Point3d.Origin;
                    if (ent is DBText) pos = ((DBText)ent).Position;
                    else if (ent is MText) pos = ((MText)ent).Location;
                    else continue;
                    items.Add(new KeyValuePair<Point3d, ObjectId>(pos, id));
                }
                tr.Commit();
            }
            items.Sort(delegate(KeyValuePair<Point3d, ObjectId> a, KeyValuePair<Point3d, ObjectId> b)
            {
                int cmp = b.Key.Y.CompareTo(a.Key.Y);
                if (cmp != 0) return cmp;
                return a.Key.X.CompareTo(b.Key.X);
            });
            int count = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var ent = tr.GetObject(items[i].Value, OpenMode.ForWrite);
                    string numStr = prefix + (startNum + i).ToString() + suffix;
                    if (chkReplace.Checked)
                    {
                        if (ent is DBText) { var _dt = (DBText)ent; _dt.TextString = numStr; count++; }
                        else if (ent is MText) { var _mt = (MText)ent; _mt.Contents = numStr; count++; }
                    }
                    else
                    {
                        if (ent is DBText) { var _dt2 = (DBText)ent; _dt2.TextString = numStr + _dt2.TextString; count++; }
                        else if (ent is MText) { var _mt2 = (MText)ent; _mt2.Contents = numStr + _mt2.Contents; count++; }
                    }
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u7f16\u53f7 {0} \u4e2a\u6587\u5b57\u5bf9\u8c61\u3002", count));
        }        [CommandMethod("CT_ISOLAYER")]
        public void IsoLayer()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            string dk = DocKey;
            if (_isoState.ContainsKey(dk))
            {
                var frozen = _isoState[dk];
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(Db.LayerTableId, OpenMode.ForRead);
                    foreach (var lid in frozen)
                    {
                        if (!lid.IsValid) continue;
                        try { var ltr = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForWrite); ltr.IsFrozen = false; } catch { }
                    }
                    tr.Commit();
                }
                _isoState.Remove(dk);
                Ed.WriteMessage("\n\u5df2\u6062\u590d\u6240\u6709\u56fe\u5c42\u3002");
                return;
            }
            var peo = new PromptEntityOptions("\n\u9009\u62e9\u8981\u5b64\u7acb\u7684\u5bf9\u8c61\uff08\u5176\u4f59\u56fe\u5c42\u5c06\u51bb\u7ed3\uff09\uff1a");
            var per = Ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            string targetLayer;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                targetLayer = ent.Layer;
                tr.Commit();
            }
            var frozenList = new List<ObjectId>();
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(Db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId lid in lt)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForRead);
                    if (ltr.Name == targetLayer || ltr.IsFrozen) continue;
                    ltr.UpgradeOpen();
                    ltr.IsFrozen = true;
                    frozenList.Add(lid);
                }
                tr.Commit();
            }
            _isoState[dk] = frozenList.ToArray();
            Ed.WriteMessage(string.Format("\n\u5df2\u5b64\u7acb\u56fe\u5c42 \"{0}\" \uff0c\u51bb\u7ed3 {1} \u4e2a\u56fe\u5c42\u3002", targetLayer, frozenList.Count));
        }
        [CommandMethod("CT_QUICKDIM")]
        public void QuickDim()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        if (ext.MinPoint.X < minX) minX = ext.MinPoint.X;
                        if (ext.MinPoint.Y < minY) minY = ext.MinPoint.Y;
                        if (ext.MaxPoint.X > maxX) maxX = ext.MaxPoint.X;
                        if (ext.MaxPoint.Y > maxY) maxY = ext.MaxPoint.Y;
                    }
                    catch { }
                }
                tr.Commit();
            }
            if (minX >= maxX || minY >= maxY) { Ed.WriteMessage("\n\u65e0\u6cd5\u8ba1\u7b97\u5305\u56f4\u76d2\u3002"); return; }
            double offset = (maxY - minY) * 0.15;
            if (offset < 5) offset = 5;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                var dimH = new AlignedDimension();
                dimH.XLine1Point = new Point3d(minX, minY - offset, 0);
                dimH.XLine2Point = new Point3d(maxX, minY - offset, 0);
                dimH.DimLinePoint = new Point3d((minX + maxX) / 2.0, minY - offset * 2, 0);
                dimH.DimensionStyle = Db.Dimstyle;
                msBtr.AppendEntity(dimH); tr.AddNewlyCreatedDBObject(dimH, true);
                var dimV = new AlignedDimension();
                dimV.XLine1Point = new Point3d(maxX + offset, minY, 0);
                dimV.XLine2Point = new Point3d(maxX + offset, maxY, 0);
                dimV.DimLinePoint = new Point3d(maxX + offset * 2, (minY + maxY) / 2.0, 0);
                dimV.DimensionStyle = Db.Dimstyle;
                msBtr.AppendEntity(dimV); tr.AddNewlyCreatedDBObject(dimV, true);
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u521b\u5efa\u5feb\u901f\u6807\u6ce8: {0:F1} x {1:F1}", maxX - minX, maxY - minY));
        }
        [CommandMethod("CT_INCCOPY")]
        public void IncCopy()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002"); return; }
            string baseText = null;
            Point3d anchor = Point3d.Origin;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    if (ent is DBText) { baseText = ((DBText)ent).TextString; anchor = ((DBText)ent).Position; break; }
                    else if (ent is MText) { baseText = ((MText)ent).Contents; anchor = ((MText)ent).Location; break; }
                }
                tr.Commit();
            }
            if (baseText == null) { Ed.WriteMessage("\n\u9009\u62e9\u7684\u5bf9\u8c61\u4e2d\u6ca1\u6709\u6587\u5b57\u3002"); return; }
            int numEnd = baseText.Length;
            int numStart = numEnd;
            while (numStart > 0 && char.IsDigit(baseText[numStart - 1])) numStart--;
            string prefix = baseText.Substring(0, numStart);
            int num = 0;
            if (numStart < numEnd) int.TryParse(baseText.Substring(numStart), out num);
            int numLen = numEnd - numStart;
            if (numLen == 0) { numLen = 1; num = 1; }
            int copyCount = 0;
            while (true)
            {
                string curText = prefix + num.ToString().PadLeft(numLen, '0');
                var ppr = Ed.GetPoint(string.Format("\n\u6307\u5b9a\u590d\u5236\u57fa\u70b9\uff08\u5f53\u524d: {0} \uff0c\u56de\u8f66\u7ed3\u675f\uff09\uff1a", curText));
                if (ppr.Status != PromptStatus.OK) break;
                num++;
                string newText = prefix + num.ToString().PadLeft(numLen, '0');
                double dx = ppr.Value.X - anchor.X;
                double dy = ppr.Value.Y - anchor.Y;
                var transform = Matrix3d.Displacement(new Vector3d(dx, dy, 0));
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;
                        var clone = (Entity)ent.Clone();
                        clone.TransformBy(transform);
                        msBtr.AppendEntity(clone);
                        tr.AddNewlyCreatedDBObject(clone, true);
                        if (clone is DBText) ((DBText)clone).TextString = newText;
                        else if (clone is MText) ((MText)clone).Contents = newText;
                    }
                    tr.Commit();
                }
                copyCount++;
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u9012\u589e\u590d\u5236 {0} \u6b21\u3002", copyCount));
        }
    }
}





