using System.Collections.Generic;

namespace BlockBrowser
{
    public static class ThumbnailMemoryCacheService
    {
        public static string GetKey(string filePath, int thumbSize)
        {
            return (filePath ?? "") + "_" + thumbSize;
        }

        public static bool HasValue<T>(IDictionary<string, T> cache, string filePath, int thumbSize)
        {
            if (cache == null) return false;

            T value;
            if (!cache.TryGetValue(GetKey(filePath, thumbSize), out value)) return false;
            return value != null;
        }

        public static List<string> FindKeysForPath(IEnumerable<string> keys, string filePath)
        {
            var result = new List<string>();
            string prefix = (filePath ?? "") + "_";
            foreach (string key in keys ?? new string[0])
            {
                if (key != null && key.StartsWith(prefix))
                    result.Add(key);
            }
            return result;
        }

        public static void MovePathEntries<T>(IDictionary<string, T> cache, string oldPath, string newPath)
        {
            if (cache == null) return;

            var keys = FindKeysForPath(cache.Keys, oldPath);
            foreach (string key in keys)
            {
                string suffix = key.Substring((oldPath ?? "").Length);
                cache[(newPath ?? "") + suffix] = cache[key];
                cache.Remove(key);
            }
        }
    }
}
