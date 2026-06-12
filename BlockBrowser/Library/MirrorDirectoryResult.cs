namespace BlockBrowser
{
    public sealed class MirrorDirectoryResult
    {
        public int CopiedNewCount { get; set; }
        public int OverwrittenCount { get; set; }
        public int DeletedCount { get; set; }
        public int ProtectedSkipCount { get; set; }

        public int ChangedCount
        {
            get { return CopiedNewCount + OverwrittenCount + DeletedCount; }
        }
    }
}
