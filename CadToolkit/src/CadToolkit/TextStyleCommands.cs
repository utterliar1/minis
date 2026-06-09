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
                Ed.WriteMessage("\n未配置 [TextStyleStandard] 文字样式规范。");
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

            var f = new Form();
            f.Text = "文字样式规范";
            f.StartPosition = FormStartPosition.CenterScreen;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false; f.MinimizeBox = false; f.ShowInTaskbar = false;
            f.AutoScaleMode = AutoScaleMode.None; f.AutoScroll = true; f.ClientSize = new Size(UiScale(700), UiScale(640));
            TextStylePlanTreeFilter previewFilter = TextStylePlanTreeFilter.All;

            var rbAll = new RadioButton();
            rbAll.Text = "全部"; rbAll.Left = UiScale(12); rbAll.Top = UiScale(12); rbAll.Width = UiScale(70); rbAll.Height = UiScale(24); rbAll.Checked = true;
            rbAll.Font = new Font("Microsoft YaHei", 9f);

            var rbUnknown = new RadioButton();
            rbUnknown.Text = "未识别"; rbUnknown.Left = UiScale(88); rbUnknown.Top = UiScale(12); rbUnknown.Width = UiScale(86); rbUnknown.Height = UiScale(24);
            rbUnknown.Font = new Font("Microsoft YaHei", 9f);

            var rbMigration = new RadioButton();
            rbMigration.Text = "将归并"; rbMigration.Left = UiScale(180); rbMigration.Top = UiScale(12); rbMigration.Width = UiScale(86); rbMigration.Height = UiScale(24);
            rbMigration.Font = new Font("Microsoft YaHei", 9f);

            var rbWhitelist = new RadioButton();
            rbWhitelist.Text = "白名单"; rbWhitelist.Left = UiScale(272); rbWhitelist.Top = UiScale(12); rbWhitelist.Width = UiScale(86); rbWhitelist.Height = UiScale(24);
            rbWhitelist.Font = new Font("Microsoft YaHei", 9f);

            var search = new TextBox();
            search.Left = UiScale(368); search.Top = UiScale(12); search.Width = UiScale(318); search.Height = UiScale(24);
            search.Font = new Font("Microsoft YaHei", 9f);

            var tree = new TreeView();
            tree.HideSelection = false;
            tree.FullRowSelect = true;
            tree.ShowNodeToolTips = true;
            tree.Font = new Font("Microsoft YaHei", 9f);
            tree.Left = UiScale(12); tree.Top = UiScale(42); tree.Width = UiScale(674); tree.Height = UiScale(360);
            BuildTextStylePlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, standards, fallbackToStandard, fallbackStyle, previewFilter, search.Text);

            var chkCurrentSpace = new CheckBox();
            chkCurrentSpace.Text = "处理当前空间文字";
            chkCurrentSpace.Left = UiScale(12); chkCurrentSpace.Top = UiScale(410); chkCurrentSpace.Width = UiScale(210); chkCurrentSpace.Height = UiScale(24); chkCurrentSpace.Checked = true;
            chkCurrentSpace.Font = new Font("Microsoft YaHei", 9f);

            var chkAttributes = new CheckBox();
            chkAttributes.Text = "处理块参照属性";
            chkAttributes.Left = UiScale(236); chkAttributes.Top = UiScale(410); chkAttributes.Width = UiScale(210); chkAttributes.Height = UiScale(24); chkAttributes.Checked = false;
            chkAttributes.Font = new Font("Microsoft YaHei", 9f);

            var chkBlockDefinitions = new CheckBox();
            chkBlockDefinitions.Text = "处理块定义内部文字";
            chkBlockDefinitions.Left = UiScale(460); chkBlockDefinitions.Top = UiScale(410); chkBlockDefinitions.Width = UiScale(226); chkBlockDefinitions.Height = UiScale(24); chkBlockDefinitions.Checked = false;
            chkBlockDefinitions.Font = new Font("Microsoft YaHei", 9f);

            var chkFallback = new CheckBox();
            chkFallback.Text = "未识别文字样式归到标准样式";
            chkFallback.Left = UiScale(12); chkFallback.Top = UiScale(442); chkFallback.Width = UiScale(330); chkFallback.Height = UiScale(24); chkFallback.Checked = fallbackToStandard;
            chkFallback.Font = new Font("Microsoft YaHei", 9f);

            var chkHeight = new CheckBox();
            chkHeight.Text = "同步固定字高";
            chkHeight.Left = UiScale(12); chkHeight.Top = UiScale(474); chkHeight.Width = UiScale(160); chkHeight.Height = UiScale(24); chkHeight.Checked = Config.TextStyleNormalizeHeight;
            chkHeight.Font = new Font("Microsoft YaHei", 9f);

            var chkWidthFactor = new CheckBox();
            chkWidthFactor.Text = "同步宽度因子";
            chkWidthFactor.Left = UiScale(180); chkWidthFactor.Top = UiScale(474); chkWidthFactor.Width = UiScale(160); chkWidthFactor.Height = UiScale(24); chkWidthFactor.Checked = Config.TextStyleNormalizeWidthFactor;
            chkWidthFactor.Font = new Font("Microsoft YaHei", 9f);

            var chkOblique = new CheckBox();
            chkOblique.Text = "同步倾斜角";
            chkOblique.Left = UiScale(348); chkOblique.Top = UiScale(474); chkOblique.Width = UiScale(150); chkOblique.Height = UiScale(24); chkOblique.Checked = Config.TextStyleNormalizeOblique;
            chkOblique.Font = new Font("Microsoft YaHei", 9f);

            var chkColorByLayer = new CheckBox();
            chkColorByLayer.Text = "颜色改为 ByLayer";
            chkColorByLayer.Left = UiScale(506); chkColorByLayer.Top = UiScale(474); chkColorByLayer.Width = UiScale(180); chkColorByLayer.Height = UiScale(24); chkColorByLayer.Checked = Config.TextStyleNormalizeColorByLayer;
            chkColorByLayer.Font = new Font("Microsoft YaHei", 9f);

            var chkDeleteUnused = new CheckBox();
            chkDeleteUnused.Text = "删除未使用旧文字样式";
            chkDeleteUnused.Left = UiScale(12); chkDeleteUnused.Top = UiScale(506); chkDeleteUnused.Width = UiScale(250); chkDeleteUnused.Height = UiScale(24); chkDeleteUnused.Checked = Config.TextStyleDeleteUnusedOldStyles;
            chkDeleteUnused.Font = new Font("Microsoft YaHei", 9f);

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

            var copy = new Button();
            copy.Text = "复制当前";
            copy.Left = UiScale(414); copy.Top = UiScale(590); copy.Width = UiScale(88); copy.Height = UiScale(28); copy.FlatStyle = FlatStyle.System;
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

            var ok = new Button();
            ok.Text = "执行"; ok.DialogResult = DialogResult.OK;
            ok.Left = UiScale(510); ok.Top = UiScale(590); ok.Width = UiScale(80); ok.Height = UiScale(28); ok.FlatStyle = FlatStyle.System;

            var cancel = new Button();
            cancel.Text = "取消"; cancel.DialogResult = DialogResult.Cancel;
            cancel.Left = UiScale(606); cancel.Top = UiScale(590); cancel.Width = UiScale(80); cancel.Height = UiScale(28); cancel.FlatStyle = FlatStyle.System;

            f.Controls.AddRange(new Control[] { rbAll, rbUnknown, rbMigration, rbWhitelist, search, tree, chkCurrentSpace, chkAttributes, chkBlockDefinitions, chkFallback, chkHeight, chkWidthFactor, chkOblique, chkColorByLayer, chkDeleteUnused, copy, ok, cancel });
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

            Ed.WriteMessage(string.Format("\n文字样式规范完成：处理 {0} 个对象，失败 {1} 个，删除未使用旧文字样式 {2} 个。", changedCount, failed, deleted));
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

        static TreeNode[] BuildTextStylePlanTreeNodes(List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle)
        {
            var nodes = new List<TreeNode>();
            int migrateObjects = SumTextStylePlanCounts(plans);
            int fallbackObjects = SumTextStylePlanCounts(fallbackPlans);
            int whitelistObjects = SumTextStylePlanCounts(whitelistPlans);
            string targetFallback = SafeStr(fallbackStyle);

            var summary = new TreeNode(string.Format("摘要：标准文字样式 {0} 个；将归并 {1} 样式 / {2} 对象；未识别 {3} 样式 / {4} 对象；白名单 {5} 样式 / {6} 对象",
                rules.Count, plans.Count, migrateObjects, fallbackPlans.Count, fallbackObjects, whitelistPlans.Count, whitelistObjects));
            nodes.Add(summary);

            var unknown = new TreeNode(string.Format("未识别文字样式（{0} 样式 / {1} 对象，{2}）",
                fallbackPlans.Count, fallbackObjects, fallbackToStandard ? "将归到 " + targetFallback : "保持原样"));
            foreach (var p in SortTextStylePlansByCount(fallbackPlans))
            {
                string text = fallbackToStandard
                    ? string.Format("{0} -> {1}    {2} 对象    {3}", p.SourceStyle, targetFallback, p.Count, SafeStr(p.Reason))
                    : string.Format("{0}    {1} 对象    保持原样    {2}", p.SourceStyle, p.Count, SafeStr(p.Reason));
                unknown.Nodes.Add(new TreeNode(text));
            }
            unknown.Expand();
            nodes.Add(unknown);

            var migrate = new TreeNode(string.Format("将归并文字样式（{0} 样式 / {1} 对象）", plans.Count, migrateObjects));
            foreach (var group in BuildTextStylePlanTargetGroups(plans))
            {
                var groupNode = new TreeNode(string.Format("{0}（{1} 样式 / {2} 对象）", group.TargetStyle, group.Plans.Count, group.Count));
                foreach (var p in SortTextStylePlansByCount(group.Plans))
                    groupNode.Nodes.Add(new TreeNode(string.Format("{0} -> {1}    {2} 对象    {3}", p.SourceStyle, p.TargetStyle, p.Count, SafeStr(p.Reason))));
                migrate.Nodes.Add(groupNode);
            }
            nodes.Add(migrate);

            var whitelist = new TreeNode(string.Format("白名单文字样式（{0} 样式 / {1} 对象，保持原样）", whitelistPlans.Count, whitelistObjects));
            foreach (var p in SortTextStylePlansByCount(whitelistPlans))
                whitelist.Nodes.Add(new TreeNode(string.Format("{0}    {1} 对象    {2}", p.SourceStyle, p.Count, SafeStr(p.Reason))));
            nodes.Add(whitelist);

            return nodes.ToArray();
        }

        static TreeNode[] BuildFilteredTextStylePlanTreeNodes(List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle, TextStylePlanTreeFilter filter)
        {
            var allNodes = BuildTextStylePlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackToStandard, fallbackStyle);
            if (filter == TextStylePlanTreeFilter.All) return allNodes;

            var nodes = new List<TreeNode>();
            nodes.Add((TreeNode)allNodes[0].Clone());
            if (filter == TextStylePlanTreeFilter.Unknown) nodes.Add((TreeNode)allNodes[1].Clone());
            if (filter == TextStylePlanTreeFilter.Migration) nodes.Add((TreeNode)allNodes[2].Clone());
            if (filter == TextStylePlanTreeFilter.Whitelist) nodes.Add((TreeNode)allNodes[3].Clone());
            return nodes.ToArray();
        }

        static TreeNode[] BuildSearchedTextStylePlanTreeNodes(List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle, TextStylePlanTreeFilter filter, string searchText)
        {
            var filtered = BuildFilteredTextStylePlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackToStandard, fallbackStyle, filter);
            string needle = SafeStr(searchText).Trim();
            if (needle.Length == 0) return filtered;

            var nodes = new List<TreeNode>();
            if (filtered.Length > 0) nodes.Add((TreeNode)filtered[0].Clone());
            for (int i = 1; i < filtered.Length; i++)
            {
                var matched = CloneTextStylePlanNodeMatches(filtered[i], needle);
                if (matched != null) nodes.Add(matched);
            }
            return nodes.ToArray();
        }

        static string FormatTextStylePlanTreeReport(TreeNode[] nodes)
        {
            var sb = new StringBuilder();
            if (nodes == null) return "";
            foreach (TreeNode node in nodes)
                AppendTextStylePlanTreeReportNode(sb, node, 0);
            return sb.ToString();
        }

        static void BuildTextStylePlanTreePreview(TreeView tree, List<TextStyleStandardPlan> plans, List<TextStyleStandardPlan> fallbackPlans, List<TextStyleStandardPlan> whitelistPlans, List<TextStyleStandardRule> rules, bool fallbackToStandard, string fallbackStyle, TextStylePlanTreeFilter filter, string searchText)
        {
            tree.BeginUpdate();
            try
            {
                tree.Nodes.Clear();
                tree.Nodes.AddRange(BuildSearchedTextStylePlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackToStandard, fallbackStyle, filter, searchText));
                if (filter != TextStylePlanTreeFilter.All || SafeStr(searchText).Trim().Length > 0)
                    tree.ExpandAll();
            }
            finally
            {
                tree.EndUpdate();
            }
        }

        static void AppendTextStylePlanTreeReportNode(StringBuilder sb, TreeNode node, int depth)
        {
            if (sb == null || node == null) return;
            if (depth > 0) sb.Append(new string(' ', depth * 2));
            sb.AppendLine(SafeStr(node.Text));
            foreach (TreeNode child in node.Nodes)
                AppendTextStylePlanTreeReportNode(sb, child, depth + 1);
        }

        static TreeNode CloneTextStylePlanNodeMatches(TreeNode node, string needle)
        {
            bool selfMatches = NodeTextContains(node, needle);
            var clone = new TreeNode(node.Text);
            clone.ToolTipText = node.ToolTipText;
            foreach (TreeNode child in node.Nodes)
            {
                var childClone = CloneTextStylePlanNodeMatches(child, needle);
                if (childClone != null) clone.Nodes.Add(childClone);
            }
            if (selfMatches || clone.Nodes.Count > 0) return clone;
            return null;
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
