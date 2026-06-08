using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockBrowser
{
    public static class SyncPlanner
    {
        public static SyncPlan CreatePlan(IEnumerable<ChangeJournalEntry> entries, IEnumerable<SyncFileSnapshot> snapshots)
        {
            var plan = new SyncPlan();
            var snapshotMap = (snapshots ?? new SyncFileSnapshot[0])
                .GroupBy(s => Normalize(s.Path))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var entry in entries ?? new ChangeJournalEntry[0])
            {
                string key = Normalize(entry.Path);
                SyncFileSnapshot snapshot;
                snapshotMap.TryGetValue(key, out snapshot);

                var decision = Decide(entry, snapshot);
                plan.Decisions.Add(decision);
                Count(plan, decision.Kind);
            }

            return plan;
        }

        private static SyncDecision Decide(ChangeJournalEntry entry, SyncFileSnapshot snapshot)
        {
            if (entry == null)
                return Decision(SyncDecisionKind.Error, "", "", "Journal entry is null.");

            if (entry.Action == LocalChangeAction.DeleteRequest)
                return Decision(SyncDecisionKind.DeleteReview, entry.Path, "", "Delete request requires NAS review.");

            if (entry.Action == LocalChangeAction.Rename)
                return Decision(SyncDecisionKind.RenameReview, entry.Path, entry.ToPath, "Rename requires confirmation.");

            if (snapshot == null)
                return Decision(SyncDecisionKind.Conflict, entry.Path, "", "Missing file snapshot.");

            if (entry.Action == LocalChangeAction.Add)
            {
                if (snapshot.LocalExists && !snapshot.NasExists)
                    return Decision(SyncDecisionKind.Upload, entry.Path, entry.Path, "New local file can be uploaded.");

                if (snapshot.LocalExists && snapshot.NasExists)
                    return Decision(SyncDecisionKind.SkipDuplicate, entry.Path, "", "NAS already has this path.");
            }

            if (entry.Action == LocalChangeAction.Edit)
            {
                bool nasChanged = snapshot.NasExists
                    && entry.BaseNasLastWriteUtc.HasValue
                    && snapshot.NasLastWriteUtc.HasValue
                    && snapshot.NasLastWriteUtc.Value > entry.BaseNasLastWriteUtc.Value;

                if (nasChanged)
                    return Decision(SyncDecisionKind.Conflict, entry.Path, "", "Local and NAS files both changed.");

                if (snapshot.LocalExists)
                    return Decision(SyncDecisionKind.VersionCopy, entry.Path, "", "Edited file should upload as version copy by default.");
            }

            if (entry.Action == LocalChangeAction.Metadata)
                return Decision(SyncDecisionKind.Upload, entry.Path, entry.Path, "Metadata change can be uploaded after merge.");

            return Decision(SyncDecisionKind.NoOp, entry.Path, "", "No sync action required.");
        }

        private static SyncDecision Decision(SyncDecisionKind kind, string path, string targetPath, string message)
        {
            return new SyncDecision
            {
                Kind = kind,
                Path = path ?? "",
                TargetPath = targetPath ?? "",
                Message = message ?? ""
            };
        }

        private static void Count(SyncPlan plan, SyncDecisionKind kind)
        {
            if (kind == SyncDecisionKind.Upload || kind == SyncDecisionKind.VersionCopy)
                plan.UploadCount++;
            else if (kind == SyncDecisionKind.SkipDuplicate)
                plan.SkippedDuplicateCount++;
            else if (kind == SyncDecisionKind.Conflict)
                plan.ConflictCount++;
            else if (kind == SyncDecisionKind.DeleteReview)
                plan.DeleteReviewCount++;
            else if (kind == SyncDecisionKind.Error)
                plan.FailedCount++;
        }

        private static string Normalize(string path)
        {
            return (path ?? "").Replace('/', '\\').Trim().ToUpperInvariant();
        }
    }
}
