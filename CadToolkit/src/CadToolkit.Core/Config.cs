using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CadToolkit.Core
{
    public enum HAlign { Left, Center, Right }
    public enum VAlign { Top, Middle, Bottom }

    public class AlignChoice
    {
        public HAlign Horizontal;
        public VAlign Vertical;
    }

    public static class Config
    {
        static readonly object _fileLock = new object();
        static readonly KeyValuePair<string, string>[] DefaultRootSettings = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("QuickBlockPrefix", "BK"),
            new KeyValuePair<string, string>("DeleteOriginal", "true"),
            new KeyValuePair<string, string>("KeepOriginal", "false"),
            new KeyValuePair<string, string>("AlignHorizontal", "0"),
            new KeyValuePair<string, string>("AlignUseFirstBase", "true"),
            new KeyValuePair<string, string>("AlignLineSpacing", "0"),
            new KeyValuePair<string, string>("IsoLayerKeepLayer0", "false"),
            new KeyValuePair<string, string>("LayerStandardFallbackTo0", "false"),
            new KeyValuePair<string, string>("LayerStandardWhitelist", "0,Defpoints,*\u56FE\u6846*,*\u89C6\u53E3*,*\u539F\u6709*,*\u65B0\u589E*"),
            new KeyValuePair<string, string>("TextStyleFallbackToStandard", "false"),
            new KeyValuePair<string, string>("TextStyleFallbackStyle", "STANDARD-TEXT"),
            new KeyValuePair<string, string>("TextStyleWhitelist", "Standard,Annotative,*DIM*"),
            new KeyValuePair<string, string>("TextStyleNormalizeHeight", "false"),
            new KeyValuePair<string, string>("TextStyleNormalizeWidthFactor", "false"),
            new KeyValuePair<string, string>("TextStyleNormalizeOblique", "false"),
            new KeyValuePair<string, string>("TextStyleNormalizeColorByLayer", "false"),
            new KeyValuePair<string, string>("TextStyleDeleteUnusedOldStyles", "false")
        };
        static string _dir;
        public static void Init(string assemblyPath)
        {
            // assembly is in C:\CadToolkit\{acad|zwcad|gcad}\CadToolkit.dll
            // go up one level to C:\CadToolkit\ for shared config
            string platDir = Path.GetDirectoryName(assemblyPath);
            _dir = Path.GetDirectoryName(platDir);
            if (string.IsNullOrEmpty(_dir)) _dir = platDir;
            EnsureConfig();
        }

        static void EnsureConfig()
        {
            try
            {
                if (!File.Exists(IniPath))
                {
                    string def = GetDefaultConfigText();
                    lock (_fileLock) { File.WriteAllText(IniPath, def, Encoding.UTF8); }
                    return;
                }
                EnsureRootDefaults();
                EnsureOfficialCommands();
            }
            catch (Exception ex) { LogConfigError("EnsureConfig failed: " + ex.Message); }
        }

        static void EnsureRootDefaults()
        {
            var lines = new List<string>(File.ReadAllLines(IniPath, Encoding.UTF8));
            int insertAt = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().StartsWith("["))
                {
                    insertAt = i;
                    break;
                }
            }

            bool changed = false;
            foreach (var setting in DefaultRootSettings)
            {
                if (HasConfigKey(lines, setting.Key)) continue;
                lines.Insert(insertAt, setting.Key + "=" + setting.Value);
                insertAt++;
                changed = true;
            }

            if (changed)
                lock (_fileLock) { File.WriteAllLines(IniPath, lines.ToArray(), Encoding.UTF8); }
        }

        static void EnsureOfficialCommands()
        {
            var lines = new List<string>(File.ReadAllLines(IniPath, Encoding.UTF8));
            bool changed = false;
            changed |= EnsureOfficialCommand(lines, "改块基点", "CT_CHANGEBASEPOINT", "快捷建块");
            changed |= RenameOfficialCommandLabel(lines, "文字样式规范", "文字规范", "CT_TEXTSTYLESTANDARD");
            changed |= EnsureOfficialCommand(lines, "文字规范", "CT_TEXTSTYLESTANDARD", "文字编号");
            if (changed)
                lock (_fileLock) { File.WriteAllLines(IniPath, lines.ToArray(), Encoding.UTF8); }
        }

        static bool RenameOfficialCommandLabel(List<string> lines, string oldLabel, string newLabel, string command)
        {
            bool changed = false;
            bool hasNew = HasConfigKey(lines, newLabel);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith(";") || trimmed.StartsWith("[")) continue;
                int eq = trimmed.IndexOf('=');
                if (eq <= 0) continue;
                string key = trimmed.Substring(0, eq).Trim();
                string value = trimmed.Substring(eq + 1).Trim();
                if (!key.Equals(oldLabel, StringComparison.OrdinalIgnoreCase) || !value.Equals(command, StringComparison.OrdinalIgnoreCase)) continue;
                if (hasNew)
                {
                    lines.RemoveAt(i);
                }
                else
                {
                    lines[i] = newLabel + "=" + command;
                    hasNew = true;
                }
                changed = true;
            }
            return changed;
        }

        static bool EnsureOfficialCommand(List<string> lines, string label, string command, string afterLabel)
        {
            if (HasConfigKey(lines, label)) return false;
            int commandsHeader = -1;
            int insertAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Equals("[Commands]", StringComparison.OrdinalIgnoreCase))
                {
                    commandsHeader = i;
                    insertAt = i + 1;
                    continue;
                }
                if (commandsHeader >= 0)
                {
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                        break;
                    if (trimmed.StartsWith(afterLabel + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        insertAt = i + 1;
                        break;
                    }
                    if (trimmed.Length > 0)
                        insertAt = i + 1;
                }
            }

            if (commandsHeader < 0) return false;
            if (insertAt < 0) insertAt = lines.Count;

            lines.Insert(insertAt, label + "=" + command);
            return true;
        }

        static bool HasConfigKey(List<string> lines, string key)
        {
            foreach (string line in lines)
            {
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#") || t.StartsWith(";") || t.StartsWith("[")) continue;
                int eq = t.IndexOf('=');
                if (eq > 0 && t.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static void LogConfigError(string msg)
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "CadToolkit.log");
                File.AppendAllText(logPath, string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg), Encoding.UTF8);
            }
            catch { }
        }

        static string GetDefaultConfigText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# CadToolkit \u914D\u7F6E\u6587\u4EF6");
            sb.AppendLine();
            sb.AppendLine("# \u5FEB\u6377\u5EFA\u5757");
            sb.AppendLine("QuickBlockPrefix=BK");
            sb.AppendLine("DeleteOriginal=true");
            sb.AppendLine("KeepOriginal=false");
            sb.AppendLine();
            sb.AppendLine("# \u6587\u5B57\u5BF9\u9F50");
            sb.AppendLine("AlignHorizontal=0");
            sb.AppendLine("AlignUseFirstBase=true");
            sb.AppendLine("AlignLineSpacing=0");
            sb.AppendLine();
            sb.AppendLine("# \u56FE\u5C42\u7BA1\u7406");
            sb.AppendLine("IsoLayerKeepLayer0=false");
            sb.AppendLine();
            sb.AppendLine("# \u56FE\u5C42\u89C4\u8303");
            sb.AppendLine("LayerStandardFallbackTo0=false");
            sb.AppendLine("LayerStandardWhitelist=0,Defpoints,*\u56FE\u6846*,*\u89C6\u53E3*,*\u539F\u6709*,*\u65B0\u589E*");
            sb.AppendLine();
            sb.AppendLine("# \u6587\u5B57\u89C4\u8303");
            sb.AppendLine("TextStyleFallbackToStandard=false");
            sb.AppendLine("TextStyleFallbackStyle=STANDARD-TEXT");
            sb.AppendLine("TextStyleWhitelist=Standard,Annotative,*DIM*");
            sb.AppendLine("TextStyleNormalizeHeight=false");
            sb.AppendLine("TextStyleNormalizeWidthFactor=false");
            sb.AppendLine("TextStyleNormalizeOblique=false");
            sb.AppendLine("TextStyleNormalizeColorByLayer=false");
            sb.AppendLine("TextStyleDeleteUnusedOldStyles=false");
            sb.AppendLine();
            sb.AppendLine("[Commands]");
            sb.AppendLine("# \u6587\u5B57\u7F16\u8F91");
            sb.AppendLine("\u67E5\u627E\u66FF\u6362=CT_FINDREPLACE");
            sb.AppendLine("\u6587\u5B57\u5BF9\u9F50=CT_ALIGN");
            sb.AppendLine("\u52A0\u4E0B\u5212\u7EBF=CT_UNDERLINE");
            sb.AppendLine("\u683C\u5F0F\u590D\u5236=CT_TEXTBRUSH");
            sb.AppendLine("\u6587\u5B57\u5408\u5E76=CT_TEXTMERGE");
            sb.AppendLine("\u6587\u5B57\u7F16\u53F7=CT_TEXTNUMBER");
            sb.AppendLine("\u6587\u5B57\u89C4\u8303=CT_TEXTSTYLESTANDARD");
            sb.AppendLine("# \u56FE\u5C42\u7BA1\u7406");
            sb.AppendLine("\u56FE\u5C42\u5F52\u96F6=CT_SETLAYER0");
            sb.AppendLine("\u56FE\u5C42\u89C4\u8303=CT_LAYERSTANDARD");
            sb.AppendLine("\u5B64\u7ACB\u56FE\u5C42=CT_ISOLAYER");
            sb.AppendLine("\u6309\u5C42\u9009\u62E9=CT_SELECTBYLAYER");
            sb.AppendLine("\u6309\u8272\u9009\u62E9=CT_SELECTBYCOLOR");
            sb.AppendLine("# \u56FE\u5757\u64CD\u4F5C");
            sb.AppendLine("\u91CD\u547D\u540D\u5757=CT_RENAMEBLOCK");
            sb.AppendLine("\u5FEB\u6377\u5EFA\u5757=CT_QUICKBLOCK");
            sb.AppendLine("\u6539\u5757\u57FA\u70B9=CT_CHANGEBASEPOINT");
            sb.AppendLine("\u6309\u5757\u9009\u62E9=CT_SELECTBYBLOCK");
            sb.AppendLine("# \u7ED8\u56FE\u6807\u6CE8");
            sb.AppendLine("\u753B\u4E2D\u5FC3\u7EBF=CT_CENTERLINE");
            sb.AppendLine("\u5FEB\u901F\u6807\u6CE8=CT_QUICKDIM");
            sb.AppendLine("\u9012\u589E\u590D\u5236=CT_INCCOPY");
            sb.AppendLine("Z\u8F74\u5F52\u96F6=CT_FLATTEN");
            sb.AppendLine();
            sb.AppendLine("[LayerStandard]");
            sb.AppendLine("0-\u8BBE\u5907\u5C42=4|CONTINUOUS|Default|true");
            sb.AppendLine("1-\u4E2D\u5FC3\u7EBF\u5C42=1|CENTER|Default|true");
            sb.AppendLine("2-\u865A\u7EBF\u5C42=4|HIDDEN|Default|true");
            sb.AppendLine("3-\u6587\u5B57\u5C42=3|CONTINUOUS|Default|true");
            sb.AppendLine("4-\u6807\u6CE8\u5C42=3|CONTINUOUS|Default|true");
            sb.AppendLine("5-\u98CE\u7F51=200|CONTINUOUS|Default|true");
            sb.AppendLine("6-\u6E9C\u7BA1=2|CONTINUOUS|Default|true");
            sb.AppendLine("7-\u6D1E\u5B54=6|CONTINUOUS|Default|true");
            sb.AppendLine("8-\u53D7\u529B\u70B9=4|CONTINUOUS|Default|true");
            sb.AppendLine("9-\u5EFA\u7B51=7|CONTINUOUS|Default|true");
            sb.AppendLine("10-\u975E\u6807=31|CONTINUOUS|Default|true");
            sb.AppendLine("11-\u586B\u5145=8|CONTINUOUS|Default|true");
            sb.AppendLine();
            sb.AppendLine("[LayerMap]");
            sb.AppendLine("0-\u8BBE\u5907\u5C42=*\u8BBE\u5907*,0-4,*VIS*");
            sb.AppendLine("1-\u4E2D\u5FC3\u7EBF\u5C42=*\u4E2D\u5FC3*,*\u4E2D\u5FC3\u7EBF*,*CENTER*,0-1,1,*AXIS*,*CLEARANCE*,ZX,ZXX");
            sb.AppendLine("2-\u865A\u7EBF\u5C42=*\u865A\u7EBF*,*HIDDEN,*DASH,*HID");
            sb.AppendLine("3-\u6587\u5B57\u5C42=*\u6587\u5B57*,*\u8BF4\u660E*,*\u7F16\u53F7*,*TEXT,*txt");
            sb.AppendLine("4-\u6807\u6CE8\u5C42=*\u6807\u6CE8*,*\u5C3A\u5BF8*,*DIM*,*dim*");
            sb.AppendLine("5-\u98CE\u7F51=*\u98CE\u7F51*,*\u98CE\u7BA1*,*\u98CE\u9053*,0-5,FW");
            sb.AppendLine("6-\u6E9C\u7BA1=*\u6E9C\u7BA1*,*\u538B\u8FD0*,*VIS5");
            sb.AppendLine("7-\u6D1E\u5B54=*\u6D1E\u53E3*,*\u6D1E\u5B54*,*\u5F00\u6D1E*");
            sb.AppendLine("8-\u53D7\u529B\u70B9=*\u53D7\u529B*,*\u53D7\u529B\u70B9*,*\u540A\u70B9*");
            sb.AppendLine("9-\u5EFA\u7B51=*\u5EFA\u7B51*,*ARCH,*WALL");
            sb.AppendLine("10-\u975E\u6807=*\u975E\u6807*,*\u975E\u6807\u51C6*");
            sb.AppendLine("11-\u586B\u5145=*\u586B\u5145*,*HATCH,*PAT");
            sb.AppendLine();
            sb.AppendLine("[TextStyleStandard]");
            sb.AppendLine("STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0");
            sb.AppendLine("TITLE-TEXT=\u9ED1\u4F53||0|1.0|0");
            sb.AppendLine();
            sb.AppendLine("[TextStyleMap]");
            sb.AppendLine("STANDARD-TEXT=Standard,txt,*\u5B8B\u4F53*,HZTXT");
            sb.AppendLine("TITLE-TEXT=*\u6807\u9898*,TITLE");
            return sb.ToString();
        }

        static string IniPath { get { return Path.Combine(_dir ?? ".", "CadToolkit.ini"); } }

        // Inline comments are recognized only when # or ; appears at the start of
        // a value token or after whitespace. Values that intentionally begin with
        // # or ; are not supported by this simple INI parser.
        static string StripInlineComment(string value)
        {
            if (value == null) return "";
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if ((ch == '#' || ch == ';') && (i == 0 || char.IsWhiteSpace(value[i - 1])))
                    return value.Substring(0, i).TrimEnd();
            }
            return value.Trim();
        }

        public static string GetString(string key, string def)
        {
            try
            {
                if (!File.Exists(IniPath)) return def;
                foreach (string line in File.ReadAllLines(IniPath, Encoding.UTF8))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#") || t.StartsWith(";") || t.StartsWith("[")) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0 && t.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        return StripInlineComment(t.Substring(eq + 1));
                }
            } catch (Exception ex) { LogConfigError("GetString failed: " + ex.Message); }
            return def;
        }

        public static int GetInt(string key, int def) { int v; return int.TryParse(GetString(key, ""), out v) ? v : def; }
        public static bool GetBool(string key, bool def) { bool v; return bool.TryParse(GetString(key, ""), out v) ? v : def; }

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var v = typeof(Config).Assembly.GetName().Version;
                    if (v != null)
                    {
                        string version = "v" + v.Major + "." + v.Minor;
                        if (v.Build > 0) version += "." + v.Build;
                        return version;
                    }
                }
                catch (Exception ex) { LogConfigError("Read assembly version failed: " + ex.Message); }
                return "v1.25";
            }
        }

        public static string Version { get { return CurrentVersion; } }
        public static string Prefix { get { return GetString("QuickBlockPrefix", "BK"); } }
        public static bool DeleteOriginal { get { return GetBool("DeleteOriginal", true); } }
        public static bool KeepOriginal { get { return GetBool("KeepOriginal", false); } }

        public static int AlignHorizontal { get { return GetInt("AlignHorizontal", 0); } set { SaveInt("AlignHorizontal", value); } }
        public static bool AlignUseFirstBase { get { return GetBool("AlignUseFirstBase", true); } set { SaveBool("AlignUseFirstBase", value); } }
        public static double AlignLineSpacing { get { double v; return double.TryParse(GetString("AlignLineSpacing", "0"), out v) ? v : 0; } set { SaveString("AlignLineSpacing", value.ToString()); } }
        public static bool IsoLayerKeepLayer0 { get { return GetBool("IsoLayerKeepLayer0", false); } }
        public static bool LayerStandardFallbackTo0 { get { return GetBool("LayerStandardFallbackTo0", false); } }
        public static string LayerStandardWhitelist { get { return GetString("LayerStandardWhitelist", GetString("LayerStandardFallbackWhitelist", "0,Defpoints,*\u56FE\u6846*,*\u89C6\u53E3*,*\u539F\u6709*,*\u65B0\u589E*")); } }
        public static bool TextStyleFallbackToStandard { get { return GetBool("TextStyleFallbackToStandard", false); } }
        public static string TextStyleFallbackStyle { get { return GetString("TextStyleFallbackStyle", "STANDARD-TEXT"); } }
        public static string TextStyleWhitelist { get { return GetString("TextStyleWhitelist", "Standard,Annotative,*DIM*"); } }
        public static bool TextStyleNormalizeHeight { get { return GetBool("TextStyleNormalizeHeight", false); } }
        public static bool TextStyleNormalizeWidthFactor { get { return GetBool("TextStyleNormalizeWidthFactor", false); } }
        public static bool TextStyleNormalizeOblique { get { return GetBool("TextStyleNormalizeOblique", false); } }
        public static bool TextStyleNormalizeColorByLayer { get { return GetBool("TextStyleNormalizeColorByLayer", false); } }
        public static bool TextStyleDeleteUnusedOldStyles { get { return GetBool("TextStyleDeleteUnusedOldStyles", false); } }

        static void SaveString(string key, string val)
        {
            try
            {
                var lines = File.Exists(IniPath) ? new List<string>(File.ReadAllLines(IniPath, Encoding.UTF8)) : new List<string>();
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("#") || t.StartsWith(";") || t.StartsWith("[")) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0 && t.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = key + "=" + val;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    // insert before [Commands] section if present, otherwise append
                    int insertAt = lines.Count;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].Trim().StartsWith("["))
                        {
                            insertAt = i;
                            break;
                        }
                    }
                    lines.Insert(insertAt, key + "=" + val);
                }
                lock (_fileLock) { File.WriteAllLines(IniPath, lines.ToArray(), Encoding.UTF8); }
            } catch (Exception ex) { LogConfigError("SaveString failed: " + ex.Message); }
        }
        static void SaveInt(string key, int val) { SaveString(key, val.ToString()); }
        static void SaveBool(string key, bool val) { SaveString(key, val.ToString()); }

        static List<KeyValuePair<string, string>> GetSectionValues(string sectionName)
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                if (!File.Exists(IniPath)) return list;
                bool inSection = false;
                foreach (string line in File.ReadAllLines(IniPath, Encoding.UTF8))
                {
                    string t = line.Trim();
                    if (t.StartsWith("["))
                    {
                        inSection = t.Equals("[" + sectionName + "]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inSection || t.Length == 0 || t.StartsWith("#") || t.StartsWith(";")) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0)
                    {
                        string key = t.Substring(0, eq).Trim();
                        string val = StripInlineComment(t.Substring(eq + 1));
                        if (key.Length > 0 && val.Length > 0)
                            list.Add(new KeyValuePair<string, string>(key, val));
                    }
                }
            } catch (Exception ex) { LogConfigError("GetSectionValues failed: " + ex.Message); }
            return list;
        }

        public static List<LayerStandardRule> GetLayerStandards()
        {
            var rules = new List<LayerStandardRule>();
            foreach (var item in GetSectionValues("LayerStandard"))
            {
                var rule = new LayerStandardRule();
                rule.Name = item.Key;
                rule.ColorIndex = 7;
                rule.Linetype = "Continuous";
                rule.LineWeight = "Default";
                rule.Plot = true;
                string[] parts = item.Value.Split('|');
                int color;
                if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out color)) rule.ColorIndex = color;
                if (parts.Length > 1 && parts[1].Trim().Length > 0) rule.Linetype = parts[1].Trim();
                if (parts.Length > 2 && parts[2].Trim().Length > 0) rule.LineWeight = parts[2].Trim();
                bool plot;
                if (parts.Length > 3 && bool.TryParse(parts[3].Trim(), out plot)) rule.Plot = plot;
                rules.Add(rule);
            }

            foreach (var item in GetSectionValues("LayerMap"))
            {
                LayerStandardRule rule = null;
                foreach (var r in rules)
                {
                    if (r.Name.Equals(item.Key, StringComparison.OrdinalIgnoreCase)) { rule = r; break; }
                }
                if (rule == null)
                {
                    rule = new LayerStandardRule { Name = item.Key, ColorIndex = 7, Linetype = "Continuous", LineWeight = "Default", Plot = true };
                    rules.Add(rule);
                }
                string[] aliases = item.Value.Split(',');
                foreach (string alias in aliases)
                {
                    string a = alias.Trim();
                    if (a.Length > 0) rule.Aliases.Add(a);
                }
            }
            return rules;
        }

        public static List<TextStyleStandardRule> GetTextStyleStandards()
        {
            var rules = new List<TextStyleStandardRule>();
            foreach (var item in GetSectionValues("TextStyleStandard"))
            {
                var rule = new TextStyleStandardRule();
                rule.Name = item.Key;
                rule.FontFile = "";
                rule.BigFontFile = "";
                rule.FixedHeight = 0;
                rule.WidthFactor = 1.0;
                rule.ObliqueAngle = 0;
                string[] parts = item.Value.Split('|');
                if (parts.Length > 0) rule.FontFile = parts[0].Trim();
                if (parts.Length > 1) rule.BigFontFile = parts[1].Trim();
                double d;
                if (parts.Length > 2 && double.TryParse(parts[2].Trim(), out d)) rule.FixedHeight = d;
                if (parts.Length > 3 && double.TryParse(parts[3].Trim(), out d)) rule.WidthFactor = d;
                if (parts.Length > 4 && double.TryParse(parts[4].Trim(), out d)) rule.ObliqueAngle = d;
                rules.Add(rule);
            }
            return rules;
        }

        public static List<TextStyleMapRule> GetTextStyleMapRules()
        {
            var rules = new List<TextStyleMapRule>();
            foreach (var item in GetSectionValues("TextStyleMap"))
            {
                var rule = new TextStyleMapRule();
                rule.TargetStyle = item.Key;
                string[] aliases = item.Value.Split(',');
                foreach (string alias in aliases)
                {
                    string a = alias.Trim();
                    if (a.Length > 0) rule.Aliases.Add(a);
                }
                rules.Add(rule);
            }
            return rules;
        }

        public static List<KeyValuePair<string, string>> GetCommands()
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                if (!File.Exists(IniPath)) return list;
                bool inSection = false;
                foreach (string line in File.ReadAllLines(IniPath, Encoding.UTF8))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#") || t.StartsWith(";")) continue;
                    if (t.StartsWith("["))
                    {
                        inSection = t.Equals("[Commands]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inSection) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0)
                    {
                        string label = t.Substring(0, eq).Trim();
                        string cmd = StripInlineComment(t.Substring(eq + 1));
                        if (label.Length > 0 && cmd.Length > 0)
                            list.Add(new KeyValuePair<string, string>(label, cmd));
                    }
                }
            } catch (Exception ex) { LogConfigError("GetCommands failed: " + ex.Message); }
            return list;
        }

                public static List<CommandGroup> GetCommandGroups()
        {
            var groups = new List<CommandGroup>();
            var current = new CommandGroup { Name = "" };
            groups.Add(current);
            try
            {
                if (!File.Exists(IniPath)) return groups;
                bool inSection = false;
                foreach (string line in File.ReadAllLines(IniPath, Encoding.UTF8))
                {
                    string t = line.Trim();
                    if (t.StartsWith("["))
                    {
                        inSection = t.Equals("[Commands]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inSection) continue;
                    // detect group header: # GroupName
                    if (t.StartsWith("#") && !t.Contains("="))
                    {
                        string name = t.TrimStart('#', ' ', '\u2500', '\u2500').Trim();
                        if (name.Length > 0)
                        {
                            current = new CommandGroup { Name = name };
                            groups.Add(current);
                        }
                        continue;
                    }
                    if (t.Length == 0 || t.StartsWith(";")) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0)
                    {
                        string label = t.Substring(0, eq).Trim();
                        string cmd = StripInlineComment(t.Substring(eq + 1));
                        if (label.Length > 0 && cmd.Length > 0)
                            current.Commands.Add(new KeyValuePair<string, string>(label, cmd));
                    }
                }
            } catch (Exception ex) { LogConfigError("GetCommandGroups failed: " + ex.Message); }
            // remove empty groups
            for (int i = groups.Count - 1; i >= 0; i--)
                if (groups[i].Commands.Count == 0) groups.RemoveAt(i);
            return groups;
        }
        public static void SaveCommand(string label, string cmd)
        {
            try
            {
                var lines = File.Exists(IniPath) ? new List<string>(File.ReadAllLines(IniPath, Encoding.UTF8)) : new List<string>();
                bool hasSection = false;
                int insertIdx = lines.Count;
                bool inSection = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    if (t.Equals("[Commands]", StringComparison.OrdinalIgnoreCase)) { inSection = true; hasSection = true; continue; }
                    if (inSection && t.StartsWith("[")) { insertIdx = i; break; }
                    if (inSection) insertIdx = i + 1;
                }
                if (!hasSection)
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add("");
                    lines.Add("[Commands]");
                    insertIdx = lines.Count;
                }
                lines.Insert(insertIdx, label + "=" + cmd);
                lock (_fileLock) { File.WriteAllLines(IniPath, lines.ToArray(), Encoding.UTF8); }
            } catch (Exception ex) { LogConfigError("SaveCommand failed: " + ex.Message); }
        }

        public static void RemoveCommand(string label)
        {
            try
            {
                if (!File.Exists(IniPath)) return;
                var lines = new List<string>(File.ReadAllLines(IniPath, Encoding.UTF8));
                bool inSection = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("["))
                    {
                        if (t.Equals("[Commands]", StringComparison.OrdinalIgnoreCase)) { inSection = true; continue; }
                        if (inSection) break;
                    }
                    if (!inSection) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0 && t.Substring(0, eq).Trim().Equals(label, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.RemoveAt(i);
                        break;
                    }
                }
                lock (_fileLock) { File.WriteAllLines(IniPath, lines.ToArray(), Encoding.UTF8); }
            } catch (Exception ex) { LogConfigError("RemoveCommand failed: " + ex.Message); }
        }

    public class CommandGroup
    {
        public string Name;
        public List<KeyValuePair<string, string>> Commands = new List<KeyValuePair<string, string>>();
    }

    public class LayerStandardRule
    {
        public string Name;
        public int ColorIndex;
        public string Linetype;
        public string LineWeight;
        public bool Plot;
        public List<string> Aliases = new List<string>();
    }

    public class TextStyleStandardRule
    {
        public string Name;
        public string FontFile;
        public string BigFontFile;
        public double FixedHeight;
        public double WidthFactor;
        public double ObliqueAngle;
    }

    public class TextStyleMapRule
    {
        public string TargetStyle;
        public List<string> Aliases = new List<string>();
    }
    }
}
