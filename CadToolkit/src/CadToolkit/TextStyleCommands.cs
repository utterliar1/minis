using System;
using System.Drawing;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using CadToolkit.Core;
using TextStyleMapRule = CadToolkit.Core.Config.TextStyleMapRule;
using TextStyleStandardRule = CadToolkit.Core.Config.TextStyleStandardRule;

#if AUTOCAD
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
#elif GSTARCAD
using GrxCAD.DatabaseServices;
using GrxCAD.Runtime;
#elif ZWCAD
using ZwSoft.ZwCAD.DatabaseServices;
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

            var standards = Config.GetTextStyleStandards();
            if (standards.Count == 0)
            {
                Ed.WriteMessage("\n未配置 [TextStyleStandard] 文字规范。");
                return;
            }

            bool processCurrentSpace = true;
            bool processAttributes = false;
            bool processBlockDefinitions = false;
            bool fallbackToStandard = Config.TextStyleFallbackToStandard;
            bool normalizeHeight = Config.TextStyleNormalizeHeight;
            bool normalizeWidthFactor = Config.TextStyleNormalizeWidthFactor;
            bool normalizeOblique = Config.TextStyleNormalizeOblique;
            bool normalizeColorByLayer = Config.TextStyleNormalizeColorByLayer;
            bool deleteUnused = Config.TextStyleDeleteUnusedOldStyles;
            string fallbackStyle = Config.TextStyleFallbackStyle;
            var mapRules = Config.GetTextStyleMapRules();
            var preview = BuildTextStyleStandardPlans(standards, mapRules, Config.TextStyleWhitelist, processCurrentSpace, processAttributes, processBlockDefinitions);
            var plans = preview.Plans;
            var fallbackPlans = preview.FallbackPlans;
            var whitelistPlans = preview.WhitelistPlans;

            var f = CreateStandardPreviewForm("文字规范");
            TextStylePlanTreeFilter previewFilter = TextStylePlanTreeFilter.All;

            var filters = CreateStandardPreviewFilterControls("全部", "未识别", "将归并", "白名单", "搜索");
            var rbAll = filters.All;
            var rbUnknown = filters.Unknown;
            var rbMigration = filters.Migration;
            var rbWhitelist = filters.Whitelist;
            var search = filters.Search;
            var tree = CreateStandardPreviewTree(318);
            BuildTextStylePlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, standards, fallbackToStandard, fallbackStyle, previewFilter, search.Text);

            var lblScope = new Label();
            lblScope.Text = "处理范围";
            lblScope.Left = UiScale(12); lblScope.Top = UiScale(372); lblScope.Width = UiScale(78); lblScope.Height = UiScale(24);
            lblScope.Font = new System.Drawing.Font("Microsoft YaHei", 9f, FontStyle.Bold);

            var chkCurrentSpace = new CheckBox();
            chkCurrentSpace.Text = "处理当前空间文字";
            chkCurrentSpace.Left = UiScale(96); chkCurrentSpace.Top = UiScale(372); chkCurrentSpace.Width = UiScale(150); chkCurrentSpace.Height = UiScale(24); chkCurrentSpace.Checked = true;
            chkCurrentSpace.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkAttributes = new CheckBox();
            chkAttributes.Text = "处理块参照属性";
            chkAttributes.Left = UiScale(252); chkAttributes.Top = UiScale(372); chkAttributes.Width = UiScale(135); chkAttributes.Height = UiScale(24); chkAttributes.Checked = false;
            chkAttributes.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkBlockDefinitions = new CheckBox();
            chkBlockDefinitions.Text = "处理块定义内部文字";
            chkBlockDefinitions.Left = UiScale(394); chkBlockDefinitions.Top = UiScale(372); chkBlockDefinitions.Width = UiScale(170); chkBlockDefinitions.Height = UiScale(24); chkBlockDefinitions.Checked = false;
            chkBlockDefinitions.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var lblMerge = new Label();
            lblMerge.Text = "归并清理";
            lblMerge.Left = UiScale(12); lblMerge.Top = UiScale(400); lblMerge.Width = UiScale(78); lblMerge.Height = UiScale(24);
            lblMerge.Font = new System.Drawing.Font("Microsoft YaHei", 9f, FontStyle.Bold);

            var chkFallback = new CheckBox();
            chkFallback.Text = "未识别文字样式归到标准样式";
            chkFallback.Left = UiScale(96); chkFallback.Top = UiScale(400); chkFallback.Width = UiScale(235); chkFallback.Height = UiScale(24); chkFallback.Checked = fallbackToStandard;
            chkFallback.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var lblAppearance = new Label();
            lblAppearance.Text = "外观同步";
            lblAppearance.Left = UiScale(12); lblAppearance.Top = UiScale(428); lblAppearance.Width = UiScale(78); lblAppearance.Height = UiScale(24);
            lblAppearance.Font = new System.Drawing.Font("Microsoft YaHei", 9f, FontStyle.Bold);

            var chkHeight = new CheckBox();
            chkHeight.Text = "固定字高";
            chkHeight.Left = UiScale(96); chkHeight.Top = UiScale(428); chkHeight.Width = UiScale(84); chkHeight.Height = UiScale(24); chkHeight.Checked = Config.TextStyleNormalizeHeight;
            chkHeight.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkWidthFactor = new CheckBox();
            chkWidthFactor.Text = "宽度因子";
            chkWidthFactor.Left = UiScale(190); chkWidthFactor.Top = UiScale(428); chkWidthFactor.Width = UiScale(90); chkWidthFactor.Height = UiScale(24); chkWidthFactor.Checked = Config.TextStyleNormalizeWidthFactor;
            chkWidthFactor.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkOblique = new CheckBox();
            chkOblique.Text = "倾斜角";
            chkOblique.Left = UiScale(286); chkOblique.Top = UiScale(428); chkOblique.Width = UiScale(72); chkOblique.Height = UiScale(24); chkOblique.Checked = Config.TextStyleNormalizeOblique;
            chkOblique.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkColorByLayer = new CheckBox();
            chkColorByLayer.Text = "颜色 ByLayer";
            chkColorByLayer.Left = UiScale(368); chkColorByLayer.Top = UiScale(428); chkColorByLayer.Width = UiScale(115); chkColorByLayer.Height = UiScale(24); chkColorByLayer.Checked = Config.TextStyleNormalizeColorByLayer;
            chkColorByLayer.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var chkDeleteUnused = new CheckBox();
            chkDeleteUnused.Text = "删除未使用旧文字样式";
            chkDeleteUnused.Left = UiScale(340); chkDeleteUnused.Top = UiScale(400); chkDeleteUnused.Width = UiScale(205); chkDeleteUnused.Height = UiScale(24); chkDeleteUnused.Checked = Config.TextStyleDeleteUnusedOldStyles;
            chkDeleteUnused.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            EventHandler refreshPreview = delegate
            {
                if (rbUnknown.Checked) previewFilter = TextStylePlanTreeFilter.Unknown;
                else if (rbMigration.Checked) previewFilter = TextStylePlanTreeFilter.Migration;
                else if (rbWhitelist.Checked) previewFilter = TextStylePlanTreeFilter.Whitelist;
                else previewFilter = TextStylePlanTreeFilter.All;
                preview = BuildTextStyleStandardPlans(standards, mapRules, Config.TextStyleWhitelist, chkCurrentSpace.Checked, chkAttributes.Checked, chkBlockDefinitions.Checked);
                plans = preview.Plans;
                fallbackPlans = preview.FallbackPlans;
                whitelistPlans = preview.WhitelistPlans;
                BuildTextStylePlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, standards, chkFallback.Checked, fallbackStyle, previewFilter, search.Text);
            };
            rbAll.CheckedChanged += refreshPreview;
            rbUnknown.CheckedChanged += refreshPreview;
            rbMigration.CheckedChanged += refreshPreview;
            rbWhitelist.CheckedChanged += refreshPreview;
            search.TextChanged += refreshPreview;
            chkFallback.CheckedChanged += refreshPreview;
            chkCurrentSpace.CheckedChanged += refreshPreview;
            chkAttributes.CheckedChanged += refreshPreview;
            chkBlockDefinitions.CheckedChanged += refreshPreview;

            var copy = CreateStandardPreviewButton("复制当前", 336, 88, DialogResult.None);
            copy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(FormatTextStylePlanTreeReport(BuildSearchedTextStylePlanTreeNodes(plans, fallbackPlans, whitelistPlans, standards, chkFallback.Checked, fallbackStyle, previewFilter, search.Text)));
                    MessageBox.Show("已复制当前视图。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("复制当前视图失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            var ok = CreateStandardPreviewButton("执行", 432, 80, DialogResult.OK);
            var cancel = CreateStandardPreviewButton("取消", 528, 80, DialogResult.Cancel);

            f.Controls.AddRange(new Control[] { rbAll, rbUnknown, rbMigration, rbWhitelist, filters.SearchLabel, search, tree, lblScope, chkCurrentSpace, chkAttributes, chkBlockDefinitions, lblMerge, chkFallback, lblAppearance, chkHeight, chkWidthFactor, chkOblique, chkColorByLayer, chkDeleteUnused, copy, ok, cancel });
            f.AcceptButton = ok; f.CancelButton = cancel;
            if (f.ShowDialog() != DialogResult.OK) { f.Dispose(); return; }
            processCurrentSpace = chkCurrentSpace.Checked;
            processAttributes = chkAttributes.Checked;
            processBlockDefinitions = chkBlockDefinitions.Checked;
            fallbackToStandard = chkFallback.Checked;
            normalizeHeight = chkHeight.Checked;
            normalizeWidthFactor = chkWidthFactor.Checked;
            normalizeOblique = chkOblique.Checked;
            normalizeColorByLayer = chkColorByLayer.Checked;
            deleteUnused = chkDeleteUnused.Checked;
            f.Dispose();
            if (!ConfirmTextStyleRiskOptions(fallbackToStandard, fallbackPlans.Count, fallbackStyle, processBlockDefinitions, normalizeHeight, normalizeWidthFactor, normalizeOblique, normalizeColorByLayer, deleteUnused)) return;

            int changedCount = 0, failed = 0, deleted = 0;
            bool changed = RunWithUndo("CT_TEXTSTYLESTANDARD", delegate
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var textStyles = (TextStyleTable)tr.GetObject(Db.TextStyleTableId, OpenMode.ForRead);
                    var standardByName = new Dictionary<string, TextStyleStandardRule>(StringComparer.OrdinalIgnoreCase);
                    foreach (var rule in standards)
                    {
                        standardByName[rule.Name] = rule;
                        ApplyTextStyleRule(tr, textStyles, rule);
                    }

                    var targetBySource = new Dictionary<string, TextStyleStandardRule>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in plans)
                        if (standardByName.ContainsKey(p.TargetStyle)) targetBySource[p.SourceStyle] = standardByName[p.TargetStyle];
                    if (fallbackToStandard && standardByName.ContainsKey(fallbackStyle))
                        foreach (var p in fallbackPlans) targetBySource[p.SourceStyle] = standardByName[fallbackStyle];

                    changedCount += ApplyTextStyleStandard(tr, targetBySource, processCurrentSpace, processAttributes, processBlockDefinitions, normalizeHeight, normalizeWidthFactor, normalizeOblique, normalizeColorByLayer, ref failed);
                    if (deleteUnused)
                        deleted += DeleteUnusedOldTextStyles(tr, textStyles, targetBySource, standards, Config.TextStyleWhitelist);
                    tr.Commit();
                }
                return true;
            });
            if (!changed) return;

            Ed.WriteMessage(string.Format("\n文字规范完成：处理 {0} 个对象，失败 {1} 个，删除未使用旧文字样式 {2} 个。", changedCount, failed, deleted));
        }

        class TextStyleStandardPlan
        {
            public string SourceStyle;
            public string TargetStyle;
            public int Count;
            public TextStyleMapRule Rule;
            public string Reason;
        }

        class TextStylePlanTargetGroup
        {
            public string TargetStyle;
            public int Count;
            public List<TextStyleStandardPlan> Plans = new List<TextStyleStandardPlan>();
        }

        enum TextStylePlanTreeFilter
        {
            All,
            Unknown,
            Migration,
            Whitelist
        }

        class TextStyleMatchDetail
        {
            public TextStyleMapRule Rule;
            public string Pattern;
            public string MatchMode;
            public bool IsStandardName;
        }

        class TextStylePatternMatch
        {
            public string Pattern;
            public string MatchMode;
        }

        class TextStylePlanResult
        {
            public List<TextStyleStandardPlan> Plans = new List<TextStyleStandardPlan>();
            public List<TextStyleStandardPlan> FallbackPlans = new List<TextStyleStandardPlan>();
            public List<TextStyleStandardPlan> WhitelistPlans = new List<TextStyleStandardPlan>();
        }

        static TextStylePlanResult BuildTextStyleStandardPlans(List<TextStyleStandardRule> standards, List<TextStyleMapRule> mapRules, string whitelist, bool currentSpace, bool attributes, bool blockDefinitions)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                if (currentSpace) CountCurrentSpaceTextStyles(tr, counts, attributes);
                if (blockDefinitions) CountBlockDefinitionTextStyles(tr, counts);
                tr.Commit();
            }

            var result = new TextStylePlanResult();
            foreach (var pair in counts)
            {
                if (IsStandardTextStyle(pair.Key, standards)) continue;
                var whitelistMatch = MatchTextStyleWhitelistPattern(pair.Key, whitelist);
                if (whitelistMatch != null)
                {
                    result.WhitelistPlans.Add(new TextStyleStandardPlan { SourceStyle = pair.Key, TargetStyle = "", Count = pair.Value, Rule = null, Reason = FormatTextStyleWhitelistReason(whitelistMatch) });
                    continue;
                }

                var match = MatchTextStyleMapDetail(pair.Key, mapRules);
                if (match == null)
                {
                    result.FallbackPlans.Add(new TextStyleStandardPlan { SourceStyle = pair.Key, TargetStyle = Config.TextStyleFallbackStyle, Count = pair.Value, Rule = null, Reason = "未识别且未命中白名单" });
                    continue;
                }

                if (pair.Key.Equals(match.Rule.TargetStyle, StringComparison.OrdinalIgnoreCase)) continue;
                result.Plans.Add(new TextStyleStandardPlan { SourceStyle = pair.Key, TargetStyle = match.Rule.TargetStyle, Count = pair.Value, Rule = match.Rule, Reason = FormatTextStyleRuleReason(match) });
            }
            result.Plans.Sort(delegate(TextStyleStandardPlan a, TextStyleStandardPlan b) { return SafeStr(a.TargetStyle).CompareTo(SafeStr(b.TargetStyle)); });
            result.FallbackPlans.Sort(delegate(TextStyleStandardPlan a, TextStyleStandardPlan b) { return SafeStr(a.SourceStyle).CompareTo(SafeStr(b.SourceStyle)); });
            result.WhitelistPlans.Sort(delegate(TextStyleStandardPlan a, TextStyleStandardPlan b) { return SafeStr(a.SourceStyle).CompareTo(SafeStr(b.SourceStyle)); });
            return result;
        }

        static void CountCurrentSpaceTextStyles(Transaction tr, Dictionary<string, int> counts, bool includeAttributes)
        {
            var btr = tr.GetObject(Db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return;
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                AddTextStyleCount(counts, GetEntityTextStyleName(ent));
                if (includeAttributes) CountBlockReferenceAttributesTextStyles(tr, ent as BlockReference, counts);
            }
        }

        static void CountBlockReferenceAttributesTextStyles(Transaction tr, BlockReference br, Dictionary<string, int> counts)
        {
            if (br == null || br.AttributeCollection == null) return;
            try
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    AddTextStyleCount(counts, GetEntityTextStyleName(att));
                }
            }
            catch (System.Exception ex) { Log("Count text style block attributes failed: " + ex.Message); }
        }

        static void CountBlockDefinitionTextStyles(Transaction tr, Dictionary<string, int> counts)
        {
            var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null || IsSkippedTextStyleBlockRecord(btr)) continue;
                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    AddTextStyleCount(counts, GetEntityTextStyleName(ent));
                }
            }
        }

        static void AddTextStyleCount(Dictionary<string, int> counts, string styleName)
        {
            if (string.IsNullOrEmpty(styleName)) return;
            if (!counts.ContainsKey(styleName)) counts[styleName] = 0;
            counts[styleName]++;
        }

        static TextStyleMatchDetail MatchTextStyleMapDetail(string styleName, List<TextStyleMapRule> rules)
        {
            foreach (var rule in rules)
            {
                if (SafeStr(rule.TargetStyle).Equals(SafeStr(styleName), StringComparison.OrdinalIgnoreCase))
                    return new TextStyleMatchDetail { Rule = rule, Pattern = rule.TargetStyle, MatchMode = GetTextStylePatternMatchMode(rule.TargetStyle), IsStandardName = true };
            }

            foreach (var rule in rules)
            {
                foreach (string alias in rule.Aliases)
                {
                    if (SafeStr(alias).Length == 0) continue;
                    if (PatternMatchesTextStyle(styleName, alias))
                        return new TextStyleMatchDetail { Rule = rule, Pattern = alias, MatchMode = GetTextStylePatternMatchMode(alias), IsStandardName = false };
                }
            }
            return null;
        }

        static TextStylePatternMatch MatchTextStyleWhitelistPattern(string styleName, string whitelist)
        {
            string[] items = SafeStr(whitelist).Split(',');
            foreach (string item in items)
            {
                string pattern = item.Trim();
                if (pattern.Length == 0) continue;
                if (PatternMatchesTextStyle(styleName, pattern))
                    return new TextStylePatternMatch { Pattern = pattern, MatchMode = GetTextStylePatternMatchMode(pattern) };
            }
            return null;
        }

        static bool PatternMatchesTextStyle(string styleName, string pattern)
        {
            if (string.IsNullOrEmpty(styleName) || string.IsNullOrEmpty(pattern)) return false;
            return SimpleWildcardMatch(styleName, pattern);
        }

        static string FormatTextStyleRuleReason(TextStyleMatchDetail match)
        {
            if (match == null) return "";
            if (match.IsStandardName) return "命中标准文字样式名";
            return string.Format("命中别名 \"{0}\"（{1}）", match.Pattern, match.MatchMode);
        }

        static string FormatTextStyleWhitelistReason(TextStylePatternMatch match)
        {
            if (match == null) return "";
            return string.Format("命中白名单 \"{0}\"（{1}）", match.Pattern, match.MatchMode);
        }

        static string GetTextStylePatternMatchMode(string pattern)
        {
            return SafeStr(pattern).IndexOf('*') >= 0 ? "通配匹配" : "全字匹配";
        }

        static int SumTextStylePlanCounts(List<TextStyleStandardPlan> plans)
        {
            int total = 0;
            foreach (var p in plans) total += p.Count;
            return total;
        }

        static List<TextStyleStandardPlan> SortTextStylePlansByCount(List<TextStyleStandardPlan> plans)
        {
            var sorted = new List<TextStyleStandardPlan>(plans);
            sorted.Sort(delegate(TextStyleStandardPlan a, TextStyleStandardPlan b)
            {
                int byCount = b.Count.CompareTo(a.Count);
                if (byCount != 0) return byCount;
                return SafeStr(a.SourceStyle).CompareTo(SafeStr(b.SourceStyle));
            });
            return sorted;
        }

        static List<TextStylePlanTargetGroup> BuildTextStylePlanTargetGroups(List<TextStyleStandardPlan> plans)
        {
            var byTarget = new Dictionary<string, TextStylePlanTargetGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in plans)
            {
                TextStylePlanTargetGroup group;
                if (!byTarget.TryGetValue(p.TargetStyle, out group))
                {
                    group = new TextStylePlanTargetGroup { TargetStyle = p.TargetStyle };
                    byTarget[p.TargetStyle] = group;
                }
                group.Count += p.Count;
                group.Plans.Add(p);
            }

            var groups = new List<TextStylePlanTargetGroup>(byTarget.Values);
            groups.Sort(delegate(TextStylePlanTargetGroup a, TextStylePlanTargetGroup b)
            {
                int byCount = b.Count.CompareTo(a.Count);
                if (byCount != 0) return byCount;
                return SafeStr(a.TargetStyle).CompareTo(SafeStr(b.TargetStyle));
            });
            return groups;
        }

        static Dictionary<string, TextStyleStandardRule> BuildTextStyleRuleLookup(List<TextStyleStandardRule> rules)
        {
            var lookup = new Dictionary<string, TextStyleStandardRule>(StringComparer.OrdinalIgnoreCase);
            if (rules == null) return lookup;
            foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.Name)) continue;
                lookup[rule.Name] = rule;
            }
            return lookup;
        }

        static string FormatTextStyleRuleDetail(TextStyleStandardRule rule)
        {
            if (rule == null) return "";
            return string.Format("字体 {0} + {1}，字高 {2}，宽度 {3}，倾斜 {4}",
                string.IsNullOrEmpty(rule.FontFile) ? "未指定" : rule.FontFile,
                string.IsNullOrEmpty(rule.BigFontFile) ? "无大字体" : rule.BigFontFile,
                FormatTextStyleDouble(rule.FixedHeight),
                FormatTextStyleDouble(rule.WidthFactor),
                FormatTextStyleDouble(rule.ObliqueAngle));
        }

        static string FormatTextStyleTargetLabel(string targetStyle, Dictionary<string, TextStyleStandardRule> ruleByName)
        {
            TextStyleStandardRule rule;
            if (ruleByName != null && ruleByName.TryGetValue(SafeStr(targetStyle), out rule))
                return string.Format("{0}（{1}）", targetStyle, FormatTextStyleRuleDetail(rule));
            return SafeStr(targetStyle);
        }

        static string FormatTextStyleDouble(double value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        static TreeNode[] BuildTextStylePlanTreeNodes(List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle)
        {
            return BuildStandardPreviewTreeNodes(BuildTextStyleStandardPreviewModel(plans, fallbackPlans, whitelistPlans, rules, fallbackToStandard, fallbackStyle), true);
        }

        static TreeNode[] BuildFilteredTextStylePlanTreeNodes(List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle, TextStylePlanTreeFilter filter)
        {
            return BuildFilteredStandardPreviewTreeNodes(BuildTextStyleStandardPreviewModel(plans, fallbackPlans, whitelistPlans, rules, fallbackToStandard, fallbackStyle), (int)filter, true);
        }

        static TreeNode[] BuildSearchedTextStylePlanTreeNodes(List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle, TextStylePlanTreeFilter filter, string searchText)
        {
            return BuildSearchedStandardPreviewTreeNodes(BuildTextStyleStandardPreviewModel(plans, fallbackPlans, whitelistPlans, rules, fallbackToStandard, fallbackStyle), (int)filter, searchText, true);
        }

        static string FormatTextStylePlanTreeReport(TreeNode[] nodes)
        {
            return FormatStandardPreviewTreeReport(nodes);
        }

        static StandardPreviewModel BuildTextStyleStandardPreviewModel(List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle)
        {
            if (plans == null) plans = new List<TextStyleStandardPlan>();
            if (fallbackPlans == null) fallbackPlans = new List<TextStyleStandardPlan>();
            if (whitelistPlans == null) whitelistPlans = new List<TextStyleStandardPlan>();
            if (rules == null) rules = new List<TextStyleStandardRule>();

            int migrateObjects = SumTextStylePlanCounts(plans);
            int fallbackObjects = SumTextStylePlanCounts(fallbackPlans);
            int whitelistObjects = SumTextStylePlanCounts(whitelistPlans);
            string targetFallback = SafeStr(fallbackStyle);
            var ruleByName = BuildTextStyleRuleLookup(rules);
            string targetFallbackLabel = FormatTextStyleTargetLabel(targetFallback, ruleByName);

            var model = new StandardPreviewModel();
            model.SummaryText = string.Format("摘要：标准文字样式 {0} 个；将归并 {1} 样式 / {2} 对象；未识别 {3} 样式 / {4} 对象；白名单 {5} 样式 / {6} 对象",
                rules.Count, plans.Count, migrateObjects, fallbackPlans.Count, fallbackObjects, whitelistPlans.Count, whitelistObjects);
            model.UnknownTitle = string.Format("未识别文字样式（{0} 样式 / {1} 对象，{2}）",
                fallbackPlans.Count, fallbackObjects, fallbackToStandard ? "将归到 " + targetFallbackLabel : "保持原样");
            model.MigrationTitle = string.Format("将归并文字样式（{0} 样式 / {1} 对象）", plans.Count, migrateObjects);
            model.WhitelistTitle = string.Format("白名单文字样式（{0} 样式 / {1} 对象，保持原样）", whitelistPlans.Count, whitelistObjects);
            model.UnknownMovesToTarget = fallbackToStandard;
            model.UnknownTargetText = targetFallback;

            foreach (var p in fallbackPlans)
                model.UnknownItems.Add(new StandardPreviewItem { SourceText = p.SourceStyle, TargetText = targetFallback, TargetLabel = targetFallbackLabel, Count = p.Count, Reason = p.Reason });
            foreach (var p in plans)
                model.MigrationItems.Add(new StandardPreviewItem { SourceText = p.SourceStyle, TargetText = p.TargetStyle, TargetLabel = FormatTextStyleTargetLabel(p.TargetStyle, ruleByName), Count = p.Count, Reason = p.Reason });
            foreach (var p in whitelistPlans)
                model.WhitelistItems.Add(new StandardPreviewItem { SourceText = p.SourceStyle, Count = p.Count, Reason = p.Reason });

            return model;
        }

        static string BuildTextStyleRiskWarning(bool fallbackToStandard, int fallbackStyleCount, string fallbackStyle, bool blockDefinitions, bool normalizeHeight, bool normalizeWidthFactor, bool normalizeOblique, bool colorByLayer, bool deleteUnused)
        {
            var lines = new List<string>();
            if (fallbackToStandard && fallbackStyleCount > 0)
                lines.Add(string.Format("未识别文字样式将被归到标准样式：{0}（{1} 个样式）", SafeStr(fallbackStyle), fallbackStyleCount));
            if (blockDefinitions)
                lines.Add("将修改块定义内部文字，可能影响同名块的所有参照。");
            if (normalizeHeight || normalizeWidthFactor || normalizeOblique || colorByLayer)
                lines.Add("将同步文字外观参数，可能改变字高、宽度因子、倾斜角或颜色。");
            if (deleteUnused)
                lines.Add("将删除未使用旧文字样式，建议确认已备份图纸。");
            if (lines.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine("本次文字规范包含高风险操作：");
            foreach (var line in lines) sb.AppendLine("- " + line);
            sb.AppendLine();
            sb.Append("确认继续执行？");
            return sb.ToString();
        }

        static bool ConfirmTextStyleRiskOptions(bool fallbackToStandard, int fallbackStyleCount, string fallbackStyle, bool blockDefinitions, bool normalizeHeight, bool normalizeWidthFactor, bool normalizeOblique, bool colorByLayer, bool deleteUnused)
        {
            string warning = BuildTextStyleRiskWarning(fallbackToStandard, fallbackStyleCount, fallbackStyle, blockDefinitions, normalizeHeight, normalizeWidthFactor, normalizeOblique, colorByLayer, deleteUnused);
            if (warning.Length == 0) return true;
            return MessageBox.Show(warning, "文字规范确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
        }

        static void BuildTextStylePlanTreePreview(TreeView tree, List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle, TextStylePlanTreeFilter filter, string searchText)
        {
            UpdateStandardPreviewTree(tree, BuildSearchedTextStylePlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackToStandard, fallbackStyle, filter, searchText), filter != TextStylePlanTreeFilter.All || SafeStr(searchText).Trim().Length > 0);
        }

        static int ApplyTextStyleStandard(Transaction tr, Dictionary<string, TextStyleStandardRule> targetBySource, bool currentSpace, bool attributes, bool blockDefinitions, bool normalizeHeight, bool normalizeWidthFactor, bool normalizeOblique, bool colorByLayer, ref int failed)
        {
            int changed = 0;
            if (currentSpace)
            {
                var btr = tr.GetObject(Db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
                changed += ApplyTextStyleStandardInBlock(tr, btr, targetBySource, attributes, normalizeHeight, normalizeWidthFactor, normalizeOblique, colorByLayer, ref failed);
            }
            if (blockDefinitions)
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId btrId in bt)
                {
                    var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                    if (btr == null || IsSkippedTextStyleBlockRecord(btr)) continue;
                    changed += ApplyTextStyleStandardInBlockDefinition(tr, btr, targetBySource, normalizeHeight, normalizeWidthFactor, normalizeOblique, colorByLayer, ref failed);
                }
            }
            return changed;
        }

        static int ApplyTextStyleStandardInBlock(Transaction tr, BlockTableRecord btr, Dictionary<string, TextStyleStandardRule> targetBySource, bool includeAttributes, bool normalizeHeight, bool normalizeWidthFactor, bool normalizeOblique, bool colorByLayer, ref int failed)
        {
            if (btr == null) return 0;
            int changed = 0;
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ApplyTextStyleEntity(tr, ent, targetBySource, normalizeHeight, normalizeWidthFactor, normalizeOblique, colorByLayer, ref failed)) changed++;
                if (includeAttributes) changed += ApplyBlockReferenceAttributeTextStyles(tr, ent as BlockReference, targetBySource, normalizeHeight, normalizeWidthFactor, normalizeOblique, colorByLayer, ref failed);
            }
            return changed;
        }

        static int ApplyBlockReferenceAttributeTextStyles(Transaction tr, BlockReference br, Dictionary<string, TextStyleStandardRule> targetBySource, bool normalizeHeight, bool normalizeWidthFactor, bool normalizeOblique, bool colorByLayer, ref int failed)
        {
            if (br == null || br.AttributeCollection == null) return 0;
            int changed = 0;
            try
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (ApplyTextStyleEntity(tr, att, targetBySource, normalizeHeight, normalizeWidthFactor, normalizeOblique, colorByLayer, ref failed)) changed++;
                }
            }
            catch (System.Exception ex) { Log("Apply text style block attributes failed: " + ex.Message); }
            return changed;
        }

        static int ApplyTextStyleStandardInBlockDefinition(Transaction tr, BlockTableRecord btr, Dictionary<string, TextStyleStandardRule> targetBySource, bool normalizeHeight, bool normalizeWidthFactor, bool normalizeOblique, bool colorByLayer, ref int failed)
        {
            if (btr == null) return 0;
            int changed = 0;
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ApplyTextStyleEntity(tr, ent, targetBySource, normalizeHeight, normalizeWidthFactor, normalizeOblique, colorByLayer, ref failed)) changed++;
            }
            return changed;
        }

        static bool ApplyTextStyleEntity(Transaction tr, Entity text, Dictionary<string, TextStyleStandardRule> targetBySource, bool normalizeHeight, bool normalizeWidthFactor, bool normalizeOblique, bool colorByLayer, ref int failed)
        {
            if (!(text is DBText) && !(text is MText)) return false;
            string source = GetEntityTextStyleName(text);
            TextStyleStandardRule rule;
            if (!targetBySource.TryGetValue(source, out rule)) return false;
            try
            {
                ObjectId targetId = EnsureTextStyleRecord(tr, rule);
                text.UpgradeOpen();
                if (colorByLayer) text.ColorIndex = 256;

                var dt = text as DBText;
                if (dt != null)
                {
                    dt.TextStyleId = targetId;
                    if (normalizeHeight && rule.FixedHeight > 0) dt.Height = rule.FixedHeight;
                    if (normalizeWidthFactor && rule.WidthFactor > 0) dt.WidthFactor = rule.WidthFactor;
                    if (normalizeOblique) SetDoubleProperty(dt, "Oblique", rule.ObliqueAngle);
                }
                var mt = text as MText;
                if (mt != null)
                {
                    mt.TextStyleId = targetId;
                    if (normalizeHeight && rule.FixedHeight > 0) mt.TextHeight = rule.FixedHeight;
                    if (normalizeWidthFactor && rule.WidthFactor > 0) SetDoubleProperty(mt, "WidthFactor", rule.WidthFactor);
                    if (normalizeOblique) SetDoubleProperty(mt, "Oblique", rule.ObliqueAngle);
                }
                return true;
            }
            catch (System.Exception ex)
            {
                failed++;
                Log("Apply text style standard failed: " + ex.Message);
                return false;
            }
        }

        static ObjectId EnsureTextStyleRecord(Transaction tr, TextStyleStandardRule rule)
        {
            var textStyles = (TextStyleTable)tr.GetObject(Db.TextStyleTableId, OpenMode.ForRead);
            ApplyTextStyleRule(tr, textStyles, rule);
            return textStyles[rule.Name];
        }

        static void ApplyTextStyleRule(Transaction tr, TextStyleTable textStyles, TextStyleStandardRule rule)
        {
            TextStyleTableRecord record;
            if (textStyles.Has(rule.Name))
            {
                record = (TextStyleTableRecord)tr.GetObject(textStyles[rule.Name], OpenMode.ForWrite);
            }
            else
            {
                textStyles.UpgradeOpen();
                record = new TextStyleTableRecord();
                record.Name = rule.Name;
                textStyles.Add(record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
            SetTextStyleRecordProperties(record, rule);
        }

        static void SetTextStyleRecordProperties(TextStyleTableRecord record, TextStyleStandardRule rule)
        {
            if (record == null || rule == null) return;
            if (!string.IsNullOrEmpty(rule.FontFile)) SetStringProperty(record, "FileName", rule.FontFile);
            if (!string.IsNullOrEmpty(rule.BigFontFile)) SetStringProperty(record, "BigFontFileName", rule.BigFontFile);
            SetDoubleProperty(record, "TextSize", rule.FixedHeight);
            if (rule.WidthFactor > 0) SetDoubleProperty(record, "XScale", rule.WidthFactor);
            SetDoubleProperty(record, "ObliquingAngle", rule.ObliqueAngle);
        }

        static int DeleteUnusedOldTextStyles(Transaction tr, TextStyleTable textStyles, Dictionary<string, TextStyleStandardRule> targetBySource, List<TextStyleStandardRule> standards, string whitelist)
        {
            int deleted = 0;
            foreach (ObjectId styleId in textStyles)
            {
                try
                {
                    var record = tr.GetObject(styleId, OpenMode.ForRead) as TextStyleTableRecord;
                    if (record == null || string.IsNullOrEmpty(record.Name)) continue;
                    if (record.Name.Equals("Standard", StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsStandardTextStyle(record.Name, standards) || IsTextStyleWhitelisted(record.Name, whitelist)) continue;
                    if (!targetBySource.ContainsKey(record.Name)) continue;
                    record.UpgradeOpen();
                    record.Erase();
                    deleted++;
                }
                catch (System.Exception ex) { Log("Delete unused old text style failed: " + ex.Message); }
            }
            return deleted;
        }

        static bool IsStandardTextStyle(string styleName, List<TextStyleStandardRule> standards)
        {
            foreach (var rule in standards)
                if (SafeStr(rule.Name).Equals(SafeStr(styleName), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool IsTextStyleWhitelisted(string styleName, string whitelist)
        {
            return MatchTextStyleWhitelistPattern(styleName, whitelist) != null;
        }

        static bool IsSkippedTextStyleBlockRecord(BlockTableRecord btr)
        {
            if (btr == null) return true;
            return TryGetBoolProperty(btr, "IsFromExternalReference")
                || TryGetBoolProperty(btr, "IsFromOverlayReference")
                || TryGetBoolProperty(btr, "IsDependent")
                || TryGetBoolProperty(btr, "IsLayout")
                || TryGetBoolProperty(btr, "IsAnonymous");
        }

        static string GetEntityTextStyleName(Entity ent)
        {
            if (ent == null) return "";
            var dt = ent as DBText;
            if (dt != null) return SafeStr(dt.TextStyleName);
            var mt = ent as MText;
            if (mt != null) return SafeStr(mt.TextStyleName);
            return "";
        }

        static void SetStringProperty(object target, string propertyName, string value)
        {
            try
            {
                var prop = target.GetType().GetProperty(propertyName);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string)) prop.SetValue(target, value, null);
            }
            catch (System.Exception ex) { Log("Set string property failed: " + propertyName + " " + ex.Message); }
        }

        static void SetDoubleProperty(object target, string propertyName, double value)
        {
            try
            {
                var prop = target.GetType().GetProperty(propertyName);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(double)) prop.SetValue(target, value, null);
            }
            catch (System.Exception ex) { Log("Set double property failed: " + propertyName + " " + ex.Message); }
        }
    }
}
