using System.Windows.Forms;

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

        [CommandMethod("KLLQ", CommandFlags.Session)]
        public void OpenBlockBrowserAlias()
        {
            OpenBlockBrowser();
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

                if (pendingCmd == "BBADD" && !string.IsNullOrEmpty(pendingCategory))
                    DoAddToLibrary(pendingCategory, pendingBlockName);
                else if (pendingCmd == "BBEXPORT")
                    DoExportBlock();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("鎵撳紑澶辫触:\n" + ex.Message, "鍧楁祻瑙堝櫒", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
