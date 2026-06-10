using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Image = System.Drawing.Image;

namespace BlockBrowser
{
    public static class ThumbnailCacheService
    {
        private const string CacheKeyVersion = "thumb-v2";

        public static string GetCachePath(string thumbnailCachePath, BlockInfo block)
        {
            return Path.Combine(thumbnailCachePath ?? "", GetCacheKey(block) + ".png");
        }

        public static string GetCacheKey(BlockInfo block)
        {
            string path = block == null ? "" : (block.FilePath ?? "");
            long size = 0;
            long ticks = 0;
            try
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    size = fi.Length;
                    ticks = fi.LastWriteTimeUtc.Ticks;
                }
            }
            catch { }

            string raw = CacheKeyVersion + "|" + path + "|" + size + "|" + ticks;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw.ToLowerInvariant()));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }

        public static Image TryLoadValidCache(string cachePath, string sourcePath, int size)
        {
            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath)) return null;

            bool hasSource = File.Exists(sourcePath ?? "");
            bool cacheValid = false;
            if (hasSource)
            {
                try
                {
                    DateTime sourceTime = File.GetLastWriteTime(sourcePath);
                    DateTime cacheTime = File.GetLastWriteTime(cachePath);
                    cacheValid = cacheTime >= sourceTime;
                }
                catch { }
            }
            else
            {
                cacheValid = true;
            }

            if (!cacheValid) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(cachePath);
                using (var ms = new MemoryStream(bytes))
                using (var cached = Image.FromStream(ms))
                {
                    return ScaleToSquare(cached, size);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BlockBrowser] 缓存读取失败: " + ex.Message);
                return null;
            }
        }

        public static bool IsBitmapUseful(Bitmap bmp)
        {
            if (bmp == null || bmp.Width < 4 || bmp.Height < 4) return false;
            try
            {
                int? first = null;
                int stepX = Math.Max(1, bmp.Width / 16);
                int stepY = Math.Max(1, bmp.Height / 16);
                for (int y = 0; y < bmp.Height; y += stepY)
                {
                    for (int x = 0; x < bmp.Width; x += stepX)
                    {
                        int argb = bmp.GetPixel(x, y).ToArgb();
                        if (!first.HasValue)
                            first = argb;
                        else if (first.Value != argb)
                            return true;
                    }
                }

                return false;
            }
            catch { return true; }
        }

        public static bool IsPreviewIconSuitable(Bitmap bmp)
        {
            if (!IsBitmapUseful(bmp)) return false;
            try
            {
                int samples = 0;
                int darkSamples = 0;
                int stepX = Math.Max(1, bmp.Width / 16);
                int stepY = Math.Max(1, bmp.Height / 16);

                for (int y = 0; y < bmp.Height; y += stepY)
                {
                    for (int x = 0; x < bmp.Width; x += stepX)
                    {
                        Color c = bmp.GetPixel(x, y);
                        samples++;
                        if (c.R < 35 && c.G < 35 && c.B < 35)
                            darkSamples++;
                    }
                }

                return samples == 0 || darkSamples < samples * 0.55;
            }
            catch
            {
                return true;
            }
        }

        public static Bitmap ScaleToSquare(Image src, int size)
        {
            if (src.Width < 1 || src.Height < 1) return new Bitmap(size, size);
            Bitmap bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float ratio = Math.Min((float)size / src.Width, (float)size / src.Height);
                int w = (int)(src.Width * ratio);
                int h = (int)(src.Height * ratio);
                int x = (size - w) / 2;
                int y = (size - h) / 2;
                g.DrawImage(src, x, y, w, h);
            }
            return bmp;
        }

        public static void SaveThumbnailCache(string cachePath, Image image)
        {
            try
            {
                string dir = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                image.Save(cachePath, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch { }
        }

        public static void RefreshThumbnail(string thumbnailCachePath, BlockInfo block)
        {
            string cachePath = GetCachePath(thumbnailCachePath, block);
            if (File.Exists(cachePath)) File.Delete(cachePath);
        }

        public static void CleanupDiskCache(string thumbnailCachePath)
        {
            CleanupDiskCache(thumbnailCachePath, 30, 100L * 1024 * 1024, 80L * 1024 * 1024);
        }

        public static void CleanupDiskCache(string thumbnailCachePath, int maxAgeDays, long maxBytes)
        {
            CleanupDiskCache(thumbnailCachePath, maxAgeDays, maxBytes, maxBytes);
        }

        public static void CleanupDiskCache(string thumbnailCachePath, int maxAgeDays, long maxBytes, long targetBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(thumbnailCachePath) || !Directory.Exists(thumbnailCachePath)) return;
                DateTime cutoff = DateTime.Now.AddDays(-maxAgeDays);
                long total = 0;
                var infos = new List<FileInfo>();
                foreach (string file in Directory.GetFiles(thumbnailCachePath, "*.png"))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        infos.Add(fi);
                        total += fi.Length;
                        if (fi.LastWriteTime < cutoff) { fi.Delete(); total -= fi.Length; }
                    }
                    catch { }
                }

                if (total > maxBytes)
                {
                    infos.Sort((a, b) => a.LastWriteTime.CompareTo(b.LastWriteTime));
                    foreach (var fi in infos)
                    {
                        if (total <= targetBytes) break;
                        try
                        {
                            if (!fi.Exists) continue;
                            total -= fi.Length;
                            fi.Delete();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
