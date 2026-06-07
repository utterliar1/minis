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
[CommandMethod("CT_RENAMEBLOCK")]
        public void RenameBlock()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            var psr = Ed.SelectImplied();
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
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            var ppr = Ed.GetPoint("\n\u6307\u5b9a\u5757\u57fa\u70b9\uff1a");
            if (ppr.Status != PromptStatus.OK) return;
            string prefix = Config.Prefix;
            bool del = Config.DeleteOriginal;
            string createdName = "";
            int objectCount = 0;
            bool changed = RunWithUndo("CT_QUICKBLOCK", delegate
            {
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
                    var ids = new ObjectIdCollection(selectedIds);
                    var mapping = new IdMapping();
                    Db.DeepCloneObjects(ids, btr.Id, mapping, false);
                    var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                    var br = new BlockReference(ppr.Value, btr.Id);
                    msBtr.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);
                    if (del)
                    {
                        foreach (ObjectId id in selectedIds)
                        {
                            tr.GetObject(id, OpenMode.ForWrite).Erase();
                        }
                    }
                    tr.Commit();
                    createdName = name;
                    objectCount = ids.Count;
                }
                return true;
            });
            if (!changed) return;
            Ed.WriteMessage(string.Format("\n\u5df2\u521b\u5efa\u5757 \"{0}\" \uff0c\u5305\u542b {1} \u4e2a\u5bf9\u8c61\u3002", createdName, objectCount));
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
            var ids = GetSelectionInScopeOrAll(sf, "\n\u9009\u62e9\u8303\u56f4=\u8303\u56f4\u5185\u540c\u540d\u5757\uff1b\u56de\u8f66=\u5168\u56fe\u540c\u540d\u5757\uff1a", delegate(Entity ent, Transaction tr)
            {
                var br = ent as BlockReference;
                if (br == null) return false;
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                return btr.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase);
            });
            if (ids == null) return;
            if (ids.Length == 0) { Ed.WriteMessage("\n\u672a\u627e\u5230\u5339\u914d\u5bf9\u8c61\u3002"); return; }
            Ed.SetImpliedSelection(ids);
            Ed.WriteMessage(string.Format("\n\u5df2\u9009\u62e9\u5757 \"{0}\" \u7684 {1} \u4e2a\u53c2\u7167\u3002", blockName, ids.Length));
        }
    }
}

