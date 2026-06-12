using System.IO;

#if AUTOCAD
using Autodesk.AutoCAD.Runtime;
#elif GSTARCAD
using GrxCAD.Runtime;
#elif ZWCAD
using ZwSoft.ZwCAD.Runtime;
#endif

[assembly: CommandClass(typeof(BlockBrowser.BlockBrowserCommands))]
[assembly: ExtensionApplication(typeof(BlockBrowser.BlockBrowserPlugin))]

namespace BlockBrowser
{
    public class BlockBrowserPlugin : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
#if GSTARCAD
                BlockLibrary.PlatformName = "GstarCAD";
#elif AUTOCAD
                BlockLibrary.PlatformName = "AutoCAD";
#elif ZWCAD
                BlockLibrary.PlatformName = "ZWCAD";
#endif
                BlockLibrary.LoadConfig();
                BlockLibrary.RefreshActiveLibrary();
                if (BlockLibrary.ActiveLibrary != null && BlockLibrary.ActiveLibrary.IsAvailable && !Directory.Exists(BlockLibrary.LibraryPath))
                    Directory.CreateDirectory(BlockLibrary.LibraryPath);
                BlockLibrary.CleanupDiskCache();
            }
            catch { }
        }
        public void Terminate() { }
    }
}
