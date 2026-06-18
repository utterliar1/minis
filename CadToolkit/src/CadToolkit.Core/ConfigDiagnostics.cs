using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CadToolkit.Core
{
    public enum ConfigDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ConfigDiagnosticIssue
    {
        public ConfigDiagnosticSeverity Severity;
        public string Code;
        public string Message;
        public int LineNumber;
        public string Section;
        public bool CanFix;
    }

    public class ConfigDiagnosticResult
    {
        public string Path;
        public List<ConfigDiagnosticIssue> Issues = new List<ConfigDiagnosticIssue>();
        public string RepairedText;
        public bool HasChanges;
        public string BackupPath;

        public bool HasErrors
        {
            get
            {
                foreach (var issue in Issues)
                    if (issue.Severity == ConfigDiagnosticSeverity.Error) return true;
                return false;
            }
        }
    }

    public static class ConfigDiagnostics
    {
        static readonly string[] RequiredSections = new string[]
        {
            "Commands",
            "LayerStandard",
            "LayerMap",
            "TextStyleStandard",
            "TextStyleMap"
        };

        static readonly KeyValuePair<string, string>[] RootSettings = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("QuickBlockPrefix", "BK"),
            new KeyValuePair<string, string>("DeleteOriginal", "true"),
            new KeyValuePair<string, string>("KeepOriginal", "false"),
            new KeyValuePair<string, string>("AlignHorizontal", "0"),
            new KeyValuePair<string, string>("AlignUseFirstBase", "true"),
            new KeyValuePair<string, string>("AlignLineSpacing", "0"),
            new KeyValuePair<string, string>("IsoLayerKeepLayer0", "false"),
            new KeyValuePair<string, string>("LayerStandardFallbackTo0", "false"),
            new KeyValuePair<string, string>("LayerStandardWhitelist", "0,Defpoints,*图框*,*视口*,*原有*,*新增*"),
            new KeyValuePair<string, string>("TextStyleFallbackToStandard", "false"),
            new KeyValuePair<string, string>("TextStyleFallbackStyle", "STANDARD-TEXT"),
            new KeyValuePair<string, string>("TextStyleWhitelist", "Standard,Annotative,*DIM*"),
            new KeyValuePair<string, string>("TextStyleNormalizeHeight", "false"),
            new KeyValuePair<string, string>("TextStyleNormalizeWidthFactor", "false"),
            new KeyValuePair<string, string>("TextStyleNormalizeOblique", "false"),
            new KeyValuePair<string, string>("TextStyleNormalizeColorByLayer", "false"),
            new KeyValuePair<string, string>("TextStyleDeleteUnusedOldStyles", "false"),
            new KeyValuePair<string, string>("BatchPlotDevice", "DWG To PDF.pc3"),
            new KeyValuePair<string, string>("BatchPlotPaper", "A3"),
            new KeyValuePair<string, string>("BatchPlotStyle", "monochrome.ctb"),
            new KeyValuePair<string, string>("BatchPlotAutoRotate", "true"),
            new KeyValuePair<string, string>("BatchPlotCenter", "true"),
            new KeyValuePair<string, string>("BatchPlotMarginPercent", "2"),
            new KeyValuePair<string, string>("BatchPlotMarginMm", "5"),
            new KeyValuePair<string, string>("BatchPlotFileNameMode", "DrawingDashIndex"),
            new KeyValuePair<string, string>("BatchPlotSortMode", "Position")
        };

        static readonly KeyValuePair<string, string>[] OfficialCommands = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("查找替换", "CT_FINDREPLACE"),
            new KeyValuePair<string, string>("文字对齐", "CT_ALIGN"),
            new KeyValuePair<string, string>("加下划线", "CT_UNDERLINE"),
            new KeyValuePair<string, string>("格式复制", "CT_TEXTBRUSH"),
            new KeyValuePair<string, string>("文字合并", "CT_TEXTMERGE"),
            new KeyValuePair<string, string>("文字编号", "CT_TEXTNUMBER"),
            new KeyValuePair<string, string>("递增复制", "CT_INCCOPY"),
            new KeyValuePair<string, string>("文字规范", "CT_TEXTSTYLESTANDARD"),
            new KeyValuePair<string, string>("图层归零", "CT_SETLAYER0"),
            new KeyValuePair<string, string>("图层规范", "CT_LAYERSTANDARD"),
            new KeyValuePair<string, string>("孤立图层", "CT_ISOLAYER"),
            new KeyValuePair<string, string>("按层选择", "CT_SELECTBYLAYER"),
            new KeyValuePair<string, string>("按色选择", "CT_SELECTBYCOLOR"),
            new KeyValuePair<string, string>("重命名块", "CT_RENAMEBLOCK"),
            new KeyValuePair<string, string>("快捷建块", "CT_QUICKBLOCK"),
            new KeyValuePair<string, string>("改块基点", "CT_CHANGEBASEPOINT"),
            new KeyValuePair<string, string>("按块选择", "CT_SELECTBYBLOCK"),
            new KeyValuePair<string, string>("画中心线", "CT_CENTERLINE"),
            new KeyValuePair<string, string>("快速标注", "CT_QUICKDIM"),
            new KeyValuePair<string, string>("批量打印", "CT_BATCHPLOT"),
            new KeyValuePair<string, string>("Z轴归零", "CT_FLATTEN")
        };

        internal class IniLine
        {
            internal string Text;
            internal string Trimmed;
            internal int Number;
            internal string Section;
            internal bool IsSection;
            internal bool IsComment;
            internal string Key;
            internal string Value;
        }

        public static ConfigDiagnosticResult Analyze(string text, string path)
        {
            string source = text ?? "";
            var result = new ConfigDiagnosticResult { Path = path, RepairedText = source };
            var lines = Parse(source);
            var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var commandPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var layerStandards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var textStyleStandards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rootCount = 0;
            int commandCount = 0;
            int layerStandardCount = 0;
            int layerMapCount = 0;
            int textStyleStandardCount = 0;
            int textStyleMapCount = 0;

            foreach (IniLine line in lines)
            {
                if (line.IsSection && line.Section != null)
                    sections.Add(line.Section);

                if (line.Key == null)
                    continue;

                if (line.Section == null)
                {
                    rootKeys.Add(line.Key);
                    rootCount++;
                    continue;
                }

                if (EqualsSection(line.Section, "Commands"))
                {
                    commandPairs.Add(line.Key + "=" + line.Value);
                    commandCount++;
                }
                else if (EqualsSection(line.Section, "LayerStandard"))
                {
                    layerStandards.Add(line.Key);
                    layerStandardCount++;
                    ValidateLayerStandard(result, line);
                }
                else if (EqualsSection(line.Section, "LayerMap"))
                {
                    layerMapCount++;
                }
                else if (EqualsSection(line.Section, "TextStyleStandard"))
                {
                    textStyleStandards.Add(line.Key);
                    textStyleStandardCount++;
                    ValidateTextStyleStandard(result, line);
                }
                else if (EqualsSection(line.Section, "TextStyleMap"))
                {
                    textStyleMapCount++;
                }
            }

            foreach (KeyValuePair<string, string> setting in RootSettings)
            {
                if (!rootKeys.Contains(setting.Key))
                    AddIssue(result, ConfigDiagnosticSeverity.Warning, "MissingRootSetting", "Missing root setting: " + setting.Key, 0, null, true);
            }

            foreach (string section in RequiredSections)
            {
                if (sections.Contains(section))
                    continue;

                string code = EqualsSection(section, "Commands") ? "MissingCommandsSection" : "MissingSection";
                AddIssue(result, ConfigDiagnosticSeverity.Warning, code, "Missing section: [" + section + "]", 0, section, true);
            }

            foreach (KeyValuePair<string, string> officialCommand in OfficialCommands)
            {
                string officialPair = officialCommand.Key + "=" + officialCommand.Value;
                if (!commandPairs.Contains(officialPair))
                    AddIssue(result, ConfigDiagnosticSeverity.Warning, "MissingOfficialCommand", "Missing official command: " + officialCommand.Key + "=" + officialCommand.Value, 0, "Commands", true);
            }

            foreach (IniLine line in lines)
            {
                if (EqualsSection(line.Section, "Commands"))
                {
                    if (line.IsComment && line.Trimmed.IndexOf('=') >= 0)
                        AddIssue(result, ConfigDiagnosticSeverity.Warning, "CommandDocCommentWithEquals", "Command comment contains '=' and may be read as documentation text.", line.Number, "Commands", IsKnownCommandDocComment(line.Trimmed));

                    if (line.Key != null && line.Key.Equals("文字样式规范", StringComparison.OrdinalIgnoreCase) && line.Value.Equals("CT_TEXTSTYLESTANDARD", StringComparison.OrdinalIgnoreCase))
                        AddIssue(result, ConfigDiagnosticSeverity.Warning, "OldOfficialCommandLabel", "Old official command label should be renamed to 文字规范.", line.Number, "Commands", true);
                }
                else if (EqualsSection(line.Section, "LayerMap") && line.Key != null)
                {
                    if (!layerStandards.Contains(line.Key))
                        AddIssue(result, ConfigDiagnosticSeverity.Error, "LayerMapTargetMissing", "LayerMap target is not defined in LayerStandard: " + line.Key, line.Number, "LayerMap", false);
                }
                else if (EqualsSection(line.Section, "TextStyleMap") && line.Key != null)
                {
                    if (!textStyleStandards.Contains(line.Key))
                        AddIssue(result, ConfigDiagnosticSeverity.Error, "TextStyleMapTargetMissing", "TextStyleMap target is not defined in TextStyleStandard: " + line.Key, line.Number, "TextStyleMap", false);
                }
            }

            AddIssue(result, ConfigDiagnosticSeverity.Info, "Summary", string.Format(CultureInfo.InvariantCulture, "Checked {0} root settings, {1} commands, {2} layer standards, {3} layer maps, {4} text style standards, {5} text style maps.", rootCount, commandCount, layerStandardCount, layerMapCount, textStyleStandardCount, textStyleMapCount), 0, null, false);
            return result;
        }

        public static ConfigDiagnosticResult Repair(string text, string path)
        {
            string source = text ?? "";
            if (string.IsNullOrWhiteSpace(source))
            {
                string defaultText = BuildMinimalDefaultConfig();
                var emptyResult = Analyze(defaultText, path);
                emptyResult.RepairedText = defaultText;
                emptyResult.HasChanges = true;
                return emptyResult;
            }

            var output = new List<string>();
            string[] rawLines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var rootKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool insertedRootSettings = false;
            string currentSection = null;

            foreach (string rawLine in rawLines)
            {
                string trimmed = rawLine.Trim();
                bool isSection = IsSectionHeader(trimmed);

                if (!insertedRootSettings && isSection)
                {
                    AddMissingRootSettings(output, rootKeys);
                    insertedRootSettings = true;
                }

                if (isSection)
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    sections.Add(currentSection);
                }
                else if (currentSection == null && !IsComment(trimmed))
                {
                    string key;
                    string value;
                    if (TrySplitKeyValue(trimmed, out key, out value))
                        rootKeys.Add(key);
                }

                output.Add(rawLine);
            }

            if (!insertedRootSettings)
                AddMissingRootSettings(output, rootKeys);

            if (!sections.Contains("Commands"))
            {
                int layerStandardIndex = FindSectionIndex(output, "LayerStandard");
                var commandsSection = new List<string>();
                commandsSection.Add("[Commands]");
                foreach (KeyValuePair<string, string> command in OfficialCommands)
                    commandsSection.Add(command.Key + "=" + command.Value);

                if (layerStandardIndex >= 0)
                    output.InsertRange(layerStandardIndex, commandsSection);
                else
                    output.AddRange(commandsSection);
            }

            RepairCommandsSection(output);

            string repaired = JoinCrLf(output);
            var result = Analyze(repaired, path);
            result.RepairedText = repaired;
            result.HasChanges = !string.Equals(NormalizeLineEndings(source), repaired, StringComparison.Ordinal);
            return result;
        }

        public static ConfigDiagnosticResult AnalyzeFile(string path)
        {
            string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            return Analyze(text, path);
        }

        public static ConfigDiagnosticResult RepairFile(string path)
        {
            string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            var result = Repair(text, path);
            if (!result.HasChanges)
                return AnalyzeFile(path);

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, Path.GetFileName(fullPath) + ".tmp-" + Guid.NewGuid().ToString("N"));
            string backupPath = null;
            try
            {
                File.WriteAllText(tempPath, result.RepairedText ?? "", Encoding.UTF8);
                if (File.Exists(fullPath))
                {
                    backupPath = CreateBackupPath(fullPath);
                    File.Replace(tempPath, fullPath, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }
            }

            var fresh = AnalyzeFile(fullPath);
            fresh.BackupPath = backupPath;
            fresh.HasChanges = true;
            fresh.RepairedText = result.RepairedText;
            return fresh;
        }

        public static string FormatReport(ConfigDiagnosticResult result)
        {
            var sb = new StringBuilder();
            int errorCount = CountIssues(result, ConfigDiagnosticSeverity.Error);
            int warningCount = CountIssues(result, ConfigDiagnosticSeverity.Warning);
            int fixableCount = CountFixableIssues(result);

            sb.AppendLine("CadToolkit 配置体检");
            sb.AppendLine("配置文件：" + (result == null ? "" : result.Path));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "结论：发现 {0} 个错误、{1} 个警告。", errorCount, warningCount));
            if (fixableCount > 0)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "提示：标记为“可自动修复”的 {0} 项可直接点击“自动修复”。修复前会自动备份原配置。", fixableCount));
            else if (errorCount == 0 && warningCount == 0)
                sb.AppendLine("提示：当前配置未发现需要处理的问题。");
            else
                sb.AppendLine("提示：未标记为“可自动修复”的项目需要手动检查配置内容。");
            sb.AppendLine();

            AppendIssueGroup(sb, "错误", result, ConfigDiagnosticSeverity.Error);
            AppendIssueGroup(sb, "警告", result, ConfigDiagnosticSeverity.Warning);
            AppendIssueGroup(sb, "信息", result, ConfigDiagnosticSeverity.Info);

            if (result != null && !string.IsNullOrEmpty(result.BackupPath))
            {
                sb.AppendLine();
                sb.AppendLine("备份文件：" + result.BackupPath);
            }

            return sb.ToString();
        }

        static int CountIssues(ConfigDiagnosticResult result, ConfigDiagnosticSeverity severity)
        {
            int count = 0;
            if (result == null)
                return count;

            foreach (ConfigDiagnosticIssue issue in result.Issues)
            {
                if (issue.Severity == severity)
                    count++;
            }

            return count;
        }

        static int CountFixableIssues(ConfigDiagnosticResult result)
        {
            int count = 0;
            if (result == null)
                return count;

            foreach (ConfigDiagnosticIssue issue in result.Issues)
            {
                if (issue.CanFix)
                    count++;
            }

            return count;
        }

        static void AppendIssueGroup(StringBuilder sb, string title, ConfigDiagnosticResult result, ConfigDiagnosticSeverity severity)
        {
            var issues = new List<ConfigDiagnosticIssue>();
            if (result != null)
            {
                foreach (ConfigDiagnosticIssue issue in result.Issues)
                {
                    if (issue.Severity == severity)
                        issues.Add(issue);
                }
            }

            if (issues.Count == 0)
                return;

            sb.AppendLine(title + " " + issues.Count.ToString(CultureInfo.InvariantCulture) + " 项");
            foreach (ConfigDiagnosticIssue issue in issues)
            {
                string suffix = issue.CanFix ? "（可自动修复）" : "";
                string location = "";
                if (!string.IsNullOrEmpty(issue.Section))
                    location += "[" + issue.Section + "] ";
                if (issue.LineNumber > 0)
                    location += "第 " + issue.LineNumber.ToString(CultureInfo.InvariantCulture) + " 行：";

                sb.AppendLine("- " + location + LocalizeIssue(issue) + suffix);
            }

            sb.AppendLine();
        }

        static string LocalizeIssue(ConfigDiagnosticIssue issue)
        {
            if (issue == null)
                return "";

            string code = issue.Code ?? "";
            string message = issue.Message ?? "";

            if (code.Equals("MissingRootSetting", StringComparison.OrdinalIgnoreCase))
                return "缺少根配置项：" + ExtractAfter(message, ": ");
            if (code.Equals("MissingCommandsSection", StringComparison.OrdinalIgnoreCase))
                return "缺少命令分组：[Commands]";
            if (code.Equals("MissingSection", StringComparison.OrdinalIgnoreCase))
                return "缺少配置分组：" + ExtractAfter(message, ": ");
            if (code.Equals("MissingOfficialCommand", StringComparison.OrdinalIgnoreCase))
                return "缺少内置命令按钮：" + ExtractAfter(message, ": ");
            if (code.Equals("CommandDocCommentWithEquals", StringComparison.OrdinalIgnoreCase))
                return "命令分组里的说明文字包含等号，可能被误读为命令配置。";
            if (code.Equals("OldOfficialCommandLabel", StringComparison.OrdinalIgnoreCase))
                return "旧命令名称“文字样式规范”应改为“文字规范”。";
            if (code.Equals("LayerMapTargetMissing", StringComparison.OrdinalIgnoreCase))
                return "图层映射目标未在 [LayerStandard] 中定义：" + ExtractAfter(message, ": ");
            if (code.Equals("TextStyleMapTargetMissing", StringComparison.OrdinalIgnoreCase))
                return "文字样式映射目标未在 [TextStyleStandard] 中定义：" + ExtractAfter(message, ": ");
            if (code.Equals("MalformedLayerStandard", StringComparison.OrdinalIgnoreCase))
                return "图层标准格式错误，应为：颜色|线型|线宽|是否打印。";
            if (code.Equals("MalformedTextStyleStandard", StringComparison.OrdinalIgnoreCase))
                return "文字样式标准格式错误，应为：字体文件|大字体文件|固定字高|宽度因子|倾斜角。";
            if (code.Equals("Summary", StringComparison.OrdinalIgnoreCase))
                return LocalizeSummary(message);

            return string.IsNullOrEmpty(message) ? code : message;
        }

        static string ExtractAfter(string text, string marker)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            int index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return text;

            return text.Substring(index + marker.Length);
        }

        static string LocalizeSummary(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "检查完成。";

            return "检查完成：" + message
                .Replace("Checked ", "")
                .Replace(" root settings", " 个根配置项")
                .Replace(" commands", " 个命令")
                .Replace(" layer standards", " 个图层标准")
                .Replace(" layer maps", " 个图层映射")
                .Replace(" text style standards", " 个文字样式标准")
                .Replace(" text style maps.", " 个文字样式映射。");
        }

        static void AddMissingRootSettings(List<string> lines, HashSet<string> rootKeys)
        {
            foreach (KeyValuePair<string, string> setting in RootSettings)
            {
                if (rootKeys.Contains(setting.Key))
                    continue;

                lines.Add(setting.Key + "=" + setting.Value);
                rootKeys.Add(setting.Key);
            }
        }

        static void RepairCommandsSection(List<string> lines)
        {
            int commandsStart = FindSectionIndex(lines, "Commands");
            if (commandsStart < 0)
                return;

            int commandsEnd = FindNextSectionIndex(lines, commandsStart + 1);
            if (commandsEnd < 0)
                commandsEnd = lines.Count;

            bool hasNewTextStyleCommand = false;
            for (int i = commandsStart + 1; i < commandsEnd; i++)
            {
                string key;
                string value;
                if (!TrySplitKeyValue(lines[i].Trim(), out key, out value))
                    continue;

                if (value.Equals("CT_TEXTSTYLESTANDARD", StringComparison.OrdinalIgnoreCase) && key.Equals("文字规范", StringComparison.OrdinalIgnoreCase))
                    hasNewTextStyleCommand = true;
            }

            for (int i = commandsEnd - 1; i > commandsStart; i--)
            {
                string trimmed = lines[i].Trim();
                string key;
                string value;

                if (IsComment(trimmed) && IsKnownCommandDocComment(trimmed))
                {
                    lines.RemoveAt(i);
                    commandsEnd--;
                    continue;
                }

                if (!TrySplitKeyValue(trimmed, out key, out value))
                    continue;

                if (value.Equals("CT_TEXTSTYLESTANDARD", StringComparison.OrdinalIgnoreCase) && key.Equals("文字样式规范", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasNewTextStyleCommand)
                    {
                        lines.RemoveAt(i);
                        commandsEnd--;
                    }
                    else
                    {
                        lines[i] = "文字规范=CT_TEXTSTYLESTANDARD";
                        hasNewTextStyleCommand = true;
                    }
                }
            }

            commandsEnd = FindNextSectionIndex(lines, commandsStart + 1);
            if (commandsEnd < 0)
                commandsEnd = lines.Count;

            var commandPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = commandsStart + 1; i < commandsEnd; i++)
            {
                string key;
                string value;
                if (TrySplitKeyValue(lines[i].Trim(), out key, out value))
                    commandPairs.Add(key + "=" + value);
            }

            foreach (KeyValuePair<string, string> command in OfficialCommands)
            {
                string officialPair = command.Key + "=" + command.Value;
                if (commandPairs.Contains(officialPair))
                    continue;

                int insertIndex = FindCommandInsertIndex(lines, commandsStart, commandsEnd, command);
                lines.Insert(insertIndex, command.Key + "=" + command.Value);
                commandPairs.Add(officialPair);
                commandsEnd++;
            }
        }

        static int FindCommandInsertIndex(List<string> lines, int commandsStart, int commandsEnd, KeyValuePair<string, string> command)
        {
            string anchorValue = null;
            if (command.Value.Equals("CT_CONFIGCHECK", StringComparison.OrdinalIgnoreCase))
                anchorValue = "CT_TEXTSTYLESTANDARD";
            else if (command.Value.Equals("CT_INCCOPY", StringComparison.OrdinalIgnoreCase))
                anchorValue = "CT_TEXTNUMBER";
            else if (command.Value.Equals("CT_CHANGEBASEPOINT", StringComparison.OrdinalIgnoreCase))
                anchorValue = "CT_QUICKBLOCK";
            else if (command.Value.Equals("CT_BATCHPLOT", StringComparison.OrdinalIgnoreCase))
                anchorValue = "CT_QUICKDIM";

            if (anchorValue != null)
            {
                for (int i = commandsStart + 1; i < commandsEnd; i++)
                {
                    string key;
                    string value;
                    if (TrySplitKeyValue(lines[i].Trim(), out key, out value) && value.Equals(anchorValue, StringComparison.OrdinalIgnoreCase))
                        return i + 1;
                }
            }

            return commandsEnd;
        }

        static string BuildMinimalDefaultConfig()
        {
            var lines = new List<string>();
            foreach (KeyValuePair<string, string> setting in RootSettings)
                lines.Add(setting.Key + "=" + setting.Value);

            lines.Add("");
            lines.Add("[Commands]");
            foreach (KeyValuePair<string, string> command in OfficialCommands)
                lines.Add(command.Key + "=" + command.Value);

            lines.Add("");
            lines.Add("[LayerStandard]");
            lines.Add("");
            lines.Add("[LayerMap]");
            lines.Add("");
            lines.Add("[TextStyleStandard]");
            lines.Add("");
            lines.Add("[TextStyleMap]");
            return JoinCrLf(lines);
        }

        static int FindSectionIndex(List<string> lines, string section)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (IsSectionHeader(trimmed) && EqualsSection(trimmed.Substring(1, trimmed.Length - 2).Trim(), section))
                    return i;
            }

            return -1;
        }

        static int FindNextSectionIndex(List<string> lines, int startIndex)
        {
            for (int i = startIndex; i < lines.Count; i++)
            {
                if (IsSectionHeader(lines[i].Trim()))
                    return i;
            }

            return -1;
        }

        static bool TrySplitKeyValue(string trimmed, out string key, out string value)
        {
            key = null;
            value = null;
            int equals = trimmed.IndexOf('=');
            if (equals < 0)
                return false;

            key = trimmed.Substring(0, equals).Trim();
            value = trimmed.Substring(equals + 1).Trim();
            return key.Length > 0;
        }

        static bool IsSectionHeader(string trimmed)
        {
            return trimmed.Length >= 2 && trimmed.StartsWith("[") && trimmed.EndsWith("]");
        }

        static bool IsComment(string trimmed)
        {
            return trimmed.StartsWith("#") || trimmed.StartsWith(";");
        }

        static bool IsKnownCommandDocComment(string trimmed)
        {
            if (trimmed.IndexOf('=') < 0)
                return false;

            return trimmed.StartsWith("# 格式", StringComparison.Ordinal)
                || trimmed.StartsWith("# 示例", StringComparison.Ordinal)
                || trimmed.StartsWith("# 标准图层", StringComparison.Ordinal)
                || trimmed.StartsWith("# 标准样式", StringComparison.Ordinal);
        }

        static string CreateBackupPath(string path)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string candidate = path + ".bak-" + stamp;
            if (!File.Exists(candidate))
                return candidate;

            for (int i = 1; i < 1000; i++)
            {
                candidate = path + ".bak-" + stamp + "-" + i.ToString("000", CultureInfo.InvariantCulture);
                if (!File.Exists(candidate))
                    return candidate;
            }

            return path + ".bak-" + stamp + "-" + Guid.NewGuid().ToString("N");
        }

        static string NormalizeLineEndings(string text)
        {
            return JoinCrLf(new List<string>((text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')));
        }

        static string JoinCrLf(List<string> lines)
        {
            return string.Join("\r\n", lines.ToArray());
        }

        static List<IniLine> Parse(string text)
        {
            string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            string[] rawLines = normalized.Split('\n');
            var lines = new List<IniLine>();
            string currentSection = null;

            for (int i = 0; i < rawLines.Length; i++)
            {
                string raw = rawLines[i];
                string trimmed = raw.Trim();
                var line = new IniLine();
                line.Text = raw;
                line.Trimmed = trimmed;
                line.Number = i + 1;
                line.Section = currentSection;
                line.IsComment = trimmed.StartsWith("#") || trimmed.StartsWith(";");

                if (trimmed.Length >= 2 && trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    line.IsSection = true;
                    line.Section = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    currentSection = line.Section;
                }
                else if (!line.IsComment)
                {
                    int equals = trimmed.IndexOf('=');
                    if (equals >= 0)
                    {
                        line.Key = trimmed.Substring(0, equals).Trim();
                        line.Value = trimmed.Substring(equals + 1).Trim();
                    }
                }

                lines.Add(line);
            }

            return lines;
        }

        static void ValidateLayerStandard(ConfigDiagnosticResult result, IniLine line)
        {
            string[] parts = (line.Value ?? "").Split('|');
            int color;
            bool plot;
            bool valid = parts.Length == 4
                && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out color)
                && bool.TryParse(parts[3].Trim(), out plot);

            if (!valid)
                AddIssue(result, ConfigDiagnosticSeverity.Error, "MalformedLayerStandard", "LayerStandard value must be color|linetype|lineweight|plot.", line.Number, "LayerStandard", false);
        }

        static void ValidateTextStyleStandard(ConfigDiagnosticResult result, IniLine line)
        {
            string[] parts = (line.Value ?? "").Split('|');
            double height;
            double width;
            double oblique;
            bool valid = parts.Length == 5
                && double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out height)
                && double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out width)
                && double.TryParse(parts[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out oblique);

            if (!valid)
                AddIssue(result, ConfigDiagnosticSeverity.Error, "MalformedTextStyleStandard", "TextStyleStandard value must be font|bigfont|height|width|oblique.", line.Number, "TextStyleStandard", false);
        }

        static bool EqualsSection(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        static void AddIssue(ConfigDiagnosticResult result, ConfigDiagnosticSeverity severity, string code, string message, int lineNumber, string section, bool canFix)
        {
            result.Issues.Add(new ConfigDiagnosticIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                LineNumber = lineNumber,
                Section = section,
                CanFix = canFix
            });
        }
    }
}
