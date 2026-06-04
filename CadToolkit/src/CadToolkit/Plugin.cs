using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
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
                    string path = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(path)) return System.Reflection.Assembly.LoadFrom(path);
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
                    string path = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(path)) return System.Reflection.Assembly.LoadFrom(path);
                    return null;
                };
                Config.Init(typeof(CadCommands).Assembly.Location);
            }
            catch { }
        }

        static Editor Ed { get { return CadApp.DocumentManager.MdiActiveDocument.Editor; } }
        static Database Db { get { return CadApp.DocumentManager.MdiActiveDocument.Database; } }
        static string SafeStr(string s) { return s == null ? "" : s; }

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
                System.Windows.Forms.MessageBox.Show("\u8BF7\u5148\u6253\u5F00\u4E00\u4E2A\u56FE\u7EBF\u6587\u4EF6\u3002", "\u63D0\u793A", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        [CommandMethod("CC")]
        public void ShowPanel()
        {
            EnsureInit();
            var cmds = Config.GetCommands();
            if (cmds.Count == 0)
            {
                System.Windows.Forms.MessageBox.Show(
                    "\u6CA1\u6709\u914D\u7F6E\u4EFB\u4F55\u547D\u4EE4\u3002\n\u8BF7\u7F16\u8F91 CadToolkit.ini \u7684 [Commands] \u6BB5\u3002",
                    "\u63D0\u793A", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var f = new Form();
            f.Text = "CadToolkit " + Config.Version;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            f.TopMost = true;
            f.ShowInTaskbar = false;
            f.BackColor = SystemColors.Control;
            var btnCancel = new Button();
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Size = new Size(0, 0);
            btnCancel.Location = new Point(-100, -100);
            f.Controls.Add(btnCancel);
            f.CancelButton = btnCancel;

            int bw = 156, bh = 34, pad = 5, cols = 2;
            int cmdRows = (int)Math.Ceiling(cmds.Count / (double)cols);
            int cmdAreaH = cmdRows * (bh + pad);
            int barH = 34;
            int w = cols * (bw + pad) + pad;
            int h = cmdAreaH + pad + barH + pad;
            f.ClientSize = new Size(w, h);

            for (int i = 0; i < cmds.Count; i++)
            {
                string label = cmds[i].Key;
                string cmd = cmds[i].Value;
                var b = new Button();
                b.Text = label;
                b.FlatStyle = FlatStyle.System;
                b.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
                b.Size = new Size(bw, bh);
                b.Location = new Point(pad + (i % cols) * (bw + pad), pad + (i / cols) * (bh + pad));
                string cmdName = cmd;
                b.Click += delegate { f.Close(); CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute(cmdName + " ", true, false, true); };
                f.Controls.Add(b);
            }

            var bar = new Panel();
            bar.Location = new Point(0, cmdAreaH + pad);
            bar.Size = new Size(w, barH);
            bar.BackColor = SystemColors.Control;

            var btnAdd = new Button();
            btnAdd.Text = "\u6DFB\u52A0";
            btnAdd.FlatStyle = FlatStyle.System;
            btnAdd.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            btnAdd.Size = new Size(50, 26);
            btnAdd.Location = new Point(pad, 4);
            btnAdd.Click += delegate
            {
                f.Close();
                using (var dlg = new AddCommandDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK && dlg.CmdLabel.Length > 0 && dlg.CmdName.Length > 0)
                        Config.SaveCommand(dlg.CmdLabel, dlg.CmdName);
                }
            };

            var btnManage = new Button();
            btnManage.Text = "\u7BA1\u7406";
            btnManage.FlatStyle = FlatStyle.System;
            btnManage.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            btnManage.Size = new Size(50, 26);
            btnManage.Location = new Point(pad + 56, 4);
            btnManage.Click += delegate
            {
                f.Close();
                using (var dlg = new ManageCommandsDialog()) { dlg.ShowDialog(); }
            };

            bar.Controls.Add(btnAdd);
            bar.Controls.Add(btnManage);
            f.Controls.Add(bar);
            f.ShowDialog();
        }

        [CommandMethod("CT_FINDREPLACE")]
        public void FindReplace()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            using (var dlg = new FindReplaceDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (string.IsNullOrEmpty(dlg.FindText)) { Ed.WriteMessage("\n\u67E5\u627E\u5185\u5BB9\u4E3A\u7A7A\u3002"); return; }
                int count = 0;
                StringComparison cmp = dlg.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in msBtr)
                    {
                        var obj = tr.GetObject(id, OpenMode.ForRead);
                        if (obj is DBText)
                        {
                            var dt = (DBText)obj;
                            string txt = SafeStr(dt.TextString);
                            string ntxt = ReplaceEx(txt, dlg.FindText, dlg.ReplaceText, cmp);
                            if (ntxt != txt) { dt.UpgradeOpen(); dt.TextString = ntxt; count++; }
                        }
                        else if (obj is MText)
                        {
                            var mt = (MText)obj;
                            string txt = SafeStr(mt.Contents);
                            string ntxt = ReplaceEx(txt, dlg.FindText, dlg.ReplaceText, cmp);
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
                                    string ntxt = ReplaceEx(txt, dlg.FindText, dlg.ReplaceText, cmp);
                                    if (ntxt != txt) { att.UpgradeOpen(); att.TextString = ntxt; count++; }
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
                Ed.WriteMessage(string.Format("\n\u5DF2\u66FF\u6362 {0} \u5904\u3002", count));
            }
        }

        [CommandMethod("CT_ALIGN")]
        public void AlignText()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = Ed.GetSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672A\u9009\u62E9\u5BF9\u8C61\u3002"); return; }
            using (var dlg = new AlignDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                int h = (int)dlg.Choice.Horizontal;
                int v = (int)dlg.Choice.Vertical;
                int count = 0;
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead);
                        if (!(ent is DBText)) continue;
                        var dt = (DBText)ent;
                        double x = dt.Position.X;
                        double y = dt.Position.Y;
                        double w = 0;
                        try { w = dt.WidthFactor * dt.TextString.Length * dt.Height * 0.6; } catch { }
                        if (h == 1) x = x + w / 2.0;
                        else if (h == 2) x = x + w;
                        double hh = dt.Height;
                        if (v == 1) y = y + hh / 2.0;
                        else if (v == 2) y = y + hh;
                        dt.UpgradeOpen();
                        dt.Position = new Point3d(x, y, 0);
                        count++;
                    }
                    tr.Commit();
                }
                Ed.WriteMessage(string.Format("\n\u5DF2\u5BF9\u9F50 {0} \u4E2A\u5355\u884C\u6587\u5B57\u3002", count));
            }
        }

        [CommandMethod("CT_UNDERLINE")]
        public void UnderlineText()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = Ed.GetSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672A\u9009\u62E9\u5BF9\u8C61\u3002"); return; }
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
                    msBtr.AppendEntity(mt);
                    tr.AddNewlyCreatedDBObject(mt, true);
                    if (!keep) { dt.UpgradeOpen(); dt.Erase(); }
                    count++;
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5DF2\u8F6C\u6362 {0} \u4E2A\u6587\u5B57\u4E3A\u5E26\u4E0B\u5212\u7EBF MText\u3002", count));
        }

        [CommandMethod("CT_RENAMEBLOCK")]
        public void RenameBlock()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            string oldName = "";
            var psr = Ed.GetSelection();
            if (psr.Status == PromptStatus.OK && psr.Value.GetObjectIds().Length == 1)
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(psr.Value.GetObjectIds()[0], OpenMode.ForRead);
                    if (ent is BlockReference)
                    {
                        var br = (BlockReference)ent;
                        var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        oldName = btr.Name;
                    }
                    tr.Commit();
                }
            }
            using (var dlg = new RenameBlockDialog(oldName))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (string.IsNullOrEmpty(dlg.OldName) || string.IsNullOrEmpty(dlg.NewName))
                {
                    Ed.WriteMessage("\n\u540D\u79F0\u4E0D\u80FD\u4E3A\u7A7A\u3002");
                    return;
                }
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                    if (!bt.Has(dlg.OldName))
                    {
                        Ed.WriteMessage(string.Format("\n\u5757 \"{0}\" \u4E0D\u5B58\u5728\u3002", dlg.OldName));
                        return;
                    }
                    if (bt.Has(dlg.NewName))
                    {
                        Ed.WriteMessage(string.Format("\n\u5757 \"{0}\" \u5DF2\u5B58\u5728\u3002", dlg.NewName));
                        return;
                    }
                    var btr = (BlockTableRecord)tr.GetObject(bt[dlg.OldName], OpenMode.ForWrite);
                    btr.Name = dlg.NewName;
                    tr.Commit();
                    Ed.WriteMessage(string.Format("\n\u5DF2\u5C06\u5757 \"{0}\" \u91CD\u547D\u540D\u4E3A \"{1}\"\u3002", dlg.OldName, dlg.NewName));
                }
            }
        }

        [CommandMethod("CT_QUICKBLOCK")]
        public void QuickBlock()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = Ed.GetSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672A\u9009\u62E9\u5BF9\u8C61\u3002"); return; }
            var ppr = Ed.GetPoint("\n\u6307\u5B9A\u5757\u57FA\u70B9\uFF1A");
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
                Ed.WriteMessage(string.Format("\n\u5DF2\u521B\u5EFA\u5757 \"{0}\"\uFF0C\u5305\u542B {1} \u4E2A\u5BF9\u8C61\u3002", name, ids.Count));
            }
        }

        [CommandMethod("CT_SETLAYER0")]
        public void SetLayer0()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = Ed.GetSelection();
            if (psr.Status != PromptStatus.OK) { Ed.WriteMessage("\n\u672A\u9009\u62E9\u5BF9\u8C61\u3002"); return; }
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
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent != null) { ent.Layer = "0"; count++; }
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5DF2\u5C06 {0} \u4E2A\u5BF9\u8C61\u6539\u5230 0 \u5C42\u3002", count));
        }
    }
}
