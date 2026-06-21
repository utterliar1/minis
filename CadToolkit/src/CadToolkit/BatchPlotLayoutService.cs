using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CadToolkit
{
    public partial class CadCommands
    {
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

        static bool TryGetBatchPlotScale(BatchPlotFrame frame, BatchPlotSettings settings, out double scale)
        {
            scale = 0;
            if (frame == null || settings == null || frame.Width <= 0 || frame.Height <= 0) return false;

            double paperWidthMm;
            double paperHeightMm;
            if (!TryGetBatchPlotPaperSizeMm(settings.PaperName, out paperWidthMm, out paperHeightMm)) return false;

            double marginMm = Math.Max(0, settings.MarginMm);
            bool landscape = IsBatchPlotLandscape(frame, settings);
            double usableWidth = landscape ? Math.Max(paperWidthMm, paperHeightMm) : Math.Min(paperWidthMm, paperHeightMm);
            double usableHeight = landscape ? Math.Min(paperWidthMm, paperHeightMm) : Math.Max(paperWidthMm, paperHeightMm);
            double contentWidth = usableWidth - (2 * marginMm);
            double contentHeight = usableHeight - (2 * marginMm);
            if (contentWidth <= 0 || contentHeight <= 0) return false;

            scale = Math.Min(contentWidth / frame.Width, contentHeight / frame.Height);
            return scale > 0;
        }

        static string BuildBatchPlotScaleInput(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            double scale;
            if (!TryGetBatchPlotScale(frame, settings, out scale)) return "F";
            return scale.ToString("0.########", CultureInfo.InvariantCulture);
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
    }
}
