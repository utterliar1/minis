using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections;
using CadToolkit.Core;
using CadToolkit.UI;
using LayerStandardRule = CadToolkit.Core.Config.LayerStandardRule;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using CadColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using GrxCAD.Runtime;
using CadColor = GrxCAD.Colors.Color;
using CadColorMethod = GrxCAD.Colors.ColorMethod;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using CadColor = ZwSoft.ZwCAD.Colors.Color;
using CadColorMethod = ZwSoft.ZwCAD.Colors.ColorMethod;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

#if GSTARCAD
[assembly: CommandClass(typeof(CadToolkit.CadCommands))]
[assembly: ExtensionApplication(typeof(CadToolkit.CadPlugin))]
#else
[assembly: CommandClass(typeof(CadToolkit.CadCommands))]
#endif

namespace CadToolkit
{
#if GSTARCAD
    public class CadPlugin : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(CadPlugin).Assembly.Location);
                System.AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, System.ResolveEventArgs e)
                {
                    string name = new System.Reflection.AssemblyName(e.Name).Name + ".dll";
                    string p2 = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(p2)) return System.Reflection.Assembly.LoadFrom(p2);
                    return null;
                };
                Config.Init(typeof(CadPlugin).Assembly.Location);
            }
            catch (System.Exception ex) { CadCommands.Log("GstarCAD Initialize failed: " + ex.Message); }
        }
        public void Terminate() { }
    }
