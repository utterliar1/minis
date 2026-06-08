using System;
using System.IO;

namespace BlockBrowser
{
    public static class BlockInfoStatusService
    {
        public static string Format(BlockInfo block)
        {
            if (block == null || string.IsNullOrEmpty(block.FilePath) || !File.Exists(block.FilePath))
            {
                return "就绪";
            }

            try
            {
                var file = new FileInfo(block.FilePath);
                return string.Format("{0}  |  {1}  |  修改: {2}",
                    block.Name,
                    FormatSize(file.Length),
                    file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
            }
            catch
            {
                return block.Name;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("F1") + " KB";
            }

            return (bytes / 1024.0 / 1024.0).ToString("F1") + " MB";
        }
    }
}
