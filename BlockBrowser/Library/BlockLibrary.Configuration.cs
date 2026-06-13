using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static string LibraryPath { get; set; }
        public const string AppVersion = "1.3.3";
        public static string PlatformName { get; set; }
        public static int ThumbSize { get; set; }
        public static double InsertScale { get; set; }
        public static double InsertRotation { get; set; }
        public static int FormWidth { get; set; }
        public static int FormHeight { get; set; }
        public static string NasLibraryPath { get; set; }
        public static string LocalMirrorPath { get; set; }
        private static readonly List<string> _protectedLocalCategories = new List<string>();
        public static List<string> ProtectedLocalCategories { get { return _protectedLocalCategories; } }
        public static bool PreferLocalWhenNasUnavailable { get; set; }
        public static bool AllowNasSync { get; set; }
        public static LibraryMode CurrentLibraryMode { get; set; }
        public static string SyncUserName { get; set; }
        public static ActiveLibraryResult ActiveLibrary { get; private set; }
        private static BlockBrowserConfigStore _configStore;

        static BlockLibrary()
        {
            _configStore = new BlockBrowserConfigStore(PluginRoot);
            ApplyConfig(BlockBrowserConfig.CreateDefault(PluginRoot));
            LoadConfig();
        }

        public static string ThumbnailCachePath { get { return Path.Combine(LibraryPath, ".thumbs"); } }
        public static string SyncLogPath { get { return Path.Combine(LocalMirrorPath ?? "", ".blockbrowser", "sync-log.txt"); } }

        private static List<string> _recentBlocks = new List<string>();
        private const int MAX_RECENT = 20;

        public static void AddRecentBlock(string filePath)
        {
            _recentBlocks.Remove(filePath);
            _recentBlocks.Insert(0, filePath);
            if (_recentBlocks.Count > MAX_RECENT)
                _recentBlocks.RemoveRange(MAX_RECENT, _recentBlocks.Count - MAX_RECENT);
            SaveConfig();
        }

        public static void RemoveRecentBlock(string filePath)
        {
            if (_recentBlocks.Remove(filePath)) SaveConfig();
        }

        private static string ConfigPath
        {
            get
            {
                return _configStore.ConfigPath;
            }
        }

        private static string DefaultConfigPath
        {
            get
            {
                // Deployment smoke tests verify the plugin knows about BlockBrowser.default.ini.
                return _configStore.DefaultConfigPath;
            }
        }

        private static string PluginRoot
        {
            get
            {
                string dllDir = Path.GetDirectoryName(typeof(BlockLibrary).Assembly.Location) ?? "";
                return Path.GetFullPath(Path.Combine(dllDir, ".."));
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return BlockBrowserConfigStore.EnsureTrailingSeparator(path);
        }

        private static string ToConfigPath(string path)
        {
            return _configStore.ToConfigPath(path);
        }

        private static string FromConfigPath(string path)
        {
            return _configStore.FromConfigPath(path);
        }

        public static bool IsSafeLibraryName(string name)
        {
            return BlockBrowserConfigStore.IsSafeLibraryName(name);
        }

        private static BlockBrowserConfig CaptureConfig()
        {
            var config = new BlockBrowserConfig
            {
                LibraryPath = LibraryPath,
                NasLibraryPath = NasLibraryPath,
                LocalMirrorPath = LocalMirrorPath,
                PreferLocalWhenNasUnavailable = PreferLocalWhenNasUnavailable,
                AllowNasSync = AllowNasSync,
                CurrentLibraryMode = CurrentLibraryMode,
                SyncUserName = SyncUserName,
                ThumbSize = ThumbSize,
                InsertScale = InsertScale,
                InsertRotation = InsertRotation,
                FormWidth = FormWidth,
                FormHeight = FormHeight
            };

            config.ProtectedLocalCategories.Clear();
            config.ProtectedLocalCategories.AddRange(ProtectedLocalCategories);
            config.RecentBlocks.AddRange(_recentBlocks);
            return config;
        }

        private static void ApplyConfig(BlockBrowserConfig config)
        {
            if (config == null) return;

            LibraryPath = config.LibraryPath;
            NasLibraryPath = config.NasLibraryPath;
            LocalMirrorPath = config.LocalMirrorPath;
            ProtectedLocalCategories.Clear();
            ProtectedLocalCategories.AddRange(config.ProtectedLocalCategories);
            PreferLocalWhenNasUnavailable = config.PreferLocalWhenNasUnavailable;
            AllowNasSync = config.AllowNasSync;
            CurrentLibraryMode = config.CurrentLibraryMode;
            SyncUserName = config.SyncUserName;
            ThumbSize = config.ThumbSize;
            InsertScale = config.InsertScale;
            InsertRotation = config.InsertRotation;
            FormWidth = config.FormWidth;
            FormHeight = config.FormHeight;

            _recentBlocks.Clear();
            _recentBlocks.AddRange(config.RecentBlocks);
        }

        public static void LoadConfig()
        {
            ApplyConfig(_configStore.Load(CaptureConfig()));
        }

        private static void EnsureUserConfigExists()
        {
            _configStore.EnsureUserConfigExists();
        }

        public static void SaveConfig()
        {
            _configStore.Save(CaptureConfig());
        }

        public static ActiveLibraryResult RefreshActiveLibrary()
        {
            var config = CaptureConfig();
            ActiveLibrary = LibraryPathService.RefreshActiveLibrary(config);
            ApplyConfig(config);
            return ActiveLibrary;
        }

        public static string LocalJournalPath
        {
            get { return LibraryPathService.GetLocalJournalPath(LocalMirrorPath); }
        }
        public static string ToLibraryRelativePath(string fullPath)
        {
            return LibraryPathService.ToLibraryRelativePath(LibraryPath, fullPath);
        }

        public static string GetProtectedLocalCategoriesText()
        {
            return string.Join(";", ProtectedLocalCategories.ToArray());
        }

        public static void SetProtectedLocalCategoriesFromText(string text)
        {
            ProtectedLocalCategories.Clear();
            foreach (string part in (text ?? "").Split(new[] { ';', '；', ',', '，', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string category = part.Trim();
                if (string.IsNullOrEmpty(category)) continue;
                if (!ProtectedLocalCategories.Contains(category)) ProtectedLocalCategories.Add(category);
            }
        }

        private static bool IsProtectedLocalCategoryPath(string relativePath)
        {
            if (ProtectedLocalCategories.Count == 0 || string.IsNullOrEmpty(relativePath)) return false;
            string[] parts = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 && ProtectedLocalCategories.Contains(parts[0], StringComparer.OrdinalIgnoreCase);
        }
        private static void EnsureActiveLibraryWritable()
        {
            if (ActiveLibrary != null && ActiveLibrary.Kind == ActiveLibraryKind.Nas && !AllowNasSync)
                throw new InvalidOperationException("当前电脑未启用写入 NAS。请先更新本地图库后，在本地副本中操作。");
        }
    }
}
