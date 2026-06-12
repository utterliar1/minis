namespace BlockBrowser
{
    public sealed class MirrorDirectoryResult
    {
        public MirrorDirectoryResult()
        {
            Entries = new System.Collections.Generic.List<MirrorDirectoryEntry>();
        }

        public int CopiedNewCount { get; set; }
        public int OverwrittenCount { get; set; }
        public int DeletedCount { get; set; }
        public int ProtectedSkipCount { get; set; }
        public System.Collections.Generic.List<MirrorDirectoryEntry> Entries { get; private set; }

        public int ChangedCount
        {
            get { return CopiedNewCount + OverwrittenCount + DeletedCount; }
        }
    }
}
