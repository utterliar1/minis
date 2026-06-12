using System;
using System.IO;

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static void CopyDirectoryContents(string sourceDir, string targetDir)
        {
            BlockFileOperations.CopyDirectoryContents(sourceDir, targetDir);
        }

        public static MirrorDirectoryResult UpdateLocalMirrorFromNas()
        {
            var preview = PreviewLocalMirrorFromNas();
            BlockFileOperations.ApplyMirrorDirectoryResult(NasLibraryPath, LocalMirrorPath, preview);
            return preview;
        }

        public static MirrorDirectoryResult PreviewLocalMirrorFromNas()
        {
            if (string.IsNullOrEmpty(NasLibraryPath) || !Directory.Exists(NasLibraryPath))
                throw new DirectoryNotFoundException("NAS library is unavailable: " + NasLibraryPath);
            if (string.IsNullOrEmpty(LocalMirrorPath))
                throw new InvalidOperationException("Local mirror path is empty.");

            var pending = ChangeJournal.Load(LocalJournalPath);
            return BlockFileOperations.PreviewMirrorDirectoryContents(NasLibraryPath, LocalMirrorPath, GetProtectedLocalPaths(pending), ProtectedLocalCategories);
        }
    }
}
