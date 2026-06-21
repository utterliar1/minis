using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;

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
        const long MinimumValidPdfBytes = 1024;
        const int PlotWaitTimeoutMs = 30000;
        const int PlotWaitIntervalMs = 100;

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
            return IsValidPdfFile(path, true);
        }

        static bool WaitForValidPdfFile(string path)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(PlotWaitTimeoutMs);
            while (DateTime.UtcNow <= deadline)
            {
                if (IsValidPdfFile(path, false)) return true;
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(PlotWaitIntervalMs);
            }

            return IsValidPdfFile(path);
        }

        static bool IsValidPdfFile(string path, bool logFailures)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                FileInfo info = new FileInfo(path);
                if (info.Length < MinimumValidPdfBytes)
                {
                    if (logFailures)
                        Log("BatchPlot PDF is too small and may be blank: " + path + "; Length=" + info.Length.ToString(CultureInfo.InvariantCulture));
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
                if (logFailures)
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

        static string DescribeBatchPlotFrame(BatchPlotFrame frame)
        {
            if (frame == null) return "";
            return "Min=(" + FormatBatchPlotNumber(frame.MinX) + "," + FormatBatchPlotNumber(frame.MinY)
                + "); Max=(" + FormatBatchPlotNumber(frame.MaxX) + "," + FormatBatchPlotNumber(frame.MaxY)
                + "); Size=" + FormatBatchPlotNumber(frame.Width)
                + "x" + FormatBatchPlotNumber(frame.Height)
                + "; Ratio=" + (frame.Height <= 0 ? "0" : FormatBatchPlotNumber(frame.Width / frame.Height));
        }

        static string FormatBatchPlotNumber(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        static void LogBatchPlotGeometry(string prefix, BatchPlotFrame frame, BatchPlotFrame plotFrame, BatchPlotSettings settings, string scaleInput)
        {
            Log(prefix + ": Original=" + DescribeBatchPlotFrame(frame)
                + "; PlotWindow=" + DescribeBatchPlotFrame(plotFrame)
                + "; Scale=" + SafeStr(scaleInput)
                + "; " + DescribeBatchPlotSettings(settings));
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

    }
}
