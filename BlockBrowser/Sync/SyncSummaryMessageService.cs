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
