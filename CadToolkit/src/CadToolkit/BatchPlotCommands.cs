using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CadToolkit.Core;
using CadToolkit.UI;

#if AUTOCAD
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
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

        const long MinimumValidPdfBytes = 1024;

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
                int success = 0;
                int failed = 0;
#if GSTARCAD
                success = RunGstarBatchPlotWithPlotCommand(frames, settings, outputToFile);
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

        static List<BatchPlotPreflightRow> BuildBatchPlotPreflightRows(List<BatchPlotFrame> frames, string outputDirectory, string drawingName, string fileNameMode, string deviceName, string plotStyle)
        {
            var rows = new List<BatchPlotPreflightRow>();
            bool outputToFile = IsPdfPlotDevice(deviceName);
            BatchPlotFrame reference = frames.Count > 0 ? frames[0] : null;
            for (int i = 0; i < frames.Count; i++)
            {
                BatchPlotFrame frame = frames[i];
                string target = "\u53D1\u9001\u5230\u6253\u5370\u673A";
                if (outputToFile)
                    target = Path.GetFileName(BuildBatchPlotOutputPath(outputDirectory, drawingName, i + 1, fileNameMode, frame));

                rows.Add(new BatchPlotPreflightRow
                {
                    Index = (i + 1).ToString("D3", CultureInfo.InvariantCulture),
                    SheetNumber = SafeStr(frame.SheetNumber),
                    SheetName = SafeStr(frame.SheetName),
                    Size = FormatBatchPlotPreflightSize(frame),
                    Orientation = frame.Width >= frame.Height ? "\u6A2A\u5411" : "\u7EB5\u5411",
                    Target = target,
                    SizeMismatched = IsBatchPlotFrameSizeMismatched(reference, frame),
                    Status = BuildBatchPlotPreflightStatus(reference, frame, outputDirectory, deviceName, plotStyle),
                    PositionOrder = i,
                    SelectionOrder = frame.SelectionOrder
                });
            }
            MarkDuplicateBatchPlotTargets(rows, outputToFile);
            return rows;
        }

        static string BuildBatchPlotPreflightStatus(BatchPlotFrame reference, BatchPlotFrame frame, string outputDirectory, string deviceName, string plotStyle)
        {
            var parts = new List<string>();
            if (IsBatchPlotFrameSizeMismatched(reference, frame)) parts.Add("\u5C3A\u5BF8\u5F02\u5E38");
            if (string.IsNullOrEmpty(SafeStr(deviceName).Trim())) parts.Add("\u8BBE\u5907\u7F3A\u5931");
            if (string.IsNullOrEmpty(SafeStr(plotStyle).Trim())) parts.Add("\u6837\u5F0F\u7F3A\u5931");
            if (IsPdfPlotDevice(deviceName) && (string.IsNullOrEmpty(SafeStr(outputDirectory).Trim()) || IsDriveOnlyPath(outputDirectory))) parts.Add("\u76EE\u5F55\u5F02\u5E38");
            return parts.Count == 0 ? "\u6B63\u5E38" : string.Join("\uFF1B", parts.ToArray());
        }

        static void MarkDuplicateBatchPlotTargets(List<BatchPlotPreflightRow> rows, bool outputToFile)
        {
            if (!outputToFile || rows == null) return;
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (BatchPlotPreflightRow row in rows)
            {
                string key = SafeStr(row.Target).Trim();
                if (string.IsNullOrEmpty(key)) continue;
                counts[key] = counts.ContainsKey(key) ? counts[key] + 1 : 1;
            }
            foreach (BatchPlotPreflightRow row in rows)
            {
                string key = SafeStr(row.Target).Trim();
                if (!string.IsNullOrEmpty(key) && counts.ContainsKey(key) && counts[key] > 1)
                    row.Status = AppendBatchPlotStatus(row.Status, "\u6587\u4EF6\u540D\u91CD\u590D");
            }
        }

        static string AppendBatchPlotStatus(string status, string addition)
        {
            if (string.IsNullOrEmpty(status) || status == "\u6B63\u5E38") return addition;
            if (status.IndexOf(addition, StringComparison.OrdinalIgnoreCase) >= 0) return status;
            return status + "\uFF1B" + addition;
        }

        static bool IsBatchPlotFrameSizeMismatched(BatchPlotFrame reference, BatchPlotFrame frame)
        {
            if (reference == null || frame == null) return false;
            return IsBatchPlotDimensionMismatched(reference.Width, frame.Width)
                || IsBatchPlotDimensionMismatched(reference.Height, frame.Height);
        }

        static bool IsBatchPlotDimensionMismatched(double reference, double value)
        {
            if (reference <= 0) return false;
            return Math.Abs(value - reference) / reference > 0.03;
        }

        static string FormatBatchPlotPreflightSize(BatchPlotFrame frame)
        {
            double width = Math.Round(frame.Width, 0);
            double height = Math.Round(frame.Height, 0);
            return width.ToString("0", CultureInfo.InvariantCulture) + " x " + height.ToString("0", CultureInfo.InvariantCulture);
        }

        static ObjectId[] CollectBatchPlotFrameBlockIds(BatchPlotFrameBlockKey frameBlockKey)
        {
            var pso = new PromptSelectionOptions();
            pso.MessageForAdding = "\n框选要打印的范围：";
            var filter = new TypedValue[] { new TypedValue(0, "INSERT") };
            var sf = new SelectionFilter(filter);
            var psr = Ed.GetSelection(pso, sf);
            if (psr.Status == PromptStatus.Cancel) return null;
            if (psr.Status != PromptStatus.OK || psr.Value == null) return new ObjectId[0];

            var ids = new List<ObjectId>();
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    try
                    {
                        if (IsBatchPlotSameFrameBlock(id, frameBlockKey, tr))
                            ids.Add(id);
                    }
                    catch (System.Exception ex)
                    {
                        Log("BatchPlot skipped block candidate: " + ex.Message);
                    }
                }
                tr.Commit();
            }
            return ids.ToArray();
        }

        static bool IsBatchPlotSameFrameBlock(ObjectId blockReferenceId, BatchPlotFrameBlockKey frameBlockKey, Transaction tr)
        {
            if (frameBlockKey == null) return false;
            BatchPlotFrameBlockKey candidate = GetBatchPlotFrameBlockKey(blockReferenceId, tr);
            if (candidate == null) return false;
            if (!candidate.DefinitionId.IsNull && !frameBlockKey.DefinitionId.IsNull)
                return candidate.DefinitionId == frameBlockKey.DefinitionId;
            return SafeStr(candidate.Name).Equals(SafeStr(frameBlockKey.Name), StringComparison.OrdinalIgnoreCase);
        }

        static BatchPlotFrameBlockKey GetBatchPlotFrameBlockKey(ObjectId blockReferenceId, Transaction tr)
        {
            var br = tr.GetObject(blockReferenceId, OpenMode.ForRead) as BlockReference;
            if (br == null) return null;

            ObjectId definitionId = ObjectId.Null;
            try
            {
                ObjectId dynamicId = br.DynamicBlockTableRecord;
                if (!dynamicId.IsNull) definitionId = dynamicId;
            }
            catch { }
            if (definitionId.IsNull) definitionId = br.BlockTableRecord;
            if (definitionId.IsNull) return null;

            var btr = tr.GetObject(definitionId, OpenMode.ForRead) as BlockTableRecord;
            string name = btr == null ? "" : btr.Name;
            if (string.IsNullOrEmpty(name)) name = Convert.ToString(definitionId);

            return new BatchPlotFrameBlockKey
            {
                DefinitionId = definitionId,
                Name = name,
                DisplayName = name
            };
        }

        static List<BatchPlotFrame> CollectPlotFrames(ObjectId[] selectedIds)
        {
            var frames = new List<BatchPlotFrame>();
            int selectionOrder = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
                {
                    try
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;
                        Extents3d ext = ent.GeometricExtents;
                        if (ext.MaxPoint.X <= ext.MinPoint.X || ext.MaxPoint.Y <= ext.MinPoint.Y) continue;

                        var frame = new BatchPlotFrame();
                        frame.Id = id;
                        frame.MinX = ext.MinPoint.X;
                        frame.MinY = ext.MinPoint.Y;
                        frame.MaxX = ext.MaxPoint.X;
                        frame.MaxY = ext.MaxPoint.Y;
                        frame.SelectionOrder = selectionOrder++;
                        ReadBatchPlotTitleBlockAttributes(ent as BlockReference, frame, tr);
                        frames.Add(frame);
                    }
                    catch (System.Exception ex)
                    {
                        Log("BatchPlot skipped frame: " + ex.Message);
                    }
                }
                tr.Commit();
            }
            return frames;
        }

        static void ReadBatchPlotTitleBlockAttributes(BlockReference br, BatchPlotFrame frame, Transaction tr)
        {
            if (br == null || frame == null || tr == null) return;
            try
            {
                foreach (ObjectId attrId in br.AttributeCollection)
                {
                    var ar = tr.GetObject(attrId, OpenMode.ForRead) as AttributeReference;
                    if (ar == null) continue;
                    string tag = SafeStr(ar.Tag).Trim();
                    string value = SafeStr(ar.TextString).Trim();
                    if (string.IsNullOrEmpty(value)) continue;
                    if (string.IsNullOrEmpty(frame.SheetNumber) && IsBatchPlotSheetNumberTag(tag))
                        frame.SheetNumber = value;
                    else if (string.IsNullOrEmpty(frame.SheetName) && IsBatchPlotSheetNameTag(tag))
                        frame.SheetName = value;
                }
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot read title block attributes failed: " + ex.Message);
            }
        }

        static bool IsBatchPlotSheetNumberTag(string tag)
        {
            string normalized = NormalizeBatchPlotAttributeTag(tag);
            return normalized == "图号"
                || normalized == "图纸编号"
                || normalized == "图纸号"
                || normalized == "SHEETNO"
                || normalized == "SHEETNUMBER"
                || normalized == "DRAWINGNO"
                || normalized == "DRAWINGNUMBER"
                || normalized == "DWGNO";
        }

        static bool IsBatchPlotSheetNameTag(string tag)
        {
            string normalized = NormalizeBatchPlotAttributeTag(tag);
            return normalized == "图名"
                || normalized == "图纸名称"
                || normalized == "图纸名"
                || normalized == "SHEETNAME"
                || normalized == "SHEETTITLE"
                || normalized == "DRAWINGNAME"
                || normalized == "DRAWINGTITLE"
                || normalized == "TITLE";
        }

        static string NormalizeBatchPlotAttributeTag(string tag)
        {
            return SafeStr(tag).Replace(" ", "").Replace("_", "").Replace("-", "").Replace("：", "").Replace(":", "").ToUpperInvariant();
        }

        static void SortPlotFrames(List<BatchPlotFrame> frames)
        {
            SortPlotFrames(frames, "Position");
        }

        static void SortPlotFrames(List<BatchPlotFrame> frames, string sortMode)
        {
            if (SafeStr(sortMode).Equals("SelectionOrder", StringComparison.OrdinalIgnoreCase))
            {
                SortPlotFramesBySelectionOrder(frames);
                return;
            }
            if (SafeStr(sortMode).Equals("SheetNumber", StringComparison.OrdinalIgnoreCase))
            {
                SortPlotFramesBySheetNumber(frames);
                return;
            }
            SortPlotFramesByPosition(frames);
        }

        static void SortPlotFramesByPosition(List<BatchPlotFrame> frames)
        {
            frames.Sort(delegate(BatchPlotFrame a, BatchPlotFrame b)
            {
                int byLeft = a.MinX.CompareTo(b.MinX);
                if (byLeft != 0) return byLeft;
                return b.MaxY.CompareTo(a.MaxY);
            });
        }

        static void SortPlotFramesBySheetNumber(List<BatchPlotFrame> frames)
        {
            frames.Sort(delegate(BatchPlotFrame a, BatchPlotFrame b)
            {
                int bySheetNumber = string.Compare(SafeStr(a.SheetNumber), SafeStr(b.SheetNumber), StringComparison.OrdinalIgnoreCase);
                if (bySheetNumber != 0) return bySheetNumber;
                int bySheetName = string.Compare(SafeStr(a.SheetName), SafeStr(b.SheetName), StringComparison.OrdinalIgnoreCase);
                if (bySheetName != 0) return bySheetName;
                int byTop = b.MaxY.CompareTo(a.MaxY);
                if (byTop != 0) return byTop;
                return a.MinX.CompareTo(b.MinX);
            });
        }

        static void SortPlotFramesBySelectionOrder(List<BatchPlotFrame> frames)
        {
            frames.Sort(delegate(BatchPlotFrame a, BatchPlotFrame b)
            {
                int left = a == null ? int.MaxValue : a.SelectionOrder;
                int right = b == null ? int.MaxValue : b.SelectionOrder;
                return left.CompareTo(right);
            });
        }

        static BatchPlotFrame ExpandBatchPlotFrame(BatchPlotFrame frame, double marginPercent)
        {
            double margin = Math.Max(0, marginPercent) / 100.0;
            if (margin <= 0 || frame == null) return frame;

            double dx = frame.Width * margin;
            double dy = frame.Height * margin;
            var expanded = new BatchPlotFrame();
            expanded.Id = frame.Id;
            expanded.MinX = frame.MinX - dx;
            expanded.MinY = frame.MinY - dy;
            expanded.MaxX = frame.MaxX + dx;
            expanded.MaxY = frame.MaxY + dy;
            return expanded;
        }

        static BatchPlotFrame ExpandBatchPlotFrameByMarginMm(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            if (frame == null || settings == null) return frame;
            double marginMm = Math.Max(0, settings.MarginMm);
            if (marginMm <= 0) return frame;

            double paperWidthMm;
            double paperHeightMm;
            if (!TryGetBatchPlotPaperSizeMm(settings.PaperName, out paperWidthMm, out paperHeightMm))
                return ExpandBatchPlotFrame(frame, settings.MarginPercent);

            bool landscape = IsBatchPlotLandscape(frame, settings);
            double usableWidth = landscape ? Math.Max(paperWidthMm, paperHeightMm) : Math.Min(paperWidthMm, paperHeightMm);
            double usableHeight = landscape ? Math.Min(paperWidthMm, paperHeightMm) : Math.Max(paperWidthMm, paperHeightMm);
            double contentWidth = usableWidth - (2 * marginMm);
            double contentHeight = usableHeight - (2 * marginMm);
            if (contentWidth <= 0 || contentHeight <= 0 || frame.Width <= 0 || frame.Height <= 0)
                return frame;

            double contentScale = Math.Min(contentWidth / frame.Width, contentHeight / frame.Height);
            if (contentScale <= 0) return frame;
            double targetWidth = usableWidth / contentScale;
            double targetHeight = usableHeight / contentScale;
            double dx = Math.Max(0, (targetWidth - frame.Width) / 2.0);
            double dy = Math.Max(0, (targetHeight - frame.Height) / 2.0);
            var expanded = new BatchPlotFrame();
            expanded.Id = frame.Id;
            expanded.MinX = frame.MinX - dx;
            expanded.MinY = frame.MinY - dy;
            expanded.MaxX = frame.MaxX + dx;
            expanded.MaxY = frame.MaxY + dy;
            return expanded;
        }

        static bool TryGetBatchPlotPaperSizeMm(string paperName, out double widthMm, out double heightMm)
        {
            string normalized = NormalizeMediaName(paperName);
            if (normalized.IndexOf("A0", StringComparison.OrdinalIgnoreCase) >= 0) { widthMm = 841; heightMm = 1189; return true; }
            if (normalized.IndexOf("A1", StringComparison.OrdinalIgnoreCase) >= 0) { widthMm = 594; heightMm = 841; return true; }
            if (normalized.IndexOf("A2", StringComparison.OrdinalIgnoreCase) >= 0) { widthMm = 420; heightMm = 594; return true; }
            if (normalized.IndexOf("A3", StringComparison.OrdinalIgnoreCase) >= 0) { widthMm = 297; heightMm = 420; return true; }
            if (normalized.IndexOf("A4", StringComparison.OrdinalIgnoreCase) >= 0) { widthMm = 210; heightMm = 297; return true; }
            widthMm = 0;
            heightMm = 0;
            return false;
        }

        static bool IsBatchPlotLandscape(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            return settings != null && settings.AutoRotate && frame != null && frame.Width > frame.Height;
        }

        static string BuildBatchPlotOutputPath(string outputDirectory, string drawingName, int index, string fileNameMode, BatchPlotFrame frame)
        {
            string name = string.IsNullOrEmpty(drawingName) ? "Drawing" : drawingName;
            string serial = index.ToString("D3");
            string mode = string.IsNullOrEmpty(fileNameMode) ? "DrawingDashIndex" : fileNameMode;
            string stem;
            if (mode.Equals("IndexOnly", StringComparison.OrdinalIgnoreCase)) stem = serial;
            else if (mode.Equals("DrawingUnderscoreIndex", StringComparison.OrdinalIgnoreCase)) stem = name + "_" + serial;
            else if (mode.Equals("SheetNumberName", StringComparison.OrdinalIgnoreCase)) stem = BuildBatchPlotSheetNumberNameStem(frame, serial);
            else stem = name + "-" + serial;
            stem = SanitizeBatchPlotFileNameStem(stem);
            string file = stem + ".pdf";
            return Path.Combine(outputDirectory, file);
        }

        static string BuildBatchPlotSheetNumberNameStem(BatchPlotFrame frame, string serial)
        {
            string sheetNumber = frame == null ? "" : SafeStr(frame.SheetNumber).Trim();
            string sheetName = frame == null ? "" : SafeStr(frame.SheetName).Trim();
            string stem = (sheetNumber + " " + sheetName).Trim();
            return string.IsNullOrEmpty(stem) ? serial : stem;
        }

        static string SanitizeBatchPlotFileNameStem(string stem)
        {
            string text = SafeStr(stem).Trim();
            foreach (char ch in Path.GetInvalidFileNameChars())
                text = text.Replace(ch.ToString(), "");
            while (text.IndexOf("  ", StringComparison.Ordinal) >= 0)
                text = text.Replace("  ", " ");
            return string.IsNullOrEmpty(text) ? "Drawing" : text;
        }

        static string GetBatchPlotOutputDirectory()
        {
            try
            {
                string file = Db.Filename;
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !IsDriveOnlyPath(dir)) return dir;
                Log("BatchPlot ignored drawing directory: file=" + SafeStr(file) + "; dir=" + SafeStr(dir));
            }
            catch (System.Exception ex) { Log("BatchPlot read drawing directory failed: " + ex.Message); }

            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        static bool IsDriveOnlyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string trimmed = path.Trim();
            return trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':';
        }

        static string GetBatchPlotDrawingName()
        {
            try
            {
                string file = Db.Filename;
                string name = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch (System.Exception ex) { Log("BatchPlot read drawing name failed: " + ex.Message); }

            return "Drawing";
        }

        static bool PlotFrameToPdf(BatchPlotFrame frame, BatchPlotSettings settings, string outputPath)
        {
#if GSTARCAD
            return PlotFrameToPdfWithPlotCommand(frame, settings, outputPath);
#else
            try
            {
                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(outputPath)) File.Delete(outputPath);

                var api = BatchPlotApi.Create();
                api.PlotFrame(frame, settings, outputPath);
                if (!IsValidPdfFile(outputPath))
                {
                    Log("BatchPlot output is not a valid PDF: " + outputPath + ": " + DescribeBatchPlotSettings(settings));
                    return false;
                }
                return true;
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot plot failed for " + outputPath + ": " + DescribeBatchPlotSettings(settings) + " " + ex);
                return false;
            }
#endif
        }

        static bool PlotFrameToDevice(BatchPlotFrame frame, BatchPlotSettings settings)
        {
#if GSTARCAD
            return PlotFrameToDeviceWithPlotCommand(frame, settings);
#else
            try
            {
                var api = BatchPlotApi.Create();
                api.PlotFrame(frame, settings, null);
                return true;
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot plot failed for printer: " + DescribeBatchPlotSettings(settings) + " " + ex);
                return false;
            }
#endif
        }

