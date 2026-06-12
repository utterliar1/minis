using System;

namespace BlockBrowser
{
    public static class MirrorSummaryMessageService
    {
        public static string FormatDialog(MirrorDirectoryResult result)
        {
            var counts = result ?? new MirrorDirectoryResult();
            return string.Format(
                "\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u5B8C\u6210:\n\u65B0\u589E: {0}\n\u8986\u76D6: {1}\n\u5220\u9664: {2}\n\u4FDD\u62A4\u8DF3\u8FC7: {3}",
                counts.CopiedNewCount,
                counts.OverwrittenCount,
                counts.DeletedCount,
                counts.ProtectedSkipCount);
        }

        public static string FormatCommand(MirrorDirectoryResult result)
        {
            var counts = result ?? new MirrorDirectoryResult();
            return string.Format(
                "\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u5B8C\u6210: {0} \u65B0\u589E, {1} \u8986\u76D6, {2} \u5220\u9664, {3} \u4FDD\u62A4\u8DF3\u8FC7",
                counts.CopiedNewCount,
                counts.OverwrittenCount,
                counts.DeletedCount,
                counts.ProtectedSkipCount);
        }
    }
}
