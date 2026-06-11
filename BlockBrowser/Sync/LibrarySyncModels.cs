using System;
using System.Runtime.Serialization;

namespace BlockBrowser
{
    public enum LibraryMode
    {
        Auto,
        Nas,
        Local
    }

    public enum ActiveLibraryKind
    {
        None,
        Nas,
        LocalMirror
    }

    public enum LocalChangeAction
    {
        Add,
        Edit,
        Rename,
        Metadata,
        DeleteRequest
    }

    public enum SyncDecisionKind
    {
        Upload,
        SkipDuplicate,
        Conflict,
        VersionCopy,
        DeleteReview,
        RenameReview,
        NoOp,
        Error
    }

    public sealed class SyncSettings
    {
        public string LibraryPath { get; set; }
        public string LocalMirrorPath { get; set; }
        public bool PreferLocalWhenNasUnavailable { get; set; }
        public bool AllowNasSync { get; set; }
        public LibraryMode CurrentLibraryMode { get; set; }
        public string UserName { get; set; }
    }

    public sealed class ActiveLibraryResult
    {
        public ActiveLibraryKind Kind { get; set; }
        public string ActivePath { get; set; }
        public string Message { get; set; }
        public bool IsAvailable { get; set; }
    }

    [DataContract]
    public sealed class ChangeJournalEntry
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public LocalChangeAction Action { get; set; }

        [DataMember(Order = 3)]
        public string Path { get; set; }

        [DataMember(Order = 4)]
        public string ToPath { get; set; }

        [DataMember(Order = 5)]
        public DateTime? BaseNasLastWriteUtc { get; set; }

        [DataMember(Order = 6)]
        public DateTime? LocalLastWriteUtc { get; set; }

        [DataMember(Order = 7)]
        public string User { get; set; }

        [DataMember(Order = 8)]
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class SyncFileSnapshot
    {
        public string Path { get; set; }
        public bool LocalExists { get; set; }
        public bool NasExists { get; set; }
        public DateTime? LocalLastWriteUtc { get; set; }
        public DateTime? NasLastWriteUtc { get; set; }
        public DateTime? BaseNasLastWriteUtc { get; set; }
    }

    public sealed class SyncDecision
    {
        public SyncDecisionKind Kind { get; set; }
        public string Path { get; set; }
        public string TargetPath { get; set; }
        public string Message { get; set; }
    }

    public sealed class SyncPlan
    {
        public SyncPlan()
        {
            Decisions = new System.Collections.Generic.List<SyncDecision>();
        }

        public System.Collections.Generic.List<SyncDecision> Decisions { get; private set; }
        public int UploadCount { get; set; }
        public int SkippedDuplicateCount { get; set; }
        public int ConflictCount { get; set; }
        public int DeleteReviewCount { get; set; }
        public int FailedCount { get; set; }
    }
}
