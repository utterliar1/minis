using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

#if AUTOCAD
using Autodesk.AutoCAD.DatabaseServices;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.DatabaseServices;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.DatabaseServices;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        static bool PlotFrameToPdf(BatchPlotFrame frame, BatchPlotSettings settings, string outputPath)
        {
#if GSTARCAD || ZWCAD || AUTOCAD
            return PlotFrameToPdfWithPlotCommand(frame, settings, outputPath);
#else
            try
            {
                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(outputPath)) File.Delete(outputPath);

                var api = BatchPlotApi.Create();
                api.PlotFrame(frame, settings, outputPath);
                if (!WaitForValidPdfFile(outputPath))
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
#if GSTARCAD || ZWCAD || AUTOCAD
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

#if GSTARCAD || ZWCAD || AUTOCAD
        static int RunBatchPlotWithPlotCommand(List<BatchPlotFrame> frames, BatchPlotSettings settings, bool outputToFile)
        {
            if (frames == null || settings == null) return 0;
            try
            {
                BatchPlotSettings resolvedSettings = ResolvePlotCommandSettings(settings);
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

                    SendPlotCommand(BuildPlotCommand(frames[i], resolvedSettings, outputPath));
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

                BatchPlotSettings resolvedSettings = ResolvePlotCommandSettings(settings);
                string commandText = BuildPlotCommand(frame, resolvedSettings, outputPath);
                SendPlotCommand(commandText);
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
                BatchPlotSettings resolvedSettings = ResolvePlotCommandSettings(settings);
                string commandText = BuildPlotCommand(frame, resolvedSettings, null);
                SendPlotCommand(commandText);
                DebugBatchPlotLog("BatchPlot -PLOT command submitted for printer: " + DescribeBatchPlotSettings(resolvedSettings));
                return true;
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot -PLOT command failed for printer: " + DescribeBatchPlotSettings(settings) + " " + ex);
                return false;
            }
        }

        static string BuildPlotCommand(BatchPlotFrame frame, BatchPlotSettings settings, string outputPath)
        {
            BatchPlotFrame plotFrame = ExpandBatchPlotFrameByMarginMm(frame, settings);
            string scaleInput = BuildPlotCommandScaleInput(frame, settings);
            LogPlotCommandGeometry(frame, plotFrame, settings, scaleInput);
            var inputs = new List<string>();
            inputs.Add(QuotePlotCommandLispString("_.-PLOT"));
            inputs.Add(QuotePlotCommandLispString("Y"));
            inputs.Add(QuotePlotCommandLispString("Model"));
            inputs.Add(QuotePlotCommandLispString(settings.DeviceName));
            inputs.Add(QuotePlotCommandLispString(settings.PaperName));
            inputs.Add(QuotePlotCommandLispString(GetPlotCommandUnitsInput(settings.PaperName)));
            inputs.Add(QuotePlotCommandLispString(GetPlotCommandOrientationInput(plotFrame, settings)));
            inputs.Add(QuotePlotCommandLispString("N"));
            inputs.Add(QuotePlotCommandLispString("W"));
            inputs.Add(QuotePlotCommandLispString(FormatPlotCommandPoint(plotFrame.MinX, plotFrame.MinY)));
            inputs.Add(QuotePlotCommandLispString(FormatPlotCommandPoint(plotFrame.MaxX, plotFrame.MaxY)));
            inputs.Add(QuotePlotCommandLispString(scaleInput));
            inputs.Add(QuotePlotCommandLispString(settings.CenterPlot ? "C" : "0,0"));
            inputs.Add(QuotePlotCommandLispString("Y"));
            inputs.Add(QuotePlotCommandLispString(settings.PlotStyle));
            inputs.Add(QuotePlotCommandLispString("Y"));
            inputs.Add(QuotePlotCommandLispString(GetPlotCommandShadeInput()));
            if (!string.IsNullOrEmpty(outputPath))
            {
                inputs.Add(QuotePlotCommandLispString(outputPath));
                inputs.Add(QuotePlotCommandLispString("N"));
                inputs.Add(QuotePlotCommandLispString("Y"));
            }
            else
            {
                inputs.Add(QuotePlotCommandLispString("N"));
                inputs.Add(QuotePlotCommandLispString("N"));
                inputs.Add(QuotePlotCommandLispString("Y"));
            }
            return "(ct-plot (list " + string.Join(" ", inputs.ToArray()) + "))\n";
        }

        static void LogPlotCommandGeometry(BatchPlotFrame frame, BatchPlotFrame plotFrame, BatchPlotSettings settings, string scaleInput)
        {
            Log("BatchPlot geometry: Original=" + DescribeBatchPlotFrame(frame)
                + "; PlotWindow=" + DescribeBatchPlotFrame(plotFrame)
                + "; Scale=" + SafeStr(scaleInput)
                + "; " + DescribeBatchPlotSettings(settings));
        }

        static BatchPlotSettings ResolvePlotCommandSettings(BatchPlotSettings settings)
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

            resolved.DeviceName = ResolvePlotCommandDeviceName(resolved.DeviceName);
            resolved.PaperName = ResolvePlotCommandPaperName(resolved.DeviceName, resolved.PaperName);
            return resolved;
        }

        static string ResolvePlotCommandDeviceName(string deviceName)
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
                        string matched = MatchPlotCommandDeviceName(enumerable, fallback);
                        if (!string.IsNullOrEmpty(matched) && !matched.Equals(fallback, StringComparison.OrdinalIgnoreCase))
                            Log("BatchPlot plot command device resolved: " + fallback + " -> " + matched);
                        return matched;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot plot command device lookup failed: " + ex.Message);
            }

            return fallback;
        }

        static string MatchPlotCommandDeviceName(System.Collections.IEnumerable devices, string fallback)
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
                string normalizedCandidateBase = NormalizeDeviceName(Path.GetFileNameWithoutExtension(candidate));
                if (normalized.Equals(normalizedNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                if (!string.IsNullOrEmpty(normalizedBaseNeedle) && normalized.Equals(normalizedBaseNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                if (!string.IsNullOrEmpty(normalizedBaseNeedle) && normalizedCandidateBase.Equals(normalizedBaseNeedle, StringComparison.OrdinalIgnoreCase)) return candidate;
                if (firstContains == null && normalized.IndexOf(normalizedNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    firstContains = candidate;
                if (firstContains == null && !string.IsNullOrEmpty(normalizedBaseNeedle) && normalized.IndexOf(normalizedBaseNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    firstContains = candidate;
                if (firstContains == null && !string.IsNullOrEmpty(normalizedBaseNeedle) && normalizedCandidateBase.IndexOf(normalizedBaseNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    firstContains = candidate;
            }

            Log("BatchPlot plot command device not found: " + fallback + "; AvailableDevices=" + string.Join("|", available.ToArray()));
            return fallback;
        }

        static string ResolvePlotCommandPaperName(string deviceName, string paperName)
        {
            string fallback = string.IsNullOrEmpty(paperName) ? "A3" : paperName;
            bool expandPaperName = ShouldUsePlotCommandExpandedPaperName(deviceName);
            string commandFallback = expandPaperName ? GetPlotCommandFallbackMediaName(fallback) : fallback;
            try
            {
                Type plotSettingsType = FindCadType(GetDatabaseNamespace() + ".PlotSettings");
                Type validatorType = FindCadType(GetDatabaseNamespace() + ".PlotSettingsValidator");
                if (plotSettingsType == null || validatorType == null) return commandFallback;

                object validator = GetStaticProperty(validatorType, "Current");
                object plotSettings = Activator.CreateInstance(plotSettingsType, new object[] { true });
                TrySetPlotCommandConfigurationWithMediaCandidate(validator, plotSettings, deviceName, fallback);
                Invoke(validator, "RefreshLists", plotSettings);
                object list = Invoke(validator, "GetCanonicalMediaNameList", plotSettings);
                var enumerable = list as System.Collections.IEnumerable;
                if (enumerable == null) return commandFallback;

                string matched = MatchPlotCommandMediaName(enumerable, fallback);
                if (!string.IsNullOrEmpty(matched) && !matched.Equals(fallback, StringComparison.OrdinalIgnoreCase))
                    Log("BatchPlot plot command media resolved: " + fallback + " -> " + matched);
                return ToPlotCommandMediaInput(matched, fallback, commandFallback, expandPaperName);
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot plot command media lookup failed: " + ex.Message);
                return commandFallback;
            }
        }

        static bool TrySetPlotCommandConfigurationWithMediaCandidate(object validator, object plotSettings, string deviceName, string paperName)
        {
            string lastError = null;
            foreach (string candidate in GetPlotCommandMediaCandidates(paperName))
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

            Log("BatchPlot plot command media bind failed: " + SafeStr(paperName) + "; " + SafeStr(lastError));
            return false;
        }

        static bool ShouldUsePlotCommandExpandedPaperName(string deviceName)
        {
            return SafeStr(deviceName).IndexOf("DWG To PDF", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static IEnumerable<string> GetPlotCommandMediaCandidates(string paperName)
        {
            string fallback = string.IsNullOrEmpty(paperName) ? "A3" : paperName.Trim();
            string commandFallback = GetPlotCommandFallbackMediaName(fallback);
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

        static string GetPlotCommandFallbackMediaName(string paperName)
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

        static string ToPlotCommandMediaInput(string matchedMediaName, string configuredPaperName, string commandFallback, bool expandPaperName)
        {
            string matched = string.IsNullOrEmpty(matchedMediaName) ? configuredPaperName : matchedMediaName.Trim();
            string zwcad = ToPlotCommandMediaInputDisplayName(matched);
            if (!string.IsNullOrEmpty(zwcad)) return zwcad;
            if (!expandPaperName) return matched;
            string normalizedMatched = NormalizeMediaName(matched);
            string normalizedConfigured = NormalizeMediaName(configuredPaperName);
            if (normalizedMatched == normalizedConfigured && IsIsoSeriesShortPaperName(normalizedMatched))
                return commandFallback;
            return matched;
        }

        static string ToPlotCommandMediaInputDisplayName(string matchedMediaName)
        {
            string matched = SafeStr(matchedMediaName).Trim();
            if (matched.Equals("ISO_full_bleed_A4_(297.00_x_210.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO full bleed A4 (297.00 x 210.00 毫米)";
            if (matched.Equals("ISO_full_bleed_A3_(420.00_x_297.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO full bleed A3 (420.00 x 297.00 毫米)";
            if (matched.Equals("ISO_full_bleed_A2_(594.00_x_420.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO full bleed A2 (594.00 x 420.00 毫米)";
            if (matched.Equals("ISO_full_bleed_A1_(841.00_x_594.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO full bleed A1 (841.00 x 594.00 毫米)";
            if (matched.Equals("ISO_full_bleed_A0_(1189.00_x_841.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO full bleed A0 (1189.00 x 841.00 毫米)";
            if (matched.Equals("ISO_expand_A4_(297.00_x_210.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO expand A4 (297.00 x 210.00 毫米)";
            if (matched.Equals("ISO_expand_A3_(420.00_x_297.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO expand A3 (420.00 x 297.00 毫米)";
            if (matched.Equals("ISO_expand_A2_(594.00_x_420.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO expand A2 (594.00 x 420.00 毫米)";
            if (matched.Equals("ISO_expand_A1_(841.00_x_594.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO expand A1 (841.00 x 594.00 毫米)";
            if (matched.Equals("ISO_expand_A0_(1189.00_x_841.00_MM)", StringComparison.OrdinalIgnoreCase))
                return "ISO expand A0 (1189.00 x 841.00 毫米)";
            return null;
        }

        static bool IsIsoSeriesShortPaperName(string normalizedPaperName)
        {
            return normalizedPaperName == "A0"
                || normalizedPaperName == "A1"
                || normalizedPaperName == "A2"
                || normalizedPaperName == "A3"
                || normalizedPaperName == "A4";
        }

        static string MatchPlotCommandMediaName(System.Collections.IEnumerable mediaNames, string fallback)
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
            Log("BatchPlot plot command media not found: " + fallback + "; AvailableMedia=" + string.Join("|", available.ToArray()));
            return fallback;
        }

        static void SendPlotCommand(string commandText)
        {
            DebugBatchPlotLog("BatchPlot -PLOT command text: " + commandText.Replace("\r", "\\r").Replace("\n", "\\n"));
            string quietCommandText = "(progn"
                + " (defun ct-has-func (_ctName) (member _ctName (atoms-family 1)))"
                + " (defun ct-plot (_ctArgs) (cond ((ct-has-func \"VL-CMDF\") (apply 'vl-cmdf _ctArgs)) ((ct-has-func \"COMMAND-S\") (apply 'command-s _ctArgs)) (T (apply 'command _ctArgs))))"
                + " (setq _ctOldBgPlot (getvar \"BACKGROUNDPLOT\")) (setvar \"CMDECHO\" 0) (setvar \"BACKGROUNDPLOT\" 0) "
                + commandText.Trim()
                + " (setvar \"BACKGROUNDPLOT\" _ctOldBgPlot) (setvar \"CMDECHO\" 1) (princ))\n";
            if (TrySendPlotCommandWithCom(quietCommandText)) return;
            CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute(quietCommandText, true, false, false);
        }

        static bool TrySendPlotCommandWithCom(string quietCommandText)
        {
#if AUTOCAD
            try
            {
                object acadApp = CadApp.AcadApplication;
                if (acadApp == null) return false;
                object activeDocument = acadApp.GetType().InvokeMember("ActiveDocument", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance, null, acadApp, null);
                if (activeDocument == null) return false;
                activeDocument.GetType().InvokeMember("SendCommand", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, activeDocument, new object[] { quietCommandText });
                return true;
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot COM SendCommand failed: " + ex.Message);
                return false;
            }
#else
            return false;
#endif
        }

        [System.Diagnostics.Conditional("BATCHPLOT_DEBUG")]
        static void DebugBatchPlotLog(string message)
        {
            Log(message);
        }

        static string FormatPlotCommandPoint(double x, double y)
        {
            return x.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture)
                + ","
                + y.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string QuotePlotCommandLispString(string value)
        {
            string text = SafeStr(value).Replace("\r", " ").Replace("\n", " ").Trim();
            text = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "\"" + text + "\"";
        }

        static string GetPlotCommandUnitsInput(string paperName)
        {
            return SafeStr(paperName).IndexOf("inch", StringComparison.OrdinalIgnoreCase) >= 0 ? "I" : "M";
        }

        static string GetPlotCommandOrientationInput(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            if (IsBatchPlotLandscape(frame, settings)) return "L";
            return "P";
        }

        static string GetPlotCommandShadeInput()
        {
            return "W";
        }

        static string BuildPlotCommandScaleInput(BatchPlotFrame frame, BatchPlotSettings settings)
        {
            return BuildBatchPlotScaleInput(frame, settings);
        }
#endif

    }
}

