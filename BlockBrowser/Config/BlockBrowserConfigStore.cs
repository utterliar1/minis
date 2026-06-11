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
                if (string.IsNullOrEmpty(val) && !key.Equals("UserName", StringComparison.OrdinalIgnoreCase)) continue;

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

            var lines = new List<string>();
            lines.Add("# 块浏览器配置文件");
            lines.Add("LibraryPath=" + ToConfigPath(config.LibraryPath));
            lines.Add("NasLibraryPath=" + ToConfigPath(config.NasLibraryPath));
            lines.Add("LocalMirrorPath=" + ToConfigPath(config.LocalMirrorPath));
            lines.Add("PreferLocalWhenNasUnavailable=" + (config.PreferLocalWhenNasUnavailable ? "1" : "0"));
            lines.Add("AllowNasSync=" + (config.AllowNasSync ? "1" : "0"));
            lines.Add("CurrentLibraryMode=" + config.CurrentLibraryMode);
            lines.Add("UserName=" + (config.SyncUserName ?? ""));
            lines.Add("ThumbSize=" + config.ThumbSize);
            lines.Add("InsertScale=" + config.InsertScale.ToString("G"));
            lines.Add("InsertRotation=" + (config.InsertRotation * 180.0 / Math.PI).ToString("G"));
            lines.Add("FormWidth=" + config.FormWidth);
            lines.Add("FormHeight=" + config.FormHeight);
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
            AddMissingConfigLine(missingLines, loadedKeys, "LibraryPath", ToConfigPath(config.LibraryPath));
            AddMissingConfigLine(missingLines, loadedKeys, "NasLibraryPath", ToConfigPath(config.NasLibraryPath));
            AddMissingConfigLine(missingLines, loadedKeys, "LocalMirrorPath", ToConfigPath(config.LocalMirrorPath));
            AddMissingConfigLine(missingLines, loadedKeys, "PreferLocalWhenNasUnavailable", config.PreferLocalWhenNasUnavailable ? "1" : "0");
            AddMissingConfigLine(missingLines, loadedKeys, "AllowNasSync", config.AllowNasSync ? "1" : "0");
            AddMissingConfigLine(missingLines, loadedKeys, "CurrentLibraryMode", config.CurrentLibraryMode.ToString());
            AddMissingConfigLine(missingLines, loadedKeys, "UserName", config.SyncUserName ?? "");
            AddMissingConfigLine(missingLines, loadedKeys, "ThumbSize", config.ThumbSize.ToString());
            AddMissingConfigLine(missingLines, loadedKeys, "InsertScale", config.InsertScale.ToString("G"));
            AddMissingConfigLine(missingLines, loadedKeys, "InsertRotation", (config.InsertRotation * 180.0 / Math.PI).ToString("G"));
            AddMissingConfigLine(missingLines, loadedKeys, "FormWidth", config.FormWidth.ToString());
            AddMissingConfigLine(missingLines, loadedKeys, "FormHeight", config.FormHeight.ToString());

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

        private static void AddMissingConfigLine(List<string> lines, HashSet<string> loadedKeys, string key, string value)
        {
            if (loadedKeys.Contains(key)) return;
            lines.Add(key + "=" + (value ?? ""));
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
