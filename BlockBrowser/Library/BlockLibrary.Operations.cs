using System;
using System.IO;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static bool RenameBlock(BlockInfo block, string newName)
        {
            EnsureActiveLibraryWritable();
            if (!BlockFileOperations.CanRenameBlock(block, newName, true)) return false;

            string oldPath = block.FilePath;
            string oldCacheKey = GetCacheKey(block);
            string newPath = BlockFileOperations.RenameBlockFile(block, newName);
            if (string.IsNullOrEmpty(newPath)) return false;

            RecordLocalChange(
                LocalChangeAction.Rename,
                ToLibraryRelativePath(oldPath),
                ToLibraryRelativePath(newPath),
                null);

            block.FilePath = newPath;
            string newCacheKey = GetCacheKey(block);
            string thumbDir = ThumbnailCachePath;
            string oldCache = Path.Combine(thumbDir, oldCacheKey + ".png");
            string newCache = Path.Combine(thumbDir, newCacheKey + ".png");
            if (File.Exists(oldCache))
            {
                try { File.Move(oldCache, newCache); } catch { }
            }
            return true;
        }
    }
}
