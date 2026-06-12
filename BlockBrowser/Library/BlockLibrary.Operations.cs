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
        public static bool RenameBlock(BlockInfo block, string newName)
        {
            EnsureActiveLibraryWritable();
            if (!BlockFileOperations.CanRenameBlock(block, newName, true)) return false;

            string oldPath = block.FilePath;
            string oldCacheKey = GetCacheKey(block);
            string newPath = BlockFileOperations.RenameBlockFile(block, newName);
            if (string.IsNullOrEmpty(newPath)) return false;

            RecordLocalChange(
                LocalChangeAction.Rename,
                ToLibraryRelativePath(oldPath),
                ToLibraryRelativePath(newPath),
                null);

            block.FilePath = newPath;
            string newCacheKey = GetCacheKey(block);
            string thumbDir = ThumbnailCachePath;
            string oldCache = Path.Combine(thumbDir, oldCacheKey + ".png");
            string newCache = Path.Combine(thumbDir, newCacheKey + ".png");
            if (File.Exists(oldCache))
            {
                try { File.Move(oldCache, newCache); } catch { }
            }
            return true;
        }

        public static void InsertBlock(BlockInfo block, double scale, double rotation)
        {
            if (block == null || !File.Exists(block.FilePath)) return;
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                PromptPointResult pr = ed.GetPoint("\n指定插入点: ");
                if (pr.Status != PromptStatus.OK) return;
                Point3d pt = pr.Value;
                string bname = block.Name;
                using (DocumentLock dl = doc.LockDocument())
                {
                    // 导入块定义（同名则生成唯一名称）
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                        if (bt.Has(bname))
                        {
                            int suffix = 2;
                            while (bt.Has(bname + "_" + suffix)) suffix++;
                            bname = bname + "_" + suffix;
                        }
                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(block.FilePath, FileOpenMode.OpenForReadAndAllShare, true, "");
                            db.Insert(bname, extDb, true);
                        }
                        tr.Commit();
                    }
                    // 创建块参照
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        if (!bt.Has(bname))
                        {
                            ed.WriteMessage("\n导入失败: " + bname);
                            tr.Commit();
                            return;
                        }
                        BlockReference bref = new BlockReference(pt, bt[bname]);
                        bref.ScaleFactors = new Scale3d(scale, scale, scale);
                        bref.Rotation = rotation;
                        BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        ms.AppendEntity(bref);
                        tr.AddNewlyCreatedDBObject(bref, true);
                        tr.Commit();
                    }
                }
                AddRecentBlock(block.FilePath);
                ed.WriteMessage("\n已插入: " + bname);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n插入失败: " + ex.Message);
            }
        }



        public static bool SaveSelectionAsBlockWithSelection(PromptSelectionResult sr, string blockName, string category, Point3d basePt)
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
                ed.WriteMessage("\n保存失败: " + ex.Message);
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
                var ow = ed.GetString("\n文件已存在，覆盖？[Y/N] <N>: ");
                if (ow.Status != PromptStatus.OK || !ow.StringResult.Trim().ToUpperInvariant().StartsWith("Y"))
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
                    ObjectIdCollection ids = new ObjectIdCollection(sr.Value.GetObjectIds());
                    using (Database newDb = db.Wblock(ids, basePt))
                    {
                        newDb.SaveAs(plan.OutputPath, DwgVersion.Current);
                    }
                }
                RefreshThumbnail(BlockWriteService.CreateSavedBlockInfo(plan.OutputPath, plan.Category));
                RecordLocalChange(LocalChangeAction.Add, ToLibraryRelativePath(plan.OutputPath), "", null);
                ed.WriteMessage(string.Format("\n块 {0} 已保存到 [{1}]，基点: ({2:F2},{3:F2})", plan.BlockName, plan.Category, basePt.X, basePt.Y));
                return true;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n保存失败: {0}", ex.Message));
                return false;
            }
        }

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
