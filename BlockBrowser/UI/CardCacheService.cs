using System.Collections.Generic;

namespace BlockBrowser
{
    public interface IBlockCardState
    {
        string FilePath { get; }
        bool IsDisposed { get; }
        bool Visible { get; }
    }

    public static class CardCacheService
    {
        public static IBlockCardState FindByPath(IEnumerable<IBlockCardState> cards, string filePath)
        {
            foreach (IBlockCardState card in cards ?? new IBlockCardState[0])
            {
                if (card == null) continue;
                if (card.FilePath == filePath) return card;
            }
            return null;
        }

        public static IBlockCardState RemoveFirstByPath(IDictionary<string, List<IBlockCardState>> categoryCards, string filePath)
        {
            if (categoryCards == null) return null;

            foreach (var kv in categoryCards)
            {
                IBlockCardState card = FindByPath(kv.Value, filePath);
                if (card != null)
                {
                    kv.Value.Remove(card);
                    return card;
                }
            }

            return null;
        }

        public static T RemoveFirstByPath<T>(IDictionary<string, List<T>> categoryCards, string filePath)
            where T : class, IBlockCardState
        {
            if (categoryCards == null) return null;

            foreach (var kv in categoryCards)
            {
                T card = FindByPath(kv.Value, filePath) as T;
                if (card != null)
                {
                    kv.Value.Remove(card);
                    return card;
                }
            }

            return null;
        }

        public static int CountVisible(IEnumerable<IBlockCardState> cards)
        {
            int count = 0;
            foreach (IBlockCardState card in cards ?? new IBlockCardState[0])
            {
                if (card != null && !card.IsDisposed && card.Visible) count++;
            }
            return count;
        }
    }
}
