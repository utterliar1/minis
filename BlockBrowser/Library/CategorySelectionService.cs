using System.Collections.Generic;

namespace BlockBrowser
{
    public static class CategorySelectionService
    {
        private const string AllCategory = "\u5168\u90e8";
        private const string RecentCategory = "\u6700\u8fd1";

        public static List<string> GetUserCategories(IEnumerable<string> categories)
        {
            var result = new List<string>();
            foreach (string category in categories ?? new string[0])
            {
                if (string.IsNullOrEmpty(category)) continue;
                if (category == AllCategory || category == RecentCategory) continue;
                result.Add(category);
            }
            return result;
        }
    }
}
