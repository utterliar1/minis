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
[CommandMethod("CT_LAYERSTANDARD")]
        public void LayerStandard()
        {
            EnsureInit();
            if (!CheckDoc()) return;

            var rules = Config.GetLayerStandards();
            if (rules.Count == 0)
            {
                Ed.WriteMessage("\n\u672a\u914d\u7f6e [LayerStandard] \u56fe\u5c42\u89c4\u8303\u3002");
                return;
            }

            var layerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForRead);
                foreach (ObjectId id in space)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    string layer = ent.Layer;
                    if (!layerCounts.ContainsKey(layer)) layerCounts[layer] = 0;
                    layerCounts[layer]++;
                }
                tr.Commit();
            }

            var plans = new List<LayerStandardPlan>();
            var fallbackPlans = new List<LayerStandardPlan>();
            var whitelistPlans = new List<LayerStandardPlan>();
            string layerWhitelist = Config.LayerStandardWhitelist;
            foreach (var pair in layerCounts)
            {
                if (IsLayerWhitelisted(pair.Key, layerWhitelist))
                {
                    whitelistPlans.Add(new LayerStandardPlan { SourceLayer = pair.Key, TargetLayer = "", Count = pair.Value, Rule = null });
                    continue;
                }
                var rule = MatchLayerRule(pair.Key, rules);
                if (rule == null)
                {
                    fallbackPlans.Add(new LayerStandardPlan { SourceLayer = pair.Key, TargetLayer = "0", Count = pair.Value, Rule = null });
                    continue;
                }
                if (pair.Key.Equals(rule.Name, StringComparison.OrdinalIgnoreCase)) continue;
                plans.Add(new LayerStandardPlan { SourceLayer = pair.Key, TargetLayer = rule.Name, Count = pair.Value, Rule = rule });
            }
            plans.Sort(delegate(LayerStandardPlan a, LayerStandardPlan b) { return a.TargetLayer.CompareTo(b.TargetLayer); });
            fallbackPlans.Sort(delegate(LayerStandardPlan a, LayerStandardPlan b) { return a.SourceLayer.CompareTo(b.SourceLayer); });
            whitelistPlans.Sort(delegate(LayerStandardPlan a, LayerStandardPlan b) { return a.SourceLayer.CompareTo(b.SourceLayer); });

            bool setByLayer = true;
            bool deleteEmpty = false;
            bool fallbackTo0 = Config.LayerStandardFallbackTo0;
            var f = new Form();
            f.Text = "\u56fe\u5c42\u89c4\u8303\u5316";
            f.StartPosition = FormStartPosition.CenterScreen;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
            f.AutoScaleMode = AutoScaleMode.None; f.AutoScroll = true; f.ClientSize = new Size(UiScale(560), UiScale(470));

            var txt = new TextBox();
            txt.Multiline = true; txt.ReadOnly = true; txt.ScrollBars = ScrollBars.Both;
            txt.WordWrap = false; txt.Font = new System.Drawing.Font("Consolas", 9f);
            txt.Left = UiScale(12); txt.Top = UiScale(12); txt.Width = UiScale(536); txt.Height = UiScale(300);
            txt.Text = FormatLayerPlan(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0);

            var chkByLayer = new CheckBox();
            chkByLayer.Text = "\u5c06\u8fc1\u79fb\u5bf9\u8c61\u7684\u989c\u8272/\u7ebf\u578b/\u7ebf\u5bbd\u6539\u4e3a ByLayer";
            chkByLayer.Left = UiScale(12); chkByLayer.Top = UiScale(322); chkByLayer.Width = UiScale(536); chkByLayer.Height = UiScale(24); chkByLayer.Checked = true;
            chkByLayer.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkDelete = new CheckBox();
            chkDelete.Text = "\u5220\u9664\u7a7a\u7684\u65e7\u56fe\u5c42";
            chkDelete.Left = UiScale(12); chkDelete.Top = UiScale(350); chkDelete.Width = UiScale(190); chkDelete.Height = UiScale(24); chkDelete.Checked = false;
            chkDelete.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkFallback = new CheckBox();
            chkFallback.Text = "\u672a\u8bc6\u522b\u56fe\u5c42\u5f52 0 \u5c42\uff08\u767d\u540d\u5355\u4e0d\u5904\u7406\uff09";
            chkFallback.Left = UiScale(12); chkFallback.Top = UiScale(378); chkFallback.Width = UiScale(536); chkFallback.Height = UiScale(24); chkFallback.Checked = fallbackTo0;
            chkFallback.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            chkFallback.CheckedChanged += delegate { txt.Text = FormatLayerPlan(plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked); };

            var ok = new Button();
            ok.Text = "\u6267\u884c"; ok.DialogResult = DialogResult.OK;
            ok.Left = UiScale(376); ok.Top = UiScale(426); ok.Width = UiScale(80); ok.Height = UiScale(28); ok.FlatStyle = FlatStyle.System;

            var cancel = new Button();
            cancel.Text = "\u53d6\u6d88"; cancel.DialogResult = DialogResult.Cancel;
            cancel.Left = UiScale(468); cancel.Top = UiScale(426); cancel.Width = UiScale(80); cancel.Height = UiScale(28); cancel.FlatStyle = FlatStyle.System;

            f.Controls.AddRange(new Control[] { txt, chkByLayer, chkDelete, chkFallback, ok, cancel });
            f.AcceptButton = ok; f.CancelButton = cancel;
            if (f.ShowDialog() != DialogResult.OK) { f.Dispose(); return; }
            setByLayer = chkByLayer.Checked;
            deleteEmpty = chkDelete.Checked;
            fallbackTo0 = chkFallback.Checked;
            f.Dispose();

            int moved = 0, failed = 0, deleted = 0;
            bool changed = RunWithUndo("CT_LAYERSTANDARD", delegate
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(Db.LayerTableId, OpenMode.ForRead);
                    foreach (var rule in rules) ApplyLayerRule(tr, lt, rule);

                    var targetBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in plans) targetBySource[p.SourceLayer] = p.TargetLayer;
                    if (fallbackTo0)
                        foreach (var p in fallbackPlans) targetBySource[p.SourceLayer] = p.TargetLayer;

                    var space = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;
                        string target;
                        if (!targetBySource.TryGetValue(ent.Layer, out target)) continue;
                        try
                        {
                            ent.UpgradeOpen();
                            ent.Layer = target;
                            if (setByLayer)
                            {
                                ent.ColorIndex = 256;
                                ent.Linetype = "ByLayer";
                                try { ent.LineWeight = LineWeight.ByLayer; } catch (System.Exception ex) { Log("Set entity lineweight ByLayer failed: " + ex.Message); }
                            }
                            moved++;
                        }
                        catch { failed++; }
                    }

                    if (deleteEmpty)
                    {
                        var oldLayers = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                        foreach (var p in plans) oldLayers[p.SourceLayer] = true;
                        if (fallbackTo0)
                            foreach (var p in fallbackPlans) oldLayers[p.SourceLayer] = true;
                        foreach (string oldLayer in new List<string>(oldLayers.Keys))
                        {
                            try
                            {
                                if (oldLayer == "0" || !lt.Has(oldLayer) || Db.Clayer == lt[oldLayer]) continue;
                                var ltr = (LayerTableRecord)tr.GetObject(lt[oldLayer], OpenMode.ForWrite);
                                ltr.Erase();
                                deleted++;
                            }
                            catch (System.Exception ex) { Log("Delete empty old layer failed for " + oldLayer + ": " + ex.Message); }
                        }
                    }
                    tr.Commit();
                }
                return true;
            });
            if (!changed) return;

            Ed.WriteMessage(string.Format("\n\u56fe\u5c42\u89c4\u8303\u5316\u5b8c\u6210\uff1a\u8fc1\u79fb {0} \u4e2a\u5bf9\u8c61\uff0c\u5931\u8d25 {1} \u4e2a\uff0c\u5220\u9664\u7a7a\u65e7\u5c42 {2} \u4e2a\u3002", moved, failed, deleted));
        }

