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
                CountLayerStandardEntities(tr, GetLayerStandardScopeIds(tr), layerCounts);
                tr.Commit();
            }

            var plans = new List<LayerStandardPlan>();
            var fallbackPlans = new List<LayerStandardPlan>();
            var whitelistPlans = new List<LayerStandardPlan>();
            string layerWhitelist = Config.LayerStandardWhitelist;
            foreach (var pair in layerCounts)
            {
                var whitelistMatch = MatchWhitelistPattern(pair.Key, layerWhitelist);
                if (whitelistMatch != null)
                {
                    whitelistPlans.Add(new LayerStandardPlan { SourceLayer = pair.Key, TargetLayer = "", Count = pair.Value, Rule = null, Reason = FormatWhitelistReason(whitelistMatch) });
                    continue;
                }
                var match = MatchLayerRuleDetail(pair.Key, rules);
                if (match == null)
                {
                    fallbackPlans.Add(new LayerStandardPlan { SourceLayer = pair.Key, TargetLayer = "0", Count = pair.Value, Rule = null, Reason = "\u672a\u8bc6\u522b\u4e14\u672a\u547d\u4e2d\u767d\u540d\u5355" });
                    continue;
                }
                var rule = match.Rule;
                if (pair.Key.Equals(rule.Name, StringComparison.OrdinalIgnoreCase)) continue;
                plans.Add(new LayerStandardPlan { SourceLayer = pair.Key, TargetLayer = rule.Name, Count = pair.Value, Rule = rule, Reason = FormatLayerRuleReason(match) });
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
            f.AutoScaleMode = AutoScaleMode.None; f.AutoScroll = true; f.ClientSize = new Size(UiScale(560), UiScale(510));

            var tree = new TreeView();
            tree.HideSelection = false;
            tree.FullRowSelect = true;
            tree.ShowNodeToolTips = true;
            tree.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            tree.Left = UiScale(12); tree.Top = UiScale(12); tree.Width = UiScale(536); tree.Height = UiScale(340);
            BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, fallbackTo0);

            var chkByLayer = new CheckBox();
            chkByLayer.Text = "\u5c06\u8fc1\u79fb\u5bf9\u8c61\u7684\u989c\u8272/\u7ebf\u578b/\u7ebf\u5bbd\u6539\u4e3a ByLayer";
            chkByLayer.Left = UiScale(12); chkByLayer.Top = UiScale(358); chkByLayer.Width = UiScale(536); chkByLayer.Height = UiScale(24); chkByLayer.Checked = true;
            chkByLayer.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkDelete = new CheckBox();
            chkDelete.Text = "\u5220\u9664\u7a7a\u7684\u65e7\u56fe\u5c42";
            chkDelete.Left = UiScale(12); chkDelete.Top = UiScale(386); chkDelete.Width = UiScale(190); chkDelete.Height = UiScale(24); chkDelete.Checked = false;
            chkDelete.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkFallback = new CheckBox();
            chkFallback.Text = "\u672a\u8bc6\u522b\u56fe\u5c42\u5f52 0 \u5c42\uff08\u767d\u540d\u5355\u4e0d\u5904\u7406\uff09";
            chkFallback.Left = UiScale(12); chkFallback.Top = UiScale(414); chkFallback.Width = UiScale(536); chkFallback.Height = UiScale(24); chkFallback.Checked = fallbackTo0;
            chkFallback.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
            chkFallback.CheckedChanged += delegate { BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked); };

            var ok = new Button();
            ok.Text = "\u6267\u884c"; ok.DialogResult = DialogResult.OK;
            ok.Left = UiScale(376); ok.Top = UiScale(470); ok.Width = UiScale(80); ok.Height = UiScale(28); ok.FlatStyle = FlatStyle.System;

            var cancel = new Button();
            cancel.Text = "\u53d6\u6d88"; cancel.DialogResult = DialogResult.Cancel;
            cancel.Left = UiScale(468); cancel.Top = UiScale(470); cancel.Width = UiScale(80); cancel.Height = UiScale(28); cancel.FlatStyle = FlatStyle.System;

            f.Controls.AddRange(new Control[] { tree, chkByLayer, chkDelete, chkFallback, ok, cancel });
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

                    moved += MoveLayerStandardEntities(tr, GetLayerStandardScopeIds(tr), targetBySource, setByLayer, ref failed);

                    if (deleteEmpty)
                    {
                        var oldLayers = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                        foreach (var p in plans) oldLayers[p.SourceLayer] = true;
                        if (fallbackTo0)
                        {
                            foreach (var p in fallbackPlans) oldLayers[p.SourceLayer] = true;
                            AddLayerStandardCleanupCandidates(tr, lt, rules, layerWhitelist, oldLayers);
                        }
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

        static List<ObjectId> GetLayerStandardScopeIds(Transaction tr)
        {
            var ids = new List<ObjectId>();
            var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                try
                {
                    var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                    if (btr == null || IsExternalBlockRecord(btr)) continue;
                    ids.Add(btrId);
                }
                catch (System.Exception ex) { Log("Read layer standard scope failed: " + ex.Message); }
            }
            return ids;
        }

        static bool IsExternalBlockRecord(BlockTableRecord btr)
        {
            return TryGetBoolProperty(btr, "IsFromExternalReference")
                || TryGetBoolProperty(btr, "IsFromOverlayReference")
                || TryGetBoolProperty(btr, "IsDependent");
        }

        static bool TryGetBoolProperty(object target, string propertyName)
        {
            try
            {
                if (target == null) return false;
                var prop = target.GetType().GetProperty(propertyName);
                if (prop == null || prop.PropertyType != typeof(bool)) return false;
                return (bool)prop.GetValue(target, null);
            }
            catch { return false; }
        }

        static void CountLayerStandardEntities(Transaction tr, List<ObjectId> scopeIds, Dictionary<string, int> layerCounts)
        {
            foreach (ObjectId btrId in scopeIds)
            {
                var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) continue;
                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    AddLayerCount(layerCounts, ent.Layer);
                    CountBlockReferenceAttributes(tr, ent as BlockReference, layerCounts);
                }
            }
        }

        static void AddLayerCount(Dictionary<string, int> layerCounts, string layer)
        {
            if (string.IsNullOrEmpty(layer)) return;
            if (!layerCounts.ContainsKey(layer)) layerCounts[layer] = 0;
            layerCounts[layer]++;
        }

        static void CountBlockReferenceAttributes(Transaction tr, BlockReference br, Dictionary<string, int> layerCounts)
        {
            if (br == null || br.AttributeCollection == null) return;
            try
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (att == null) continue;
                    AddLayerCount(layerCounts, att.Layer);
                }
            }
            catch (System.Exception ex) { Log("Count block attributes failed: " + ex.Message); }
        }

        static int MoveLayerStandardEntities(Transaction tr, List<ObjectId> scopeIds, Dictionary<string, string> targetBySource, bool setByLayer, ref int failed)
        {
            int moved = 0;
            foreach (ObjectId btrId in scopeIds)
            {
                var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) continue;
                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    if (MoveLayerStandardEntity(ent, targetBySource, setByLayer, ref failed)) moved++;
                    moved += MoveBlockReferenceAttributes(tr, ent as BlockReference, targetBySource, setByLayer, ref failed);
                }
            }
            return moved;
        }

        static bool MoveLayerStandardEntity(Entity ent, Dictionary<string, string> targetBySource, bool setByLayer, ref int failed)
        {
            string target;
            if (!targetBySource.TryGetValue(ent.Layer, out target)) return false;
            try
            {
                ent.UpgradeOpen();
                ent.Layer = target;
                if (setByLayer) ApplyByLayer(ent);
                return true;
            }
            catch (System.Exception ex)
            {
                failed++;
                Log("Move layer standard entity failed: " + ex.Message);
                return false;
            }
        }

        static int MoveBlockReferenceAttributes(Transaction tr, BlockReference br, Dictionary<string, string> targetBySource, bool setByLayer, ref int failed)
        {
            if (br == null || br.AttributeCollection == null) return 0;
            int moved = 0;
            try
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (att == null) continue;
                    if (MoveLayerStandardEntity(att, targetBySource, setByLayer, ref failed)) moved++;
                }
            }
            catch (System.Exception ex) { Log("Move block attributes failed: " + ex.Message); }
            return moved;
        }

        static void ApplyByLayer(Entity ent)
        {
            ent.ColorIndex = 256;
            ent.Linetype = "ByLayer";
            try { ent.LineWeight = LineWeight.ByLayer; } catch (System.Exception ex) { Log("Set entity lineweight ByLayer failed: " + ex.Message); }
        }

        static void AddLayerStandardCleanupCandidates(Transaction tr, LayerTable lt, List<LayerStandardRule> rules, string whitelist, Dictionary<string, bool> oldLayers)
        {
            foreach (ObjectId layerId in lt)
            {
                try
                {
                    var ltr = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
                    if (ltr == null || string.IsNullOrEmpty(ltr.Name) || ltr.IsDependent) continue;
                    if (ltr.Name == "0" || IsLayerWhitelisted(ltr.Name, whitelist) || IsStandardLayer(ltr.Name, rules)) continue;
                    oldLayers[ltr.Name] = true;
                }
                catch (System.Exception ex) { Log("Collect empty old layer candidate failed: " + ex.Message); }
            }
        }

        static bool IsStandardLayer(string layerName, List<LayerStandardRule> rules)
        {
            foreach (var rule in rules)
                if (rule.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void BuildLayerPlanTreePreview(TreeView tree, List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0)
        {
            tree.BeginUpdate();
            try
            {
                tree.Nodes.Clear();
                tree.Nodes.AddRange(BuildLayerPlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0));
            }
            finally
            {
                tree.EndUpdate();
            }
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
                var state = _isoState[dk];
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    foreach (var lid in state.FrozenLayers)
                    {
                        if (!lid.IsValid) continue;
                        try { var ltr = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForWrite); ltr.IsFrozen = false; } catch (System.Exception ex) { Log("Restore isolated layer failed: " + ex.Message); }
                    }
                    RestoreIsoCurrentLayer(state);
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
            ObjectId previousCurrentLayer = Db.Clayer;
            bool hasPreviousCurrentLayer = previousCurrentLayer.IsValid;
            bool keepLayer0 = Config.IsoLayerKeepLayer0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(Db.LayerTableId, OpenMode.ForRead);
                EnsureIsoCurrentLayer(lt, targetLayer);
                foreach (ObjectId lid in lt)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForRead);
                    if (IsIsoTargetLayer(ltr.Name, targetLayer) || ShouldKeepIsoLayer0(ltr.Name, keepLayer0) || ltr.IsFrozen || ltr.IsDependent) continue;
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
            _isoState[dk] = new IsoLayerState { FrozenLayers = frozenList.ToArray(), PreviousCurrentLayer = previousCurrentLayer, HasPreviousCurrentLayer = hasPreviousCurrentLayer };
            Ed.WriteMessage(string.Format("\n\u5df2\u5b64\u7acb\u56fe\u5c42 \"{0}\" \uff0c\u51bb\u7ed3 {1} \u4e2a\u56fe\u5c42\u3002", targetLayer, frozenList.Count));
        }

        static bool IsIsoTargetLayer(string layerName, string targetLayer)
        {
            return SafeStr(layerName).Equals(SafeStr(targetLayer), StringComparison.OrdinalIgnoreCase);
        }

        static bool ShouldKeepIsoLayer0(string layerName, bool keepLayer0)
        {
            return keepLayer0 && SafeStr(layerName).Equals("0", StringComparison.OrdinalIgnoreCase);
        }

        static void EnsureIsoCurrentLayer(LayerTable lt, string targetLayer)
        {
            try
            {
                if (!lt.Has(targetLayer)) return;
                ObjectId targetId = lt[targetLayer];
                if (Db.Clayer == targetId) return;
                Db.Clayer = targetId;
            }
            catch (System.Exception ex) { Log("Switch current layer for isolate failed: " + ex.Message); }
        }

        static void RestoreIsoCurrentLayer(IsoLayerState state)
        {
            try
            {
                if (state == null || !state.HasPreviousCurrentLayer || !state.PreviousCurrentLayer.IsValid) return;
                Db.Clayer = state.PreviousCurrentLayer;
            }
            catch (System.Exception ex) { Log("Restore current layer after isolate failed: " + ex.Message); }
        }
    }
}


