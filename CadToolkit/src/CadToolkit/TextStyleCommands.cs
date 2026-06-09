using CadToolkit.Core;

#if AUTOCAD
using Autodesk.AutoCAD.Runtime;
#elif GSTARCAD
using GrxCAD.Runtime;
#elif ZWCAD
using ZwSoft.ZwCAD.Runtime;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        [CommandMethod("CT_TEXTSTYLESTANDARD")]
        public void TextStyleStandard()
        {
            EnsureInit();
            if (!CheckDoc()) return;
            Ed.WriteMessage("\n文字样式规范功能正在开发中。");
        }
    }
}
