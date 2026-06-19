namespace BlockBrowser
{
    public static class BlockBrowserInfoService
    {
        public static string[] FormatLines(string appVersion, string platformName, string libraryPath)
        {
            return new[]
            {
                "=== 块浏览器 v" + appVersion + " (" + platformName + ") ===",
                "库: " + libraryPath,
                "命令: BB KLLQ BBADD BBEXPORT BBMIRROR BBSYNC BBTHUMB BBINFO"
            };
        }
    }
}
