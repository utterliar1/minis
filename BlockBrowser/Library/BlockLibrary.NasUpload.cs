using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
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
