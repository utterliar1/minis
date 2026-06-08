using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Image = System.Drawing.Image;
using DrawingFont = System.Drawing.Font;

namespace BlockBrowser
{
    public static class PlaceholderImageFactory
    {
        private static readonly Dictionary<string, Image> PlaceholderCache = new Dictionary<string, Image>();
        private static readonly Dictionary<int, DrawingFont> FontCache = new Dictionary<int, DrawingFont>();
        private const int PlaceholderCacheMax = 200;

        public static void Clear()
        {
            foreach (var kv in PlaceholderCache) { try { kv.Value.Dispose(); } catch { } }
            PlaceholderCache.Clear();
        }

        public static Image Generate(string name, int size)
        {
            string cacheKey = (name ?? "?") + "_" + size;
            if (PlaceholderCache.ContainsKey(cacheKey) && PlaceholderCache[cacheKey] != null)
            {
                try { return new Bitmap(PlaceholderCache[cacheKey]); }
                catch { PlaceholderCache.Remove(cacheKey); }
            }

            try
            {
                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(240, 242, 245));
                    using (var p = new Pen(Color.FromArgb(180, 190, 205), 1))
                        g.DrawRectangle(p, 0, 0, size - 1, size - 1);

                    int isz = size / 3, ix = (size - isz) / 2, iy = size / 5;
                    using (var br = new SolidBrush(Color.FromArgb(100, 140, 190)))
                        g.FillRectangle(br, ix, iy, isz, isz);
                    using (var p = new Pen(Color.FromArgb(70, 100, 150), 2))
                    {
                        g.DrawRectangle(p, ix, iy, isz, isz);
                        int cx = size / 2, cy = iy + isz / 2;
                        g.DrawLine(p, cx - isz / 4, cy, cx + isz / 4, cy);
                        g.DrawLine(p, cx, cy - isz / 4, cx, cy + isz / 4);
                    }

                    string displayName = name ?? "?";
                    if (displayName.Length > 10) displayName = displayName.Substring(0, 9) + "..";

                    int fontSize = Math.Max(7, size / 14);
                    DrawingFont font;
                    if (!FontCache.TryGetValue(fontSize, out font))
                    {
                        font = new DrawingFont("Microsoft YaHei", fontSize, FontStyle.Regular);
                        FontCache[fontSize] = font;
                    }

                    var textRect = new RectangleF(2, size * 0.68f, size - 4, size * 0.30f);
                    g.DrawString(displayName, font, Brushes.DimGray, textRect,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                }

                if (PlaceholderCache.Count >= PlaceholderCacheMax)
                    Clear();

                PlaceholderCache[cacheKey] = new Bitmap(bmp);
                return bmp;
            }
            catch
            {
                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(230, 230, 235));
                    g.DrawRectangle(Pens.Gray, 0, 0, size - 1, size - 1);
                }
                return bmp;
            }
        }
    }
}
