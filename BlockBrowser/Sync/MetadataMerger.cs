using System;
using System.Collections.Generic;
using System.Linq;

namespace BlockBrowser
{
    public sealed class MetadataMergeResult
    {
        public MetadataMergeResult()
        {
            Tags = new List<string>();
        }

        public List<string> Tags { get; private set; }
        public string Note { get; set; }
        public bool HasNoteConflict { get; set; }
    }

    public static class MetadataMerger
    {
        public static MetadataMergeResult Merge(IEnumerable<string> nasTags, string nasNote, IEnumerable<string> localTags, string localNote)
        {
            var result = new MetadataMergeResult();
            var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string tag in nasTags ?? new string[0])
                if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag.Trim());

            foreach (string tag in localTags ?? new string[0])
                if (!string.IsNullOrWhiteSpace(tag)) tags.Add(tag.Trim());

            result.Tags.AddRange(tags);

            string nNote = (nasNote ?? "").Trim();
            string lNote = (localNote ?? "").Trim();

            if (string.IsNullOrEmpty(nNote))
                result.Note = lNote;
            else if (string.IsNullOrEmpty(lNote) || string.Equals(nNote, lNote, StringComparison.Ordinal))
                result.Note = nNote;
            else
            {
                result.Note = nNote;
                result.HasNoteConflict = true;
            }

            return result;
        }
    }
}
