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
                    string def = "# CadToolkit \u914D\u7F6E\u6587\u4EF6\r\nVersion=v1.21\r\n\r\n# \u5FEB\u6377\u5EFA\u5757\r\nQuickBlockPrefix=BK\r\nDeleteOriginal=true\r\nKeepOriginal=false\r\n\r\n[Commands]\r\n# \u6587\u5B57\u7F16\u8F91\r\n\u67E5\u627E\u66FF\u6362=CT_FINDREPLACE\r\n\u6587\u5B57\u5BF9\u9F50=CT_ALIGN\r\n\u52A0\u4E0B\u5212\u7EBF=CT_UNDERLINE\r\n\u683C\u5F0F\u590D\u5236=CT_TEXTBRUSH\r\n\u6587\u5B57\u5408\u5E76=CT_TEXTMERGE\r\n\u6587\u5B57\u7F16\u53F7=CT_TEXTNUMBER\r\n# \u56FE\u5C42\u7BA1\u7406\r\n\u56FE\u5C42\u5F52\u96F6=CT_SETLAYER0\r\n\u5B64\u7ACB\u56FE\u5C42=CT_ISOLAYER\r\n\u6309\u5C42\u9009\u62E9=CT_SELECTBYLAYER\r\n\u6309\u8272\u9009\u62E9=CT_SELECTBYCOLOR\r\n\u6309\u5757\u9009\u62E9=CT_SELECTBYBLOCK\r\n# \u56FE\u5757\u64CD\u4F5C\r\n\u91CD\u547D\u540D\u5757=CT_RENAMEBLOCK\r\n\u5FEB\u6377\u5EFA\u5757=CT_QUICKBLOCK\r\n# \u7ED8\u56FE\u6807\u6CE8\r\n\u753B\u4E2D\u5FC3\u7EBF=CT_CENTERLINE\r\n\u5FEB\u901F\u6807\u6CE8=CT_QUICKDIM\r\n\u9012\u589E\u590D\u5236=CT_INCCOPY\r\n";
                    lock (_fileLock) { File.WriteAllText(IniPath, def, Encoding.UTF8); }
                }
            }
            catch { }
        }

        static string IniPath { get { return Path.Combine(_dir ?? ".", "CadToolkit.ini"); } }

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
                        return t.Substring(eq + 1).Trim();
                }
            } catch { }
            return def;
        }

        public static int GetInt(string key, int def) { int v; return int.TryParse(GetString(key, ""), out v) ? v : def; }
        public static bool GetBool(string key, bool def) { bool v; return bool.TryParse(GetString(key, ""), out v) ? v : def; }

        public static string Version { get { return GetString("Version", "v1.0"); } }
        public static string Prefix { get { return GetString("QuickBlockPrefix", "BK"); } }
        public static bool DeleteOriginal { get { return GetBool("DeleteOriginal", true); } }
        public static bool KeepOriginal { get { return GetBool("KeepOriginal", false); } }

        public static int AlignHorizontal { get { return GetInt("AlignHorizontal", 0); } set { SaveInt("AlignHorizontal", value); } }
        public static bool AlignUseFirstBase { get { return GetBool("AlignUseFirstBase", true); } set { SaveBool("AlignUseFirstBase", value); } }
        public static double AlignLineSpacing { get { double v; return double.TryParse(GetString("AlignLineSpacing", "0"), out v) ? v : 0; } set { SaveString("AlignLineSpacing", value.ToString()); } }

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
            } catch { }
        }
        static void SaveInt(string key, int val) { SaveString(key, val.ToString()); }
        static void SaveBool(string key, bool val) { SaveString(key, val.ToString()); }

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
                        string cmd = t.Substring(eq + 1).Trim();
                        if (label.Length > 0 && cmd.Length > 0)
                            list.Add(new KeyValuePair<string, string>(label, cmd));
                    }
                }
            } catch { }
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
                        string cmd = t.Substring(eq + 1).Trim();
                        if (label.Length > 0 && cmd.Length > 0)
                            current.Commands.Add(new KeyValuePair<string, string>(label, cmd));
                    }
                }
            } catch { }
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
                int insertIdx = lines.Count;
                bool inSection = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    if (t.Equals("[Commands]", StringComparison.OrdinalIgnoreCase)) { inSection = true; continue; }
                    if (inSection && t.StartsWith("[")) { insertIdx = i; break; }
                    if (inSection) insertIdx = i + 1;
                }
                lines.Insert(insertIdx, label + "=" + cmd);
                lock (_fileLock) { File.WriteAllLines(IniPath, lines.ToArray(), Encoding.UTF8); }
            } catch { }
        }

        public static void RemoveCommand(string label)
        {
            try
            {
                if (!File.Exists(IniPath)) return;
                var lines = new List<string>(File.ReadAllLines(IniPath, Encoding.UTF8));
                bool inSection = false;
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("[")) { if (t.Equals("[Commands]", StringComparison.OrdinalIgnoreCase)) inSection = true; else if (inSection) break; }
                    if (!inSection) continue;
                    int eq = t.IndexOf('=');
                    if (eq > 0 && t.Substring(0, eq).Trim().Equals(label, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.RemoveAt(i);
                        break;
                    }
                }
                lock (_fileLock) { File.WriteAllLines(IniPath, lines.ToArray(), Encoding.UTF8); }
            } catch { }
        }

    public class CommandGroup
    {
        public string Name;
        public List<KeyValuePair<string, string>> Commands = new List<KeyValuePair<string, string>>();
    }
    }
}
