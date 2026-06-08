using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BlockBrowser
{
    public static class BlockLibraryService
    {
        private const string AllCategory = "\u5168\u90e8";
        private const string RecentCategory = "\u6700\u8fd1";
        private const string UncategorizedCategory = "\u672a\u5206\u7c7b";

        public static List<string> GetCategories(string libraryPath)
        {
            return GetWritableCategories(libraryPath);
        }

        public static List<string> GetWritableCategories(string libraryPath)
        {
            return GetCategoriesCore(libraryPath, false);
        }

        public static List<string> GetBrowsableCategories(string libraryPath)
        {
            return GetCategoriesCore(libraryPath, true);
        }

        public static List<BlockInfo> GetBlocks(string libraryPath, string category, IEnumerable<string> recentBlocks)
        {
            EnsureLibraryDirectory(libraryPath);
            var blocks = new List<BlockInfo>();

            if (category == RecentCategory)
            {
                foreach (string recentPath in recentBlocks ?? new string[0])
                {
                    if (File.Exists(recentPath))
                        blocks.Add(new BlockInfo { FilePath = recentPath, Category = RecentCategory });
                }
                return blocks;
            }

            var paths = new List<string>();
            if (string.IsNullOrEmpty(category) || category == AllCategory)
            {
                paths.Add(libraryPath);
                paths.AddRange(Directory.GetDirectories(libraryPath).Where(d => !Path.GetFileName(d).StartsWith(".")));
            }
            else
            {
                string categoryPath = Path.Combine(libraryPath, category);
                if (Directory.Exists(categoryPath)) paths.Add(categoryPath);
            }

            foreach (string path in paths)
            {
                string cat = path == libraryPath ? UncategorizedCategory : Path.GetFileName(path);
                foreach (string dwg in Directory.GetFiles(path, "*.dwg"))
                    blocks.Add(new BlockInfo { FilePath = dwg, Category = cat });
            }

            return blocks.OrderBy(b => b.Category).ThenBy(b => b.Name).ToList();
        }

        public static List<BlockInfo> GetBlocks(string libraryPath, string category, object[] recentBlocks)
        {
            return GetBlocks(libraryPath, category, NormalizeRecentBlocks(recentBlocks));
        }

        private static List<string> GetCategoriesCore(string libraryPath, bool requireBlocks)
        {
            EnsureLibraryDirectory(libraryPath);

            var categories = new List<string> { AllCategory, RecentCategory };
            var dirs = new List<string>();
            foreach (string dir in Directory.GetDirectories(libraryPath))
            {
                string name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name) || name.StartsWith(".")) continue;
                if (requireBlocks && Directory.GetFiles(dir, "*.dwg").Length == 0) continue;
                dirs.Add(name);
            }

            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            categories.AddRange(dirs);
            return categories;
        }

        private static IEnumerable<string> NormalizeRecentBlocks(IEnumerable<object> recentBlocks)
        {
            if (recentBlocks == null) return new string[0];
            return recentBlocks.Select(p => p == null ? "" : p.ToString());
        }

        private static void EnsureLibraryDirectory(string libraryPath)
        {
            if (string.IsNullOrEmpty(libraryPath))
                throw new ArgumentException("Library path is required.", "libraryPath");
            if (!Directory.Exists(libraryPath)) Directory.CreateDirectory(libraryPath);
        }
    }
}
