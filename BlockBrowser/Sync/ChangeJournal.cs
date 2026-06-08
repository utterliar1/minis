using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace BlockBrowser
{
    public static class ChangeJournal
    {
        public static List<ChangeJournalEntry> Load(string journalPath)
        {
            if (string.IsNullOrEmpty(journalPath) || !File.Exists(journalPath))
                return new List<ChangeJournalEntry>();

            using (var stream = File.OpenRead(journalPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<ChangeJournalEntry>));
                var result = serializer.ReadObject(stream) as List<ChangeJournalEntry>;
                return result ?? new List<ChangeJournalEntry>();
            }
        }

        public static void Save(string journalPath, IEnumerable<ChangeJournalEntry> entries)
        {
            if (string.IsNullOrEmpty(journalPath))
                throw new ArgumentException("Journal path is required.", "journalPath");

            string dir = Path.GetDirectoryName(journalPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var list = new List<ChangeJournalEntry>(entries ?? new ChangeJournalEntry[0]);
            using (var stream = File.Create(journalPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<ChangeJournalEntry>));
                serializer.WriteObject(stream, list);
            }
        }

        public static string CreateId(DateTime utcNow, string user, int sequence)
        {
            string safeUser = string.IsNullOrEmpty(user) ? "user" : user.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                safeUser = safeUser.Replace(c, '_');

            return string.Format("{0}-{1}-{2:000}", utcNow.ToString("yyyyMMdd-HHmmss"), safeUser, sequence);
        }
    }
}
