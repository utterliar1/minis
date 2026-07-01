using System;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.EditorInput;
using GrxCAD.Runtime;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Runtime;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace BlockBrowser
{
    public partial class BlockBrowserCommands
    {
        [CommandMethod("BBSYNC", CommandFlags.Session)]
        public void SyncLocalChanges()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (!BlockLibrary.AllowNasSync)
                {
                    ed.WriteMessage("\n\u5F53\u524D\u7535\u8111\u672A\u542F\u7528\u540C\u6B65\u5230 NAS\u3002\u8BF7\u8054\u7CFB\u6307\u5B9A\u7EF4\u62A4\u4EBA\u3002");
                    return;
                }

                OpenSyncCenterDialog();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n\u540C\u6B65\u5931\u8D25: " + ex.Message);
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
                if (preview.ChangedCount == 0)
                {
                    ed.WriteMessage("\n" + MirrorSummaryMessageService.FormatCommand(preview));
                    return;
                }

                var confirm = ed.GetString("\n\u786E\u8BA4\u66F4\u65B0\u672C\u5730\u56FE\u5E93? [Y/N] ");
                if (confirm.Status != PromptStatus.OK || !string.Equals((confirm.StringResult ?? "").Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                {
                    ed.WriteMessage("\n\u5DF2\u53D6\u6D88\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u3002");
                    return;
                }

                var result = BlockLibrary.UpdateLocalMirrorFromNas();
                ed.WriteMessage("\n" + MirrorSummaryMessageService.FormatCommand(result));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u5931\u8D25: " + ex.Message);
            }
        }
    }
}
