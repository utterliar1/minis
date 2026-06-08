using System;
using System.Collections.Generic;

namespace BlockBrowser
{
    public static class ResourceDisposalService
    {
        public static void DisposeQuietly(IDisposable item)
        {
            if (item == null) return;
            try { item.Dispose(); }
            catch { }
        }

        public static void DisposeAll<T>(IEnumerable<T> items) where T : IDisposable
        {
            if (items == null) return;
            foreach (T item in items)
            {
                DisposeQuietly(item);
            }
        }

        public static void DisposeDictionaryValuesAndClear<T>(IDictionary<string, List<T>> itemsByKey)
            where T : IDisposable
        {
            if (itemsByKey == null) return;

            foreach (var kv in itemsByKey)
            {
                DisposeAll(kv.Value);
            }
            itemsByKey.Clear();
        }

        public static void DisposeDictionaryValuesAndClear<T>(IDictionary<string, T> itemsByKey)
            where T : IDisposable
        {
            if (itemsByKey == null) return;

            foreach (var kv in itemsByKey)
            {
                DisposeQuietly(kv.Value);
            }
            itemsByKey.Clear();
        }
    }
}
