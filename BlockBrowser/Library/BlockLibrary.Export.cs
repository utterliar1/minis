using System;
using System.IO;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif


namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static bool ExportBlockFromCurrentDrawing(string blockName, string category)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            Editor ed = doc.Editor;
            try
            {
                EnsureActiveLibraryWritable();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n导出失败: " + ex.Message);
                return false;
            }
            BlockWritePlan plan = BlockWriteService.PrepareSaveTarget(LibraryPath, blockName, category);
            if (!plan.IsValid)
            {
                ed.WriteMessage("\n名称或分类包含非法字符，取消。");
                return false;
            }
            // 覆盖确认
            if (plan.Exists)
            {
                var ow2 = ed.GetString("\n文件已存在，覆盖？[Y/N] <N>: ");
                if (ow2.Status != PromptStatus.OK || !ow2.StringResult.Trim().ToUpperInvariant().StartsWith("Y"))
                {
                    ed.WriteMessage("\n取消。");
                    return false;
                }
            }
            try
            {
                using (DocumentLock dl = doc.LockDocument())
                {
                    Database db = doc.Database;
                    ObjectId blockId;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        if (!bt.Has(plan.BlockName))
                        {
                            ed.WriteMessage(string.Format("\n未找到块: {0}", plan.BlockName));
                            tr.Commit();
                            return false;
                        }
                        blockId = bt[plan.BlockName];
                        tr.Commit();
                    }
                    using (Database newDb = db.Wblock(blockId))
                        newDb.SaveAs(plan.OutputPath, DwgVersion.Current);
                }
                RefreshThumbnail(BlockWriteService.CreateSavedBlockInfo(plan.OutputPath, plan.Category));
                RecordLocalChange(LocalChangeAction.Add, ToLibraryRelativePath(plan.OutputPath), "", null);
                ed.WriteMessage(string.Format("\n块 {0} 已导出到 [{1}]", plan.BlockName, plan.Category));
                return true;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n导出失败: {0}", ex.Message));
                return false;
            }
        }
    }
}
