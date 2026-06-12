using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

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
    internal static class CadThumbnailRenderer
    {
        internal static Bitmap TryRender(string filePath, int size)
        {
            try
            {
                using (var db = new Database(false, true))
                {
                    db.ReadDwgFile(filePath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                        Bitmap previewIcon = TryRenderPreviewIcon(tr, bt, size);
                        if (previewIcon != null) return previewIcon;

                        Bitmap modelSpace = TryRenderModelSpace(tr, bt, size);
                        if (modelSpace != null) return modelSpace;

                        return TryRenderLargestBlockDefinition(tr, bt, size);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BlockBrowser] CadThumbnailRenderer: " + ex.Message);
                return null;
            }
        }

        private static Bitmap TryRenderPreviewIcon(Transaction tr, BlockTable bt, int size)
        {
            foreach (ObjectId btrId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                if (!btr.IsLayout && !btr.Name.StartsWith("*") && btr.HasPreviewIcon)
                {
                    try
                    {
                        using (Bitmap icon = btr.PreviewIcon)
                        {
                            if (icon != null &&
                                ThumbnailCacheService.IsBitmapUseful(icon) &&
                                ThumbnailCacheService.IsPreviewIconSuitable(icon))
                            {
                                return ThumbnailCacheService.ScaleToSquare(icon, size);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[BlockBrowser] PreviewIcon: " + ex.Message);
                    }
                }
            }

            return null;
        }

        private static Bitmap TryRenderModelSpace(Transaction tr, BlockTable bt, int size)
        {
            if (!bt.Has("*Model_Space")) return null;

            var msBtr = (BlockTableRecord)tr.GetObject(bt["*Model_Space"], OpenMode.ForRead);
            int msCount = CountEntities(msBtr);
            if (msCount <= 0) return null;

            return RenderBlockRecord(tr, msBtr, size);
        }

        private static Bitmap TryRenderLargestBlockDefinition(Transaction tr, BlockTable bt, int size)
        {
            BlockTableRecord bestBtr = null;
            int bestCount = 0;

            foreach (ObjectId btrId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                if (!btr.IsLayout && !btr.Name.StartsWith("*"))
                {
                    int count = CountEntities(btr);
                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestBtr = btr;
                    }
                }
            }

            return bestBtr == null ? null : RenderBlockRecord(tr, bestBtr, size);
        }

        private static int CountEntities(BlockTableRecord btr)
        {
            int count = 0;
            foreach (ObjectId ignored in btr)
            {
                count++;
            }

            return count;
        }

        private static Bitmap RenderBlockRecord(Transaction tr, BlockTableRecord btr, int size)
        {
            Bitmap result = new Bitmap(size, size);
            using (var g = Graphics.FromImage(result))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                RenderBlockContents(g, tr, btr, size);
            }

            return result;
        }

        private static void RenderBlockContents(Graphics g, Transaction tr, BlockTableRecord btr, int size)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int count = 0;
            var visited = new HashSet<ObjectId>();
            CollectExt(tr, btr, Matrix3d.Identity, ref minX, ref minY, ref maxX, ref maxY, ref count, visited);
            if (count == 0 || minX >= maxX || minY >= maxY) return;

            double graphWidth = maxX - minX;
            double graphHeight = maxY - minY;
            int margin = 8;
            int drawWidth = size - margin * 2;
            int drawHeight = size - margin * 2;
            double scale = Math.Min(drawWidth / graphWidth, drawHeight / graphHeight);
            double offsetX = margin + (drawWidth - graphWidth * scale) / 2.0;
            double offsetY = margin + (drawHeight - graphHeight * scale) / 2.0;

            using (var pen = new Pen(Color.FromArgb(30, 60, 120), 1.0f))
            {
                var drawVisited = new HashSet<ObjectId>();
                DrawBtr(g, tr, btr, pen, minX, minY, scale, offsetX, offsetY, size, Matrix3d.Identity, drawVisited);
            }
        }

        private static void CollectExt(Transaction tr, BlockTableRecord btr, Matrix3d xf,
            ref double minX, ref double minY, ref double maxX, ref double maxY, ref int count, HashSet<ObjectId> visited)
        {
            if (!visited.Add(btr.ObjectId)) return;

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
                        CollectExt(tr, def, xf * br.BlockTransform, ref minX, ref minY, ref maxX, ref maxY, ref count, visited);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[BlockBrowser] CollectExt: " + ex.Message);
                    }
                }
                else
                {
                    count++;
                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        UpdateBounds(ext.MinPoint, xf, ref minX, ref minY, ref maxX, ref maxY);
                        UpdateBounds(ext.MaxPoint, xf, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    catch
                    {
                    }
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
            Pen pen, double minX, double minY, double scale, double offsetX, double offsetY, int size,
            Matrix3d xf, HashSet<ObjectId> visited)
        {
            if (!visited.Add(btr.ObjectId)) return;

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
                        DrawBtr(g, tr, def, pen, minX, minY, scale, offsetX, offsetY, size, xf * br.BlockTransform, visited);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[BlockBrowser] DrawBtr: " + ex.Message);
                    }
                }
                else
                {
                    DrawEntXf(g, ent, pen, minX, minY, scale, offsetX, offsetY, size, xf);
                }
            }
        }

        private static PointF Xf(Point3d pt, Matrix3d xf, double minX, double minY, double scale, double offsetX, double offsetY, int size)
        {
            Point3d p = pt.TransformBy(xf);
            return new PointF((float)((p.X - minX) * scale + offsetX), (float)(size - ((p.Y - minY) * scale + offsetY)));
        }

        private static void DrawEntXf(Graphics g, Entity ent, Pen pen,
            double minX, double minY, double scale, double offsetX, double offsetY, int size, Matrix3d xf)
        {
            try
            {
                if (ent is Line)
                {
                    Line line = (Line)ent;
                    g.DrawLine(pen, Xf(line.StartPoint, xf, minX, minY, scale, offsetX, offsetY, size),
                        Xf(line.EndPoint, xf, minX, minY, scale, offsetX, offsetY, size));
                }
                else if (ent is Circle)
                {
                    Circle circle = (Circle)ent;
                    PointF center = Xf(circle.Center, xf, minX, minY, scale, offsetX, offsetY, size);
                    float radius = (float)(circle.Radius * scale);
                    if (radius > 0.5f) g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
                }
                else if (ent is Arc)
                {
                    Arc arc = (Arc)ent;
                    PointF center = Xf(arc.Center, xf, minX, minY, scale, offsetX, offsetY, size);
                    float radius = (float)(arc.Radius * scale);
                    if (radius > 0.5f)
                    {
                        float startAngle = (float)(arc.StartAngle * 180.0 / Math.PI);
                        float sweepAngle = (float)((arc.EndAngle - arc.StartAngle) * 180.0 / Math.PI);
                        if (sweepAngle < 0) sweepAngle += 360f;
                        g.DrawArc(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2, startAngle, sweepAngle);
                    }
                }
                else if (ent is Polyline)
                {
                    Polyline polyline = (Polyline)ent;
                    int vertexCount = polyline.NumberOfVertices;
                    if (vertexCount >= 2)
                    {
                        PointF[] points = new PointF[vertexCount + (polyline.Closed ? 1 : 0)];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            points[i] = Xf(polyline.GetPoint3dAt(i), xf, minX, minY, scale, offsetX, offsetY, size);
                        }

                        if (polyline.Closed) points[vertexCount] = points[0];
                        g.DrawLines(pen, points);
                    }
                }
                else if (ent is Spline)
                {
                    try
                    {
                        Entity splinePolyline = ((Spline)ent).ToPolyline() as Entity;
                        if (splinePolyline != null) DrawEntXf(g, splinePolyline, pen, minX, minY, scale, offsetX, offsetY, size, xf);
                    }
                    catch
                    {
                    }
                }
                else if (ent is Ellipse)
                {
                    Ellipse ellipse = (Ellipse)ent;
                    PointF center = Xf(ellipse.Center, xf, minX, minY, scale, offsetX, offsetY, size);
                    float radiusX = (float)(ellipse.MajorAxis.Length * scale);
                    float radiusY = (float)(ellipse.MinorAxis.Length * scale);
                    if (radiusX > 0.5f && radiusY > 0.5f)
                    {
                        g.DrawEllipse(pen, center.X - radiusX, center.Y - radiusY, radiusX * 2, radiusY * 2);
                    }
                }
                else if (ent is DBPoint)
                {
                    PointF point = Xf(((DBPoint)ent).Position, xf, minX, minY, scale, offsetX, offsetY, size);
                    g.FillRectangle(Brushes.DarkSlateBlue, point.X - 1, point.Y - 1, 3, 3);
                }
                else
                {
                    DrawEntityExtents(g, ent, minX, minY, scale, offsetX, offsetY, size, xf);
                }
            }
            catch
            {
            }
        }

        private static void DrawEntityExtents(Graphics g, Entity ent,
            double minX, double minY, double scale, double offsetX, double offsetY, int size, Matrix3d xf)
        {
            try
            {
                Extents3d ext = ent.GeometricExtents;
                PointF p1 = Xf(ext.MinPoint, xf, minX, minY, scale, offsetX, offsetY, size);
                PointF p2 = Xf(ext.MaxPoint, xf, minX, minY, scale, offsetX, offsetY, size);
                float width = Math.Abs(p2.X - p1.X);
                float height = Math.Abs(p2.Y - p1.Y);
                if (width > 1 && height > 1)
                {
                    using (var lightPen = new Pen(Color.FromArgb(150, 170, 200), 0.6f))
                    {
                        g.DrawRectangle(lightPen, Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), width, height);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
