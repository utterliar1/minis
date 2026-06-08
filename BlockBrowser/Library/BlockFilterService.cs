using System.Collections.Generic;

namespace BlockBrowser
{
    public static class BlockFilterService
    {
        public static bool Matches(BlockInfo block, string keyword)
        {
            string normalized = NormalizeKeyword(keyword);
            if (string.IsNullOrEmpty(normalized)) return true;
            if (block == null) return false;

            string name = (block.Name ?? "").ToLowerInvariant();
            string category = (block.Category ?? "").ToLowerInvariant();
            return name.Contains(normalized) || category.Contains(normalized);
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

        private static string NormalizeKeyword(string keyword)
        {
            return (keyword ?? "").Trim().ToLowerInvariant();
        }
    }
}
