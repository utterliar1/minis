using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CadToolkit.Core;
using CadToolkit.UI;

#if AUTOCAD
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
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        [CommandMethod("CT_BATCHPLOT", CommandFlags.UsePickSet)]
        public void BatchPlot()
        {
            var peo = new PromptEntityOptions("\n选择一个图框块作为模板：");
            peo.SetRejectMessage("\n只能选择块参照。");
            peo.AddAllowedClass(typeof(BlockReference), true);
            var per = Ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            BatchPlotFrameBlockKey frameBlockKey;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                frameBlockKey = GetBatchPlotFrameBlockKey(per.ObjectId, tr);
                tr.Commit();
            }
            if (frameBlockKey == null)
            {
                Ed.WriteMessage("\n未能识别图框块。");
                return;
            }

            ObjectId[] selectedIds = CollectBatchPlotFrameBlockIds(frameBlockKey);
            if (selectedIds == null) return;
            if (selectedIds.Length == 0)
            {
                Ed.WriteMessage("\n未在选择范围内找到同名图框块。");
                return;
            }

            List<BatchPlotFrame> frames = CollectPlotFrames(selectedIds);
            if (frames.Count == 0)
            {
                Ed.WriteMessage("\n未找到可打印的图框范围。");
                return;
            }

            string outputDirectory = GetBatchPlotOutputDirectory();
            string drawingName = GetBatchPlotDrawingName();
            SortPlotFrames(frames, "Position");
            List<BatchPlotPreflightRow> preflightRows = BuildBatchPlotPreflightRows(frames, outputDirectory, drawingName, Config.BatchPlotFileNameMode, Config.BatchPlotDevice, Config.BatchPlotStyle);
            using (var dlg = new BatchPlotDialog(frames.Count, frameBlockKey.DisplayName, preflightRows, outputDirectory, drawingName))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                SortPlotFrames(frames, dlg.SortMode);
                if (dlg.ReverseOrder) frames.Reverse();
                var settings = new BatchPlotSettings();
                settings.DeviceName = dlg.DeviceName;
                settings.PaperName = dlg.PaperName;
                settings.PlotStyle = dlg.PlotStyle;
                settings.AutoRotate = dlg.AutoRotate;
                settings.CenterPlot = dlg.CenterPlot;
                settings.MarginPercent = dlg.MarginPercent;
                settings.MarginMm = dlg.MarginMm;
                settings.FileNameMode = dlg.FileNameMode;
                settings.SortMode = dlg.SortMode;
                settings.ReverseOrder = dlg.ReverseOrder;
                settings.OutputDirectory = outputDirectory;
                settings.DrawingName = drawingName;

                bool outputToFile = IsPdfPlotDevice(settings.DeviceName);
                preflightRows = BuildBatchPlotPreflightRows(frames, settings.OutputDirectory, settings.DrawingName, settings.FileNameMode, settings.DeviceName, settings.PlotStyle);
                if (!ConfirmBatchPlotPreflightIssues(preflightRows)) return;

                int success = 0;
                int failed = 0;
#if GSTARCAD || ZWCAD
                success = RunBatchPlotWithPlotCommand(frames, settings, outputToFile);
                failed = Math.Max(0, frames.Count - success);
#else
                for (int i = 0; i < frames.Count; i++)
                {
                    string path = outputToFile ? BuildBatchPlotOutputPath(settings.OutputDirectory, settings.DrawingName, i + 1, settings.FileNameMode, frames[i]) : null;
                    if (outputToFile ? PlotFrameToPdf(frames[i], settings, path) : PlotFrameToDevice(frames[i], settings)) success++;
                    else failed++;
                }
#endif

                string target = outputToFile ? "\u8F93\u51FA\u76EE\u5F55\uFF1A" + settings.OutputDirectory : "\u5DF2\u53D1\u9001\u5230\u6253\u5370\u673A\uFF1A" + settings.DeviceName;
                Ed.WriteMessage(string.Format("\n批量打印完成：成功 {0} 张，失败 {1} 张。{2}", success, failed, target));
            }
        }

    }
}
