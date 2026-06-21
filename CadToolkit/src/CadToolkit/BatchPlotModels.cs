using System;

#if AUTOCAD
using Autodesk.AutoCAD.DatabaseServices;
#elif GSTARCAD
using GrxCAD.DatabaseServices;
#elif ZWCAD
using ZwSoft.ZwCAD.DatabaseServices;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        class BatchPlotSettings
        {
            public string DeviceName;
            public string PaperName;
            public string PlotStyle;
            public bool AutoRotate;
            public bool CenterPlot;
            public double MarginPercent;
            public double MarginMm;
            public string FileNameMode;
            public string SortMode;
            public bool ReverseOrder;
            public string OutputDirectory;
            public string DrawingName;
        }

        class BatchPlotFrame
        {
            public ObjectId Id;
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;
            public string SheetNumber;
            public string SheetName;
            public int SelectionOrder;

            public double Width { get { return MaxX - MinX; } }
            public double Height { get { return MaxY - MinY; } }
        }

        class BatchPlotFrameBlockKey
        {
            public ObjectId DefinitionId;
            public string Name;
            public string DisplayName;
        }
    }
}
