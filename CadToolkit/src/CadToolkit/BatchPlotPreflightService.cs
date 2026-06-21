using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using CadToolkit.UI;

namespace CadToolkit
{
    public partial class CadCommands
    {
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

        static bool HasBatchPlotBlockingPreflightIssue(List<BatchPlotPreflightRow> rows)
        {
            if (rows == null) return false;
            foreach (BatchPlotPreflightRow row in rows)
                if (row != null && IsBatchPlotBlockingStatus(row.Status)) return true;
            return false;
        }

        static bool IsBatchPlotBlockingStatus(string status)
        {
            string text = SafeStr(status);
            return text.IndexOf("\u76EE\u5F55\u5F02\u5E38", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("\u6587\u4EF6\u540D\u91CD\u590D", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("\u8BBE\u5907\u7F3A\u5931", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("\u6837\u5F0F\u7F3A\u5931", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool ConfirmBatchPlotPreflightIssues(List<BatchPlotPreflightRow> rows)
        {
            if (rows == null) return true;

            var issues = new List<BatchPlotPreflightRow>();
            foreach (BatchPlotPreflightRow row in rows)
            {
                if (row == null) continue;
                string status = SafeStr(row.Status).Trim();
                if (status.Length > 0 && status != "\u6B63\u5E38") issues.Add(row);
            }
            if (issues.Count == 0) return true;

            bool hasBlocking = HasBatchPlotBlockingPreflightIssue(rows);
            var message = new System.Text.StringBuilder();
            message.AppendLine(hasBlocking ? "\u6279\u91CF\u6253\u5370\u9884\u68C0\u53D1\u73B0\u4E25\u91CD\u95EE\u9898\uFF0C\u5EFA\u8BAE\u5148\u4FEE\u590D\u540E\u518D\u6253\u5370\u3002" : "\u6279\u91CF\u6253\u5370\u9884\u68C0\u53D1\u73B0\u63D0\u9192\u9879\u3002");
            message.AppendLine();
            int count = Math.Min(issues.Count, 6);
            for (int i = 0; i < count; i++)
            {
                BatchPlotPreflightRow row = issues[i];
                message.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}  {1}  {2}", SafeStr(row.Index), SafeStr(row.Target), SafeStr(row.Status)));
            }
            if (issues.Count > count)
                message.AppendLine(string.Format(CultureInfo.InvariantCulture, "\u8FD8\u6709 {0} \u9879\u95EE\u9898\u672A\u663E\u793A\u3002", issues.Count - count));
            message.AppendLine();
            message.Append(hasBlocking ? "\u4ECD\u8981\u7EE7\u7EED\u6253\u5370\u5417\uFF1F" : "\u662F\u5426\u7EE7\u7EED\u6253\u5370\uFF1F");

            return MessageBox.Show(message.ToString(), "\u6279\u91CF\u6253\u5370\u9884\u68C0", MessageBoxButtons.OKCancel, hasBlocking ? MessageBoxIcon.Warning : MessageBoxIcon.Information) == DialogResult.OK;
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
    }
}
