using System;
using System.IO;

namespace BlockBrowser
{
    public static class LibraryPathService
    {
        public static ActiveLibraryResult RefreshActiveLibrary(BlockBrowserConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");

            var settings = new SyncSettings
            {
                LibraryPath = config.NasLibraryPath,
                LocalMirrorPath = config.LocalMirrorPath,
                PreferLocalWhenNasUnavailable = config.PreferLocalWhenNasUnavailable,
                CurrentLibraryMode = config.CurrentLibraryMode,
                UserName = config.SyncUserName
            };

            bool nasAvailable = !string.IsNullOrEmpty(config.NasLibraryPath) && Directory.Exists(config.NasLibraryPath);
            bool localAvailable = !string.IsNullOrEmpty(config.LocalMirrorPath) && Directory.Exists(config.LocalMirrorPath);
            ActiveLibraryResult activeLibrary = ActiveLibraryResolver.Resolve(settings, nasAvailable, localAvailable);

            config.LibraryPath = activeLibrary.IsAvailable ? activeLibrary.ActivePath : config.NasLibraryPath;
            return activeLibrary;
        }

        public static string GetLocalJournalPath(string localMirrorPath)
        {
            return Path.Combine(localMirrorPath ?? "", ".blockbrowser", "local-changes.json");
        }

        public static string ToLibraryRelativePath(string libraryPath, string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(libraryPath))
                return fullPath ?? "";

            try
            {
                string root = BlockBrowserConfigStore.EnsureTrailingSeparator(Path.GetFullPath(libraryPath));
                string path = Path.GetFullPath(fullPath);
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return path.Substring(root.Length);
            }
            catch { }

            return fullPath;
        }
    }
}
