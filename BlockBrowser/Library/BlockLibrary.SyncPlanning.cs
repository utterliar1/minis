using System;
using System.Collections.Generic;
using System.IO;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static List<SyncFileSnapshot> BuildSnapshots(IEnumerable<ChangeJournalEntry> entries)
        {
            var list = new List<SyncFileSnapshot>();
            foreach (var entry in entries ?? new ChangeJournalEntry[0])
            {
                string rel = entry.Path ?? "";
                string localPath = Path.Combine(LocalMirrorPath ?? "", rel);
                string nasPath = Path.Combine(NasLibraryPath ?? "", rel);
                bool localExists = File.Exists(localPath);
                bool nasExists = File.Exists(nasPath);
                list.Add(new SyncFileSnapshot
                {
                    Path = rel,
                    LocalExists = localExists,
                    NasExists = nasExists,
                    LocalLastWriteUtc = localExists ? (DateTime?)File.GetLastWriteTimeUtc(localPath) : null,
                    NasLastWriteUtc = nasExists ? (DateTime?)File.GetLastWriteTimeUtc(nasPath) : null,
                    BaseNasLastWriteUtc = entry.BaseNasLastWriteUtc
                });
            }
            return list;
        }

        public static SyncPlan PreviewLocalSync()
        {
            EnsureNasSyncAllowed();
            var entries = ChangeJournal.Load(LocalJournalPath);
            var syncEntries = new List<ChangeJournalEntry>(entries);
            syncEntries.AddRange(LocalOnlySyncDiscovery.Discover(
                LocalMirrorPath,
                NasLibraryPath,
                entries,
                SyncUserName,
                DateTime.UtcNow,
                ProtectedLocalCategories));
            return SyncPlanner.CreatePlan(syncEntries, BuildSnapshots(syncEntries));
        }
    }
}
