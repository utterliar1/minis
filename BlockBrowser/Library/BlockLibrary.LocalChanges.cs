using System;
using System.Collections.Generic;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        private static int _journalSequence;

        public static void RecordLocalChange(LocalChangeAction action, string relativePath, string toRelativePath, DateTime? baseNasLastWriteUtc)
        {
            if (ActiveLibrary == null || ActiveLibrary.Kind != ActiveLibraryKind.LocalMirror)
                return;
            if (IsProtectedLocalCategoryPath(relativePath) || IsProtectedLocalCategoryPath(toRelativePath))
                return;

            var entries = ChangeJournal.Load(LocalJournalPath);
            _journalSequence++;
            DateTime now = DateTime.UtcNow;
            entries.Add(new ChangeJournalEntry
            {
                Id = ChangeJournal.CreateId(now, SyncUserName, _journalSequence),
                Action = action,
                Path = relativePath ?? "",
                ToPath = toRelativePath ?? "",
                BaseNasLastWriteUtc = baseNasLastWriteUtc,
                LocalLastWriteUtc = now,
                User = string.IsNullOrEmpty(SyncUserName) ? Environment.UserName : SyncUserName,
                CreatedUtc = now
            });
            ChangeJournal.Save(LocalJournalPath, entries);
        }

        private static IEnumerable<string> GetProtectedLocalPaths(IEnumerable<ChangeJournalEntry> entries)
        {
            foreach (var entry in entries ?? new ChangeJournalEntry[0])
            {
                if (entry == null) continue;
                if (!string.IsNullOrEmpty(entry.Path)) yield return entry.Path;
                if (!string.IsNullOrEmpty(entry.ToPath)) yield return entry.ToPath;
            }
        }
    }
}
