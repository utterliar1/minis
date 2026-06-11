using System;
using System.Collections.Generic;
using System.IO;

namespace BlockBrowser
{
    public static class BlockFileOperations
    {
        public static void CopyDirectoryContents(string sourceDir, string targetDir)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException(sourceDir);
            if (string.IsNullOrEmpty(targetDir))
                throw new ArgumentException("Target directory is required.", "targetDir");

            Directory.CreateDirectory(targetDir);
            string sourceRoot = BlockBrowserConfigStore.EnsureTrailingSeparator(sourceDir);

            foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(sourceRoot.Length);
                if (IsInternalLibraryPath(rel)) continue;
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(sourceRoot.Length);
                if (IsInternalLibraryPath(rel)) continue;

                string dest = Path.Combine(targetDir, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(file, dest, true);
            }
        }

        public static void MirrorDirectoryContents(string sourceDir, string targetDir, IEnumerable<string> protectedRelativePaths)
        {
            MirrorDirectoryContents(sourceDir, targetDir, protectedRelativePaths, null);
        }

        public static void MirrorDirectoryContents(string sourceDir, string targetDir, IEnumerable<string> protectedRelativePaths, IEnumerable<string> protectedCategoryNames)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException(sourceDir);
            if (string.IsNullOrEmpty(targetDir))
                throw new ArgumentException("Target directory is required.", "targetDir");

            Directory.CreateDirectory(targetDir);
            string sourceRoot = BlockBrowserConfigStore.EnsureTrailingSeparator(sourceDir);
            string targetRoot = BlockBrowserConfigStore.EnsureTrailingSeparator(targetDir);
            var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var protectedPaths = BuildProtectedPathSet(protectedRelativePaths);
            var protectedCategories = BuildProtectedPathSet(protectedCategoryNames);

            foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(sourceRoot.Length);
                if (IsInternalLibraryPath(rel)) continue;
                if (IsProtectedCategoryPath(rel, protectedCategories)) continue;
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(sourceRoot.Length);
                if (IsInternalLibraryPath(rel)) continue;
                if (IsProtectedCategoryPath(rel, protectedCategories)) continue;

                string key = NormalizeRelativePath(rel);
                sourcePaths.Add(key);
                if (protectedPaths.Contains(key)) continue;

                string dest = Path.Combine(targetDir, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(file, dest, true);
            }

            foreach (string file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(targetRoot.Length);
                if (IsInternalLibraryPath(rel)) continue;
                if (IsProtectedCategoryPath(rel, protectedCategories)) continue;

                string key = NormalizeRelativePath(rel);
                if (sourcePaths.Contains(key) || protectedPaths.Contains(key)) continue;
                File.Delete(file);
            }
        }

        public static bool CanRenameBlock(BlockInfo block, string newName, bool checkCollision)
        {
            if (block == null || string.IsNullOrEmpty(newName)) return false;
            newName = newName.Trim();
            if (!BlockBrowserConfigStore.IsSafeLibraryName(newName)) return false;

            string target = GetRenameTargetPath(block, newName);
            if (string.IsNullOrEmpty(target)) return false;
            if (checkCollision && File.Exists(target)) return false;
            return true;
        }

        public static string GetRenameTargetPath(BlockInfo block, string newName)
        {
            if (block == null || string.IsNullOrEmpty(newName)) return "";
            string oldPath = block.FilePath;
            string dir = Path.GetDirectoryName(oldPath);
            if (string.IsNullOrEmpty(dir)) return "";
            return Path.Combine(dir, newName.Trim() + ".dwg");
        }

        public static string RenameBlockFile(BlockInfo block, string newName)
        {
            if (!CanRenameBlock(block, newName, true)) return "";
            string newPath = GetRenameTargetPath(block, newName);
            File.Move(block.FilePath, newPath);
            return newPath;
        }

        public static bool CanOpenForExclusiveWrite(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            try
            {
                using (File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool IsInternalLibraryPath(string relativePath)
        {
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in segments)
            {
                if (segment.Equals(".thumbs", StringComparison.OrdinalIgnoreCase)
                    || segment.Equals(".blockbrowser", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static HashSet<string> BuildProtectedPathSet(IEnumerable<string> relativePaths)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (relativePaths == null) return set;
            foreach (string path in relativePaths)
            {
                string normalized = NormalizeRelativePath(path);
                if (!string.IsNullOrEmpty(normalized)) set.Add(normalized);
            }
            return set;
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? "").Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();
        }

        private static bool IsProtectedCategoryPath(string relativePath, HashSet<string> protectedCategories)
        {
            if (protectedCategories == null || protectedCategories.Count == 0) return false;
            string normalized = NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(normalized)) return false;

            int sep = normalized.IndexOf(Path.DirectorySeparatorChar);
            string firstSegment = sep >= 0 ? normalized.Substring(0, sep) : normalized;
            return protectedCategories.Contains(firstSegment);
        }
    }
}
