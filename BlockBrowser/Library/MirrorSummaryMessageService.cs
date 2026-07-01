using System;
using System.Text;

namespace BlockBrowser
{
    public static class MirrorSummaryMessageService
    {
        private const int PreviewLimit = 20;

        public static string FormatDialog(MirrorDirectoryResult result)
        {
            var counts = result ?? new MirrorDirectoryResult();
            return string.Format(
                counts.ChangedCount == 0
                    ? "\u672C\u5730\u56FE\u5E93\u5DF2\u662F\u6700\u65B0:\n\u65B0\u589E: {0}\n\u8986\u76D6: {1}\n\u5220\u9664: {2}\n\u4FDD\u62A4\u8DF3\u8FC7: {3}\n\u672A\u53D8\u5316\u8DF3\u8FC7: {4}"
                    : "\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u5B8C\u6210:\n\u65B0\u589E: {0}\n\u8986\u76D6: {1}\n\u5220\u9664: {2}\n\u4FDD\u62A4\u8DF3\u8FC7: {3}\n\u672A\u53D8\u5316\u8DF3\u8FC7: {4}",
                counts.CopiedNewCount,
                counts.OverwrittenCount,
                counts.DeletedCount,
                counts.ProtectedSkipCount,
                counts.UnchangedSkipCount);
        }

        public static string FormatCommand(MirrorDirectoryResult result)
        {
            var counts = result ?? new MirrorDirectoryResult();
            return string.Format(
                counts.ChangedCount == 0
                    ? "\u672C\u5730\u56FE\u5E93\u5DF2\u662F\u6700\u65B0: {0} \u65B0\u589E, {1} \u8986\u76D6, {2} \u5220\u9664, {3} \u4FDD\u62A4\u8DF3\u8FC7, {4} \u672A\u53D8\u5316\u8DF3\u8FC7"
                    : "\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u5B8C\u6210: {0} \u65B0\u589E, {1} \u8986\u76D6, {2} \u5220\u9664, {3} \u4FDD\u62A4\u8DF3\u8FC7, {4} \u672A\u53D8\u5316\u8DF3\u8FC7",
                counts.CopiedNewCount,
                counts.OverwrittenCount,
                counts.DeletedCount,
                counts.ProtectedSkipCount,
                counts.UnchangedSkipCount);
        }

        public static string FormatPreviewDialog(MirrorDirectoryResult result)
        {
            var counts = result ?? new MirrorDirectoryResult();
            var sb = new StringBuilder();
            sb.AppendLine("\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u9884\u89C8:");
            AppendCounts(sb, counts);
            AppendEntries(sb, counts, true);
            sb.AppendLine();
            sb.Append("\u662F\u5426\u7EE7\u7EED\u66F4\u65B0\u672C\u5730\u56FE\u5E93\uFF1F");
            return sb.ToString();
        }

        public static string FormatPreviewCommand(MirrorDirectoryResult result)
        {
            var counts = result ?? new MirrorDirectoryResult();
            var sb = new StringBuilder();
            sb.AppendLine("\u66F4\u65B0\u672C\u5730\u56FE\u5E93\u9884\u89C8:");
            AppendCounts(sb, counts);
            AppendEntries(sb, counts, true);
            return sb.ToString().TrimEnd();
        }

        private static void AppendCounts(StringBuilder sb, MirrorDirectoryResult counts)
        {
            sb.AppendLine(string.Format("\u65B0\u589E: {0}", counts.CopiedNewCount));
            sb.AppendLine(string.Format("\u8986\u76D6: {0}", counts.OverwrittenCount));
            sb.AppendLine(string.Format("\u5220\u9664: {0}", counts.DeletedCount));
            sb.AppendLine(string.Format("\u4FDD\u62A4\u8DF3\u8FC7: {0}", counts.ProtectedSkipCount));
            sb.AppendLine(string.Format("\u672A\u53D8\u5316\u8DF3\u8FC7: {0}", counts.UnchangedSkipCount));
        }

        private static void AppendEntries(StringBuilder sb, MirrorDirectoryResult result, bool includeEmpty)
        {
            if (result.Entries == null || result.Entries.Count == 0)
            {
                if (includeEmpty)
                {
                    sb.AppendLine();
                    sb.AppendLine("\u6682\u65E0\u9700\u8981\u66F4\u65B0\u7684\u6587\u4EF6\u3002");
                }
                return;
            }

            sb.AppendLine();
            sb.AppendLine("\u660E\u7EC6\u9884\u89C8:");
            int count = Math.Min(PreviewLimit, result.Entries.Count);
            for (int i = 0; i < count; i++)
            {
                var entry = result.Entries[i];
                if (entry == null) continue;
                sb.AppendLine(string.Format("- {0}: {1}", FormatAction(entry.Action), entry.RelativePath));
            }
            if (result.Entries.Count > count)
                sb.AppendLine(string.Format("... \u53E6\u6709 {0} \u9879", result.Entries.Count - count));
        }

        private static string FormatAction(MirrorDirectoryAction action)
        {
            switch (action)
            {
                case MirrorDirectoryAction.CopyNew:
                    return "\u65B0\u589E";
                case MirrorDirectoryAction.Overwrite:
                    return "\u8986\u76D6";
                case MirrorDirectoryAction.Delete:
                    return "\u5220\u9664";
                case MirrorDirectoryAction.ProtectedSkip:
                    return "\u4FDD\u62A4\u8DF3\u8FC7";
                case MirrorDirectoryAction.ProtectedCategorySkip:
                    return "\u767D\u540D\u5355\u8DF3\u8FC7";
                case MirrorDirectoryAction.ProtectedLocalChangeSkip:
                    return "\u672C\u5730\u53D8\u66F4\u8DF3\u8FC7";
                default:
                    return "\u672A\u77E5";
            }
        }
    }
}
