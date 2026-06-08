namespace BlockBrowser
{
    public static class ThumbnailLoadProgressService
    {
        public const int DefaultBatchSize = 5;

        public static bool IsComplete(int currentIndex, int totalCount)
        {
            return currentIndex >= totalCount;
        }

        public static string FormatLoadingStatus(int currentIndex, int totalCount)
        {
            return string.Format("加载中... {0}/{1}", currentIndex, totalCount);
        }

        public static string FormatFailedReadyStatus(int failCount)
        {
            return string.Format("就绪（{0}个加载失败）", failCount);
        }
    }
}