[CommandMethod("CT_SETLAYER0")]
        public void SetLayer0()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            int count = 0;
            bool hasBlocks = false;
            bool changed = RunWithUndo("CT_SETLAYER0", delegate
            {
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
                    foreach (ObjectId id in selectedIds)
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
                            {
                                SetBlockLayer0(tr, br.BlockTableRecord);
                                hasBlocks = true;
                            }
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
                return true;
            });
            if (!changed) return;
            Ed.WriteMessage(string.Format("\n\u5df2\u5c06 {0} \u4e2a\u5bf9\u8c61\u6539\u5230 0 \u5c42\u3002", count));
            if (hasBlocks) Ed.WriteMessage("\n\u6ce8\u610f\uff1a\u5757\u5b9a\u4e49\u5185\u7684\u5bf9\u8c61\u4e5f\u5df2\u5f52\u96f6\uff0c\u5c06\u5f71\u54cd\u6240\u6709\u540c\u540d\u5757\u5b9e\u4f8b\u3002");
        }

[CommandMethod("CT_SELECTBYLAYER")]
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
            var ids = GetSelectionInScopeOrAll(sf, "\n\u9009\u62e9\u8303\u56f4=\u8303\u56f4\u5185\u540c\u56fe\u5c42\uff1b\u56de\u8f66=\u5168\u56fe\u540c\u56fe\u5c42\uff1a", delegate(Entity ent, Transaction tr) { return ent.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase); });
            if (ids == null) return;
            if (ids.Length == 0) { Ed.WriteMessage("\n\u672a\u627e\u5230\u5339\u914d\u5bf9\u8c61\u3002"); return; }
            Ed.SetImpliedSelection(ids);
            Ed.WriteMessage(string.Format("\n\u5df2\u9009\u62e9\u56fe\u5c42 \"{0}\" \u4e0a\u7684 {1} \u4e2a\u5bf9\u8c61\u3002", layerName, ids.Length));
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
            var ids = GetSelectionInScopeOrAll(sf, "\n\u9009\u62e9\u8303\u56f4=\u8303\u56f4\u5185\u540c\u989c\u8272\uff1b\u56de\u8f66=\u5168\u56fe\u540c\u989c\u8272\uff1a", delegate(Entity ent, Transaction tr) { return ent.ColorIndex == colorIndex; });
            if (ids == null) return;
            if (ids.Length == 0) { Ed.WriteMessage("\n\u672a\u627e\u5230\u5339\u914d\u5bf9\u8c61\u3002"); return; }
            Ed.SetImpliedSelection(ids);
            string colorName = colorIndex == 256 ? "ByLayer" : (colorIndex == 0 ? "ByBlock" : colorIndex.ToString());
            Ed.WriteMessage(string.Format("\n\u5df2\u9009\u62e9\u989c\u8272 {0} \u7684 {1} \u4e2a\u5bf9\u8c61\u3002", colorName, ids.Length));
        }

[CommandMethod("CT_ISOLAYER")]
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
                    foreach (var lid in frozen)
                    {
                        if (!lid.IsValid) continue;
                        try { var ltr = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForWrite); ltr.IsFrozen = false; } catch (System.Exception ex) { Log("Restore isolated layer failed: " + ex.Message); }
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
                    if (ltr.Name == targetLayer || ltr.Name == "0" || ltr.IsFrozen || lid == Db.Clayer || ltr.IsDependent) continue;
                    try
                    {
                        ltr.UpgradeOpen();
                        ltr.IsFrozen = true;
                        frozenList.Add(lid);
                    }
                    catch (System.Exception ex) { Log("Freeze non-isolated layer failed: " + ex.Message); }
                }
                tr.Commit();
            }
            _isoState[dk] = frozenList.ToArray();
            Ed.WriteMessage(string.Format("\n\u5df2\u5b64\u7acb\u56fe\u5c42 \"{0}\" \uff0c\u51bb\u7ed3 {1} \u4e2a\u56fe\u5c42\u3002", targetLayer, frozenList.Count));
        }
    }
}


