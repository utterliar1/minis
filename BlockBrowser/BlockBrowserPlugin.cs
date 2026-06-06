using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Image = System.Drawing.Image;
using DrawingFont = System.Drawing.Font;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using GrxCAD.Runtime;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

[assembly: CommandClass(typeof(BlockBrowser.BlockBrowserCommands))]
[assembly: ExtensionApplication(typeof(BlockBrowser.BlockBrowserPlugin))]

namespace BlockBrowser
{
    public class BlockInfo
    {
        private string _name;
        private string _filePath;
        public string FilePath
        {
            get { return _filePath; }
            set { _filePath = value; _name = Path.GetFileNameWithoutExtension(value); }
        }
        public string Name { get { return _name ?? ""; } }
        public string Category { get; set; }
    }

    public static class BlockLibrary
    {
        public static string LibraryPath { get; set; }
        public const string AppVersion = "1.25";
        public static string PlatformName { get; set; }
        public static int ThumbSize { get; set; }
        public static double InsertScale { get; set; }
        public static double InsertRotation { get; set; }
        public static int FormWidth { get; set; }
        public static int FormHeight { get; set; }

        static BlockLibrary()
        {
            string dllDir = Path.GetDirectoryName(typeof(BlockLibrary).Assembly.Location) ?? "";
            string pluginRoot = Path.GetFullPath(Path.Combine(dllDir, ".."));
            LibraryPath = Path.Combine(pluginRoot, "我的常用块");
            ThumbSize = 128;
            InsertScale = 1.0;
            InsertRotation = 0;
            FormWidth = 1000;
            FormHeight = 650;
            LoadConfig();
        }

        public static string ThumbnailCachePath { get { return Path.Combine(LibraryPath, ".thumbs"); } }

        private static List<string> _recentBlocks = new List<string>();
        private const int MAX_RECENT = 20;

        public static void AddRecentBlock(string filePath)
        {
            _recentBlocks.Remove(filePath);
            _recentBlocks.Insert(0, filePath);
            if (_recentBlocks.Count > MAX_RECENT)
                _recentBlocks.RemoveRange(MAX_RECENT, _recentBlocks.Count - MAX_RECENT);
            SaveConfig();
        }

        public static void RemoveRecentBlock(string filePath)
        {
            if (_recentBlocks.Remove(filePath)) SaveConfig();
        }

        private static string ConfigPath
        {
            get
            {
                string dllDir = Path.GetDirectoryName(typeof(BlockLibrary).Assembly.Location) ?? "";
                string pluginRoot = Path.GetFullPath(Path.Combine(dllDir, ".."));
                return Path.Combine(pluginRoot, "config.ini");
            }
        }

