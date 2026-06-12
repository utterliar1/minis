using System.Collections.Generic;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static List<string> GetCategories()
        {
            return MergeProtectedCategories(BlockLibraryService.GetCategories(LibraryPath));
        }

        public static List<string> GetBrowsableCategories()
        {
            return BlockLibraryService.GetBrowsableCategories(LibraryPath);
        }

        private static List<string> MergeProtectedCategories(List<string> categories)
        {
            var merged = categories ?? new List<string>();
            foreach (string category in ProtectedLocalCategories)
            {
                if (string.IsNullOrWhiteSpace(category)) continue;
                if (!merged.Contains(category)) merged.Add(category);
            }
            return merged;
        }

        public static CategoryCreationResult CreateCategory(string category)
        {
            EnsureActiveLibraryWritable();
            return CategoryCreationService.CreateCategory(LibraryPath, category);
        }

        public static List<BlockInfo> GetBlocks(string category)
        {
            return BlockLibraryService.GetBlocks(LibraryPath, category, _recentBlocks);
        }
    }
}
