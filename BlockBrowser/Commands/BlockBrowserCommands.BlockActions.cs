using System.Collections.Generic;
using System.Windows.Forms;

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

namespace BlockBrowser
{
    public partial class BlockBrowserCommands
    {
        private void DoAddToLibrary(string category, string blockName)
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var sr = ed.GetSelection();
                if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n\u672A\u9009\u62E9\u5BF9\u8C61\uFF0C\u53D6\u6D88\u3002"); return; }
                var pr = ed.GetPoint("\n\u6307\u5B9A\u5757\u57FA\u70B9\uFF08\u56DE\u8F66\u7528\u539F\u70B9\uFF09: ");
                Point3d basePt;
                if (pr.Status == PromptStatus.OK)
                    basePt = pr.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                else if (pr.Status == PromptStatus.None)
                    basePt = new Point3d(0, 0, 0);
                else
                { ed.WriteMessage("\n\u53D6\u6D88\u3002"); return; }
                BlockLibrary.SaveSelectionAsBlockWithSelection(sr, blockName, category, basePt);
            }
            catch (System.Exception ex) { ed.WriteMessage("\n\u6DFB\u52A0\u5931\u8D25: " + ex.Message); }
        }

        private void DoExportBlock()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try
            {
                var blockNames = new List<string>();
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        if (!btr.IsLayout && !btr.Name.StartsWith("*"))
                            blockNames.Add(btr.Name);
                    }
                    tr.Commit();
                }

                if (blockNames.Count == 0)
                {
                    ed.WriteMessage("\n\u5F53\u524D\u56FE\u7EB8\u6CA1\u6709\u5757\u5B9A\u4E49\u3002");
                    return;
                }

                blockNames.Sort();

                var selectedBlocks = new List<string>();
                string selCategory = null;
                var categories = CategorySelectionService.GetUserCategories(BlockLibrary.GetCategories());
                using (var form = new ExportBlocksDialog(blockNames, categories))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        selectedBlocks.AddRange(form.SelectedBlocks);
                        selCategory = form.SelectedCategory;
                    }
                }

                var request = ExportBlockRequestService.CreatePlan(selectedBlocks, selCategory, BlockLibrary.IsSafeLibraryName);
                if (request.Action == ExportBlockRequestAction.Cancel) { ed.WriteMessage("\n\u53D6\u6D88\u3002"); return; }
                if (request.Action == ExportBlockRequestAction.InvalidCategory) { ed.WriteMessage("\n\u5206\u7C7B\u5305\u542B\u975E\u6CD5\u5B57\u7B26\uFF0C\u53D6\u6D88\u3002"); return; }

                int ok = 0, fail = 0;
                foreach (var blk in request.SelectedBlocks)
                {
                    if (BlockLibrary.ExportBlockFromCurrentDrawing(blk, request.Category)) ok++; else fail++;
                }
                ed.WriteMessage("\n" + ExportBlockRequestService.FormatCompletion(ok, fail));
            }
            catch (System.Exception ex) { ed.WriteMessage("\n\u5BFC\u51FA\u5931\u8D25: " + ex.Message); }
        }

        [CommandMethod("BBADD", CommandFlags.Session)]
        public void AddToLibrary()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                ed.WriteMessage("\n\u5757\u5E93: " + BlockLibrary.LibraryPath);
                var cr = ed.GetString("\n\u5206\u7C7B [\u5E38\u7528]: ");
                string cat = "\u5E38\u7528";
                if (cr.Status == PromptStatus.OK && !string.IsNullOrEmpty(cr.StringResult)) cat = cr.StringResult.Trim();
                var nr = ed.GetString("\n\u5757\u540D\u79F0: ");
                if (nr.Status != PromptStatus.OK || string.IsNullOrEmpty(nr.StringResult)) { ed.WriteMessage("\n\u53D6\u6D88\u3002"); return; }
                var pr = ed.GetPoint("\n\u6307\u5B9A\u5757\u57FA\u70B9\uFF08\u56DE\u8F66\u7528\u539F\u70B9\uFF09: ");
                Point3d basePt;
                if (pr.Status == PromptStatus.OK) basePt = pr.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                else if (pr.Status == PromptStatus.None) basePt = new Point3d(0, 0, 0);
                else { ed.WriteMessage("\n\u53D6\u6D88\u3002"); return; }
                var sr = ed.GetSelection();
                if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n\u672A\u9009\u62E9\u5BF9\u8C61\uFF0C\u53D6\u6D88\u3002"); return; }
                BlockLibrary.SaveSelectionAsBlockWithSelection(sr, nr.StringResult.Trim(), cat, basePt);
            }
            catch (System.Exception ex) { ed.WriteMessage("\n\u6DFB\u52A0\u5931\u8D25: " + ex.Message); }
        }

        [CommandMethod("BBEXPORT", CommandFlags.Session)]
        public void ExportBlockToLibrary()
        {
            DoExportBlock();
        }
    }
}
