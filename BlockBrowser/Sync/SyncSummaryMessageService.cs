namespace BlockBrowser
{
    public static class SyncSummaryMessageService
    {
        public static string FormatDialog(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            return string.Format(
                "同步完成:\n上传: {0}\n重复跳过: {1}\n冲突: {2}\n删除待确认: {3}\n失败: {4}",
                counts.UploadCount,
                counts.SkippedDuplicateCount,
                counts.ConflictCount,
                counts.DeleteReviewCount,
                counts.FailedCount);
        }

        public static string FormatCommand(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            return string.Format(
                "同步完成: {0} 上传, {1} 重复跳过, {2} 冲突, {3} 删除待确认, {4} 失败",
                counts.UploadCount,
                counts.SkippedDuplicateCount,
                counts.ConflictCount,
                counts.DeleteReviewCount,
                counts.FailedCount);
        }

        public static string FormatPreviewDialog(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\u540C\u6B65\u9884\u89C8:");
            sb.AppendLine(string.Format("\u5C06\u4E0A\u4F20: {0}", counts.UploadCount));
            sb.AppendLine(string.Format("\u91CD\u590D\u8DF3\u8FC7: {0}", counts.SkippedDuplicateCount));
            sb.AppendLine(string.Format("\u51B2\u7A81: {0}", counts.ConflictCount));
            sb.AppendLine(string.Format("\u5220\u9664\u5F85\u786E\u8BA4: {0}", counts.DeleteReviewCount));
            sb.AppendLine(string.Format("\u5931\u8D25: {0}", counts.FailedCount));
            AppendDecisionSamples(sb, plan);
            sb.AppendLine();
            sb.Append("\u662F\u5426\u7EE7\u7EED\u540C\u6B65\u5230 NAS\uFF1F");
            return sb.ToString();
        }

        public static string FormatPreviewCommand(SyncPlan plan)
        {
            var counts = SyncSummaryCounts.FromPlan(plan);
            return string.Format(
                "\u540C\u6B65\u9884\u89C8: {0} \u5C06\u4E0A\u4F20, {1} \u91CD\u590D\u8DF3\u8FC7, {2} \u51B2\u7A81, {3} \u5220\u9664\u5F85\u786E\u8BA4, {4} \u5931\u8D25",
                counts.UploadCount,
                counts.SkippedDuplicateCount,
                counts.ConflictCount,
                counts.DeleteReviewCount,
                counts.FailedCount);
        }

        private static void AppendDecisionSamples(System.Text.StringBuilder sb, SyncPlan plan)
        {
            if (plan == null || plan.Decisions == null || plan.Decisions.Count == 0) return;

            sb.AppendLine();
            sb.AppendLine("\u660E\u7EC6\u9884\u89C8:");
            int count = System.Math.Min(10, plan.Decisions.Count);
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
                    return "\u8DF3\u8FC7";
                case SyncDecisionKind.Conflict:
                    return "\u51B2\u7A81";
                case SyncDecisionKind.DeleteReview:
                    return "\u5220\u9664\u786E\u8BA4";
                case SyncDecisionKind.RenameReview:
                    return "\u91CD\u547D\u540D\u786E\u8BA4";
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
                    FailedCount = plan.FailedCount
                };
            }
        }
    }
}
