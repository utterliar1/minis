using System;
using System.Collections.Generic;
using System.IO;
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
    public class BlockBrowserCommands
    {
        [CommandMethod("BB", CommandFlags.Session)]
        public void OpenBlockBrowser()
        {
            OpenBlockBrowserCore();
        }

        [CommandMethod("BB_PANEL", CommandFlags.Session)]
        public void OpenBlockBrowserPanel()
        {
            OpenBlockBrowserCore();
        }

        [CommandMethod("BBPANEL", CommandFlags.Session)]
        public void OpenBlockBrowserPanelCompat()
        {
            OpenBlockBrowserCore();
        }

        private void OpenBlockBrowserCore()
        {
            try
            {
                string pendingCmd = null, pendingCategory = null, pendingBlockName = null;
                using (var form = new BlockBrowserForm())
                {
                    CadApp.ShowModalDialog(form);
                    if (form.DialogResult == DialogResult.OK && form.SelectedInsertBlock != null)
                    {
                        BlockLibrary.InsertBlock(form.SelectedInsertBlock, form.InsertScale, form.InsertRotation);
                    }
                    else if (form.DialogResult == DialogResult.Abort && !string.IsNullOrEmpty(form.PendingCommand))
                    {
                        pendingCmd = form.PendingCommand;
                        pendingCategory = form.PendingCategory;
                        pendingBlockName = form.PendingBlockName;
                    }
                }
                // 窗口关闭后，CAD原生接管焦点，直接执行操作
                if (pendingCmd == "BBADD" && !string.IsNullOrEmpty(pendingCategory))
                    DoAddToLibrary(pendingCategory, pendingBlockName);
                else if (pendingCmd == "BBEXPORT")
                    DoExportBlock();
            }
            catch (System.Exception ex) { MessageBox.Show("打开失败:\n" + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void DoAddToLibrary(string category, string blockName)
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try {
            var sr = ed.GetSelection();
            if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择对象，取消。"); return; }
            var pr = ed.GetPoint("\n指定块基点（回车用原点）: ");
            Point3d basePt;
            if (pr.Status == PromptStatus.OK)
                basePt = pr.Value;
            else if (pr.Status == PromptStatus.None)
                basePt = new Point3d(0, 0, 0);
            else
                { ed.WriteMessage("\n取消。"); return; }
            BlockLibrary.SaveSelectionAsBlockWithSelection(sr, blockName, category, basePt);
            } catch (System.Exception ex) { ed.WriteMessage("\n添加失败: " + ex.Message); }
        }

        private void DoExportBlock()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try {

            // Read all user blocks from current drawing
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
                ed.WriteMessage("\n当前图纸没有块定义。");
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
            if (request.Action == ExportBlockRequestAction.Cancel) { ed.WriteMessage("\n取消。"); return; }
            if (request.Action == ExportBlockRequestAction.InvalidCategory) { ed.WriteMessage("\n分类包含非法字符，取消。"); return; }
            int ok = 0, fail = 0;
            foreach (var blk in request.SelectedBlocks)
            {
                if (BlockLibrary.ExportBlockFromCurrentDrawing(blk, request.Category)) ok++; else fail++;
            }
            ed.WriteMessage("\n" + ExportBlockRequestService.FormatCompletion(ok, fail));
            } catch (System.Exception ex) { ed.WriteMessage("\n导出失败: " + ex.Message); }
        }
        [CommandMethod("KLLQ", CommandFlags.Session)]
        public void OpenBlockBrowserAlias() { OpenBlockBrowser(); }
        [CommandMethod("BBADD", CommandFlags.Session)]
        public void AddToLibrary()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try {
            ed.WriteMessage("\n块库: " + BlockLibrary.LibraryPath);
            var cr = ed.GetString("\n分类 [常用]: ");
            string cat = "常用";
            if (cr.Status == PromptStatus.OK && !string.IsNullOrEmpty(cr.StringResult)) cat = cr.StringResult.Trim();
            var nr = ed.GetString("\n块名称: ");
            if (nr.Status != PromptStatus.OK || string.IsNullOrEmpty(nr.StringResult)) { ed.WriteMessage("\n取消。"); return; }
            var pr = ed.GetPoint("\n指定块基点（回车用原点）: ");
            Point3d basePt;
            if (pr.Status == PromptStatus.OK) basePt = pr.Value;
            else if (pr.Status == PromptStatus.None) basePt = new Point3d(0, 0, 0);
            else { ed.WriteMessage("\n取消。"); return; }
            var sr = ed.GetSelection();
            if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择对象，取消。"); return; }
            BlockLibrary.SaveSelectionAsBlockWithSelection(sr, nr.StringResult.Trim(), cat, basePt);
            } catch (System.Exception ex) { ed.WriteMessage("\n添加失败: " + ex.Message); }
        }
        [CommandMethod("BBEXPORT", CommandFlags.Session)]
        public void ExportBlockToLibrary()
        {
            DoExportBlock();
        }
        [CommandMethod("BBSYNC", CommandFlags.Session)]
        public void SyncLocalChanges()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (!BlockLibrary.AllowNasSync)
                {
                    ed.WriteMessage("\n当前电脑未启用同步到 NAS。请联系指定维护人。");
                    return;
                }

                OpenSyncCenterDialog();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n同步失败: " + ex.Message);
            }
        }

        private void OpenSyncCenterDialog()
        {
            using (var dlg = new SyncCenterDialog(
                () => BlockLibrary.PreviewLocalSync(),
                () => BlockLibrary.SyncSafeUploadsToNas(),
                BlockLibrary.SyncLogPath))
            {
                CadApp.ShowModalDialog(dlg);
            }
        }

        [CommandMethod("BBMIRROR", CommandFlags.Session)]
        public void UpdateLocalMirror()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var preview = BlockLibrary.PreviewLocalMirrorFromNas();
                ed.WriteMessage("\n" + MirrorSummaryMessageService.FormatPreviewCommand(preview));
                var confirm = ed.GetString("\n确认更新本地图库? [Y/N] ");
                if (confirm.Status != PromptStatus.OK || !string.Equals((confirm.StringResult ?? "").Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                {
                    ed.WriteMessage("\n已取消更新本地图库。");
                    return;
                }

                var result = BlockLibrary.UpdateLocalMirrorFromNas();
                ed.WriteMessage("\n" + MirrorSummaryMessageService.FormatCommand(result));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n更新本地图库失败: " + ex.Message);
            }
        }
        [CommandMethod("BBTHUMB", CommandFlags.Session)]
        public void RefreshThumbnails()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                string cp = BlockLibrary.ThumbnailCachePath;
                if (Directory.Exists(cp)) { Directory.Delete(cp, true); ed.WriteMessage("\n缓存已清除: " + cp); }
                else ed.WriteMessage("\n缓存不存在。");
            }
            catch (System.Exception ex) { ed.WriteMessage("\n清除失败: " + ex.Message); }
        }
        [CommandMethod("BBINFO", CommandFlags.Session)]
        public void ShowInfo()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                foreach (var line in BlockBrowserInfoService.FormatLines(
                    BlockLibrary.AppVersion,
                    BlockLibrary.PlatformName,
                    BlockLibrary.LibraryPath))
                {
                    ed.WriteMessage("\n" + line);
                }
            }
            catch (System.Exception ex) { ed.WriteMessage("\n错误: " + ex.Message); }
        }
    }
}