        public static void LoadConfig()
        {
            try
            {
                string cfgPath = ConfigPath;
                if (!File.Exists(cfgPath)) return;
                foreach (string line in File.ReadAllLines(cfgPath, System.Text.Encoding.UTF8))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = trimmed.Substring(0, eq).Trim();
                    string val = trimmed.Substring(eq + 1).Trim();
                    if (key.Equals("ThumbSize", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) { int ts; if (int.TryParse(val, out ts) && ts >= 40 && ts <= 512) ThumbSize = ts; }
                    else if (key.Equals("InsertScale", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) { double ds; if (double.TryParse(val, out ds) && ds > 0) InsertScale = ds; }
                    else if (key.Equals("InsertRotation", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) { double dr; if (double.TryParse(val, out dr)) InsertRotation = dr * Math.PI / 180.0; }
                    else if (key.Equals("FormWidth", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) { int fw; if (int.TryParse(val, out fw) && fw >= 400) FormWidth = fw; }
                    else if (key.Equals("FormHeight", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) { int fh; if (int.TryParse(val, out fh) && fh >= 300) FormHeight = fh; }
                    else if (key.Equals("RecentBlocks", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                    {
                        _recentBlocks.Clear();
                        foreach (var rp in val.Split('|'))
                        {
                            string t = rp.Trim();
                            if (!string.IsNullOrEmpty(t) && !_recentBlocks.Contains(t))
                                _recentBlocks.Add(t);
                        }
                    }
                    else if (key.Equals("RecentBlocks", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                    {
                        _recentBlocks.Clear();
                        foreach (var rp in val.Split('|'))
                        {
                            string t = rp.Trim();
                            if (!string.IsNullOrEmpty(t) && !_recentBlocks.Contains(t))
                                _recentBlocks.Add(t);
                        }
                    }
                    else if (key.Equals("LibraryPath", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                    {
                        if (Path.IsPathRooted(val))
                            LibraryPath = val;
                        else
                        {
                            string dllDir2 = Path.GetDirectoryName(typeof(BlockLibrary).Assembly.Location) ?? "";
                            string pluginRoot2 = Path.GetFullPath(Path.Combine(dllDir2, ".."));
                            LibraryPath = Path.Combine(pluginRoot2, val);
                        }
                    }
                }
            }
            catch { }
        }

        public static void SaveConfig()
        {
            try
            {
                string cfgPath = ConfigPath;
                string dir = Path.GetDirectoryName(cfgPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var lines = new List<string>();
                lines.Add("# 块浏览器配置文件");
                lines.Add("LibraryPath=" + LibraryPath);
                lines.Add("ThumbSize=" + ThumbSize);
                lines.Add("InsertScale=" + InsertScale.ToString("G"));
                lines.Add("InsertRotation=" + (InsertRotation * 180.0 / Math.PI).ToString("G"));
                lines.Add("FormWidth=" + FormWidth);
                lines.Add("FormHeight=" + FormHeight);
                if (_recentBlocks.Count > 0)
                    lines.Add("RecentBlocks=" + string.Join("|", _recentBlocks.ToArray()));
                File.WriteAllLines(cfgPath, lines, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        public static List<string> GetCategories()
        {
            if (!Directory.Exists(LibraryPath)) Directory.CreateDirectory(LibraryPath);
            var c = new List<string> { "全部", "最近" };
            var dirs = new List<string>();
            foreach (var d in Directory.GetDirectories(LibraryPath))
            {
                string n = Path.GetFileName(d);
                if (!n.StartsWith(".") && Directory.GetFiles(d, "*.dwg").Length > 0)
                    dirs.Add(n);
            }
            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            c.AddRange(dirs);
            return c;
        }

        public static List<BlockInfo> GetBlocks(string category)
        {
            if (!Directory.Exists(LibraryPath)) Directory.CreateDirectory(LibraryPath);
            var blocks = new List<BlockInfo>();

            if (category == "最近")
            {
                foreach (var rp in _recentBlocks)
                {
                    if (File.Exists(rp))
                        blocks.Add(new BlockInfo { FilePath = rp, Category = "最近" });
                }
                return blocks;
            }

            var paths = new List<string>();
            if (string.IsNullOrEmpty(category) || category == "全部")
            {
                paths.Add(LibraryPath);
                paths.AddRange(Directory.GetDirectories(LibraryPath).Where(d => !Path.GetFileName(d).StartsWith(".")));
            }
            else
            {
                string cp = Path.Combine(LibraryPath, category);
                if (Directory.Exists(cp)) paths.Add(cp);
            }
            foreach (var p in paths)
            {
                string cat = p == LibraryPath ? "未分类" : Path.GetFileName(p);
                foreach (var dwg in Directory.GetFiles(p, "*.dwg"))
                    blocks.Add(new BlockInfo { FilePath = dwg, Category = cat });
            }
            return blocks.OrderBy(b => b.Category).ThenBy(b => b.Name).ToList();
        }

        public static Image GetThumbnail(BlockInfo block, int size)
        {
            if (block == null) return GeneratePlaceholder("?", size);
            string cachePath = Path.Combine(ThumbnailCachePath, GetCacheKey(block) + ".png");
            bool hasSrc = File.Exists(block.FilePath);
            // Check cache with file modification time validation
            if (File.Exists(cachePath))
            {
                bool cacheValid = false;
                if (hasSrc)
                {
                    try
                    {
                        DateTime srcTime = File.GetLastWriteTime(block.FilePath);
                        DateTime cacheTime = File.GetLastWriteTime(cachePath);
                        cacheValid = cacheTime >= srcTime;
                    }
                    catch { }
                }
                else
                {
                    cacheValid = true;
                }
                if (cacheValid)
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(cachePath);
                        using (var ms = new MemoryStream(bytes))
                        using (var c = Image.FromStream(ms))
                        {
                            return ScaleToSquare(c, size);
                        }
                    }
                    catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("[BlockBrowser] 缓存读取失败: " + ex.Message); }
                }
            }
            if (!hasSrc) return GeneratePlaceholder(block.Name, size);
            try
            {
                using (var db = new Database(false, true))
                {
                    db.ReadDwgFile(block.FilePath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        // Try PreviewIcon first, validate not empty
                        foreach (ObjectId btrId in bt)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            if (!btr.IsLayout && !btr.Name.StartsWith("*") && btr.HasPreviewIcon)
                            {
                                try
                                {
                                    using (Bitmap icon = btr.PreviewIcon)
                                    {
                                        if (icon != null && IsBitmapUseful(icon))
                                        {
                                            Bitmap scaled = ScaleToSquare(icon, size);
                                            SaveThumbnailCache(cachePath, scaled);
                                            return scaled;
                                        }
                                    }
                                }
                                catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("[BlockBrowser] PreviewIcon 失败: " + ex.Message); }
                            }
                        }
                        // Fallback: render model space first, then largest block
                        Bitmap renderResult = null;
                        // Try model space (for files from "添加到库")
                        if (bt.Has("*Model_Space"))
                        {
                            var msBtr = (BlockTableRecord)tr.GetObject(bt["*Model_Space"], OpenMode.ForRead);
                            int msCnt = 0; foreach (ObjectId eid in msBtr) msCnt++;
                            if (msCnt > 0)
                            {
                                renderResult = new Bitmap(size, size);
                                using (var g = Graphics.FromImage(renderResult))
                                {
                                    g.SmoothingMode = SmoothingMode.AntiAlias;
                                    g.Clear(Color.White);
                                    RenderBlockContents(g, tr, msBtr, size);
                                }
                            }
                        }
                        // If model space empty, try largest block definition (for files from "导出块")
                        if (renderResult == null)
                        {
                            BlockTableRecord bestBtr = null;
                            int bestCount = 0;
                            foreach (ObjectId btrId in bt)
                            {
                                var btr2 = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                                if (!btr2.IsLayout && !btr2.Name.StartsWith("*"))
                                {
                                    int cnt = 0; foreach (ObjectId eid in btr2) cnt++;
                                    if (cnt > bestCount) { bestCount = cnt; bestBtr = btr2; }
                                }
                            }
                            if (bestBtr != null)
                            {
                                renderResult = new Bitmap(size, size);
                                using (var g = Graphics.FromImage(renderResult))
                                {
                                    g.SmoothingMode = SmoothingMode.AntiAlias;
                                    g.Clear(Color.White);
                                    RenderBlockContents(g, tr, bestBtr, size);
                                }
                            }
                        }
                        if (renderResult != null)
                        {
                            SaveThumbnailCache(cachePath, renderResult);
                            return renderResult;
                        }
                    }
                }
            }
            catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("[BlockBrowser] GetThumbnail: " + ex.Message); }
            return GeneratePlaceholder(block.Name, size);
        }

        private static bool IsBitmapUseful(Bitmap bmp)
        {
            if (bmp.Width < 4 || bmp.Height < 4) return false;
            try
            {
                // Quick check: 3 corners + center
                Color c0 = bmp.GetPixel(0, 0);
                Color cm = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
                Color ce = bmp.GetPixel(bmp.Width - 1, bmp.Height - 1);
                return !(c0.ToArgb() == cm.ToArgb() && cm.ToArgb() == ce.ToArgb());
            }
            catch { return true; }
        }

        public static Bitmap ScaleToSquare(Image src, int size)
        {
            if (src.Width < 1 || src.Height < 1) return new Bitmap(size, size);
            Bitmap bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float ratio = System.Math.Min((float)size / src.Width, (float)size / src.Height);
                int w = (int)(src.Width * ratio);
                int h = (int)(src.Height * ratio);
                int x = (size - w) / 2;
                int y = (size - h) / 2;
                g.DrawImage(src, x, y, w, h);
            }
            return bmp;
        }

        private static void RenderBlockContents(Graphics g, Transaction tr, BlockTableRecord btr, int size)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int cnt = 0;
            var visited = new HashSet<ObjectId>();
            CollectExt(tr, btr, Matrix3d.Identity, ref minX, ref minY, ref maxX, ref maxY, ref cnt, visited);
            if (cnt == 0 || minX >= maxX || minY >= maxY) return;
            double gw = maxX - minX, gh = maxY - minY;
            int m = 8, dw = size - m * 2, dh = size - m * 2;
            double sc = Math.Min(dw / gw, dh / gh);
            double ox = m + (dw - gw * sc) / 2.0, oy = m + (dh - gh * sc) / 2.0;
            using (var pen = new Pen(Color.FromArgb(30, 60, 120), 1.0f))
            {
                var drawVisited = new HashSet<ObjectId>();
                DrawBtr(g, tr, btr, pen, minX, minY, sc, ox, oy, size, Matrix3d.Identity, drawVisited);
            }
        }

        private static void CollectExt(Transaction tr, BlockTableRecord btr, Matrix3d xf,
            ref double minX, ref double minY, ref double maxX, ref double maxY, ref int cnt, HashSet<ObjectId> visited)
        {
            if (!visited.Add(btr.ObjectId)) return; // 循环引用保护
            foreach (ObjectId eid in btr)
            {
                Entity ent = tr.GetObject(eid, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (ent is BlockReference)
                {
                    BlockReference br = (BlockReference)ent;
                    try
                    {
                        BlockTableRecord def = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        CollectExt(tr, def, xf * br.BlockTransform, ref minX, ref minY, ref maxX, ref maxY, ref cnt, visited);
                    }
                    catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("[BlockBrowser] CollectExt: " + ex.Message); }
                }
                else
                {
                    cnt++;
                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        UpdateBounds(ext.MinPoint, xf, ref minX, ref minY, ref maxX, ref maxY);
                        UpdateBounds(ext.MaxPoint, xf, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    catch { }
                }
            }
        }

        private static void UpdateBounds(Point3d pt, Matrix3d xf,
            ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            Point3d p = pt.TransformBy(xf);
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        private static void DrawBtr(Graphics g, Transaction tr, BlockTableRecord btr,
            Pen pen, double minX, double minY, double sc, double ox, double oy, int sz, Matrix3d xf, HashSet<ObjectId> visited)
        {
            if (!visited.Add(btr.ObjectId)) return; // 循环引用保护
            foreach (ObjectId eid in btr)
            {
                Entity ent = tr.GetObject(eid, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (ent is BlockReference)
                {
                    BlockReference br = (BlockReference)ent;
                    try
                    {
                        BlockTableRecord def = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        DrawBtr(g, tr, def, pen, minX, minY, sc, ox, oy, sz, xf * br.BlockTransform, visited);
                    }
                    catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine("[BlockBrowser] DrawBtr: " + ex.Message); }
                }
                else
                {
                    DrawEntXf(g, ent, pen, minX, minY, sc, ox, oy, sz, xf);
                }
            }
        }

        private static PointF Xf(Point3d pt, Matrix3d xf, double minX, double minY, double sc, double ox, double oy, int sz)
        {
            Point3d p = pt.TransformBy(xf);
            return new PointF((float)((p.X - minX) * sc + ox), (float)(sz - ((p.Y - minY) * sc + oy)));
        }

        private static void DrawEntXf(Graphics g, Entity ent, Pen pen,
            double minX, double minY, double sc, double ox, double oy, int sz, Matrix3d xf)
        {
            try
            {
                if (ent is Line)
                {
                    Line l = (Line)ent;
                    g.DrawLine(pen, Xf(l.StartPoint, xf, minX, minY, sc, ox, oy, sz), Xf(l.EndPoint, xf, minX, minY, sc, ox, oy, sz));
                }
                else if (ent is Circle)
                {
                    Circle c = (Circle)ent;
                    PointF ct = Xf(c.Center, xf, minX, minY, sc, ox, oy, sz);
                    float r = (float)(c.Radius * sc);
                    if (r > 0.5f) g.DrawEllipse(pen, ct.X - r, ct.Y - r, r * 2, r * 2);
                }
                else if (ent is Arc)
                {
                    Arc a = (Arc)ent;
                    PointF ct = Xf(a.Center, xf, minX, minY, sc, ox, oy, sz);
                    float r = (float)(a.Radius * sc);
                    if (r > 0.5f)
                    {
                        float sa = (float)(a.StartAngle * 180.0 / Math.PI);
                        float sw = (float)((a.EndAngle - a.StartAngle) * 180.0 / Math.PI);
                        if (sw < 0) sw += 360f;
                        g.DrawArc(pen, ct.X - r, ct.Y - r, r * 2, r * 2, sa, sw);
                    }
                }
                else if (ent is Polyline)
                {
                    Polyline pl = (Polyline)ent;
                    int n = pl.NumberOfVertices;
                    if (n >= 2)
                    {
                        PointF[] pts = new PointF[n + (pl.Closed ? 1 : 0)];
                        for (int i = 0; i < n; i++)
                            pts[i] = Xf(pl.GetPoint3dAt(i), xf, minX, minY, sc, ox, oy, sz);
                        if (pl.Closed) pts[n] = pts[0];
                        g.DrawLines(pen, pts);
                    }
                }
                else if (ent is Spline)
                {
                    try
                    {
                        Entity sp = ((Spline)ent).ToPolyline() as Entity;
                        if (sp != null) DrawEntXf(g, sp, pen, minX, minY, sc, ox, oy, sz, xf);
                    }
                    catch { }
                }
                else if (ent is Ellipse)
                {
                    Ellipse el = (Ellipse)ent;
                    PointF ct = Xf(el.Center, xf, minX, minY, sc, ox, oy, sz);
                    float rx = (float)(el.MajorAxis.Length * sc), ry = (float)(el.MinorAxis.Length * sc);
                    if (rx > 0.5f && ry > 0.5f) g.DrawEllipse(pen, ct.X - rx, ct.Y - ry, rx * 2, ry * 2);
                }
                else if (ent is DBPoint)
                {
                    PointF sp = Xf(((DBPoint)ent).Position, xf, minX, minY, sc, ox, oy, sz);
                    g.FillRectangle(Brushes.DarkSlateBlue, sp.X - 1, sp.Y - 1, 3, 3);
                }
                else
                {
                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        PointF p1 = Xf(ext.MinPoint, xf, minX, minY, sc, ox, oy, sz);
                        PointF p2 = Xf(ext.MaxPoint, xf, minX, minY, sc, ox, oy, sz);
                        float w = Math.Abs(p2.X - p1.X), h = Math.Abs(p2.Y - p1.Y);
                        if (w > 1 && h > 1)
                        {
                            using (var lp = new Pen(Color.FromArgb(150, 170, 200), 0.6f))
                                g.DrawRectangle(lp, Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), w, h);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        
        private static Dictionary<string, Image> _placeholderCache = new Dictionary<string, Image>();
        private static Dictionary<int, DrawingFont> _fontCache = new Dictionary<int, DrawingFont>();
        private const int PLACEHOLDER_CACHE_MAX = 200;

        public static void ClearPlaceholderCache()
        {
            foreach (var kv in _placeholderCache) { try { kv.Value.Dispose(); } catch { } }
            _placeholderCache.Clear();
        }

        public static Image GeneratePlaceholder(string name, int size)
        {
            string cacheKey = (name ?? "?") + "_" + size;
            if (_placeholderCache.ContainsKey(cacheKey) && _placeholderCache[cacheKey] != null)
            {
                try { return new Bitmap(_placeholderCache[cacheKey]); }
                catch { _placeholderCache.Remove(cacheKey); }
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
                    string dn = name ?? "?";
                    if (dn.Length > 10) dn = dn.Substring(0, 9) + "..";
                    int fontSize = Math.Max(7, size / 14);
                    DrawingFont font;
                    if (!_fontCache.TryGetValue(fontSize, out font))
                    {
                        font = new DrawingFont("Microsoft YaHei", fontSize, FontStyle.Regular);
                        _fontCache[fontSize] = font;
                    }
                    var textRect = new RectangleF(2, size * 0.68f, size - 4, size * 0.30f);
                    g.DrawString(dn, font, Brushes.DimGray, textRect,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                }
                // 缓存超限时清理
                if (_placeholderCache.Count >= PLACEHOLDER_CACHE_MAX)
                {
                    foreach (var kv in _placeholderCache) { try { kv.Value.Dispose(); } catch { } }
                    _placeholderCache.Clear();
                }
                _placeholderCache[cacheKey] = new Bitmap(bmp);
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

        private static void SaveThumbnailCache(string cachePath, Image image)
        {
            try
            {
                string dir = Path.GetDirectoryName(cachePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                image.Save(cachePath, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch { }
        }

        private static string GetCacheKey(BlockInfo block)
        {
            string path = block.FilePath ?? "";
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
            string raw = path + "|" + size + "|" + ticks;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw.ToLowerInvariant()));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }

        public static void RefreshThumbnail(BlockInfo block)
        {
            string cp = Path.Combine(ThumbnailCachePath, GetCacheKey(block) + ".png");
            if (File.Exists(cp)) File.Delete(cp);
        }

        public static void CleanupDiskCache()
        {
            try
            {
                string cp = ThumbnailCachePath;
                if (!Directory.Exists(cp)) return;
                var cutoff = DateTime.Now.AddDays(-30);
                long total = 0;
                var infos = new List<System.IO.FileInfo>();
                foreach (var f in Directory.GetFiles(cp, "*.png"))
                {
                    try
                    {
                        var fi = new System.IO.FileInfo(f);
                        infos.Add(fi);
                        total += fi.Length;
                        if (fi.LastWriteTime < cutoff) { fi.Delete(); total -= fi.Length; }
                    }
                    catch { }
                }
                if (total > 100L * 1024 * 1024)
                {
                    infos.Sort((a, b) => a.LastWriteTime.CompareTo(b.LastWriteTime));
                    foreach (var fi in infos)
                    {
                        if (total <= 80L * 1024 * 1024) break;
                        try { total -= fi.Length; fi.Delete(); } catch { }
                    }
                }
            }
            catch { }
        }

        public static bool RenameBlock(BlockInfo block, string newName)
        {
            if (block == null || string.IsNullOrEmpty(newName)) return false;
            string oldPath = block.FilePath;
            string dir = Path.GetDirectoryName(oldPath);
            if (string.IsNullOrEmpty(dir)) return false;
            string newPath = Path.Combine(dir, newName + ".dwg");
            if (File.Exists(newPath)) return false;
            // Validate filename chars
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in newName) { foreach (char ic in invalid) { if (c == ic) return false; } }
            // Rename DWG file
            File.Move(oldPath, newPath);
            // Rename thumbnail cache
            string oldCacheKey = GetCacheKey(block);
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

        public static void InsertBlock(BlockInfo block, double scale, double rotation)
        {
            if (block == null || !File.Exists(block.FilePath)) return;
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                PromptPointResult pr = ed.GetPoint("\n指定插入点: ");
                if (pr.Status != PromptStatus.OK) return;
                Point3d pt = pr.Value;
                string bname = block.Name;
                using (DocumentLock dl = doc.LockDocument())
                {
                    // 导入块定义（同名则生成唯一名称）
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                        if (bt.Has(bname))
                        {
                            int suffix = 2;
                            while (bt.Has(bname + "_" + suffix)) suffix++;
                            bname = bname + "_" + suffix;
                        }
                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(block.FilePath, FileOpenMode.OpenForReadAndAllShare, true, "");
                            db.Insert(bname, extDb, true);
                        }
                        tr.Commit();
                    }
                    // 创建块参照
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        if (!bt.Has(bname))
                        {
                            ed.WriteMessage("\n导入失败: " + bname);
                            tr.Commit();
                            return;
                        }
                        BlockReference bref = new BlockReference(pt, bt[bname]);
                        bref.ScaleFactors = new Scale3d(scale, scale, scale);
                        bref.Rotation = rotation;
                        BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        ms.AppendEntity(bref);
                        tr.AddNewlyCreatedDBObject(bref, true);
                        tr.Commit();
                    }
                }
                AddRecentBlock(block.FilePath);
                ed.WriteMessage("\n已插入: " + bname);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n插入失败: " + ex.Message);
            }
        }

        

        public static bool SaveSelectionAsBlockWithSelection(PromptSelectionResult sr, string blockName, string category, Point3d basePt)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            Editor ed = doc.Editor;
            string catDir = Path.Combine(LibraryPath, category);
            if (!Directory.Exists(catDir)) Directory.CreateDirectory(catDir);
            string outPath = Path.Combine(catDir, blockName + ".dwg");
            // 覆盖确认
            if (File.Exists(outPath))
            {
                var ow = ed.GetString("\n文件已存在，覆盖？[Y/N] <N>: ");
                if (ow.Status != PromptStatus.OK || !ow.StringResult.Trim().ToUpperInvariant().StartsWith("Y"))
                {
                    ed.WriteMessage("\n取消。");
                    return false;
                }
            }
            try
            {
                using (DocumentLock dl = doc.LockDocument())
                {
                    Database db = doc.Database;
                    ObjectIdCollection ids = new ObjectIdCollection(sr.Value.GetObjectIds());
                    using (Database newDb = db.Wblock(ids, basePt))
                    {
                        newDb.SaveAs(outPath, DwgVersion.Current);
                    }
                }
                RefreshThumbnail(new BlockInfo { FilePath = outPath, Category = category });
                ed.WriteMessage(string.Format("\n块 {0} 已保存到 [{1}]，基点: ({2:F2},{3:F2})", blockName, category, basePt.X, basePt.Y));
                return true;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n保存失败: {0}", ex.Message));
                return false;
            }
        }

        public static bool ExportBlockFromCurrentDrawing(string blockName, string category)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            Editor ed = doc.Editor;
            string catDir = Path.Combine(LibraryPath, category);
            if (!Directory.Exists(catDir)) Directory.CreateDirectory(catDir);
            string outPath = Path.Combine(catDir, blockName + ".dwg");
            // 覆盖确认
            if (File.Exists(outPath))
            {
                var ow2 = ed.GetString("\n文件已存在，覆盖？[Y/N] <N>: ");
                if (ow2.Status != PromptStatus.OK || !ow2.StringResult.Trim().ToUpperInvariant().StartsWith("Y"))
                {
                    ed.WriteMessage("\n取消。");
                    return false;
                }
            }
            try
            {
                using (DocumentLock dl = doc.LockDocument())
                {
                    Database db = doc.Database;
                    ObjectId blockId;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        if (!bt.Has(blockName))
                        {
                            ed.WriteMessage(string.Format("\n未找到块: {0}", blockName));
                            tr.Commit();
                            return false;
                        }
                        blockId = bt[blockName];
                        tr.Commit();
                    }
                    using (Database newDb = db.Wblock(blockId))
                        newDb.SaveAs(outPath, DwgVersion.Current);
                }
                RefreshThumbnail(new BlockInfo { FilePath = outPath, Category = category });
                ed.WriteMessage(string.Format("\n块 {0} 已导出到 [{1}]", blockName, category));
                return true;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n导出失败: {0}", ex.Message));
                return false;
            }
        }

    }

    public class BlockBrowserPlugin : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
#if GSTARCAD
                BlockLibrary.PlatformName = "GstarCAD";
#elif AUTOCAD
                BlockLibrary.PlatformName = "AutoCAD";
#elif ZWCAD
                BlockLibrary.PlatformName = "ZWCAD";
#endif
                BlockLibrary.LoadConfig();
                if (!Directory.Exists(BlockLibrary.LibraryPath)) Directory.CreateDirectory(BlockLibrary.LibraryPath);
                BlockLibrary.CleanupDiskCache();
            }
            catch { }
        }
        public void Terminate() { }
    }

    public class BlockBrowserCommands
    {
        [CommandMethod("BB", CommandFlags.Session)]
        public void OpenBlockBrowser()
        {
            try
            {
                string pendingCmd = null, pendingCategory = null, pendingBlockName = null;
                using (var form = new BlockBrowserForm())
                {
                    CadApp.ShowModalDialog(form);
                    if (form.DialogResult == DialogResult.OK && form.SelectedInsertBlock != null)
                    {
                        BlockLibrary.InsertBlock(form.SelectedInsertBlock, form.InsertScale, form.InsertRotation);
                    }
                    else if (form.DialogResult == DialogResult.Abort && !string.IsNullOrEmpty(form.PendingCommand))
                    {
                        pendingCmd = form.PendingCommand;
                        pendingCategory = form.PendingCategory;
                        pendingBlockName = form.PendingBlockName;
                    }
                }
                // 窗口关闭后，CAD原生接管焦点，直接执行操作
                if (pendingCmd == "BBADD" && !string.IsNullOrEmpty(pendingCategory))
                    DoAddToLibrary(pendingCategory, pendingBlockName);
                else if (pendingCmd == "BBEXPORT")
                    DoExportBlock();
            }
            catch (System.Exception ex) { MessageBox.Show("打开失败:\n" + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void DoAddToLibrary(string category, string blockName)
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try {
            var sr = ed.GetSelection();
            if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择对象，取消。"); return; }
            var pr = ed.GetPoint("\n指定块基点（回车用原点）: ");
            Point3d basePt;
            if (pr.Status == PromptStatus.OK)
                basePt = pr.Value;
            else if (pr.Status == PromptStatus.None)
                basePt = new Point3d(0, 0, 0);
            else
                { ed.WriteMessage("\n取消。"); return; }
            BlockLibrary.SaveSelectionAsBlockWithSelection(sr, blockName, category, basePt);
            } catch (System.Exception ex) { ed.WriteMessage("\n添加失败: " + ex.Message); }
        }

        private void DoExportBlock()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            try {

            // Read all user blocks from current drawing
            var blockNames = new List<string>();
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId btrId in bt)
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    if (!btr.IsLayout && !btr.Name.StartsWith("*"))
                        blockNames.Add(btr.Name);
                }
                tr.Commit();
            }

            if (blockNames.Count == 0)
            {
                ed.WriteMessage("\n当前图纸没有块定义。");
                return;
            }

            blockNames.Sort();

            // Dialog with multi-select
            var selectedBlocks = new List<string>();
            string selCategory = null;
            using (var form = new Form())
            {
                form.Text = "导出块到库";
                form.Size = new Size(400, 480);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.ShowInTaskbar = false;
                form.MaximizeBox = false; form.MinimizeBox = false;

                var lblSearch = new Label { Text = "搜索:", Location = new Point(15, 12), AutoSize = true };
                var txtSearch = new TextBox { Location = new Point(55, 9), Width = 320 };
                var lbl1 = new Label { Text = "选择块 (Ctrl/Shift 多选):", Location = new Point(15, 38), AutoSize = true };
                var lst = new ListBox { Location = new Point(15, 55), Size = new Size(360, 260), SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended };
                foreach (var name in blockNames) lst.Items.Add(name);
                if (lst.Items.Count > 0) lst.SelectedIndex = 0;
                txtSearch.TextChanged += (s2, e2) =>
                {
                    string kw = txtSearch.Text.Trim().ToLowerInvariant();
                    lst.Items.Clear();
                    foreach (var name in blockNames)
                        if (string.IsNullOrEmpty(kw) || name.ToLowerInvariant().Contains(kw))
                            lst.Items.Add(name);
                    if (lst.Items.Count > 0) lst.SelectedIndex = 0;
                };

                var lblCount = new Label { Text = "已选: 0", Location = new Point(280, 322), AutoSize = true, ForeColor = System.Drawing.Color.FromArgb(80, 80, 100) };
                lst.SelectedIndexChanged += (s2, e2) => { lblCount.Text = "已选: " + lst.SelectedIndices.Count; };

                var lbl2 = new Label { Text = "分类:", Location = new Point(15, 325), AutoSize = true };
                var cmb = new ComboBox { Location = new Point(15, 345), Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
                var categories = BlockLibrary.GetCategories().Where(c => c != "全部").ToList();
                cmb.Items.AddRange(categories.ToArray());
                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;

                var btnOk = new Button { Text = "导出", DialogResult = DialogResult.OK, Location = new Point(200, 410) };
                var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(290, 410) };
                form.Controls.AddRange(new Control[] { lblSearch, txtSearch, lbl1, lst, lblCount, lbl2, cmb, btnOk, btnCancel });
                form.AcceptButton = btnOk; form.CancelButton = btnCancel;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    foreach (var item in lst.SelectedItems) selectedBlocks.Add(item.ToString());
                    selCategory = cmb.Text.Trim();
                }
            }

            if (selectedBlocks.Count == 0 || string.IsNullOrEmpty(selCategory)) { ed.WriteMessage("\n取消。"); return; }
            int ok = 0, fail = 0;
            foreach (var blk in selectedBlocks)
            {
                if (BlockLibrary.ExportBlockFromCurrentDrawing(blk, selCategory)) ok++; else fail++;
            }
            ed.WriteMessage(string.Format("\n导出完成: {0} 成功, {1} 失败", ok, fail));
            } catch (System.Exception ex) { ed.WriteMessage("\n导出失败: " + ex.Message); }
        }
        [CommandMethod("KLLQ", CommandFlags.Session)]
        public void OpenBlockBrowserAlias() { OpenBlockBrowser(); }
        [CommandMethod("BBADD", CommandFlags.Session)]
        public void AddToLibrary()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try {
            ed.WriteMessage("\n块库: " + BlockLibrary.LibraryPath);
            var cr = ed.GetString("\n分类 [常用]: ");
            string cat = "常用";
            if (cr.Status == PromptStatus.OK && !string.IsNullOrEmpty(cr.StringResult)) cat = cr.StringResult.Trim();
            var nr = ed.GetString("\n块名称: ");
            if (nr.Status != PromptStatus.OK || string.IsNullOrEmpty(nr.StringResult)) { ed.WriteMessage("\n取消。"); return; }
            var pr = ed.GetPoint("\n指定块基点（回车用原点）: ");
            Point3d basePt;
            if (pr.Status == PromptStatus.OK) basePt = pr.Value;
            else if (pr.Status == PromptStatus.None) basePt = new Point3d(0, 0, 0);
            else { ed.WriteMessage("\n取消。"); return; }
            var sr = ed.GetSelection();
            if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择对象，取消。"); return; }
            BlockLibrary.SaveSelectionAsBlockWithSelection(sr, nr.StringResult.Trim(), cat, basePt);
            } catch (System.Exception ex) { ed.WriteMessage("\n添加失败: " + ex.Message); }
        }
        [CommandMethod("BBEXPORT", CommandFlags.Session)]
        public void ExportBlockToLibrary()
        {
            DoExportBlock();
        }
        [CommandMethod("BBTHUMB", CommandFlags.Session)]
        public void RefreshThumbnails()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                string cp = BlockLibrary.ThumbnailCachePath;
                if (Directory.Exists(cp)) { Directory.Delete(cp, true); ed.WriteMessage("\n缓存已清除: " + cp); }
                else ed.WriteMessage("\n缓存不存在。");
            }
            catch (System.Exception ex) { ed.WriteMessage("\n清除失败: " + ex.Message); }
        }
        [CommandMethod("BBINFO", CommandFlags.Session)]
        public void ShowInfo()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                ed.WriteMessage("\n=== 块浏览器 v1.23 (" + BlockLibrary.PlatformName + ") ===");
                ed.WriteMessage("\n库: " + BlockLibrary.LibraryPath);
                ed.WriteMessage("\n命令: BB KLLQ BBADD BBEXPORT BBTHUMB");
            }
            catch (System.Exception ex) { ed.WriteMessage("\n错误: " + ex.Message); }
        }
    }
}
