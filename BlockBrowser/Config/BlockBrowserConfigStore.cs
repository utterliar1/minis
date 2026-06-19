using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlockBrowser
{
    public sealed class BlockBrowserConfigStore
    {
        private readonly string _pluginRoot;

        public BlockBrowserConfigStore(string pluginRoot)
        {
            _pluginRoot = Path.GetFullPath(pluginRoot ?? "");
            ConfigPath = Path.Combine(_pluginRoot, "config.ini");
            DefaultConfigPath = Path.Combine(_pluginRoot, "BlockBrowser.default.ini");
        }

        public string ConfigPath { get; private set; }
        public string DefaultConfigPath { get; private set; }

        public BlockBrowserConfig Load(BlockBrowserConfig defaults)
        {
            var config = defaults == null ? BlockBrowserConfig.CreateDefault(_pluginRoot) : defaults.Clone();
            try
            {
                EnsureUserConfigExists();
                if (!File.Exists(ConfigPath)) return config;

                bool loadedNasLibraryPath = false;
                var loadedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = trimmed.Substring(0, eq).Trim();
                    string val = trimmed.Substring(eq + 1).Trim();
                    loadedKeys.Add(key);
                    if (string.IsNullOrEmpty(val)
                        && !key.Equals("UserName", StringComparison.OrdinalIgnoreCase)
                        && !key.Equals("ProtectedLocalCategories", StringComparison.OrdinalIgnoreCase)) continue;

                    if (key.Equals("ThumbSize", StringComparison.OrdinalIgnoreCase))
                    {
                        int ts;
                        if (int.TryParse(val, out ts) && ts >= 40 && ts <= 512) config.ThumbSize = ts;
                    }
                    else if (key.Equals("InsertScale", StringComparison.OrdinalIgnoreCase))
                    {
                        double ds;
                        if (double.TryParse(val, out ds) && ds > 0) config.InsertScale = ds;
                    }
                    else if (key.Equals("InsertRotation", StringComparison.OrdinalIgnoreCase))
                    {
                        double dr;
                        if (double.TryParse(val, out dr)) config.InsertRotation = dr * Math.PI / 180.0;
                    }
                    else if (key.Equals("FormWidth", StringComparison.OrdinalIgnoreCase))
                    {
                        int fw;
                        if (int.TryParse(val, out fw) && fw >= 400) config.FormWidth = fw;
                    }
                    else if (key.Equals("FormHeight", StringComparison.OrdinalIgnoreCase))
                    {
                        int fh;
                        if (int.TryParse(val, out fh) && fh >= 300) config.FormHeight = fh;
                    }
                    else if (key.Equals("RecentBlocks", StringComparison.OrdinalIgnoreCase))
                    {
                        config.RecentBlocks.Clear();
                        foreach (string recentPath in val.Split('|'))
                        {
                            string recent = recentPath.Trim();
                            if (!string.IsNullOrEmpty(recent) && !config.RecentBlocks.Contains(recent))
                                config.RecentBlocks.Add(recent);
                        }
                    }
                    else if (key.Equals("LibraryPath", StringComparison.OrdinalIgnoreCase))
                    {
                        config.LibraryPath = FromConfigPath(val);
                    }
                    else if (key.Equals("NasLibraryPath", StringComparison.OrdinalIgnoreCase))
                    {
                        config.NasLibraryPath = FromConfigPath(val);
                        loadedNasLibraryPath = true;
                    }
                    else if (key.Equals("LocalMirrorPath", StringComparison.OrdinalIgnoreCase))
                    {
                        config.LocalMirrorPath = FromConfigPath(val);
                    }
                    else if (key.Equals("ProtectedLocalCategories", StringComparison.OrdinalIgnoreCase))
                    {
                        config.ProtectedLocalCategories.Clear();
                        config.ProtectedLocalCategories.AddRange(ParseProtectedLocalCategories(val));
                    }
                    else if (key.Equals("PreferLocalWhenNasUnavailable", StringComparison.OrdinalIgnoreCase))
                    {
                        config.PreferLocalWhenNasUnavailable = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("AllowNasSync", StringComparison.OrdinalIgnoreCase))
                    {
                        config.AllowNasSync = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("CurrentLibraryMode", StringComparison.OrdinalIgnoreCase))
                    {
                        LibraryMode mode;
                        if (Enum.TryParse<LibraryMode>(val, true, out mode)) config.CurrentLibraryMode = mode;
                    }
                    else if (key.Equals("UserName", StringComparison.OrdinalIgnoreCase))
                    {
                        config.SyncUserName = val;
                    }
                }

                if (!loadedNasLibraryPath || string.IsNullOrEmpty(config.NasLibraryPath))
                    config.NasLibraryPath = config.LibraryPath;

                AppendMissingConfigKeys(config, loadedKeys);
            }
            catch { }

            return config;
        }

        public void Save(BlockBrowserConfig config)
        {
            if (config == null) return;

            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var lines = BuildConfigLines(config, true);
                if (config.RecentBlocks.Count > 0)
                    lines.Add("RecentBlocks=" + string.Join("|", config.RecentBlocks.ToArray()));

                File.WriteAllLines(ConfigPath, lines, Encoding.UTF8);
            }
            catch { }
        }

        private void AppendMissingConfigKeys(BlockBrowserConfig config, HashSet<string> loadedKeys)
        {
            if (config == null || loadedKeys == null || !File.Exists(ConfigPath)) return;

            var missingLines = new List<string>();
            AddMissingConfigEntry(missingLines, loadedKeys, "LibraryPath", ToConfigPath(config.LibraryPath), "\u5F53\u524D\u5B9E\u9645\u4F7F\u7528\u7684\u56FE\u5E93\u8DEF\u5F84\u3002\u901A\u5E38\u7531 CurrentLibraryMode \u81EA\u52A8\u6307\u5411 NAS \u6216\u672C\u5730\u526F\u672C\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "NasLibraryPath", ToConfigPath(config.NasLibraryPath), "NAS \u4E3B\u56FE\u5E93\u8DEF\u5F84\u3002\u666E\u901A\u540C\u4E8B\u4E00\u822C\u53EA\u8BFB\uFF1B\u6307\u5B9A\u7EF4\u62A4\u4EBA\u786E\u8BA4\u540E\u624D\u5199\u5165\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "LocalMirrorPath", ToConfigPath(config.LocalMirrorPath), "NAS \u56FE\u5E93\u7684\u672C\u5730\u526F\u672C\u8DEF\u5F84\u3002\u51FA\u5DEE\u6216 NAS \u4E0D\u53EF\u7528\u65F6\u4F18\u5148\u4F7F\u7528\u8FD9\u91CC\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "ProtectedLocalCategories", FormatProtectedLocalCategories(config.ProtectedLocalCategories), "\u672C\u5730\u4FDD\u62A4\u5206\u7C7B\u767D\u540D\u5355\uFF0C\u591A\u4E2A\u5206\u7C7B\u7528\u82F1\u6587\u5206\u53F7 ; \u5206\u9694\uFF1B\u8FD9\u4E9B\u5206\u7C7B\u4E0D\u4F1A\u88AB NAS \u66F4\u65B0\u8986\u76D6\u6216\u5220\u9664\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "PreferLocalWhenNasUnavailable", config.PreferLocalWhenNasUnavailable ? "1" : "0", "Auto \u6A21\u5F0F\u4E0B NAS \u4E0D\u53EF\u8BBF\u95EE\u65F6\u662F\u5426\u81EA\u52A8\u4F7F\u7528\u672C\u5730\u526F\u672C\u30021=\u542F\u7528\uFF0C0=\u7981\u7528\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "AllowNasSync", config.AllowNasSync ? "1" : "0", "\u662F\u5426\u5141\u8BB8\u672C\u673A\u628A\u672C\u5730\u53D8\u66F4\u540C\u6B65\u5230 NAS\u30020=\u53EA\u8BFB\uFF0C1=\u5141\u8BB8\u5199\u5165 NAS\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "CurrentLibraryMode", config.CurrentLibraryMode.ToString(), "\u56FE\u5E93\u6A21\u5F0F\u3002Local=\u4F7F\u7528\u672C\u5730\u526F\u672C\uFF0CNas=\u76F4\u63A5\u4F7F\u7528 NAS\uFF0CAuto=NAS \u53EF\u7528\u65F6\u7528 NAS\u3001\u4E0D\u53EF\u7528\u65F6\u7528\u672C\u5730\u526F\u672C\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "UserName", config.SyncUserName ?? "", "\u540C\u6B65\u8BB0\u5F55\u91CC\u7684\u7528\u6237\u540D\uFF1B\u7559\u7A7A\u65F6\u7A0B\u5E8F\u53EF\u4F7F\u7528\u7CFB\u7EDF\u7528\u6237\u540D\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "ThumbSize", config.ThumbSize.ToString(), "\u7F29\u7565\u56FE\u5C3A\u5BF8\uFF0C\u5355\u4F4D\u50CF\u7D20\uFF1B\u5EFA\u8BAE 96-192\uFF0C\u9ED8\u8BA4 128\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "InsertScale", config.InsertScale.ToString("G"), "\u63D2\u5165\u5757\u9ED8\u8BA4\u6BD4\u4F8B\uFF1B\u53EF\u5728\u5DE5\u5177\u680F\u201C\u63D2\u5165\u8BBE\u7F6E\u201D\u91CC\u4FEE\u6539\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "InsertRotation", (config.InsertRotation * 180.0 / Math.PI).ToString("G"), "\u63D2\u5165\u5757\u9ED8\u8BA4\u65CB\u8F6C\u89D2\u5EA6\uFF0C\u5355\u4F4D\u4E3A\u5EA6\uFF1B\u53EF\u5728\u5DE5\u5177\u680F\u201C\u63D2\u5165\u8BBE\u7F6E\u201D\u91CC\u4FEE\u6539\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "FormWidth", config.FormWidth.ToString(), "BB \u9762\u677F\u9ED8\u8BA4\u5BBD\u5EA6\uFF1B\u7A0B\u5E8F\u4F1A\u7ED3\u5408\u5C4F\u5E55\u5DE5\u4F5C\u533A\u9650\u5236\u5B9E\u9645\u5927\u5C0F\u3002");
            AddMissingConfigEntry(missingLines, loadedKeys, "FormHeight", config.FormHeight.ToString(), "BB \u9762\u677F\u9ED8\u8BA4\u9AD8\u5EA6\uFF1B\u7A0B\u5E8F\u4F1A\u7ED3\u5408\u5C4F\u5E55\u5DE5\u4F5C\u533A\u9650\u5236\u5B9E\u9645\u5927\u5C0F\u3002");

            if (missingLines.Count > 0)
            {
                var lines = new List<string>(File.ReadAllLines(ConfigPath, Encoding.UTF8));
                int insertAt = lines.Count;
                for (int i = 0; i < lines.Count; i++)
                {
                    string trimmed = (lines[i] ?? "").Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        insertAt = i;
                        break;
                    }
                }

                lines.InsertRange(insertAt, missingLines);
                File.WriteAllLines(ConfigPath, lines.ToArray(), Encoding.UTF8);
            }
        }

        private List<string> BuildConfigLines(BlockBrowserConfig config, bool userConfig)
        {
            var lines = new List<string>();
            lines.Add(userConfig ? "# BlockBrowser \u914D\u7F6E\u6587\u4EF6" : "# BlockBrowser \u9ED8\u8BA4\u914D\u7F6E\u6A21\u677F");
            lines.Add("# \u63D0\u9192\uFF1A\u4E0D\u8981\u968F\u610F\u6539\u53D8\u914D\u7F6E\u9879\u987A\u5E8F\u3002\u7A0B\u5E8F\u5347\u7EA7\u4F1A\u6309\u8FD9\u4E2A\u987A\u5E8F\u8865\u9F50\u7F3A\u5931\u9879\uFF0C\u4E5F\u65B9\u4FBF\u540C\u4E8B\u6392\u67E5\u95EE\u9898\u3002");
            lines.Add("");
            AddConfigEntry(lines, "LibraryPath", ToConfigPath(config.LibraryPath), "\u5F53\u524D\u5B9E\u9645\u4F7F\u7528\u7684\u56FE\u5E93\u8DEF\u5F84\u3002\u901A\u5E38\u7531 CurrentLibraryMode \u81EA\u52A8\u6307\u5411 NAS \u6216\u672C\u5730\u526F\u672C\u3002");
            AddConfigEntry(lines, "NasLibraryPath", ToConfigPath(config.NasLibraryPath), "NAS \u4E3B\u56FE\u5E93\u8DEF\u5F84\u3002\u666E\u901A\u540C\u4E8B\u4E00\u822C\u53EA\u8BFB\uFF1B\u6307\u5B9A\u7EF4\u62A4\u4EBA\u786E\u8BA4\u540E\u624D\u5199\u5165\u3002");
            AddConfigEntry(lines, "LocalMirrorPath", ToConfigPath(config.LocalMirrorPath), "NAS \u56FE\u5E93\u7684\u672C\u5730\u526F\u672C\u8DEF\u5F84\u3002\u51FA\u5DEE\u6216 NAS \u4E0D\u53EF\u7528\u65F6\u4F18\u5148\u4F7F\u7528\u8FD9\u91CC\u3002");
            AddConfigEntry(lines, "ProtectedLocalCategories", FormatProtectedLocalCategories(config.ProtectedLocalCategories), "\u672C\u5730\u4FDD\u62A4\u5206\u7C7B\u767D\u540D\u5355\uFF0C\u591A\u4E2A\u5206\u7C7B\u7528\u82F1\u6587\u5206\u53F7 ; \u5206\u9694\uFF1B\u8FD9\u4E9B\u5206\u7C7B\u4E0D\u4F1A\u88AB NAS \u66F4\u65B0\u8986\u76D6\u6216\u5220\u9664\u3002");
            AddConfigEntry(lines, "PreferLocalWhenNasUnavailable", config.PreferLocalWhenNasUnavailable ? "1" : "0", "Auto \u6A21\u5F0F\u4E0B NAS \u4E0D\u53EF\u8BBF\u95EE\u65F6\u662F\u5426\u81EA\u52A8\u4F7F\u7528\u672C\u5730\u526F\u672C\u30021=\u542F\u7528\uFF0C0=\u7981\u7528\u3002");
            AddConfigEntry(lines, "AllowNasSync", config.AllowNasSync ? "1" : "0", "\u662F\u5426\u5141\u8BB8\u672C\u673A\u628A\u672C\u5730\u53D8\u66F4\u540C\u6B65\u5230 NAS\u30020=\u53EA\u8BFB\uFF0C1=\u5141\u8BB8\u5199\u5165 NAS\u3002");
            AddConfigEntry(lines, "CurrentLibraryMode", config.CurrentLibraryMode.ToString(), "\u56FE\u5E93\u6A21\u5F0F\u3002Local=\u4F7F\u7528\u672C\u5730\u526F\u672C\uFF0CNas=\u76F4\u63A5\u4F7F\u7528 NAS\uFF0CAuto=NAS \u53EF\u7528\u65F6\u7528 NAS\u3001\u4E0D\u53EF\u7528\u65F6\u7528\u672C\u5730\u526F\u672C\u3002");
            AddConfigEntry(lines, "UserName", config.SyncUserName ?? "", "\u540C\u6B65\u8BB0\u5F55\u91CC\u7684\u7528\u6237\u540D\uFF1B\u7559\u7A7A\u65F6\u7A0B\u5E8F\u53EF\u4F7F\u7528\u7CFB\u7EDF\u7528\u6237\u540D\u3002");
            AddConfigEntry(lines, "ThumbSize", config.ThumbSize.ToString(), "\u7F29\u7565\u56FE\u5C3A\u5BF8\uFF0C\u5355\u4F4D\u50CF\u7D20\uFF1B\u5EFA\u8BAE 96-192\uFF0C\u9ED8\u8BA4 128\u3002");
            AddConfigEntry(lines, "InsertScale", config.InsertScale.ToString("G"), "\u63D2\u5165\u5757\u9ED8\u8BA4\u6BD4\u4F8B\uFF1B\u53EF\u5728\u5DE5\u5177\u680F\u201C\u63D2\u5165\u8BBE\u7F6E\u201D\u91CC\u4FEE\u6539\u3002");
            AddConfigEntry(lines, "InsertRotation", (config.InsertRotation * 180.0 / Math.PI).ToString("G"), "\u63D2\u5165\u5757\u9ED8\u8BA4\u65CB\u8F6C\u89D2\u5EA6\uFF0C\u5355\u4F4D\u4E3A\u5EA6\uFF1B\u53EF\u5728\u5DE5\u5177\u680F\u201C\u63D2\u5165\u8BBE\u7F6E\u201D\u91CC\u4FEE\u6539\u3002");
            AddConfigEntry(lines, "FormWidth", config.FormWidth.ToString(), "BB \u9762\u677F\u9ED8\u8BA4\u5BBD\u5EA6\uFF1B\u7A0B\u5E8F\u4F1A\u7ED3\u5408\u5C4F\u5E55\u5DE5\u4F5C\u533A\u9650\u5236\u5B9E\u9645\u5927\u5C0F\u3002");
            AddConfigEntry(lines, "FormHeight", config.FormHeight.ToString(), "BB \u9762\u677F\u9ED8\u8BA4\u9AD8\u5EA6\uFF1B\u7A0B\u5E8F\u4F1A\u7ED3\u5408\u5C4F\u5E55\u5DE5\u4F5C\u533A\u9650\u5236\u5B9E\u9645\u5927\u5C0F\u3002");
            return lines;
        }

        private static void AddConfigEntry(List<string> lines, string key, string value, string comment)
        {
            if (lines.Count > 0 && lines[lines.Count - 1] != "") lines.Add("");
            lines.Add("# " + key + "\uFF1A" + comment);
            lines.Add(key + "=" + (value ?? ""));
        }

        private static void AddMissingConfigEntry(List<string> lines, HashSet<string> loadedKeys, string key, string value, string comment)
        {
            if (loadedKeys.Contains(key)) return;
            if (lines.Count > 0 && lines[lines.Count - 1] != "") lines.Add("");
            lines.Add("# " + key + "\uFF1A" + comment);
            lines.Add(key + "=" + (value ?? ""));
        }

        private static List<string> ParseProtectedLocalCategories(string value)
        {
            var categories = new List<string>();
            if (string.IsNullOrEmpty(value)) return categories;

            foreach (string part in value.Split(new[] { ';', '\uFF1B', ',', '\uFF0C', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string category = part.Trim();
                if (string.IsNullOrEmpty(category)) continue;
                if (!categories.Contains(category)) categories.Add(category);
            }

            return categories;
        }

        private static string FormatProtectedLocalCategories(IEnumerable<string> categories)
        {
            var list = new List<string>();
            foreach (string category in categories ?? new string[0])
            {
                string item = (category ?? "").Trim();
                if (string.IsNullOrEmpty(item)) continue;
                if (!list.Contains(item)) list.Add(item);
            }

            return string.Join(";", list.ToArray());
        }

        public void EnsureUserConfigExists()
        {
            try
            {
                if (File.Exists(ConfigPath)) return;

                string dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(DefaultConfigPath))
                    File.Copy(DefaultConfigPath, ConfigPath, false);
            }
            catch { }
        }

        public string ToConfigPath(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && path.Contains("%")) return path;
                string full = Path.GetFullPath(path);
                string root = EnsureTrailingSeparator(_pluginRoot);
                if (full.Equals(_pluginRoot, StringComparison.OrdinalIgnoreCase)) return ".";
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(root.Length);
                return full;
            }
            catch
            {
                return path;
            }
        }

        public string FromConfigPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string expanded = Environment.ExpandEnvironmentVariables(path);
            if (Path.IsPathRooted(expanded))
                return expanded;
            return Path.Combine(_pluginRoot, expanded);
        }

        public static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar) return path;
            return path + Path.DirectorySeparatorChar;
        }

        public static bool IsSafeLibraryName(string name)
        {
            return LibraryNameRules.IsSafeLibraryName(name);
        }
    }
}
