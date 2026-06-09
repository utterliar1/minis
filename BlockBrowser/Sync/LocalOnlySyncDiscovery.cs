using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BlockBrowser
{
    public static class LocalOnlySyncDiscovery
    {
        private static readonly string[] InternalDirectoryNames = { ".blockbrowser", ".thumbs" };

        public static List<ChangeJournalEntry> Discover(
            string localMirrorPath,
            string nasLibraryPath,
            IEnumerable<ChangeJournalEntry> existingEntries,
            string userName,
            DateTime utcNow)
        {
            var results = new List<ChangeJournalEntry>();
            if (string.IsNullOrEmpty(localMirrorPath) || string.IsNullOrEmpty(nasLibraryPath))
                return results;
            if (!Directory.Exists(localMirrorPath) || !Directory.Exists(nasLibraryPath))
                return results;

            var existingPaths = new HashSet<string>(
                (existingEntries ?? new ChangeJournalEntry[0]).Select(e => Normalize(e == null ? "" : e.Path)),
                StringComparer.OrdinalIgnoreCase);

            int sequence = 0;
            foreach (string file in Directory.EnumerateFiles(localMirrorPath, "*.dwg", SearchOption.AllDirectories))
            {
                string rel = ToRelativePath(localMirrorPath, file);
                if (string.IsNullOrEmpty(rel) || IsInternalPath(rel))
                    continue;

                string key = Normalize(rel);
                if (existingPaths.Contains(key))
                    continue;

                string nasFile = Path.Combine(nasLibraryPath, rel);
                if (File.Exists(nasFile))
                    continue;

                sequence++;
                existingPaths.Add(key);
                results.Add(new ChangeJournalEntry
                {
                    Id = ChangeJournal.CreateId(utcNow, userName, sequence),
                    Action = LocalChangeAction.Add,
                    Path = rel,
                    ToPath = "",
                    BaseNasLastWriteUtc = null,
                    LocalLastWriteUtc = File.GetLastWriteTimeUtc(file),
                    User = string.IsNullOrEmpty(userName) ? Environment.UserName : userName,
                    CreatedUtc = utcNow
                });
            }

            return results;
        }

        private static string ToRelativePath(string root, string fullPath)
        {
            var rootUri = new Uri(EnsureTrailingSeparator(Path.GetFullPath(root)));
            var fileUri = new Uri(Path.GetFullPath(fullPath));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', '\\');
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private static bool IsInternalPath(string relativePath)
        {
            string[] parts = (relativePath ?? "").Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Any(p => InternalDirectoryNames.Contains(p, StringComparer.OrdinalIgnoreCase));
        }

        private static string Normalize(string path)
        {
            return (path ?? "").Replace('/', '\\').Trim().ToUpperInvariant();
        }
    }
}
