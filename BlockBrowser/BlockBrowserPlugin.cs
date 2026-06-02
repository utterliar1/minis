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
        public static string PlatformName { get; set; }

        static BlockLibrary()
        {
            string dllDir = Path.GetDirectoryName(typeof(BlockLibrary).Assembly.Location) ?? "";
            string pluginRoot = Path.GetFullPath(Path.Combine(dllDir, ".."));
            LibraryPath = Path.Combine(pluginRoot, "我的常用块");
            LoadConfig();
        }

        public static string ThumbnailCachePath { get { return Path.Combine(LibraryPath, ".thumbs"); } }

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
                foreach (string line in File.ReadAllLines(cfgPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = trimmed.Substring(0, eq).Trim();
                    string val = trimmed.Substring(eq + 1).Trim();
                    if (key.Equals("LibraryPath", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
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
                File.WriteAllLines(cfgPath, lines);
            }
            catch { }
        }

        public static List<string> GetCategories()
        {
            if (!Directory.Exists(LibraryPath)) Directory.CreateDirectory(LibraryPath);
            var c = new List<string> { "全部" };
            foreach (var d in Directory.GetDirectories(LibraryPath))
            {
                string n = Path.GetFileName(d);
                if (!n.StartsWith(".")) c.Add(n);
            }
            return c;
        }

        public static List<BlockInfo> GetBlocks(string category)
        {
            if (!Directory.Exists(LibraryPath)) Directory.CreateDirectory(LibraryPath);
            var blocks = new List<BlockInfo>();
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
            if (File.Exists(cachePath))
            {
                try
                {
                    Image c = Image.FromFile(cachePath);
                    Image r = new Bitmap(c, size, size);
                    c.Dispose();
                    return r;
                }
                catch { }
            }
            if (!File.Exists(block.FilePath)) return GeneratePlaceholder(block.Name, size);
            try
            {
                using (var db = new Database(false, true))
                {
                    db.ReadDwgFile(block.FilePath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        foreach (ObjectId btrId in bt)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            if (!btr.IsLayout && !btr.Name.StartsWith("*") && btr.HasPreviewIcon)
                            {
                                Bitmap icon = btr.PreviewIcon;
                                if (icon != null)
                                {
                                    Bitmap scaled = new Bitmap(icon, size, size);
                                    icon.Dispose();
                                    SaveThumbnailCache(cachePath, scaled);
                                    return scaled;
                                }
                            }
                        }
                        if (bt.Has("*Model_Space"))
                        {
                            var ms = (BlockTableRecord)tr.GetObject(bt["*Model_Space"], OpenMode.ForRead);
                            Bitmap bmp = new Bitmap(size, size);
                            using (var g = Graphics.FromImage(bmp))
                            {
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.Clear(Color.White);
                                RenderBlockContents(g, tr, ms, size);
                            }
                            SaveThumbnailCache(cachePath, bmp);
                            return bmp;
                        }
                    }
                }
            }
            catch { }
            return GeneratePlaceholder(block.Name, size);
        }

        private static void RenderBlockContents(Graphics g, Transaction tr, BlockTableRecord btr, int size)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int cnt = 0;
            CollectExt(tr, btr, Matrix3d.Identity, ref minX, ref minY, ref maxX, ref maxY, ref cnt);
            if (cnt == 0 || minX >= maxX || minY >= maxY) return;
            double gw = maxX - minX, gh = maxY - minY;
            int m = 8, dw = size - m * 2, dh = size - m * 2;
            double sc = Math.Min(dw / gw, dh / gh);
            double ox = m + (dw - gw * sc) / 2.0, oy = m + (dh - gh * sc) / 2.0;
            using (var pen = new Pen(Color.FromArgb(30, 60, 120), 1.0f))
                DrawBtr(g, tr, btr, pen, minX, minY, sc, ox, oy, size, Matrix3d.Identity);
        }

        private static void CollectExt(Transaction tr, BlockTableRecord btr, Matrix3d xf,
            ref double minX, ref double minY, ref double maxX, ref double maxY, ref int cnt)
        {
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
                        CollectExt(tr, def, xf * br.BlockTransform, ref minX, ref minY, ref maxX, ref maxY, ref cnt);
                    }
                    catch { }
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
            Pen pen, double minX, double minY, double sc, double ox, double oy, int sz, Matrix3d xf)
        {
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
                        DrawBtr(g, tr, def, pen, minX, minY, sc, ox, oy, sz, xf * br.BlockTransform);
                    }
                    catch { }
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

        public static Image GeneratePlaceholder(string name, int size)
        {
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
                    float fontSize = Math.Max(7f, size / 14f);
                    using (var f = new DrawingFont("Microsoft YaHei", fontSize, FontStyle.Regular))
                    {
                        var textRect = new RectangleF(2, size * 0.68f, size - 4, size * 0.30f);
                        g.DrawString(dn, f, Brushes.DimGray, textRect,
                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                    }
                }
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
            string key = block.FilePath ?? "";
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }

        public static void RefreshThumbnail(BlockInfo block)
        {
            string cp = Path.Combine(ThumbnailCachePath, GetCacheKey(block) + ".png");
            if (File.Exists(cp)) File.Delete(cp);
        }

        public static void InsertBlock(BlockInfo block, double scale, double rotation)
        {
            if (block == null) return;
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            string tempCopy = null;
            try
            {
                PromptPointResult pr = ed.GetPoint("\n指定插入点: ");
                if (pr.Status != PromptStatus.OK) return;
                Point3d pt = pr.Value;
                string bname = block.Name;
                using (DocumentLock dl = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                        if (!bt.Has(bname))
                        {
                            tempCopy = Path.Combine(Path.GetTempPath(), "_bb_" + Guid.NewGuid().ToString("N") + ".dwg");
                            File.Copy(block.FilePath, tempCopy, true);
                            using (Database extDb = new Database(false, true))
                            {
                                extDb.ReadDwgFile(tempCopy, FileOpenMode.OpenForReadAndAllShare, true, "");
                                db.Insert(bname, extDb, false);
                            }
                        }
                        tr.Commit();
                    }
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
                ed.WriteMessage("\n已插入: " + bname);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n插入失败: " + ex.Message);
            }
            finally
            {
                try { if (tempCopy != null && File.Exists(tempCopy)) File.Delete(tempCopy); } catch { }
            }
        }

        public static bool SaveSelectionAsBlock(string blockName, string category)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            Editor ed = doc.Editor;
            PromptSelectionResult sr = ed.GetSelection();
            if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择对象。"); return false; }
            string catDir = Path.Combine(LibraryPath, category);
            if (!Directory.Exists(catDir)) Directory.CreateDirectory(catDir);
            string outPath = Path.Combine(catDir, blockName + ".dwg");
            try
            {
                using (DocumentLock dl = doc.LockDocument())
                {
                    Database db = doc.Database;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                        BlockTableRecord tempBtr = new BlockTableRecord();
                        tempBtr.Name = blockName;
                        ObjectId tempId = bt.Add(tempBtr);
                        tr.AddNewlyCreatedDBObject(tempBtr, true);
                        foreach (ObjectId soId in sr.Value.GetObjectIds())
                        {
                            Entity ent = (Entity)tr.GetObject(soId, OpenMode.ForWrite);
                            Entity clone = ent.Clone() as Entity;
                            tempBtr.AppendEntity(clone);
                            tr.AddNewlyCreatedDBObject(clone, true);
                        }
                        tr.Commit();
                        using (Database newDb = db.Wblock(tempId))
#if ZWCAD
                            newDb.DxfOut(outPath, 0, DwgVersion.Current, true);
#else
                            newDb.DxfOut(outPath, 0, DwgVersion.Current);
#endif
                    }
                    RefreshThumbnail(new BlockInfo { FilePath = outPath, Category = category });
                    ed.WriteMessage(string.Format("\n块 {0} 已保存到 [{1}]", blockName, category));
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(string.Format("\n保存失败: {0}", ex.Message));
                return false;
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
            try
            {
                using (DocumentLock dl = doc.LockDocument())
                {
                    Database db = doc.Database;
                    // 将选中的对象复制到新数据库，以basePt为基点
                    ObjectIdCollection ids = new ObjectIdCollection(sr.Value.GetObjectIds());
                    using (Database newDb = db.Wblock(ids, basePt))
                    {
#if ZWCAD
                        newDb.DxfOut(outPath, 0, DwgVersion.Current, true);
#else
                        newDb.DxfOut(outPath, 0, DwgVersion.Current);
#endif
                    }
                }
                RefreshThumbnail(new BlockInfo { FilePath = outPath, Category = category });
                ed.WriteMessage(string.Format("\n块 {0} 已保存到 [{1}]，基点: ({1:F2},{2:F2})", blockName, category, basePt.X, basePt.Y));
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
#if ZWCAD
                        newDb.DxfOut(outPath, 0, DwgVersion.Current, true);
#else
                        newDb.DxfOut(outPath, 0, DwgVersion.Current);
#endif
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
                BlockLibrary.PlatformName = "浩辰CAD";
#elif AUTOCAD
                BlockLibrary.PlatformName = "AutoCAD";
#elif ZWCAD
                BlockLibrary.PlatformName = "中望CAD";
#endif
                BlockLibrary.LoadConfig();
                if (!Directory.Exists(BlockLibrary.LibraryPath)) Directory.CreateDirectory(BlockLibrary.LibraryPath);
                foreach (var cat in new[] { "常用", "电气", "建筑", "机械", "标注", "其他" })
                {
                    string p = Path.Combine(BlockLibrary.LibraryPath, cat);
                    if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                }
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
                    else if (form.DialogResult == DialogResult.Abort && !string.IsNullOrEmpty(form._pendingCommand))
                    {
                        pendingCmd = form._pendingCommand;
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

        private string _pendingCategory;
        private string _pendingBlockName;

        private void DoAddToLibrary(string category, string blockName)
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            // 1. 选基点
            var pr = ed.GetPoint("\n指定块基点: ");
            Point3d basePt = (pr.Status == PromptStatus.OK) ? pr.Value : new Point3d(0, 0, 0);
            // 2. 选对象
            var sr = ed.GetSelection();
            if (sr.Status != PromptStatus.OK) { ed.WriteMessage("\n未选择对象，取消。"); return; }
            // 3. 保存
            BlockLibrary.SaveSelectionAsBlockWithSelection(sr, blockName, category, basePt);
        }

        private void DoExportBlock()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            // 读取当前图纸的所有用户块
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

            // 弹窗选择块和分类
            string selBlock = null;
            string selCategory = null;
            using (var form = new Form())
            {
                form.Text = "导出块到库";
                form.Size = new Size(400, 450);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false; form.MinimizeBox = false;

                var lbl1 = new Label { Text = "选择块:", Location = new Point(15, 15), AutoSize = true };
                var lst = new ListBox { Location = new Point(15, 35), Size = new Size(360, 280) };
                foreach (var name in blockNames) lst.Items.Add(name);
                if (lst.Items.Count > 0) lst.SelectedIndex = 0;

                var lbl2 = new Label { Text = "分类:", Location = new Point(15, 325), AutoSize = true };
                var cmb = new ComboBox { Location = new Point(15, 345), Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
                var categories = BlockLibrary.GetCategories().Where(c => c != "全部").ToList();
                cmb.Items.AddRange(categories.ToArray());
                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;

                var btnOk = new Button { Text = "导出", DialogResult = DialogResult.OK, Location = new Point(200, 380) };
                var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(290, 380) };
                form.Controls.AddRange(new Control[] { lbl1, lst, lbl2, cmb, btnOk, btnCancel });
                form.AcceptButton = btnOk; form.CancelButton = btnCancel;

                if (form.ShowDialog() == DialogResult.OK && lst.SelectedItem != null)
                {
                    selBlock = lst.SelectedItem.ToString();
                    selCategory = cmb.Text.Trim();
                }
            }

            if (string.IsNullOrEmpty(selBlock) || string.IsNullOrEmpty(selCategory)) { ed.WriteMessage("\n取消。"); return; }
            BlockLibrary.ExportBlockFromCurrentDrawing(selBlock, selCategory);
        }
        [CommandMethod("KLLQ", CommandFlags.Session)]
        public void OpenBlockBrowserAlias() { OpenBlockBrowser(); }
        [CommandMethod("BBADD", CommandFlags.Session)]
        public void AddToLibrary()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n块库: " + BlockLibrary.LibraryPath);
            var cr = ed.GetString("\n分类 [常用]: ");
            string cat = "常用";
            if (cr.Status == PromptStatus.OK && !string.IsNullOrEmpty(cr.StringResult)) cat = cr.StringResult.Trim();
            var nr = ed.GetString("\n块名称: ");
            if (nr.Status != PromptStatus.OK || string.IsNullOrEmpty(nr.StringResult)) { ed.WriteMessage("\n取消。"); return; }
            BlockLibrary.SaveSelectionAsBlock(nr.StringResult.Trim(), cat);
        }
        [CommandMethod("BBEXPORT", CommandFlags.Session)]
        public void ExportBlockToLibrary()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n块库: " + BlockLibrary.LibraryPath);
            var cr = ed.GetString("\n分类 [常用]: ");
            string cat = "常用";
            if (cr.Status == PromptStatus.OK && !string.IsNullOrEmpty(cr.StringResult)) cat = cr.StringResult.Trim();
            var nr = ed.GetString("\n块名称: ");
            if (nr.Status != PromptStatus.OK || string.IsNullOrEmpty(nr.StringResult)) { ed.WriteMessage("\n取消。"); return; }
            BlockLibrary.ExportBlockFromCurrentDrawing(nr.StringResult.Trim(), cat);
        }
        [CommandMethod("BBTHUMB", CommandFlags.Session)]
        public void RefreshThumbnails()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            string cp = BlockLibrary.ThumbnailCachePath;
            if (Directory.Exists(cp)) { Directory.Delete(cp, true); ed.WriteMessage("\n缓存已清除: " + cp); }
            else ed.WriteMessage("\n缓存不存在。");
        }
        [CommandMethod("BBINFO", CommandFlags.Session)]
        public void ShowInfo()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 块浏览器 v1.0 (" + BlockLibrary.PlatformName + ") ===");
            ed.WriteMessage("\n库: " + BlockLibrary.LibraryPath);
            ed.WriteMessage("\n命令: BB KLLQ BBINSERT BBADD BBEXPORT BBTHUMB");
        }
    }
}