#endif

    public partial class CadCommands
    {
        static bool _initDone;
        static ObjectId[] _pendingSelection;
        class IsoLayerState
        {
            public ObjectId[] FrozenLayers;
            public ObjectId PreviousCurrentLayer;
            public bool HasPreviousCurrentLayer;
        }

        static Dictionary<string, IsoLayerState> _isoState = new Dictionary<string, IsoLayerState>();

        static void EnsureInit()
        {
            if (_initDone) return;
            _initDone = true;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(CadCommands).Assembly.Location);
                System.AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, System.ResolveEventArgs e)
                {
                    string name = new System.Reflection.AssemblyName(e.Name).Name + ".dll";
                    string p2 = System.IO.Path.Combine(dir, name);
                    if (System.IO.File.Exists(p2)) return System.Reflection.Assembly.LoadFrom(p2);
                    return null;
                };
                Config.Init(typeof(CadCommands).Assembly.Location);
            }
            catch (System.Exception ex) { Log("EnsureInit failed: " + ex.Message); }
        }

        static Editor Ed { get { return CadApp.DocumentManager.MdiActiveDocument.Editor; } }
        static Database Db { get { return CadApp.DocumentManager.MdiActiveDocument.Database; } }
        static string DocKey { get { var d = CadApp.DocumentManager.MdiActiveDocument; if (d == null) return ""; try { return d.Database.Filename; } catch { return d.GetHashCode().ToString(); } } }
        static string SafeStr(string s) { return s == null ? "" : s; }
        static string PlatformName
        {
            get
            {
#if AUTOCAD
                return "AutoCAD";
#elif ZWCAD
                return "ZWCAD";
#elif GSTARCAD
                return "GstarCAD";
#else
                return "CAD";
#endif
            }
        }

        static ObjectId GetTextStyleId(Transaction tr, string name)
        {
            var tst = (TextStyleTable)tr.GetObject(Db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has(name)) return tst[name];
            return Db.Textstyle;
        }

        static string ReplaceEx(string src, string oldStr, string rep, StringComparison comp)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(oldStr)) return src;
            int idx = 0; var sb = new StringBuilder();
            while (idx <= src.Length)
            {
                int pos = src.IndexOf(oldStr, idx, comp);
                if (pos < 0) { sb.Append(src, idx, src.Length - idx); break; }
                sb.Append(src, idx, pos - idx).Append(rep);
                idx = pos + oldStr.Length;
            }
            return sb.ToString();
        }

        static bool CheckDoc()
        {
            if (CadApp.DocumentManager.MdiActiveDocument == null)
            {
                MessageBox.Show("\u8bf7\u5148\u6253\u5f00\u4e00\u4e2a\u56fe\u7ebf\u6587\u4ef6\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        static PromptSelectionResult GetPendingOrSelection()
        {
            if (_pendingSelection != null && _pendingSelection.Length > 0)
            {
                var ids = _pendingSelection;
                _pendingSelection = null;
                Ed.SetImpliedSelection(ids);
                var result = Ed.SelectImplied();
                if (result.Status == PromptStatus.OK && result.Value != null && result.Value.Count > 0)
                    return result;
            }
            var psr = Ed.SelectImplied();
            if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
                return psr;
            return Ed.GetSelection();
        }

        static ObjectId[] GetSelectionOrAbort()
        {
            return GetSelectionOrAbort("\n\u672a\u9009\u62e9\u5bf9\u8c61\u3002");
        }

        static ObjectId[] GetSelectionOrAbort(string emptyMessage)
        {
            EnsureInit();
            if (!CheckDoc()) return null;
            var psr = GetPendingOrSelection();
            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                Ed.WriteMessage(emptyMessage);
                return null;
            }
            return psr.Value.GetObjectIds();
        }

        internal static void Log(string msg)
        {
            try
            {
                string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CadToolkit.log");
                System.IO.File.AppendAllText(logPath, string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg));
            }
            catch { }
        }

        static bool RunWithUndo(string name, Func<bool> action)
        {
            bool started = TryInvokeUndoMethod(name, "StartUndoMark");
            try
            {
                return action();
            }
            catch (System.Exception ex)
            {
                Ed.WriteMessage(string.Format("\n{0} 执行失败：{1}", name, ex.Message));
                Log(name + " failed: " + ex);
                return false;
            }
            finally
            {
                if (started)
                {
                    TryInvokeUndoMethod(name, "EndUndoMark");
                }
            }
        }

        static bool TryInvokeUndoMethod(string commandName, string methodName)
        {
            try
            {
                object doc = CadApp.DocumentManager.MdiActiveDocument;
                if (TryInvokeNoArg(doc, methodName)) return true;
                if (TryInvokeNoArg(Db, methodName)) return true;
                Log(commandName + " " + methodName + " unavailable.");
            }
            catch (System.Exception ex)
            {
                Log(commandName + " " + methodName + " failed: " + ex.Message);
            }
            return false;
        }

        static bool TryInvokeNoArg(object target, string methodName)
        {
            if (target == null) return false;
            var method = target.GetType().GetMethod(methodName, Type.EmptyTypes);
            if (method == null) return false;
            method.Invoke(target, null);
            return true;
        }

        class LayerStandardPlan
        {
            public string SourceLayer;
            public string TargetLayer;
            public int Count;
            public LayerStandardRule Rule;
            public string Reason;
        }

        enum LayerPlanTreeFilter
        {
            All,
            Unknown,
            Migration,
            Whitelist
        }

        class LayerRuleMatch
        {
            public LayerStandardRule Rule;
            public string Pattern;
            public string MatchMode;
            public bool IsStandardName;
        }

        class LayerPatternMatch
        {
            public string Pattern;
            public string MatchMode;
        }

        static LayerStandardRule MatchLayerRule(string layerName, List<LayerStandardRule> rules)
        {
            var match = MatchLayerRuleDetail(layerName, rules);
            return match == null ? null : match.Rule;
        }

        static LayerRuleMatch MatchLayerRuleDetail(string layerName, List<LayerStandardRule> rules)
        {
            foreach (var rule in rules)
                if (rule.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    return new LayerRuleMatch { Rule = rule, Pattern = rule.Name, MatchMode = GetLayerPatternMatchMode(rule.Name), IsStandardName = true };

            foreach (var rule in rules)
            {
                foreach (string alias in rule.Aliases)
                {
                    if (alias.Length == 0) continue;
                    if (IsLayerAliasMatch(layerName, alias))
                        return new LayerRuleMatch { Rule = rule, Pattern = alias, MatchMode = GetLayerPatternMatchMode(alias), IsStandardName = false };
                }
            }
            return null;
        }

        static string FormatLayerRuleReason(LayerRuleMatch match)
        {
            if (match == null) return "";
            if (match.IsStandardName) return "\u547d\u4e2d\u6807\u51c6\u56fe\u5c42\u540d";
            return string.Format("\u547d\u4e2d\u522b\u540d \"{0}\"\uff08{1}\uff09", match.Pattern, match.MatchMode);
        }

        static string FormatWhitelistReason(LayerPatternMatch match)
        {
            if (match == null) return "";
            return string.Format("\u547d\u4e2d\u767d\u540d\u5355 \"{0}\"\uff08{1}\uff09", match.Pattern, match.MatchMode);
        }

        static string GetLayerPatternMatchMode(string pattern)
        {
            return SafeStr(pattern).IndexOf('*') >= 0 ? "\u901a\u914d\u5339\u914d" : "\u5168\u5b57\u5339\u914d";
        }

        static bool IsLayerAliasMatch(string layerName, string alias)
        {
            if (string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(alias)) return false;
            return SimpleWildcardMatch(layerName, alias);
        }

        static bool SimpleWildcardMatch(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            if (pattern == "*") return true;
            if (pattern.StartsWith("*") && pattern.EndsWith("*") && pattern.Length > 2)
                return text.IndexOf(pattern.Substring(1, pattern.Length - 2), StringComparison.OrdinalIgnoreCase) >= 0;
            if (pattern.StartsWith("*"))
                return text.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            if (pattern.EndsWith("*"))
                return text.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase);
            return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsLayerWhitelisted(string layerName, string whitelist)
        {
            return MatchWhitelistPattern(layerName, whitelist) != null;
        }

        static LayerPatternMatch MatchWhitelistPattern(string layerName, string whitelist)
        {
            string[] items = SafeStr(whitelist).Split(',');
            foreach (string item in items)
            {
                string pattern = item.Trim();
                if (pattern.Length == 0) continue;
                if (SimpleWildcardMatch(layerName, pattern))
                    return new LayerPatternMatch { Pattern = pattern, MatchMode = GetLayerPatternMatchMode(pattern) };
            }
            return null;
        }

        static ObjectId[] GetSelectionInScopeOrAll(SelectionFilter sf, string message, Func<Entity, Transaction, bool> matches)
        {
            var pso = new PromptSelectionOptions();
            pso.MessageForAdding = message;
            var psr = Ed.GetSelection(pso);
            if (psr.Status == PromptStatus.OK)
                return FilterObjectIds(psr.Value.GetObjectIds(), matches);
            if (psr.Status == PromptStatus.Cancel) return null;

            Ed.WriteMessage("\n\u672a\u9009\u62e9\u8303\u56f4\uff0c\u6539\u4e3a\u641c\u7d22\u5168\u56fe\u3002");
            psr = Ed.SelectAll(sf);
            if (psr.Status != PromptStatus.OK || psr.Value == null)
                return new ObjectId[0];
            return psr.Value.GetObjectIds();
        }

        static ObjectId[] FilterObjectIds(ObjectId[] sourceIds, Func<Entity, Transaction, bool> matches)
        {
            var result = new List<ObjectId>();
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in sourceIds)
                {
                    try
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent != null && matches(ent, tr)) result.Add(id);
                    }
                    catch (System.Exception ex) { Log("FilterObjectIds skipped object: " + ex.Message); }
                }
                tr.Commit();
            }
            return result.ToArray();
        }

        static LineWeight ParseLineWeight(string value)
        {
            if (string.IsNullOrEmpty(value)) return (LineWeight)(-3);
            string t = value.Trim();
            if (t.Equals("Default", StringComparison.OrdinalIgnoreCase) || t == "\u9ed8\u8ba4")
                return (LineWeight)(-3);
            if (t.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)) return LineWeight.ByLayer;
            if (t.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)) return LineWeight.ByBlock;

            double mm;
            if (double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mm))
                return (LineWeight)(int)Math.Round(mm * 100.0);
            return (LineWeight)(-3);
        }

        static void EnsureLineType(Transaction tr, string lineTypeName)
        {
            if (string.IsNullOrEmpty(lineTypeName)) return;
            try
            {
                var lt = (LinetypeTable)tr.GetObject(Db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has(lineTypeName)) return;
                string fileName = "acad.lin";
#if ZWCAD
                fileName = "zwcad.lin";
#elif GSTARCAD
                fileName = "gcad.lin";
#endif
                Db.LoadLineTypeFile(lineTypeName, fileName);
            }
            catch (System.Exception ex) { Log("EnsureLineType failed for " + lineTypeName + ": " + ex.Message); }
        }

        static void ApplyLayerRule(Transaction tr, LayerTable lt, LayerStandardRule rule)
        {
            EnsureLineType(tr, rule.Linetype);
            LayerTableRecord ltr;
            if (lt.Has(rule.Name))
            {
                ltr = (LayerTableRecord)tr.GetObject(lt[rule.Name], OpenMode.ForWrite);
            }
            else
            {
                lt.UpgradeOpen();
                ltr = new LayerTableRecord();
                ltr.Name = rule.Name;
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }

            try { ltr.Color = CadColor.FromColorIndex(CadColorMethod.ByAci, (short)rule.ColorIndex); } catch (System.Exception ex) { Log("ApplyLayerRule color failed for " + rule.Name + ": " + ex.Message); }
            try { ltr.LineWeight = ParseLineWeight(rule.LineWeight); } catch (System.Exception ex) { Log("ApplyLayerRule lineweight failed for " + rule.Name + ": " + ex.Message); }
            try { ltr.IsPlottable = rule.Plot; } catch (System.Exception ex) { Log("ApplyLayerRule plot failed for " + rule.Name + ": " + ex.Message); }
            try
            {
                var lineTypes = (LinetypeTable)tr.GetObject(Db.LinetypeTableId, OpenMode.ForRead);
                if (lineTypes.Has(rule.Linetype)) ltr.LinetypeObjectId = lineTypes[rule.Linetype];
            }
            catch (System.Exception ex) { Log("ApplyLayerRule linetype failed for " + rule.Name + ": " + ex.Message); }
        }

        class LayerPlanTargetGroup
        {
            public string TargetLayer;
            public int Count;
            public List<LayerStandardPlan> Plans = new List<LayerStandardPlan>();
        }

        static int SumLayerPlanCounts(List<LayerStandardPlan> plans)
        {
            int total = 0;
            foreach (var p in plans) total += p.Count;
            return total;
        }

        static List<LayerStandardPlan> SortLayerPlansByCount(List<LayerStandardPlan> plans)
        {
            var sorted = new List<LayerStandardPlan>(plans);
            sorted.Sort(delegate(LayerStandardPlan a, LayerStandardPlan b)
            {
                int byCount = b.Count.CompareTo(a.Count);
                if (byCount != 0) return byCount;
                return SafeStr(a.SourceLayer).CompareTo(SafeStr(b.SourceLayer));
            });
            return sorted;
        }

        static List<LayerPlanTargetGroup> BuildLayerPlanTargetGroups(List<LayerStandardPlan> plans)
        {
            var byTarget = new Dictionary<string, LayerPlanTargetGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in plans)
            {
                LayerPlanTargetGroup group;
                if (!byTarget.TryGetValue(p.TargetLayer, out group))
                {
                    group = new LayerPlanTargetGroup { TargetLayer = p.TargetLayer };
                    byTarget[p.TargetLayer] = group;
                }
                group.Count += p.Count;
                group.Plans.Add(p);
            }

            var groups = new List<LayerPlanTargetGroup>(byTarget.Values);
            groups.Sort(delegate(LayerPlanTargetGroup a, LayerPlanTargetGroup b)
            {
                int byCount = b.Count.CompareTo(a.Count);
                if (byCount != 0) return byCount;
                return SafeStr(a.TargetLayer).CompareTo(SafeStr(b.TargetLayer));
            });
            return groups;
        }

        static TreeNode[] BuildLayerPlanTreeNodes(List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0)
        {
            var nodes = new List<TreeNode>();
            int migrateObjects = SumLayerPlanCounts(plans);
            int fallbackObjects = SumLayerPlanCounts(fallbackPlans);
            int whitelistObjects = SumLayerPlanCounts(whitelistPlans);

            var summary = new TreeNode(string.Format("摘要：标准图层 {0} 个；将迁移 {1} 层 / {2} 对象；未识别 {3} 层 / {4} 对象；白名单 {5} 层 / {6} 对象",
                rules.Count, plans.Count, migrateObjects, fallbackPlans.Count, fallbackObjects, whitelistPlans.Count, whitelistObjects));
            nodes.Add(summary);

            var unknown = new TreeNode(string.Format("未识别图层（{0} 层 / {1} 对象，{2}）",
                fallbackPlans.Count, fallbackObjects, fallbackTo0 ? "将归到 0 层" : "保持原样"));
            foreach (var p in SortLayerPlansByCount(fallbackPlans))
            {
                string text = fallbackTo0
                    ? string.Format("{0} -> 0    {1} 对象    {2}", p.SourceLayer, p.Count, SafeStr(p.Reason))
                    : string.Format("{0}    {1} 对象    保持原样    {2}", p.SourceLayer, p.Count, SafeStr(p.Reason));
                unknown.Nodes.Add(new TreeNode(text));
            }
            unknown.Expand();
            nodes.Add(unknown);

            var migrate = new TreeNode(string.Format("将迁移图层（{0} 层 / {1} 对象）", plans.Count, migrateObjects));
            foreach (var group in BuildLayerPlanTargetGroups(plans))
            {
                var groupNode = new TreeNode(string.Format("{0}（{1} 层 / {2} 对象）", group.TargetLayer, group.Plans.Count, group.Count));
                foreach (var p in SortLayerPlansByCount(group.Plans))
                    groupNode.Nodes.Add(new TreeNode(string.Format("{0} -> {1}    {2} 对象    {3}", p.SourceLayer, p.TargetLayer, p.Count, SafeStr(p.Reason))));
                migrate.Nodes.Add(groupNode);
            }
            nodes.Add(migrate);

            var whitelist = new TreeNode(string.Format("白名单图层（{0} 层 / {1} 对象，保持原样）", whitelistPlans.Count, whitelistObjects));
            foreach (var p in SortLayerPlansByCount(whitelistPlans))
                whitelist.Nodes.Add(new TreeNode(string.Format("{0}    {1} 对象    {2}", p.SourceLayer, p.Count, SafeStr(p.Reason))));
            nodes.Add(whitelist);

            return nodes.ToArray();
        }

        static TreeNode[] BuildFilteredLayerPlanTreeNodes(List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0, LayerPlanTreeFilter filter)
        {
            var allNodes = BuildLayerPlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0);
            if (filter == LayerPlanTreeFilter.All) return allNodes;

            var nodes = new List<TreeNode>();
            nodes.Add((TreeNode)allNodes[0].Clone());
            if (filter == LayerPlanTreeFilter.Unknown) nodes.Add((TreeNode)allNodes[1].Clone());
            if (filter == LayerPlanTreeFilter.Migration) nodes.Add((TreeNode)allNodes[2].Clone());
            if (filter == LayerPlanTreeFilter.Whitelist) nodes.Add((TreeNode)allNodes[3].Clone());
            return nodes.ToArray();
        }

        static TreeNode[] BuildSearchedLayerPlanTreeNodes(List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0, LayerPlanTreeFilter filter, string searchText)
        {
            var filtered = BuildFilteredLayerPlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0, filter);
            return FilterStandardPreviewNodes(filtered, searchText);
        }

        static string FormatLayerPlanTreeReport(TreeNode[] nodes)
        {
            return FormatStandardPreviewTreeReport(nodes);
        }

        static bool NodeTextContains(TreeNode node, string needle)
        {
            if (node == null) return false;
            return SafeStr(node.Text).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string FormatLayerPlan(List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\u672c\u6b21\u4f1a\u68c0\u67e5\u5e76\u7edf\u4e00\u8fd9\u4e9b\u6807\u51c6\u56fe\u5c42\uff1a" + rules.Count + " \u4e2a");
            sb.AppendLine();
            sb.AppendLine("\u5df2\u8bc6\u522b\u7684\u65e7\u56fe\u5c42\uff08\u6267\u884c\u540e\u4f1a\u8fc1\u79fb\u5230\u5bf9\u5e94\u6807\u51c6\u56fe\u5c42\uff09\uff1a");
            if (plans.Count == 0)
            {
                sb.AppendLine("  \u6ca1\u6709\u627e\u5230\u9700\u8981\u8fc1\u79fb\u7684\u65e7\u56fe\u5c42\u3002");
            }
            else
            {
                foreach (var p in plans)
                    sb.AppendLine(string.Format("  {0}  ->  {1}    {2} \u4e2a\u5bf9\u8c61    \u539f\u56e0\uff1a{3}", p.SourceLayer, p.TargetLayer, p.Count, SafeStr(p.Reason)));
            }
            sb.AppendLine();
            sb.AppendLine("\u672a\u8bc6\u522b\u56fe\u5c42\u7684\u5904\u7406\u65b9\u5f0f\uff1a" + (fallbackTo0 ? "\u5f00\u542f\uff08\u4e0d\u5728\u767d\u540d\u5355\u91cc\u7684\u672a\u8bc6\u522b\u56fe\u5c42\u4f1a\u5f52\u5230 0 \u5c42\uff09" : "\u5173\u95ed\uff08\u672a\u8bc6\u522b\u56fe\u5c42\u4fdd\u6301\u539f\u6837\uff09"));
            if (fallbackTo0 && fallbackPlans.Count > 0)
                sb.AppendLine("  \u6ce8\u610f\uff1a\u4ee5\u4e0b\u672a\u8bc6\u522b\u4e14\u975e\u767d\u540d\u5355\u56fe\u5c42\u4f1a\u88ab\u5f52\u5230 0 \u5c42\u3002");
            if (fallbackPlans.Count == 0)
            {
                sb.AppendLine("  \u6ca1\u6709\u672a\u8bc6\u522b\u7684\u975e\u767d\u540d\u5355\u56fe\u5c42\u3002");
            }
            else
            {
                foreach (var p in fallbackPlans)
                {
                    if (fallbackTo0)
                        sb.AppendLine(string.Format("  {0}  ->  0    {1} \u4e2a\u5bf9\u8c61    \u539f\u56e0\uff1a{2}", p.SourceLayer, p.Count, SafeStr(p.Reason)));
                    else
                        sb.AppendLine(string.Format("  {0}    {1} \u4e2a\u5bf9\u8c61    \u5c06\u4fdd\u6301\u539f\u6837    \u539f\u56e0\uff1a{2}", p.SourceLayer, p.Count, SafeStr(p.Reason)));
                }
            }
            sb.AppendLine();
            sb.AppendLine("\u767d\u540d\u5355\u56fe\u5c42\uff08\u4fdd\u6301\u539f\u6837\uff0c\u4e0d\u53c2\u4e0e\u89c4\u8303\u5316\uff0c\u4e5f\u4e0d\u4f1a\u5f52\u5230 0 \u5c42\uff09\uff1a");
            if (whitelistPlans.Count == 0)
            {
                sb.AppendLine("  \u6ca1\u6709\u547d\u4e2d\u767d\u540d\u5355\u7684\u56fe\u5c42\u3002");
            }
            else
            {
                foreach (var p in whitelistPlans)
                    sb.AppendLine(string.Format("  {0}    {1} \u4e2a\u5bf9\u8c61    \u539f\u56e0\uff1a{2}", p.SourceLayer, p.Count, SafeStr(p.Reason)));
            }
            return sb.ToString();
        }

        static double _uiScaleFactor;

        static int UiScale(int value)
        {
            try
            {
                if (_uiScaleFactor <= 0)
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                        _uiScaleFactor = g.DpiX / 96.0;
                }
                return Math.Max(1, (int)Math.Round(value * _uiScaleFactor));
            }
            catch { return value; }
        }

        [CommandMethod("CT_PANEL")]
        public void ShowPanel()
        {
            EnsureInit();
            var groups = Config.GetCommandGroups();
            int totalCmds = 0;
            foreach (var g in groups) totalCmds += g.Commands.Count;
            if (totalCmds == 0)
            {
                MessageBox.Show("\u6ca1\u6709\u914d\u7f6e\u4efb\u4f55\u547d\u4ee4\u3002\n\u8bf7\u7f16\u8f91 CadToolkit.ini \u7684 [Commands] \u6bb5\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!CheckDoc()) return;

            _pendingSelection = null;
            try
            {
                string pfPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(CadCommands).Assembly.Location), "..", "pickfirst.txt");
                if (System.IO.File.Exists(pfPath))
                {
                    string handleStr = System.IO.File.ReadAllText(pfPath).Trim();
                    try { System.IO.File.Delete(pfPath); } catch (System.Exception ex) { Log("Delete pickfirst.txt failed: " + ex.Message); }
                    if (!string.IsNullOrEmpty(handleStr))
                    {
                        string[] handles = handleStr.Split(new char[] { ',' });
                        var ids = new List<ObjectId>();
                        foreach (string h in handles)
                        {
                            try
                            {
                                long val = long.Parse(h.Trim(), System.Globalization.NumberStyles.HexNumber);
                                ObjectId oid = Db.GetObjectId(false, new Handle(val), 0);
                                if (oid.IsValid) ids.Add(oid);
                            }
                            catch (System.Exception ex) { Log("Invalid pickfirst handle skipped: " + ex.Message); }
                        }
                        if (ids.Count > 0) _pendingSelection = ids.ToArray();
                    }
                }
            }
            catch (System.Exception ex) { Log("Read pickfirst selection failed: " + ex.Message); }

            var action = PanelBuilder.Show("CadToolkit - " + PlatformName, Config.Version + " | WLUP", groups);

            if (action == null) return;
            if (action.Kind == "CMD")
            {
                string cmdName = action.CommandName;
                System.EventHandler idle = null;
                idle = delegate(object sender, System.EventArgs ea)
                {
                    try { CadApp.Idle -= idle; } catch {}
                    CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute(cmdName + " ", true, false, true);
                };
                CadApp.Idle += idle;
            }
            else if (action.Kind == "ADD")
            {
                using (var dlg = new AddCommandDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK && dlg.CmdLabel != null && dlg.CmdLabel.Length > 0 && dlg.CmdName != null && dlg.CmdName.Length > 0)
                        Config.SaveCommand(dlg.CmdLabel, dlg.CmdName);
                }
            }
            else if (action.Kind == "MANAGE")
            {
                using (var dlg = new ManageCommandsDialog()) { dlg.ShowDialog(); }
            }
        }

        static int ReplaceInBlock(Transaction tr, BlockTableRecord btr, string findText, string replaceText, StringComparison cmp)
        {
            int count = 0;
            foreach (ObjectId id in btr)
            {
                var obj = tr.GetObject(id, OpenMode.ForRead);
                if (obj is DBText)
                {
                    var dt = (DBText)obj;
                    string txt = SafeStr(dt.TextString);
                    string ntxt = ReplaceEx(txt, findText, replaceText, cmp);
                    if (ntxt != txt) { dt.UpgradeOpen(); dt.TextString = ntxt; count++; }
                }
                else if (obj is MText)
                {
                    var mt = (MText)obj;
                    string txt = SafeStr(mt.Contents);
                    string ntxt = ReplaceEx(txt, findText, replaceText, cmp);
                    if (ntxt != txt) { mt.UpgradeOpen(); mt.Contents = ntxt; count++; }
                }
                else if (obj is BlockReference)
                {
                    var br = (BlockReference)obj;
                    if (br.AttributeCollection.Count > 0)
                    {
                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                            if (att == null) continue;
                            string txt = SafeStr(att.TextString);
                            string ntxt = ReplaceEx(txt, findText, replaceText, cmp);
                            if (ntxt != txt) { att.UpgradeOpen(); att.TextString = ntxt; count++; }
                        }
                    }
                }
            }
            return count;
        }
        static void SetBlockLayer0(Transaction tr, ObjectId btrId)
        {
            int count = 0;
            var btr = tr.GetObject(btrId, OpenMode.ForWrite) as BlockTableRecord;
            if (btr == null) return;
            foreach (ObjectId entId in btr)
            {
                var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (ent is BlockReference)
                {
                    var innerBr = (BlockReference)ent;
                    innerBr.UpgradeOpen();
                    innerBr.Layer = "0";
                    innerBr.ColorIndex = 256;
                    count++;
                    if (innerBr.BlockTableRecord.IsValid)
                        SetBlockLayer0(tr, innerBr.BlockTableRecord);
                }
                else
                {
                    ent.UpgradeOpen();
                    ent.Layer = "0";
                    ent.ColorIndex = 256;
                    count++;
                }
            }
            return;
        }
        // ========== Z轴归零 ==========
        }
}