#if GSTARCAD
        static int RunGstarBatchPlotWithPlotCommand(List<BatchPlotFrame> frames, BatchPlotSettings settings, bool outputToFile)
        {
            if (frames == null || settings == null) return 0;
            try
            {
                BatchPlotSettings resolvedSettings = ResolveGstarPlotCommandSettings(settings);
                for (int i = 0; i < frames.Count; i++)
                {
                    string outputPath = null;
                    if (outputToFile)
                    {
                        outputPath = BuildBatchPlotOutputPath(settings.OutputDirectory, settings.DrawingName, i + 1, settings.FileNameMode, frames[i]);
                        string dir = Path.GetDirectoryName(outputPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        if (File.Exists(outputPath)) File.Delete(outputPath);
                    }

                    SendGstarPlotCommand(BuildGstarPlotCommand(frames[i], resolvedSettings, outputPath));
                }

                DebugBatchPlotLog("BatchPlot -PLOT batch commands submitted: Count=" + frames.Count.ToString(CultureInfo.InvariantCulture) + "; " + DescribeBatchPlotSettings(resolvedSettings));
                return frames.Count;
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot -PLOT batch command failed: " + DescribeBatchPlotSettings(settings) + " " + ex);
                return 0;
            }
        }

        static bool PlotFrameToPdfWithPlotCommand(BatchPlotFrame frame, BatchPlotSettings settings, string outputPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(outputPath)) File.Delete(outputPath);

                BatchPlotSettings resolvedSettings = ResolveGstarPlotCommandSettings(settings);
                string commandText = BuildGstarPlotCommand(frame, resolvedSettings, outputPath);
                SendGstarPlotCommand(commandText);
                DebugBatchPlotLog("BatchPlot -PLOT command submitted for " + outputPath + ": " + DescribeBatchPlotSettings(resolvedSettings));
                return true;
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot -PLOT command failed for " + outputPath + ": " + DescribeBatchPlotSettings(settings) + " " + ex);
                return false;
            }
        }

        static bool PlotFrameToDeviceWithPlotCommand(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            try
            {
                BatchPlotSettings resolvedSettings = ResolveGstarPlotCommandSettings(settings);
                string commandText = BuildGstarPlotCommand(frame, resolvedSettings, null);
                SendGstarPlotCommand(commandText);
                DebugBatchPlotLog("BatchPlot -PLOT command submitted for printer: " + DescribeBatchPlotSettings(resolvedSettings));
                return true;
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot -PLOT command failed for printer: " + DescribeBatchPlotSettings(settings) + " " + ex);
                return false;
            }
        }

        static string BuildGstarPlotCommand(BatchPlotFrame frame, BatchPlotSettings settings, string outputPath)
        {
            BatchPlotFrame plotFrame = ExpandBatchPlotFrameByMarginMm(frame, settings);
            var inputs = new List<string>();
            inputs.Add(QuoteGstarLispString("_.-PLOT"));
            inputs.Add(QuoteGstarLispString("Y"));
            inputs.Add(QuoteGstarLispString("Model"));
            inputs.Add(QuoteGstarLispString(settings.DeviceName));
            inputs.Add(QuoteGstarLispString(settings.PaperName));
            inputs.Add(QuoteGstarLispString(GetGstarPlotUnitsInput(settings.PaperName)));
            inputs.Add(QuoteGstarLispString(GetGstarPlotOrientationInput(plotFrame, settings)));
            inputs.Add(QuoteGstarLispString("N"));
            inputs.Add(QuoteGstarLispString("W"));
            inputs.Add(QuoteGstarLispString(FormatGstarPlotPoint(plotFrame.MinX, plotFrame.MinY)));
            inputs.Add(QuoteGstarLispString(FormatGstarPlotPoint(plotFrame.MaxX, plotFrame.MaxY)));
            inputs.Add(QuoteGstarLispString(BuildGstarPlotScaleInput(frame, settings)));
            inputs.Add(QuoteGstarLispString(settings.CenterPlot ? "C" : "0,0"));
            inputs.Add(QuoteGstarLispString("Y"));
            inputs.Add(QuoteGstarLispString(settings.PlotStyle));
            inputs.Add(QuoteGstarLispString("Y"));
            inputs.Add(QuoteGstarLispString(GetGstarPlotShadeInput()));
            if (!string.IsNullOrEmpty(outputPath))
            {
                inputs.Add(QuoteGstarLispString(outputPath));
                inputs.Add(QuoteGstarLispString("N"));
                inputs.Add(QuoteGstarLispString("Y"));
            }
            else
            {
                inputs.Add(QuoteGstarLispString("N"));
                inputs.Add(QuoteGstarLispString("N"));
                inputs.Add(QuoteGstarLispString("Y"));
            }
            return "(ct-plot (list " + string.Join(" ", inputs.ToArray()) + "))\n";
        }

        static BatchPlotSettings ResolveGstarPlotCommandSettings(BatchPlotSettings settings)
        {
            var resolved = new BatchPlotSettings();
            resolved.DeviceName = settings.DeviceName;
            resolved.PaperName = settings.PaperName;
            resolved.PlotStyle = settings.PlotStyle;
            resolved.AutoRotate = settings.AutoRotate;
            resolved.CenterPlot = settings.CenterPlot;
            resolved.MarginPercent = settings.MarginPercent;
            resolved.MarginMm = settings.MarginMm;
            resolved.FileNameMode = settings.FileNameMode;
            resolved.OutputDirectory = settings.OutputDirectory;
            resolved.DrawingName = settings.DrawingName;

            resolved.DeviceName = ResolveGstarPlotCommandDeviceName(resolved.DeviceName);
            resolved.PaperName = ResolveGstarPlotCommandPaperName(resolved.DeviceName, resolved.PaperName);
            return resolved;
        }

        static string ResolveGstarPlotCommandDeviceName(string deviceName)
        {
            string fallback = string.IsNullOrEmpty(deviceName) ? "DWG To PDF.pc3" : deviceName;
            try
            {
                Type validatorType = FindCadType(GetDatabaseNamespace() + ".PlotSettingsValidator");
                if (validatorType != null)
                {
                    object validator = GetStaticProperty(validatorType, "Current");
                    object list = InvokeOptionalArgumentList(validator, "GetPlotDeviceList");
                    var enumerable = list as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        string matched = MatchGstarPlotDeviceName(enumerable, fallback);
                        if (!string.IsNullOrEmpty(matched) && !matched.Equals(fallback, StringComparison.OrdinalIgnoreCase))
                            Log("BatchPlot Gstar device resolved: " + fallback + " -> " + matched);
                        return matched;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot Gstar device lookup failed: " + ex.Message);
            }

            return fallback;
        }

        static string MatchGstarPlotDeviceName(System.Collections.IEnumerable devices, string fallback)
        {
            string normalizedNeedle = NormalizeDeviceName(fallback);
            string normalizedBaseNeedle = NormalizeDeviceName(Path.GetFileNameWithoutExtension(fallback));
            string firstContains = null;
            var available = new List<string>();
            foreach (object item in devices)
            {
                string candidate = Convert.ToString(item);
                if (string.IsNullOrEmpty(candidate)) continue;
                available.Add(candidate);
                if (candidate.Equals(fallback, StringComparison.OrdinalIgnoreCase)) return candidate;

                string normalized = NormalizeDeviceName(candidate);
                if (normalized.Equals(normalizedNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                if (!string.IsNullOrEmpty(normalizedBaseNeedle) && normalized.Equals(normalizedBaseNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                if (firstContains == null && normalized.IndexOf(normalizedNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    firstContains = candidate;
                if (firstContains == null && !string.IsNullOrEmpty(normalizedBaseNeedle) && normalized.IndexOf(normalizedBaseNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    firstContains = candidate;
            }

            Log("BatchPlot Gstar device not found: " + fallback + "; AvailableDevices=" + string.Join("|", available.ToArray()));
            return fallback;
        }

        static string ResolveGstarPlotCommandPaperName(string deviceName, string paperName)
        {
            string fallback = string.IsNullOrEmpty(paperName) ? "A3" : paperName;
            bool expandPaperName = ShouldUseGstarExpandedPaperName(deviceName);
            string commandFallback = expandPaperName ? GetGstarPlotCommandFallbackMediaName(fallback) : fallback;
            try
            {
                Type plotSettingsType = FindCadType(GetDatabaseNamespace() + ".PlotSettings");
                Type validatorType = FindCadType(GetDatabaseNamespace() + ".PlotSettingsValidator");
                if (plotSettingsType == null || validatorType == null) return commandFallback;

                object validator = GetStaticProperty(validatorType, "Current");
                object plotSettings = Activator.CreateInstance(plotSettingsType, new object[] { true });
                TrySetGstarPlotConfigurationWithMediaCandidate(validator, plotSettings, deviceName, fallback);
                Invoke(validator, "RefreshLists", plotSettings);
                object list = Invoke(validator, "GetCanonicalMediaNameList", plotSettings);
                var enumerable = list as System.Collections.IEnumerable;
                if (enumerable == null) return commandFallback;

                string matched = MatchGstarPlotMediaName(enumerable, fallback);
                if (!string.IsNullOrEmpty(matched) && !matched.Equals(fallback, StringComparison.OrdinalIgnoreCase))
                    Log("BatchPlot Gstar media resolved: " + fallback + " -> " + matched);
                return ToGstarPlotCommandMediaInput(matched, fallback, commandFallback, expandPaperName);
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot Gstar media lookup failed: " + ex.Message);
                return commandFallback;
            }
        }

        static bool TrySetGstarPlotConfigurationWithMediaCandidate(object validator, object plotSettings, string deviceName, string paperName)
        {
            string lastError = null;
            foreach (string candidate in GetGstarPlotMediaCandidates(paperName))
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                try
                {
                    Invoke(validator, "SetPlotConfigurationName", plotSettings, deviceName, candidate);
                    return true;
                }
                catch (System.Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            Log("BatchPlot Gstar media bind failed: " + SafeStr(paperName) + "; " + SafeStr(lastError));
            return false;
        }

        static bool ShouldUseGstarExpandedPaperName(string deviceName)
        {
            return SafeStr(deviceName).IndexOf("DWG To PDF", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static IEnumerable<string> GetGstarPlotMediaCandidates(string paperName)
        {
            string fallback = string.IsNullOrEmpty(paperName) ? "A3" : paperName.Trim();
            string commandFallback = GetGstarPlotCommandFallbackMediaName(fallback);
            yield return commandFallback;
            if (!commandFallback.Equals(fallback, StringComparison.OrdinalIgnoreCase))
                yield return fallback;

            string normalized = NormalizeMediaName(fallback);
            if (normalized == "A4")
            {
                yield return "ISO full bleed A4 (297.00 x 210.00 毫米)";
                yield return "ISO A4 (297.00 x 210.00 毫米)";
                yield return "ISO_A4_(297.00_x_210.00_MM)";
                yield return "ISO_full_bleed_A4_(297.00_x_210.00_MM)";
            }
            else if (normalized == "A3")
            {
                yield return "ISO full bleed A3 (420.00 x 297.00 毫米)";
                yield return "ISO A3 (420.00 x 297.00 毫米)";
                yield return "ISO_A3_(420.00_x_297.00_MM)";
                yield return "ISO_full_bleed_A3_(420.00_x_297.00_MM)";
            }
            else if (normalized == "A2")
            {
                yield return "ISO full bleed A2 (594.00 x 420.00 毫米)";
                yield return "ISO A2 (594.00 x 420.00 毫米)";
                yield return "ISO_A2_(594.00_x_420.00_MM)";
                yield return "ISO_full_bleed_A2_(594.00_x_420.00_MM)";
            }
            else if (normalized == "A1")
            {
                yield return "ISO full bleed A1 (841.00 x 594.00 毫米)";
                yield return "ISO A1 (841.00 x 594.00 毫米)";
                yield return "ISO_A1_(841.00_x_594.00_MM)";
                yield return "ISO_full_bleed_A1_(841.00_x_594.00_MM)";
            }
            else if (normalized == "A0")
            {
                yield return "ISO full bleed A0 (1189.00 x 841.00 毫米)";
                yield return "ISO A0 (1189.00 x 841.00 毫米)";
                yield return "ISO_A0_(1189.00_x_841.00_MM)";
                yield return "ISO_full_bleed_A0_(1189.00_x_841.00_MM)";
            }
        }

        static string GetGstarPlotCommandFallbackMediaName(string paperName)
        {
            string fallback = string.IsNullOrEmpty(paperName) ? "A3" : paperName.Trim();
            string normalized = NormalizeMediaName(fallback);
            if (normalized == "A4") return "ISO full bleed A4 (297.00 x 210.00 毫米)";
            if (normalized == "A3") return "ISO full bleed A3 (420.00 x 297.00 毫米)";
            if (normalized == "A2") return "ISO full bleed A2 (594.00 x 420.00 毫米)";
            if (normalized == "A1") return "ISO full bleed A1 (841.00 x 594.00 毫米)";
            if (normalized == "A0") return "ISO full bleed A0 (1189.00 x 841.00 毫米)";
            return fallback;
        }

        static string ToGstarPlotCommandMediaInput(string matchedMediaName, string configuredPaperName, string commandFallback, bool expandPaperName)
        {
            string matched = string.IsNullOrEmpty(matchedMediaName) ? configuredPaperName : matchedMediaName.Trim();
            if (!expandPaperName) return matched;
            string normalizedMatched = NormalizeMediaName(matched);
            string normalizedConfigured = NormalizeMediaName(configuredPaperName);
            if (normalizedMatched == normalizedConfigured && IsIsoSeriesShortPaperName(normalizedMatched))
                return commandFallback;
            return matched;
        }

        static bool IsIsoSeriesShortPaperName(string normalizedPaperName)
        {
            return normalizedPaperName == "A0"
                || normalizedPaperName == "A1"
                || normalizedPaperName == "A2"
                || normalizedPaperName == "A3"
                || normalizedPaperName == "A4";
        }

        static string MatchGstarPlotMediaName(System.Collections.IEnumerable mediaNames, string fallback)
        {
            string normalizedNeedle = NormalizeMediaName(fallback);
            string looseNeedle = SafeStr(fallback).Replace(" ", "").Replace("_", "").ToUpperInvariant();
            string firstContains = null;
            var available = new List<string>();
            foreach (object item in mediaNames)
            {
                string candidate = Convert.ToString(item);
                if (string.IsNullOrEmpty(candidate)) continue;
                available.Add(candidate);
                if (candidate.Equals(fallback, StringComparison.OrdinalIgnoreCase)) return candidate;

                string normalized = NormalizeMediaName(candidate);
                if (normalized.Equals(normalizedNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                if (firstContains == null && normalized.IndexOf(looseNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    firstContains = candidate;
            }

            if (firstContains != null) return firstContains;
            Log("BatchPlot Gstar media not found: " + fallback + "; AvailableMedia=" + string.Join("|", available.ToArray()));
            return fallback;
        }

        static void SendGstarPlotCommand(string commandText)
        {
            DebugBatchPlotLog("BatchPlot -PLOT command text: " + commandText.Replace("\r", "\\r").Replace("\n", "\\n"));
            string quietCommandText = "(progn"
                + " (defun ct-has-func (_ctName) (member _ctName (atoms-family 1)))"
                + " (defun ct-plot (_ctArgs) (cond ((ct-has-func \"VL-CMDF\") (apply 'vl-cmdf _ctArgs)) ((ct-has-func \"COMMAND-S\") (apply 'command-s _ctArgs)) (T (apply 'command _ctArgs))))"
                + " (setq _ctOldBgPlot (getvar \"BACKGROUNDPLOT\")) (setvar \"CMDECHO\" 0) (setvar \"BACKGROUNDPLOT\" 0) "
                + commandText.Trim()
                + " (setvar \"BACKGROUNDPLOT\" _ctOldBgPlot) (setvar \"CMDECHO\" 1) (princ))\n";
            CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute(quietCommandText, true, false, false);
        }

        [System.Diagnostics.Conditional("BATCHPLOT_DEBUG")]
        static void DebugBatchPlotLog(string message)
        {
            Log(message);
        }

        static string FormatGstarPlotPoint(double x, double y)
        {
            return x.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture)
                + ","
                + y.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string QuoteGstarLispString(string value)
        {
            string text = SafeStr(value).Replace("\r", " ").Replace("\n", " ").Trim();
            text = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "\"" + text + "\"";
        }

        static string GetGstarPlotUnitsInput(string paperName)
        {
            return SafeStr(paperName).IndexOf("inch", StringComparison.OrdinalIgnoreCase) >= 0 ? "I" : "M";
        }

        static string GetGstarPlotOrientationInput(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            if (IsBatchPlotLandscape(frame, settings)) return "L";
            return "P";
        }

        static string GetGstarPlotShadeInput()
        {
            return "W";
        }

        static string BuildGstarPlotScaleInput(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            if (frame == null || settings == null) return "F";
            double marginMm = Math.Max(0, settings.MarginMm);
            if (marginMm <= 0 || frame.Width <= 0 || frame.Height <= 0) return "F";

            double paperWidthMm;
            double paperHeightMm;
            if (!TryGetBatchPlotPaperSizeMm(settings.PaperName, out paperWidthMm, out paperHeightMm)) return "F";

            bool landscape = IsBatchPlotLandscape(frame, settings);
            double usableWidth = landscape ? Math.Max(paperWidthMm, paperHeightMm) : Math.Min(paperWidthMm, paperHeightMm);
            double usableHeight = landscape ? Math.Min(paperWidthMm, paperHeightMm) : Math.Max(paperWidthMm, paperHeightMm);
            double contentWidth = usableWidth - (2 * marginMm);
            double contentHeight = usableHeight - (2 * marginMm);
            if (contentWidth <= 0 || contentHeight <= 0) return "F";

            double scale = Math.Min(contentWidth / frame.Width, contentHeight / frame.Height);
            if (scale <= 0) return "F";
            return scale.ToString("0.########", CultureInfo.InvariantCulture);
        }
#endif

        static bool IsPdfPlotDevice(string deviceName)
        {
            string rawName = SafeStr(deviceName).Trim();
            string name = rawName.ToUpperInvariant();
            if (name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (name.IndexOf("ADOBE PDF", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("PDF24", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("MICROSOFT PRINT TO PDF", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return rawName.IndexOf("DWG To PDF", StringComparison.OrdinalIgnoreCase) >= 0
                || name.EndsWith(".PC3", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsValidPdfFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                FileInfo info = new FileInfo(path);
                if (info.Length < MinimumValidPdfBytes)
                {
                    Log("BatchPlot PDF is too small and may be blank: " + path + "; Length=" + info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    return false;
                }
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length < 4) return false;
                    byte[] header = new byte[4];
                    int read = stream.Read(header, 0, header.Length);
                    if (read < 4) return false;
                    return System.Text.Encoding.ASCII.GetString(header, 0, header.Length) == "%PDF";
                }
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot PDF validation failed: " + path + ": " + ex.Message);
                return false;
            }
        }

        static string DescribeBatchPlotSettings(BatchPlotSettings settings)
        {
            if (settings == null) return "";
            return "Device=" + SafeStr(settings.DeviceName)
                + "; Paper=" + SafeStr(settings.PaperName)
                + "; Style=" + SafeStr(settings.PlotStyle)
                + "; MarginMm=" + settings.MarginMm.ToString(CultureInfo.InvariantCulture)
                + "; FileNameMode=" + SafeStr(settings.FileNameMode);
        }

        static ObjectId GetCurrentLayoutId()
        {
            try
            {
                Type layoutManagerType = FindCadType(GetDatabaseNamespace() + ".LayoutManager");
                if (layoutManagerType != null)
                {
                    object current = GetStaticProperty(layoutManagerType, "Current");
                    if (current != null)
                    {
                        string currentLayout = Convert.ToString(GetProperty(current, "CurrentLayout"));
                        object id = Invoke(current, "GetLayoutId", currentLayout);
                        if (id is ObjectId) return (ObjectId)id;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot current layout lookup failed: " + ex.Message);
            }
            return Db.CurrentSpaceId;
        }

        static string GetDatabaseNamespace()
        {
#if AUTOCAD
            return "Autodesk.AutoCAD.DatabaseServices";
#elif GSTARCAD
            return "GrxCAD.DatabaseServices";
#elif ZWCAD
            return "ZwSoft.ZwCAD.DatabaseServices";
#else
            return "";
#endif
        }

        static string GetGeometryNamespace()
        {
#if AUTOCAD
            return "Autodesk.AutoCAD.Geometry";
#elif GSTARCAD
            return "GrxCAD.Geometry";
#elif ZWCAD
            return "ZwSoft.ZwCAD.Geometry";
#else
            return "";
#endif
        }

        static string GetPlottingNamespace()
        {
#if AUTOCAD
            return "Autodesk.AutoCAD.PlottingServices";
#elif GSTARCAD
            return "GrxCAD.PlottingServices";
#elif ZWCAD
            return "ZwSoft.ZwCAD.PlottingServices";
#else
            return "";
#endif
        }

        static Type FindCadType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return Type.GetType(fullName, false);
        }

        static object GetStaticProperty(Type type, string name)
        {
            return type.InvokeMember(name, BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Static, null, null, null);
        }

        static object GetProperty(object target, string name)
        {
            return target.GetType().InvokeMember(name, BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance, null, target, null);
        }

        static void SetProperty(object target, string name, object value)
        {
            target.GetType().InvokeMember(name, BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance, null, target, new object[] { value });
        }

        static object Invoke(object target, string name, params object[] args)
        {
            return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, target, args);
        }

        static object InvokeStatic(Type type, string name, params object[] args)
        {
            return type.InvokeMember(name, BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static, null, null, args);
        }

        static object InvokeOptionalArgumentList(object target, string name, params object[] args)
        {
            try { return Invoke(target, name, args); }
            catch (TargetParameterCountException)
            {
                try { return Invoke(target, name); }
                catch (System.Exception ex) { Log("BatchPlot optional invocation failed: " + name + ": " + ex.Message); return null; }
            }
            catch (MissingMethodException)
            {
                try { return Invoke(target, name); }
                catch (System.Exception ex) { Log("BatchPlot optional invocation failed: " + name + ": " + ex.Message); return null; }
            }
        }

        static object ParseEnum(Type enumType, string value)
        {
            return Enum.Parse(enumType, value);
        }

        static string NormalizeDeviceName(string name)
        {
            return SafeStr(name).Replace(" ", "").Replace("_", "").Replace("-", "").Replace(".", "").Replace("(", "").Replace(")", "").ToUpperInvariant();
        }

        static string NormalizeMediaName(string name)
        {
            return SafeStr(name).Replace(" ", "").Replace("_", "").Replace("-", "").Replace("(", "").Replace(")", "").ToUpperInvariant();
        }

        class BatchPlotApi
        {
            Type PlotSettingsType;
            Type PlotSettingsValidatorType;
            Type PlotInfoType;
            Type PlotInfoValidatorType;
            Type PlotFactoryType;
            Type PlotPageInfoType;
            Type Extents2dType;
            Type Point2dType;
            Type PlotTypeEnum;
            Type StdScaleTypeEnum;
            Type PlotRotationEnum;
            Type MatchingPolicyEnum;
            Type ProcessPlotStateEnum;

            internal static BatchPlotApi Create()
            {
                var api = new BatchPlotApi();
                string dbNs = GetDatabaseNamespace();
                string geoNs = GetGeometryNamespace();
                string plotNs = GetPlottingNamespace();

                api.PlotSettingsType = RequiredTypeFromCandidates(dbNs + ".PlotSettings", GetGstarPlotFallback("PlotSettings"));
                api.PlotSettingsValidatorType = RequiredTypeFromCandidates(dbNs + ".PlotSettingsValidator", GetGstarPlotFallback("PlotSettingsValidator"));
                api.PlotInfoType = RequiredTypeFromCandidates(dbNs + ".PlotInfo", GetGstarPlotFallback("PlotInfo"));
                api.PlotInfoValidatorType = RequiredTypeFromCandidates(dbNs + ".PlotInfoValidator", GetGstarPlotFallback("PlotInfoValidator"));
                api.PlotTypeEnum = RequiredType(dbNs + ".PlotType");
                api.StdScaleTypeEnum = RequiredType(dbNs + ".StdScaleType");
                api.PlotRotationEnum = RequiredType(dbNs + ".PlotRotation");
                api.MatchingPolicyEnum = OptionalTypeFromCandidates(dbNs + ".MatchingPolicy", GetGstarPlotFallback("MatchingPolicy"));
                api.Extents2dType = RequiredTypeFromCandidates(geoNs + ".Extents2d", GetGstarPlotFallback("Extents2d"));
                api.Point2dType = RequiredTypeFromCandidates(geoNs + ".Point2d", GetGstarPlotFallback("Point2d"));
                api.PlotFactoryType = RequiredTypeFromCandidates(plotNs + ".PlotFactory", GetGstarPlotFallback("PlotFactory"));
                api.PlotPageInfoType = RequiredTypeFromCandidates(plotNs + ".PlotPageInfo", GetGstarPlotFallback("PlotPageInfo"));
                api.ProcessPlotStateEnum = RequiredType(plotNs + ".ProcessPlotState");
                return api;
            }

            static Type RequiredType(string fullName)
            {
                Type type = FindCadType(fullName);
                if (type == null) throw new InvalidOperationException("CAD plot type not found: " + fullName);
                return type;
            }

            static Type RequiredTypeFromCandidates(string fullName, string[] fallbackNames)
            {
                Type type = FindCadType(fullName);
                if (type != null) return type;

                if (fallbackNames != null)
                {
                    foreach (string fallbackName in fallbackNames)
                    {
                        type = FindCadType(fallbackName);
                        if (type != null) return type;
                    }
                }

                string suffix = fallbackNames == null || fallbackNames.Length == 0 ? "" : "; fallback=" + string.Join(",", fallbackNames);
                throw new InvalidOperationException("CAD plot type not found: " + fullName + suffix);
            }

            static Type OptionalTypeFromCandidates(string fullName, string[] fallbackNames)
            {
                Type type = FindCadType(fullName);
                if (type != null) return type;

                if (fallbackNames != null)
                {
                    foreach (string fallbackName in fallbackNames)
                    {
                        type = FindCadType(fallbackName);
                        if (type != null) return type;
                    }
                }

                Log("BatchPlot optional CAD plot type not found: " + fullName);
                return null;
            }

            static string[] GetGstarPlotFallback(string shortName)
            {
#if GSTARCAD
                if (shortName == "PlotInfo") return new string[] { "GrxCAD.PlottingServices.PlotInfo", "GcPlPlotInfo" };
                if (shortName == "PlotInfoValidator") return new string[] { "GrxCAD.PlottingServices.PlotInfoValidator", "GcPlPlotInfoValidator" };
                if (shortName == "PlotFactory") return new string[] { "GcPlPlotFactory" };
                if (shortName == "PlotPageInfo") return new string[] { "GrxCAD.PlottingServices.PlotPageInfo", "GcPlPlotPageInfo" };
                if (shortName == "PlotSettings") return new string[] { "GcDb.PlotSettings", "OdDbPlotSettings" };
                if (shortName == "PlotSettingsValidator") return new string[] { "GcDb.PlotSettingsValidator", "OdDbPlotSettingsValidator" };
                if (shortName == "MatchingPolicy") return new string[] { "GrxCAD.PlottingServices.MatchingPolicy", "GcPlMatchingPolicy" };
                if (shortName == "Extents2d") return new string[] { "GrxCAD.DatabaseServices.Extents2d", "GrxCAD.Geometry.Extents2D", "GcGeExtents2d", "OdGeExtents2d" };
                if (shortName == "Point2d") return new string[] { "GrxCAD.Geometry.Point2D", "GcGePoint2d", "OdGePoint2d" };
#endif
                return new string[0];
            }

            internal void PlotFrame(BatchPlotFrame frame, BatchPlotSettings settings, string outputPath)
            {
                object plotSettings = Activator.CreateInstance(PlotSettingsType, new object[] { true });
                ObjectId layoutId = GetCurrentLayoutId();
                CopyFromCurrentLayout(plotSettings, layoutId);
                object validator = GetStaticProperty(PlotSettingsValidatorType, "Current");

                string deviceName = ResolvePlotDeviceName(validator, plotSettings, settings.DeviceName);
                string mediaName = ResolveCanonicalMediaName(validator, plotSettings, settings.PaperName);
                Invoke(validator, "SetPlotConfigurationName", plotSettings, deviceName, mediaName);
                SafeInvoke(validator, "RefreshLists", plotSettings);
                Invoke(validator, "SetPlotWindowArea", plotSettings, CreateExtents2d(ExpandBatchPlotFrameByMarginMm(frame, settings)));
                Invoke(validator, "SetPlotType", plotSettings, ParseEnum(PlotTypeEnum, "Window"));
                Invoke(validator, "SetUseStandardScale", plotSettings, true);
                Invoke(validator, "SetStdScaleType", plotSettings, ParseEnum(StdScaleTypeEnum, "ScaleToFit"));
                Invoke(validator, "SetPlotCentered", plotSettings, settings.CenterPlot);
                Invoke(validator, "SetPlotRotation", plotSettings, ParseEnum(PlotRotationEnum, GetRotationName(frame, settings)));
                if (!string.IsNullOrEmpty(settings.PlotStyle))
                    SafeInvoke(validator, "SetCurrentStyleSheet", plotSettings, settings.PlotStyle);

                object plotInfo = Activator.CreateInstance(PlotInfoType);
                SetProperty(plotInfo, "Layout", layoutId);
                SetProperty(plotInfo, "OverrideSettings", plotSettings);

                object plotInfoValidator = Activator.CreateInstance(PlotInfoValidatorType);
                if (MatchingPolicyEnum != null)
                    SafeSetProperty(plotInfoValidator, "MediaMatchingPolicy", ParseEnum(MatchingPolicyEnum, "MatchEnabled"));
                Invoke(plotInfoValidator, "Validate", plotInfo);

                object state = GetStaticProperty(PlotFactoryType, "ProcessPlotState");
                if (!state.Equals(ParseEnum(ProcessPlotStateEnum, "NotPlotting")))
                    throw new InvalidOperationException("CAD is already plotting.");

                object engine = InvokeStatic(PlotFactoryType, "CreatePublishEngine");
                try
                {
                    Invoke(engine, "BeginPlot", null, null);
                    bool plotToFile = !string.IsNullOrEmpty(outputPath);
                    Invoke(engine, "BeginDocument", plotInfo, settings.DrawingName, null, 1, plotToFile, outputPath);
                    object pageInfo = Activator.CreateInstance(PlotPageInfoType);
                    Invoke(engine, "BeginPage", pageInfo, plotInfo, true, null);
                    SafeInvoke(engine, "BeginGenerateGraphics");
                    SafeInvoke(engine, "EndGenerateGraphics");
                    Invoke(engine, "EndPage", null);
                    Invoke(engine, "EndDocument", null);
                    Invoke(engine, "EndPlot", null);
                }
                finally
                {
                    IDisposable disposable = engine as IDisposable;
                    if (disposable != null) disposable.Dispose();
                }
            }

            void CopyFromCurrentLayout(object plotSettings, ObjectId layoutId)
            {
                try
                {
                    using (var tr = Db.TransactionManager.StartTransaction())
                    {
                        object layout = tr.GetObject(layoutId, OpenMode.ForRead);
                        Invoke(plotSettings, "CopyFrom", layout);
                        tr.Commit();
                    }
                }
                catch (System.Exception ex)
                {
                    Log("BatchPlot CopyFromCurrentLayout failed: " + ex.Message);
                }
            }

            string ResolvePlotDeviceName(object validator, object plotSettings, string deviceName)
            {
                string fallback = string.IsNullOrEmpty(deviceName) ? "DWG To PDF.pc3" : deviceName;
                try
                {
                    SafeInvoke(validator, "RefreshLists", plotSettings);
                    object list = InvokeOptionalArgumentList(validator, "GetPlotDeviceList", plotSettings);
                    var enumerable = list as System.Collections.IEnumerable;
                    if (enumerable == null) return fallback;

                    string normalizedNeedle = NormalizeDeviceName(fallback);
                    string firstContains = null;
                    var available = new List<string>();
                    foreach (object item in enumerable)
                    {
                        string candidate = Convert.ToString(item);
                        if (string.IsNullOrEmpty(candidate)) continue;
                        available.Add(candidate);
                        if (candidate.Equals(fallback, StringComparison.OrdinalIgnoreCase)) return candidate;
                        string normalized = NormalizeDeviceName(candidate);
                        if (normalized.Equals(normalizedNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                        if (firstContains == null && normalized.IndexOf(normalizedNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                            firstContains = candidate;
                    }
                    if (firstContains != null) return firstContains;
                    Log("BatchPlot device not found: " + fallback + "; AvailableDevices=" + string.Join("|", available.ToArray()));
                }
                catch (System.Exception ex)
                {
                    Log("BatchPlot device name lookup failed: " + ex.Message);
                }
                return fallback;
            }

            string ResolveCanonicalMediaName(object validator, object plotSettings, string paperName)
            {
                string fallback = string.IsNullOrEmpty(paperName) ? "A3" : paperName;
                try
                {
                    SafeInvoke(validator, "RefreshLists", plotSettings);
                    object list = Invoke(validator, "GetCanonicalMediaNameList", plotSettings);
                    var enumerable = list as System.Collections.IEnumerable;
                    if (enumerable == null) return fallback;

                    string normalizedNeedle = NormalizeMediaName(fallback);
                    string looseNeedle = fallback.Replace(" ", "").Replace("_", "").ToUpperInvariant();
                    string firstContains = null;
                    foreach (object item in enumerable)
                    {
                        string candidate = Convert.ToString(item);
                        if (candidate.Equals(fallback, StringComparison.OrdinalIgnoreCase)) return candidate;
                        string normalized = NormalizeMediaName(candidate);
                        if (normalized.Equals(normalizedNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                        if (firstContains == null && normalized.IndexOf(looseNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                            firstContains = candidate;
                    }
                    if (firstContains != null) return firstContains;
                }
                catch (System.Exception ex)
                {
                    Log("BatchPlot media name lookup failed: " + ex.Message);
                }
                return fallback;
            }

            object CreateExtents2d(BatchPlotFrame frame)
            {
                try
                {
                    return Activator.CreateInstance(Extents2dType, new object[] { frame.MinX, frame.MinY, frame.MaxX, frame.MaxY });
                }
                catch (MissingMethodException firstError)
                {
                    try
                    {
                        object minPoint = CreatePoint2d(frame.MinX, frame.MinY); object maxPoint = CreatePoint2d(frame.MaxX, frame.MaxY);
                        return Activator.CreateInstance(Extents2dType, new object[] { minPoint, maxPoint });
                    }
                    catch (System.Exception secondError)
                    {
                        throw new InvalidOperationException("BatchPlot failed to create plot window extents. Numeric constructor: "
                            + firstError.Message + "; point constructor: " + secondError.Message, secondError);
                    }
                }
            }

            object CreatePoint2d(double x, double y)
            {
                return Activator.CreateInstance(Point2dType, new object[] { x, y });
            }

            object InvokeOptionalArgumentList(object target, string name, params object[] args)
            {
                try { return Invoke(target, name, args); }
                catch (TargetParameterCountException)
                {
                    try { return Invoke(target, name); }
                    catch (System.Exception ex) { Log("BatchPlot optional invocation failed: " + name + ": " + ex.Message); return null; }
                }
                catch (MissingMethodException)
                {
                    try { return Invoke(target, name); }
                    catch (System.Exception ex) { Log("BatchPlot optional invocation failed: " + name + ": " + ex.Message); return null; }
                }
            }

            string GetRotationName(BatchPlotFrame frame, BatchPlotSettings settings)
            {
                if (settings.AutoRotate && frame.Width > frame.Height) return "Degrees090";
                return "Degrees000";
            }

            void SafeInvoke(object target, string name, params object[] args)
            {
                try { Invoke(target, name, args); }
                catch (System.Exception ex) { Log("BatchPlot optional call failed: " + name + ": " + ex.Message); }
            }

            void SafeSetProperty(object target, string name, object value)
            {
                try { SetProperty(target, name, value); }
                catch (System.Exception ex) { Log("BatchPlot optional property failed: " + name + ": " + ex.Message); }
            }
        }
    }
}
