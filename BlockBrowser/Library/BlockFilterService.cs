using System.Collections.Generic;

namespace BlockBrowser
{
    public static class BlockFilterService
    {
        public static bool Matches(BlockInfo block, string keyword)
        {
            string[] tokens = GetSearchTokens(keyword);
            if (tokens.Length == 0) return true;
            if (block == null) return false;

            string name = (block.Name ?? "").ToLowerInvariant();
            foreach (string token in tokens)
            {
                if (!name.Contains(token)) return false;
            }

            return true;
        }

        public static int CountMatches(IEnumerable<BlockInfo> blocks, string keyword)
        {
            int count = 0;
            foreach (BlockInfo block in blocks ?? new BlockInfo[0])
            {
                if (Matches(block, keyword)) count++;
            }
            return count;
        }

        public static string FormatCount(int count)
        {
            return count + " 个";
        }

        private static string[] GetSearchTokens(string keyword)
        {
            return (keyword ?? "")
                .ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
