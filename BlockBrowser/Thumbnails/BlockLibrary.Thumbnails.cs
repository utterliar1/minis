using System.Drawing;
using System.IO;
using Image = System.Drawing.Image;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static Image GetThumbnail(BlockInfo block, int size)
        {
            if (block == null) return GeneratePlaceholder("?", size);

            string cachePath = ThumbnailCacheService.GetCachePath(ThumbnailCachePath, block);
            bool hasSource = File.Exists(block.FilePath);
            Image cached = ThumbnailCacheService.TryLoadValidCache(cachePath, block.FilePath, size);
            if (cached != null) return cached;

            if (!hasSource) return GeneratePlaceholder(block.Name, size);

            try
            {
                Bitmap rendered = CadThumbnailRenderer.TryRender(block.FilePath, size);
                if (rendered != null)
                {
                    SaveThumbnailCache(cachePath, rendered);
                    return rendered;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BlockBrowser] GetThumbnail: " + ex.Message);
            }

            return GeneratePlaceholder(block.Name, size);
        }

        private static bool IsBitmapUseful(Bitmap bmp)
        {
            return ThumbnailCacheService.IsBitmapUseful(bmp);
        }

        public static Bitmap ScaleToSquare(Image src, int size)
        {
            return ThumbnailCacheService.ScaleToSquare(src, size);
        }

        public static void ClearPlaceholderCache()
        {
            PlaceholderImageFactory.Clear();
        }

        public static Image GeneratePlaceholder(string name, int size)
        {
            return PlaceholderImageFactory.Generate(name, size);
        }

        private static void SaveThumbnailCache(string cachePath, Image image)
        {
            ThumbnailCacheService.SaveThumbnailCache(cachePath, image);
        }

        private static string GetCacheKey(BlockInfo block)
        {
            return ThumbnailCacheService.GetCacheKey(block);
        }

        public static void RefreshThumbnail(BlockInfo block)
        {
            ThumbnailCacheService.RefreshThumbnail(ThumbnailCachePath, block);
        }

        public static void CleanupDiskCache()
        {
            ThumbnailCacheService.CleanupDiskCache(ThumbnailCachePath);
        }
    }
}
