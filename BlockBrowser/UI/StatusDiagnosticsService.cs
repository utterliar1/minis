using System;
using System.Text;

namespace BlockBrowser
{
    public static class StatusDiagnosticsService
    {
        public static string FormatReport(
            string appVersion,
            string platformName,
            LibraryMode currentLibraryMode,
            ActiveLibraryResult activeLibrary,
            string libraryPath,
            string nasLibraryPath,
            string localMirrorPath,
            bool nasAvailable,
            bool localMirrorAvailable,
            bool allowNasSync,
            int localChangeCount,
            int thumbnailCacheCount,
            string syncUserName,
            string localJournalPath,
            string thumbnailCachePath)
        {
            var activeKind = activeLibrary == null ? ActiveLibraryKind.None : activeLibrary.Kind;
            var activePath = activeLibrary == null ? "" : activeLibrary.ActivePath;
            var activeAvailable = activeLibrary != null && activeLibrary.IsAvailable;
            var activeMessage = activeLibrary == null ? "" : activeLibrary.Message;

            var sb = new StringBuilder();
            sb.AppendLine("块浏览器状态诊断");
            sb.AppendLine("版本: " + (appVersion ?? ""));
            sb.AppendLine("平台: " + (platformName ?? ""));
            sb.AppendLine("当前模式: " + currentLibraryMode);
            sb.AppendLine();
            sb.AppendLine("当前图库: " + activeKind);
            sb.AppendLine("实际路径: " + (activePath ?? ""));
            sb.AppendLine("实际路径可用: " + YesNo(activeAvailable));
            sb.AppendLine("状态: " + (activeMessage ?? ""));
            sb.AppendLine();
            sb.AppendLine("LibraryPath: " + (libraryPath ?? ""));
            sb.AppendLine("NasLibraryPath: " + (nasLibraryPath ?? ""));
            sb.AppendLine("LocalMirrorPath: " + (localMirrorPath ?? ""));
            sb.AppendLine("NAS 可访问: " + YesNo(nasAvailable));
            sb.AppendLine("本地副本可访问: " + YesNo(localMirrorAvailable));
            sb.AppendLine("允许同步到 NAS: " + YesNo(allowNasSync));
            sb.AppendLine();
            sb.AppendLine("本地变更记录: " + localChangeCount);
            sb.AppendLine("变更记录文件: " + (localJournalPath ?? ""));
            sb.AppendLine("缩略图缓存: " + thumbnailCacheCount);
            sb.AppendLine("缩略图目录: " + (thumbnailCachePath ?? ""));
            sb.AppendLine("同步用户: " + (syncUserName ?? ""));
            return sb.ToString();
        }

        private static string YesNo(bool value)
        {
            return value ? "是" : "否";
        }
    }
}
