using System.IO;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.Runtime;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.Runtime;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace BlockBrowser
{
    public partial class BlockBrowserCommands
    {
        [CommandMethod("BBTHUMB", CommandFlags.Session)]
        public void RefreshThumbnails()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                string cp = BlockLibrary.ThumbnailCachePath;
                if (Directory.Exists(cp)) { Directory.Delete(cp, true); ed.WriteMessage("\n\u7F13\u5B58\u5DF2\u6E05\u9664: " + cp); }
                else ed.WriteMessage("\n\u7F13\u5B58\u4E0D\u5B58\u5728\u3002");
            }
            catch (System.Exception ex) { ed.WriteMessage("\n\u6E05\u9664\u5931\u8D25: " + ex.Message); }
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
            catch (System.Exception ex) { ed.WriteMessage("\n\u9519\u8BEF: " + ex.Message); }
        }
    }
}
