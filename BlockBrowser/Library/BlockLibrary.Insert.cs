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



    }
}
