using System;
using System.IO;
using System.Text;

namespace BlockBrowser
{
    public static class SyncSummaryMessageService
    {
        public static string FormatDialog(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            return string.Format(
                "\u540C\u6B65\u5B8C\u6210:\n\u4E0A\u4F20: {0}\n\u91CD\u590D\u8DF3\u8FC7: {1}\n\u51B2\u7A81: {2}\n\u5220\u9664\u5F85\u786E\u8BA4: {3}\n\u767D\u540D\u5355\u8DF3\u8FC7: {4}\n\u5931\u8D25: {5}",
                counts.UploadCount,
                counts.SkippedDuplicateCount,
                counts.ConflictCount,
                counts.DeleteReviewCount,
                counts.ProtectedCategorySkipCount,
                counts.FailedCount);
        }

        public static string FormatCommand(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            return string.Format(
                "\u540C\u6B65\u5B8C\u6210: {0} \u4E0A\u4F20, {1} \u91CD\u590D\u8DF3\u8FC7, {2} \u51B2\u7A81, {3} \u5220\u9664\u5F85\u786E\u8BA4, {4} \u767D\u540D\u5355\u8DF3\u8FC7, {5} \u5931\u8D25",
                counts.UploadCount,
                counts.SkippedDuplicateCount,
                counts.ConflictCount,
                counts.DeleteReviewCount,
                counts.ProtectedCategorySkipCount,
                counts.FailedCount);
        }

        public static string FormatPreviewDialog(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            var sb = new StringBuilder();
            sb.AppendLine("\u540C\u6B65\u9884\u89C8:");
            AppendCounts(sb, counts);
            AppendDecisionSamples(sb, plan);
            sb.AppendLine();
            sb.Append("\u662F\u5426\u7EE7\u7EED\u540C\u6B65\u5230 NAS\uFF1F");
            return sb.ToString();
        }

        public static string FormatPreviewCommand(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            return string.Format(
                "\u540C\u6B65\u9884\u89C8: {0} \u5C06\u4E0A\u4F20, {1} \u91CD\u590D\u8DF3\u8FC7, {2} \u51B2\u7A81, {3} \u5220\u9664\u5F85\u786E\u8BA4, {4} \u767D\u540D\u5355\u8DF3\u8FC7, {5} \u5931\u8D25",
                counts.UploadCount,
                counts.SkippedDuplicateCount,
                counts.ConflictCount,
                counts.DeleteReviewCount,
                counts.ProtectedCategorySkipCount,
                counts.FailedCount);
        }

        public static string FormatDetailedReport(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            var sb = new StringBuilder();
            sb.AppendLine("\u540C\u6B65\u660E\u7EC6");
            AppendCounts(sb, counts);
            sb.AppendLine();

            if (plan == null || plan.Decisions == null || plan.Decisions.Count == 0)
            {
                sb.AppendLine("\u6682\u65E0\u540C\u6B65\u9879\u3002");
                return sb.ToString();
            }

            foreach (var decision in plan.Decisions)
            {
                if (decision == null) continue;
                string path = string.IsNullOrEmpty(decision.Path) ? decision.TargetPath : decision.Path;
                sb.AppendLine(string.Format("- {0}: {1}", FormatKind(decision.Kind), path));
                if (!string.IsNullOrEmpty(decision.Message))
                    sb.AppendLine(string.Format("  {0}", decision.Message));
            }

            return sb.ToString();
        }

        public static void AppendLog(string logPath, SyncPlan plan)
        {
            if (string.IsNullOrEmpty(logPath)) return;

            string dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====");
            sb.AppendLine(FormatDetailedReport(plan));
            File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
        }

        private static void AppendCounts(StringBuilder sb, SyncSummaryCounts counts)
        {
            sb.AppendLine(string.Format("\u5C06\u4E0A\u4F20: {0}", counts.UploadCount));
            sb.AppendLine(string.Format("\u91CD\u590D\u8DF3\u8FC7: {0}", counts.SkippedDuplicateCount));
            sb.AppendLine(string.Format("\u51B2\u7A81: {0}", counts.ConflictCount));
            sb.AppendLine(string.Format("\u5220\u9664\u5F85\u786E\u8BA4: {0}", counts.DeleteReviewCount));
            sb.AppendLine(string.Format("\u767D\u540D\u5355\u8DF3\u8FC7: {0}", counts.ProtectedCategorySkipCount));
            sb.AppendLine(string.Format("\u5931\u8D25: {0}", counts.FailedCount));
        }

        private static void AppendDecisionSamples(StringBuilder sb, SyncPlan plan)
        {
            if (plan == null || plan.Decisions == null || plan.Decisions.Count == 0) return;

            sb.AppendLine();
            sb.AppendLine("\u660E\u7EC6\u9884\u89C8:");
            int count = Math.Min(10, plan.Decisions.Count);
            for (int i = 0; i < count; i++)
            {
                var decision = plan.Decisions[i];
                if (decision == null) continue;
                string path = string.IsNullOrEmpty(decision.Path) ? decision.TargetPath : decision.Path;
                sb.AppendLine(string.Format("- {0}: {1}", FormatKind(decision.Kind), path));
            }
            if (plan.Decisions.Count > count)
                sb.AppendLine(string.Format("... \u53E6\u6709 {0} \u9879", plan.Decisions.Count - count));
        }

        private static string FormatKind(SyncDecisionKind kind)
        {
            switch (kind)
            {
                case SyncDecisionKind.Upload:
                case SyncDecisionKind.VersionCopy:
                    return "\u4E0A\u4F20";
                case SyncDecisionKind.SkipDuplicate:
                    return "\u91CD\u590D\u8DF3\u8FC7";
                case SyncDecisionKind.Conflict:
                    return "\u51B2\u7A81";
                case SyncDecisionKind.DeleteReview:
                    return "\u5220\u9664\u786E\u8BA4";
                case SyncDecisionKind.RenameReview:
                    return "\u91CD\u547D\u540D\u786E\u8BA4";
                case SyncDecisionKind.ProtectedCategorySkip:
                    return "\u767D\u540D\u5355\u8DF3\u8FC7";
                case SyncDecisionKind.Error:
                    return "\u5931\u8D25";
                default:
                    return "\u65E0\u64CD\u4F5C";
            }
        }

        private struct SyncSummaryCounts
        {
            public int UploadCount;
            public int SkippedDuplicateCount;
            public int ConflictCount;
            public int DeleteReviewCount;
            public int ProtectedCategorySkipCount;
            public int FailedCount;

            public static SyncSummaryCounts FromPlan(SyncPlan plan)
            {
                if (plan == null) return new SyncSummaryCounts();

                return new SyncSummaryCounts
                {
                    UploadCount = plan.UploadCount,
                    SkippedDuplicateCount = plan.SkippedDuplicateCount,
                    ConflictCount = plan.ConflictCount,
                    DeleteReviewCount = plan.DeleteReviewCount,
                    ProtectedCategorySkipCount = plan.ProtectedCategorySkipCount,
                    FailedCount = plan.FailedCount
                };
            }
        }
    }
}
