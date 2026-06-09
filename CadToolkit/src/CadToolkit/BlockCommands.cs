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

[CommandMethod("CT_CHANGEBASEPOINT")]
        public void ChangeBlockBasepoint()
        {
            EnsureInit();
            if (!CheckDoc()) return;

            var peo = new PromptEntityOptions("\n选择要改基点的块：");
            peo.SetRejectMessage("\n只能选择块参照。");
            peo.AddAllowedClass(typeof(BlockReference), true);
            var per = Ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            string blockName = "";
            string rejectReason = "";
            ObjectId blockDefId = default(ObjectId);
            ObjectId[] referenceIds = null;

            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var br = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                blockName = btr.Name;
                blockDefId = br.BlockTableRecord;

                if (!CanChangeBlockBasepoint(br, btr, out rejectReason))
                {
                    Ed.WriteMessage("\n当前块不支持改基点：" + rejectReason);
                    return;
                }

                tr.Commit();
            }

            var ppr = Ed.GetPoint("\n指定新的块基点：");
            if (ppr.Status != PromptStatus.OK) return;

            bool changed = RunWithUndo("CT_CHANGEBASEPOINT", delegate
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var selectedBr = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    var selectedBtr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForWrite);
                    if (!CanChangeBlockBasepoint(selectedBr, selectedBtr, out rejectReason))
                    {
                        Ed.WriteMessage("\n当前块不支持改基点：" + rejectReason);
                        return false;
                    }

                    Point3d oldOrigin = selectedBtr.Origin;
                    Point3d newOrigin = TransformPointByInverse(ppr.Value, selectedBr.BlockTransform);
                    referenceIds = GetBlockReferencesForDefinition(tr, blockDefId);

                    var shifts = new Dictionary<ObjectId, Vector3d>();
                    foreach (ObjectId id in referenceIds)
                    {
                        var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;
                        Point3d oldBasePoint = oldOrigin.TransformBy(br.BlockTransform);
                        Point3d newBasePoint = newOrigin.TransformBy(br.BlockTransform);
                        shifts[id] = VectorBetween(oldBasePoint, newBasePoint);
                    }

                    selectedBtr.Origin = newOrigin;

                    foreach (var pair in shifts)
                    {
                        var br = tr.GetObject(pair.Key, OpenMode.ForWrite) as BlockReference;
                        if (br == null) continue;
                        Vector3d shift = pair.Value;
                        br.Position = AddVector(br.Position, shift);
                    }

                    tr.Commit();
                }
                return true;
            });

            if (!changed) return;
            int affectedReferences = referenceIds == null ? 0 : referenceIds.Length;
            Ed.WriteMessage(string.Format("\n已修改块 \"{0}\" 的基点，影响 {1} 个同定义参照。", blockName, affectedReferences));
        }

        static bool CanChangeBlockBasepoint(BlockReference br, BlockTableRecord btr, out string reason)
        {
            reason = "";
            if (br == null || btr == null) { reason = "块数据无效"; return false; }
            if (TryGetBoolProperty(br, "IsDynamicBlock")) { reason = "动态块暂不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsDynamicBlock")) { reason = "动态块暂不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsAnonymous")) { reason = "匿名块暂不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsLayout")) { reason = "布局块不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsFromExternalReference") || TryGetBoolProperty(btr, "IsFromOverlayReference") || TryGetBoolProperty(btr, "IsDependent"))
            {
                reason = "外部参照或依赖块不支持";
                return false;
            }
            return true;
        }

        static Point3d TransformPointByInverse(Point3d point, Matrix3d matrix)
        {
            try
            {
                var method = typeof(Matrix3d).GetMethod("Inverse", new Type[0]);
                if (method != null)
                {
                    var inverse = (Matrix3d)method.Invoke(matrix, null);
                    return point.TransformBy(inverse);
                }
            }
            catch (System.Exception ex) { Log("Transform point by inverse failed: " + ex.Message); }
            return point;
        }

        static Vector3d VectorBetween(Point3d fromPoint, Point3d toPoint)
        {
            return new Vector3d(toPoint.X - fromPoint.X, toPoint.Y - fromPoint.Y, toPoint.Z - fromPoint.Z);
        }

        static Point3d AddVector(Point3d point, Vector3d vector)
        {
            return new Point3d(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);
        }

        static ObjectId[] GetBlockReferencesForDefinition(Transaction tr, ObjectId blockDefId)
        {
            var ids = new List<ObjectId>();
            var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                var owner = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (owner == null) continue;
                if (TryGetBoolProperty(owner, "IsFromExternalReference") || TryGetBoolProperty(owner, "IsFromOverlayReference") || TryGetBoolProperty(owner, "IsDependent")) continue;
                foreach (ObjectId entId in owner)
                {
                    var br = tr.GetObject(entId, OpenMode.ForRead) as BlockReference;
                    if (br == null) continue;
                    if (br.BlockTableRecord == blockDefId) ids.Add(entId);
                }
            }
            return ids.ToArray();
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

