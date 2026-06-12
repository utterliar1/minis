using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Image = System.Drawing.Image;

#if AUTOCAD
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
#elif GSTARCAD
using GrxCAD.DatabaseServices;
using GrxCAD.Geometry;
#elif ZWCAD
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
#endif

namespace BlockBrowser
{
    public static partial class BlockLibrary
    {
        public static Image GetThumbnail(BlockInfo block, int size)
        {
            if (block == null) return GeneratePlaceholder("?", size);
            string cachePath = ThumbnailCacheService.GetCachePath(ThumbnailCachePath, block);
            bool hasSrc = File.Exists(block.FilePath);
            Image cached = ThumbnailCacheService.TryLoadValidCache(cachePath, block.FilePath, size);
            if (cached != null) return cached;

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
                                        if (icon != null && IsBitmapUseful(icon) && ThumbnailCacheService.IsPreviewIconSuitable(icon))
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
            return ThumbnailCacheService.IsBitmapUseful(bmp);
        }

        public static Bitmap ScaleToSquare(Image src, int size)
        {
            return ThumbnailCacheService.ScaleToSquare(src, size);
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
