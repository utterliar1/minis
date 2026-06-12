using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        public static void CopyDirectoryContents(string sourceDir, string targetDir)
        {
            BlockFileOperations.CopyDirectoryContents(sourceDir, targetDir);
        }

        public static MirrorDirectoryResult UpdateLocalMirrorFromNas()
        {
            var preview = PreviewLocalMirrorFromNas();
            BlockFileOperations.ApplyMirrorDirectoryResult(NasLibraryPath, LocalMirrorPath, preview);
            return preview;
        }

        public static MirrorDirectoryResult PreviewLocalMirrorFromNas()
        {
            if (string.IsNullOrEmpty(NasLibraryPath) || !Directory.Exists(NasLibraryPath))
                throw new DirectoryNotFoundException("NAS library is unavailable: " + NasLibraryPath);
            if (string.IsNullOrEmpty(LocalMirrorPath))
                throw new InvalidOperationException("Local mirror path is empty.");

            var pending = ChangeJournal.Load(LocalJournalPath);
            return BlockFileOperations.PreviewMirrorDirectoryContents(NasLibraryPath, LocalMirrorPath, GetProtectedLocalPaths(pending), ProtectedLocalCategories);
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

        public static SyncPlan SyncSafeUploadsToNas()
        {
            EnsureNasSyncAllowed();
            if (string.IsNullOrEmpty(NasLibraryPath) || !Directory.Exists(NasLibraryPath))
                throw new DirectoryNotFoundException("NAS library is unavailable: " + NasLibraryPath);

            var entries = ChangeJournal.Load(LocalJournalPath);
            var syncEntries = new List<ChangeJournalEntry>(entries);
            syncEntries.AddRange(LocalOnlySyncDiscovery.Discover(
                LocalMirrorPath,
                NasLibraryPath,
                entries,
                SyncUserName,
                DateTime.UtcNow,
                ProtectedLocalCategories));
            var plan = SyncPlanner.CreatePlan(syncEntries, BuildSnapshots(syncEntries));
            var uploadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var decision in plan.Decisions)
            {
                if (decision.Kind != SyncDecisionKind.Upload)
                    continue;

                string src = Path.Combine(LocalMirrorPath, decision.Path);
                string dst = Path.Combine(NasLibraryPath, decision.TargetPath);
                if (File.Exists(dst))
                    continue;

                string dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir)) Directory.CreateDirectory(dstDir);
                File.Copy(src, dst, false);
                uploadedPaths.Add(decision.Path ?? "");
            }

            if (uploadedPaths.Count > 0)
            {
                var remaining = entries
                    .Where(e => !uploadedPaths.Contains(e.Path ?? ""))
                    .ToList();
                if (remaining.Count != entries.Count)
                    ChangeJournal.Save(LocalJournalPath, remaining);
            }

            return plan;
        }

        private static void EnsureNasSyncAllowed()
        {
            if (!AllowNasSync)
                throw new InvalidOperationException("当前电脑未启用同步到 NAS。请联系指定维护人。");
        }
    }
}
