using System;
using System.Collections.Generic;
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
                api.PlotInfoType = RequiredTypeFromCandidates(plotNs + ".PlotInfo", GetGstarPlotFallback("PlotInfo"));
                api.PlotInfoValidatorType = RequiredTypeFromCandidates(plotNs + ".PlotInfoValidator", GetGstarPlotFallback("PlotInfoValidator"));
                api.PlotTypeEnum = RequiredType(dbNs + ".PlotType");
                api.StdScaleTypeEnum = RequiredType(dbNs + ".StdScaleType");
                api.PlotRotationEnum = RequiredType(dbNs + ".PlotRotation");
                api.MatchingPolicyEnum = OptionalTypeFromCandidates(dbNs + ".MatchingPolicy", GetGstarPlotFallback("MatchingPolicy"));
                api.Extents2dType = RequiredTypeFromCandidates(dbNs + ".Extents2d", GetBatchPlotExtentsFallback(geoNs));
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

            static string[] GetBatchPlotExtentsFallback(string geoNs)
            {
                var names = new List<string>();
                if (!string.IsNullOrEmpty(geoNs))
                {
                    names.Add(geoNs + ".Extents2d");
                    names.Add(geoNs + ".Extents2D");
                }

                names.AddRange(GetGstarPlotFallback("Extents2d"));
                return names.ToArray();
            }

            internal void PlotFrame(BatchPlotFrame frame, BatchPlotSettings settings, string outputPath)
            {
                object plotSettings = Activator.CreateInstance(PlotSettingsType, new object[] { true });
                ObjectId layoutId = GetCurrentLayoutId();
                CopyFromCurrentLayout(plotSettings, layoutId);
                object validator = GetStaticProperty(PlotSettingsValidatorType, "Current");

                string deviceName = ResolvePlotDeviceName(validator, plotSettings, settings.DeviceName);
                BindPlotDeviceForMediaLookup(validator, plotSettings, deviceName);
                string mediaName = ResolveCanonicalMediaName(validator, plotSettings, settings.PaperName);
                Invoke(validator, "SetPlotConfigurationName", plotSettings, deviceName, mediaName);
                SafeInvoke(validator, "RefreshLists", plotSettings);
                BatchPlotFrame plotFrame = ExpandBatchPlotFrameByMarginMm(frame, settings);
                string scaleInput = BuildBatchPlotScaleInput(frame, settings);
                LogBatchPlotGeometry("BatchPlot API geometry", frame, plotFrame, settings, scaleInput);
                Invoke(validator, "SetPlotWindowArea", plotSettings, CreateExtents2d(plotFrame));
                Invoke(validator, "SetPlotType", plotSettings, ParseEnum(PlotTypeEnum, "Window"));
                if (!SetCustomBatchPlotScale(validator, plotSettings, frame, settings))
                {
                    Invoke(validator, "SetUseStandardScale", plotSettings, true);
                    Invoke(validator, "SetStdScaleType", plotSettings, ParseEnum(StdScaleTypeEnum, "ScaleToFit"));
                }
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

                object oldBackgroundPlot = null;
                bool restoreBackgroundPlot = false;
                try
                {
                    oldBackgroundPlot = CadApp.GetSystemVariable("BACKGROUNDPLOT");
                    CadApp.SetSystemVariable("BACKGROUNDPLOT", 0);
                    restoreBackgroundPlot = true;
                }
                catch (System.Exception ex)
                {
                    Log("BatchPlot set BACKGROUNDPLOT failed: " + ex.Message);
                }

                try
                {
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
                        SafeInvokeOptionalMethod(engine, "BeginGenerateGraphics", new object[] { null });
                        SafeInvokeOptionalMethod(engine, "EndGenerateGraphics", new object[] { null });
                        InvokeOptionalMethod(engine, "EndPage", new object[] { null });
                        InvokeOptionalMethod(engine, "EndDocument", new object[] { null });
                        InvokeOptionalMethod(engine, "EndPlot", new object[] { null });
                    }
                    finally
                    {
                        IDisposable disposable = engine as IDisposable;
                        if (disposable != null) disposable.Dispose();
                    }
                }
                finally
                {
                    if (restoreBackgroundPlot)
                    {
                        try { CadApp.SetSystemVariable("BACKGROUNDPLOT", oldBackgroundPlot); }
                        catch (System.Exception ex) { Log("BatchPlot restore BACKGROUNDPLOT failed: " + ex.Message); }
                    }
                }
            }

            bool SetCustomBatchPlotScale(object validator, object plotSettings, BatchPlotFrame frame, BatchPlotSettings settings)
            {
                double scale;
                if (!TryGetBatchPlotScale(frame, settings, out scale)) return false;

                try
                {
                    object customScale = CreateCustomScale(scale);
                    if (customScale == null) return false;
                    Invoke(validator, "SetUseStandardScale", plotSettings, false);
                    Invoke(validator, "SetCustomPrintScale", plotSettings, customScale);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log("BatchPlot SetCustomPrintScale failed: " + ex.Message);
                    return false;
                }
            }

            object CreateCustomScale(double scale)
            {
                Type customScaleType = OptionalTypeFromCandidates(GetDatabaseNamespace() + ".CustomScale", GetGstarPlotFallback("CustomScale"));
                if (customScaleType == null) return null;

                ConstructorInfo ctor = customScaleType.GetConstructor(new Type[] { typeof(double), typeof(double) });
                if (ctor != null) return ctor.Invoke(new object[] { scale, 1.0 });

                object customScale = Activator.CreateInstance(customScaleType);
                SafeSetProperty(customScale, "Numerator", scale);
                SafeSetProperty(customScale, "Denominator", 1.0);
                return customScale;
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

            bool BindPlotDeviceForMediaLookup(object validator, object plotSettings, string deviceName)
            {
                try
                {
                    Invoke(validator, "SetPlotConfigurationName", plotSettings, deviceName, null);
                    SafeInvoke(validator, "RefreshLists", plotSettings);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log("BatchPlot device bind for media lookup failed: " + SafeStr(deviceName) + ": " + ex.Message);
                    return false;
                }
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
                    var available = new List<string>();
                    foreach (object item in enumerable)
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
                    Log("BatchPlot media not found: " + fallback + "; AvailableMedia=" + string.Join("|", available.ToArray()));
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

            void InvokeOptionalMethod(object target, string name, params object[] args)
            {
                try
                {
                    InvokeMethodByArgumentCount(target, name, args);
                }
                catch (TargetParameterCountException)
                {
                    InvokeMethodByArgumentCount(target, name, new object[0]);
                }
                catch (MissingMethodException)
                {
                    InvokeMethodByArgumentCount(target, name, new object[0]);
                }
            }

            object InvokeMethodByArgumentCount(object target, string name, object[] args)
            {
                object[] actualArgs = args ?? new object[0];
                foreach (MethodInfo method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!method.Name.Equals(name, StringComparison.Ordinal)) continue;
                    if (method.GetParameters().Length == actualArgs.Length)
                        return method.Invoke(target, actualArgs);
                }

                return Invoke(target, name, actualArgs);
            }

            void SafeInvokeOptionalMethod(object target, string name, params object[] args)
            {
                try { InvokeOptionalMethod(target, name, args); }
                catch (System.Exception ex) { Log("BatchPlot optional call failed: " + name + ": " + ex.Message); }
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
