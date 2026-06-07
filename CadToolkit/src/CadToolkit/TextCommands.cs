using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections;
using CadToolkit.Core;
using CadToolkit.UI;
using LayerStandardRule = CadToolkit.Core.Config.LayerStandardRule;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using CadColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using GrxCAD.Runtime;
using CadColor = GrxCAD.Colors.Color;
using CadColorMethod = GrxCAD.Colors.ColorMethod;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using CadColor = ZwSoft.ZwCAD.Colors.Color;
using CadColorMethod = ZwSoft.ZwCAD.Colors.ColorMethod;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
[CommandMethod("CT_FINDREPLACE")]
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
                        if (btr.Name.StartsWith("*")) continue;
                        count += ReplaceInBlock(tr, btr, dlg.FindText, dlg.ReplaceText, cmp);
                    }
                    tr.Commit();
                }
                Ed.WriteMessage(string.Format("\n\u5df2\u66ff\u6362 {0} \u5904\u3002", count));
            }
        }

[CommandMethod("CT_ALIGN")]
        public void AlignText()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            using (var dlg = new AlignDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                int h = dlg.HorzIndex;
                bool useFirst = dlg.UseFirstBase;
                double spacing = dlg.LineSpacing;
                var texts = new List<DBText>();
                double baseX = 0;
                double baseY = 0;
                bool changed = RunWithUndo("CT_ALIGN", delegate
                {
                    using (var tr = Db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId id in selectedIds)
                        {
                            var ent = tr.GetObject(id, OpenMode.ForRead);
                            if (ent is DBText) texts.Add((DBText)ent);
                        }
                        if (texts.Count == 0) { Ed.WriteMessage("\n\u672a\u627e\u5230\u5355\u884c\u6587\u5b57\u3002"); return false; }
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
                            if (ppr.Status != PromptStatus.OK) return false;
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
                    return true;
                });
                if (!changed) return;
                Ed.WriteMessage(string.Format("\n\u5df2\u5bf9\u9f50 {0} \u4e2a\u6587\u5b57\u3002", texts.Count));
            }
        }

[CommandMethod("CT_UNDERLINE")]
        public void UnderlineText()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            bool keep = Config.KeepOriginal;
            int count = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (ObjectId id in selectedIds)
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

[CommandMethod("CT_TEXTBRUSH")]
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
            ObjectId[] selectedIds = GetSelectionOrAbort("\n\u672a\u9009\u62e9\u76ee\u6807\u6587\u5b57\u3002");
            if (selectedIds == null) return;
            int count = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
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
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            var texts = new List<KeyValuePair<double, string>>();
            double maxHeight = 0;
            double posX = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
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
            bool changed = RunWithUndo("CT_TEXTMERGE", delegate
            {
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
                return true;
            });
            if (!changed) return;
            Ed.WriteMessage(string.Format("\n\u5df2\u5408\u5e76 {0} \u4e2a\u6587\u5b57\u4e3a\u591a\u884c\u6587\u5b57\u3002", texts.Count));
        }

[CommandMethod("CT_TEXTNUMBER")]
        public void TextNumber()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            var f = new Form();
            f.Text = "\u6587\u5b57\u7f16\u53f7";
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
            f.AutoScaleMode = AutoScaleMode.None; f.AutoScroll = true; f.ClientSize = new Size(UiScale(320), UiScale(132));
            var l1 = new Label(); l1.Text = "\u524d\u7f00\uff1a"; l1.Left = UiScale(16); l1.Top = UiScale(16); l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t1 = new TextBox(); t1.Left = UiScale(76); t1.Top = UiScale(12); t1.Width = UiScale(70); t1.Text = ""; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var l2 = new Label(); l2.Text = "\u540e\u7f00\uff1a"; l2.Left = UiScale(160); l2.Top = UiScale(16); l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t2 = new TextBox(); t2.Left = UiScale(220); t2.Top = UiScale(12); t2.Width = UiScale(70); t2.Text = ""; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var l3 = new Label(); l3.Text = "\u8d77\u59cb\u53f7\uff1a"; l3.Left = UiScale(16); l3.Top = UiScale(52); l3.AutoSize = true; l3.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
            var t3 = new TextBox(); t3.Left = UiScale(76); t3.Top = UiScale(48); t3.Width = UiScale(70); t3.Text = "1"; t3.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
            var chkReplace = new CheckBox(); chkReplace.Text = "\u66ff\u6362\uff08\u7528\u7f16\u53f7\u66ff\u6362\u539f\u6587\uff09"; chkReplace.Left = UiScale(160); chkReplace.Top = UiScale(50); chkReplace.Width = UiScale(150); chkReplace.Height = UiScale(24); chkReplace.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            var ok = new Button(); ok.Text = "\u786e\u5b9a"; ok.DialogResult = DialogResult.OK; ok.Left = UiScale(132); ok.Top = UiScale(92); ok.Width = UiScale(80); ok.Height = UiScale(28); ok.FlatStyle = FlatStyle.System;
            var cancel = new Button(); cancel.Text = "\u53d6\u6d88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = UiScale(224); cancel.Top = UiScale(92); cancel.Width = UiScale(76); cancel.Height = UiScale(28); cancel.FlatStyle = FlatStyle.System;
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
                foreach (ObjectId id in selectedIds)
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
        }
    }
}


