using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Image = System.Drawing.Image;

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
    public static class BlockLibrary
    {
        public static string LibraryPath { get; set; }
        public const string AppVersion = "1.3.1";
        public static string PlatformName { get; set; }
        public static int ThumbSize { get; set; }
        public static double InsertScale { get; set; }
        public static double InsertRotation { get; set; }
        public static int FormWidth { get; set; }
        public static int FormHeight { get; set; }
        public static string NasLibraryPath { get; set; }
        public static string LocalMirrorPath { get; set; }
        private static readonly List<string> _protectedLocalCategories = new List<string>();
        public static List<string> ProtectedLocalCategories { get { return _protectedLocalCategories; } }
        public static bool PreferLocalWhenNasUnavailable { get; set; }
        public static bool AllowNasSync { get; set; }
        public static LibraryMode CurrentLibraryMode { get; set; }
        public static string SyncUserName { get; set; }
        public static ActiveLibraryResult ActiveLibrary { get; private set; }
        private static BlockBrowserConfigStore _configStore;

        static BlockLibrary()
        {
            _configStore = new BlockBrowserConfigStore(PluginRoot);
            ApplyConfig(BlockBrowserConfig.CreateDefault(PluginRoot));
            LoadConfig();
        }

        public static string ThumbnailCachePath { get { return Path.Combine(LibraryPath, ".thumbs"); } }
        public static string SyncLogPath { get { return Path.Combine(LocalMirrorPath ?? "", ".blockbrowser", "sync-log.txt"); } }

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
                return _configStore.ConfigPath;
            }
        }

        private static string DefaultConfigPath
        {
            get
            {
                // Deployment smoke tests verify the plugin knows about BlockBrowser.default.ini.
                return _configStore.DefaultConfigPath;
            }
        }

        private static string PluginRoot
        {
            get
            {
                string dllDir = Path.GetDirectoryName(typeof(BlockLibrary).Assembly.Location) ?? "";
                return Path.GetFullPath(Path.Combine(dllDir, ".."));
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return BlockBrowserConfigStore.EnsureTrailingSeparator(path);
        }

        private static string ToConfigPath(string path)
        {
            return _configStore.ToConfigPath(path);
        }

        private static string FromConfigPath(string path)
        {
            return _configStore.FromConfigPath(path);
        }

        public static bool IsSafeLibraryName(string name)
        {
            return BlockBrowserConfigStore.IsSafeLibraryName(name);
        }

        private static BlockBrowserConfig CaptureConfig()
        {
            var config = new BlockBrowserConfig
            {
                LibraryPath = LibraryPath,
                NasLibraryPath = NasLibraryPath,
                LocalMirrorPath = LocalMirrorPath,
                PreferLocalWhenNasUnavailable = PreferLocalWhenNasUnavailable,
                AllowNasSync = AllowNasSync,
                CurrentLibraryMode = CurrentLibraryMode,
                SyncUserName = SyncUserName,
                ThumbSize = ThumbSize,
                InsertScale = InsertScale,
                InsertRotation = InsertRotation,
                FormWidth = FormWidth,
                FormHeight = FormHeight
            };

            config.ProtectedLocalCategories.Clear();
            config.ProtectedLocalCategories.AddRange(ProtectedLocalCategories);
            config.RecentBlocks.AddRange(_recentBlocks);
            return config;
        }

        private static void ApplyConfig(BlockBrowserConfig config)
        {
            if (config == null) return;

            LibraryPath = config.LibraryPath;
            NasLibraryPath = config.NasLibraryPath;
            LocalMirrorPath = config.LocalMirrorPath;
            ProtectedLocalCategories.Clear();
            ProtectedLocalCategories.AddRange(config.ProtectedLocalCategories);
            PreferLocalWhenNasUnavailable = config.PreferLocalWhenNasUnavailable;
            AllowNasSync = config.AllowNasSync;
            CurrentLibraryMode = config.CurrentLibraryMode;
            SyncUserName = config.SyncUserName;
            ThumbSize = config.ThumbSize;
            InsertScale = config.InsertScale;
            InsertRotation = config.InsertRotation;
            FormWidth = config.FormWidth;
            FormHeight = config.FormHeight;

            _recentBlocks.Clear();
            _recentBlocks.AddRange(config.RecentBlocks);
        }

        public static void LoadConfig()
        {
            ApplyConfig(_configStore.Load(CaptureConfig()));
        }

        private static void EnsureUserConfigExists()
        {
            _configStore.EnsureUserConfigExists();
        }

        public static void SaveConfig()
        {
            _configStore.Save(CaptureConfig());
        }

        public static ActiveLibraryResult RefreshActiveLibrary()
        {
            var config = CaptureConfig();
            ActiveLibrary = LibraryPathService.RefreshActiveLibrary(config);
            ApplyConfig(config);
            return ActiveLibrary;
        }

        public static string LocalJournalPath
        {
            get { return LibraryPathService.GetLocalJournalPath(LocalMirrorPath); }
        }

        private static int _journalSequence;

        public static void RecordLocalChange(LocalChangeAction action, string relativePath, string toRelativePath, DateTime? baseNasLastWriteUtc)
        {
            if (ActiveLibrary == null || ActiveLibrary.Kind != ActiveLibraryKind.LocalMirror)
                return;
            if (IsProtectedLocalCategoryPath(relativePath) || IsProtectedLocalCategoryPath(toRelativePath))
                return;

            var entries = ChangeJournal.Load(LocalJournalPath);
            _journalSequence++;
            DateTime now = DateTime.UtcNow;
            entries.Add(new ChangeJournalEntry
            {
                Id = ChangeJournal.CreateId(now, SyncUserName, _journalSequence),
                Action = action,
                Path = relativePath ?? "",
                ToPath = toRelativePath ?? "",
                BaseNasLastWriteUtc = baseNasLastWriteUtc,
                LocalLastWriteUtc = now,
                User = string.IsNullOrEmpty(SyncUserName) ? Environment.UserName : SyncUserName,
                CreatedUtc = now
            });
            ChangeJournal.Save(LocalJournalPath, entries);
        }

        public static string ToLibraryRelativePath(string fullPath)
        {
            return LibraryPathService.ToLibraryRelativePath(LibraryPath, fullPath);
        }

        public static string GetProtectedLocalCategoriesText()
        {
            return string.Join(";", ProtectedLocalCategories.ToArray());
        }

        public static void SetProtectedLocalCategoriesFromText(string text)
        {
            ProtectedLocalCategories.Clear();
            foreach (string part in (text ?? "").Split(new[] { ';', '；', ',', '，', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string category = part.Trim();
                if (string.IsNullOrEmpty(category)) continue;
                if (!ProtectedLocalCategories.Contains(category)) ProtectedLocalCategories.Add(category);
            }
        }

        private static bool IsProtectedLocalCategoryPath(string relativePath)
        {
            if (ProtectedLocalCategories.Count == 0 || string.IsNullOrEmpty(relativePath)) return false;
            string[] parts = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 && ProtectedLocalCategories.Contains(parts[0], StringComparer.OrdinalIgnoreCase);
        }

        public static void CopyDirectoryContents(string sourceDir, string targetDir)
        {
            BlockFileOperations.CopyDirectoryContents(sourceDir, targetDir);
        }

        public static MirrorDirectoryResult UpdateLocalMirrorFromNas()
        {
            if (string.IsNullOrEmpty(NasLibraryPath) || !Directory.Exists(NasLibraryPath))
                throw new DirectoryNotFoundException("NAS library is unavailable: " + NasLibraryPath);
            if (string.IsNullOrEmpty(LocalMirrorPath))
                throw new InvalidOperationException("Local mirror path is empty.");

            var pending = ChangeJournal.Load(LocalJournalPath);
            if (pending.Count > 0 && AllowNasSync)
                throw new InvalidOperationException("Local changes are pending. Sync or clear local changes before updating the local mirror from NAS.");

            return BlockFileOperations.MirrorDirectoryContents(NasLibraryPath, LocalMirrorPath, GetProtectedLocalPaths(pending), ProtectedLocalCategories);
        }

        private static IEnumerable<string> GetProtectedLocalPaths(IEnumerable<ChangeJournalEntry> entries)
        {
            foreach (var entry in entries ?? new ChangeJournalEntry[0])
            {
                if (entry == null) continue;
                if (!string.IsNullOrEmpty(entry.Path)) yield return entry.Path;
                if (!string.IsNullOrEmpty(entry.ToPath)) yield return entry.ToPath;
            }
        }

        public static List<SyncFileSnapshot> BuildSnapshots(IEnumerable<ChangeJournalEntry> entries)
        {
            var list = new List<SyncFileSnapshot>();
            foreach (var entry in entries ?? new ChangeJournalEntry[0])
            {
                string rel = entry.Path ?? "";
                string localPath = Path.Combine(LocalMirrorPath ?? "", rel);
                string nasPath = Path.Combine(NasLibraryPath ?? "", rel);
                bool localExists = File.Exists(localPath);
                bool nasExists = File.Exists(nasPath);
                list.Add(new SyncFileSnapshot
                {
                    Path = rel,
                    LocalExists = localExists,
                    NasExists = nasExists,
                    LocalLastWriteUtc = localExists ? (DateTime?)File.GetLastWriteTimeUtc(localPath) : null,
                    NasLastWriteUtc = nasExists ? (DateTime?)File.GetLastWriteTimeUtc(nasPath) : null,
                    BaseNasLastWriteUtc = entry.BaseNasLastWriteUtc
                });
            }
            return list;
        }

        public static SyncPlan PreviewLocalSync()
        {
            EnsureNasSyncAllowed();
            var entries = ChangeJournal.Load(LocalJournalPath);
            var syncEntries = new List<ChangeJournalEntry>(entries);
            syncEntries.AddRange(LocalOnlySyncDiscovery.Discover(
                LocalMirrorPath,
                NasLibraryPath,
                entries,
                SyncUserName,
                DateTime.UtcNow,
                ProtectedLocalCategories));
            return SyncPlanner.CreatePlan(syncEntries, BuildSnapshots(syncEntries));
        }

        public static SyncPlan SyncSafeUploadsToNas()
        {
            EnsureNasSyncAllowed();
            if (string.IsNullOrEmpty(NasLibraryPath) || !Directory.Exists(NasLibraryPath))
                throw new DirectoryNotFoundException("NAS library is unavailable: " + NasLibraryPath);

            var entries = ChangeJournal.Load(LocalJournalPath);
            var syncEntries = new List<ChangeJournalEntry>(entries);
            syncEntries.AddRange(LocalOnlySyncDiscovery.Discover(
                LocalMirrorPath,
                NasLibraryPath,
                entries,
                SyncUserName,
                DateTime.UtcNow,
                ProtectedLocalCategories));
            var plan = SyncPlanner.CreatePlan(syncEntries, BuildSnapshots(syncEntries));
            var uploadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var decision in plan.Decisions)
            {
                if (decision.Kind != SyncDecisionKind.Upload)
                    continue;

                string src = Path.Combine(LocalMirrorPath, decision.Path);
                string dst = Path.Combine(NasLibraryPath, decision.TargetPath);
                if (File.Exists(dst))
                    continue;

                string dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir)) Directory.CreateDirectory(dstDir);
                File.Copy(src, dst, false);
                uploadedPaths.Add(decision.Path ?? "");
            }

            if (uploadedPaths.Count > 0)
            {
                var remaining = entries
                    .Where(e => !uploadedPaths.Contains(e.Path ?? ""))
                    .ToList();
                if (remaining.Count != entries.Count)
                    ChangeJournal.Save(LocalJournalPath, remaining);
            }

            return plan;
        }

        private static void EnsureNasSyncAllowed()
        {
            if (!AllowNasSync)
                throw new InvalidOperationException("当前电脑未启用同步到 NAS。请联系指定维护人。");
        }

        private static void EnsureActiveLibraryWritable()
        {
            if (ActiveLibrary != null && ActiveLibrary.Kind == ActiveLibraryKind.Nas && !AllowNasSync)
                throw new InvalidOperationException("当前电脑未启用写入 NAS。请先更新本地图库后，在本地副本中操作。");
        }

        public static List<string> GetCategories()
        {
            return MergeProtectedCategories(BlockLibraryService.GetCategories(LibraryPath));
        }

        public static List<string> GetBrowsableCategories()
        {
            return BlockLibraryService.GetBrowsableCategories(LibraryPath);
        }

        private static List<string> MergeProtectedCategories(List<string> categories)
        {
            var merged = categories ?? new List<string>();
            foreach (string category in ProtectedLocalCategories)
            {
                if (string.IsNullOrWhiteSpace(category)) continue;
                if (!merged.Contains(category)) merged.Add(category);
            }
            return merged;
        }

        public static CategoryCreationResult CreateCategory(string category)
        {
            EnsureActiveLibraryWritable();
            return CategoryCreationService.CreateCategory(LibraryPath, category);
        }

        public static List<BlockInfo> GetBlocks(string category)
        {
            return BlockLibraryService.GetBlocks(LibraryPath, category, _recentBlocks);
        }

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
            try
            {
                EnsureActiveLibraryWritable();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n保存失败: " + ex.Message);
                return false;
            }
            BlockWritePlan plan = BlockWriteService.PrepareSaveTarget(LibraryPath, blockName, category);
            if (!plan.IsValid)
            {
                ed.WriteMessage("\n名称或分类包含非法字符，取消。");
                return false;
            }
            // 覆盖确认
            if (plan.Exists)
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
                        newDb.SaveAs(plan.OutputPath, DwgVersion.Current);
                    }
                }
                RefreshThumbnail(BlockWriteService.CreateSavedBlockInfo(plan.OutputPath, plan.Category));
                RecordLocalChange(LocalChangeAction.Add, ToLibraryRelativePath(plan.OutputPath), "", null);
                ed.WriteMessage(string.Format("\n块 {0} 已保存到 [{1}]，基点: ({2:F2},{3:F2})", plan.BlockName, plan.Category, basePt.X, basePt.Y));
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
            try
            {
                EnsureActiveLibraryWritable();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n导出失败: " + ex.Message);
                return false;
            }
            BlockWritePlan plan = BlockWriteService.PrepareSaveTarget(LibraryPath, blockName, category);
            if (!plan.IsValid)
            {
                ed.WriteMessage("\n名称或分类包含非法字符，取消。");
                return false;
            }
            // 覆盖确认
            if (plan.Exists)
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
                        if (!bt.Has(plan.BlockName))
                        {
                            ed.WriteMessage(string.Format("\n未找到块: {0}", plan.BlockName));
                            tr.Commit();
                            return false;
                        }
                        blockId = bt[plan.BlockName];
                        tr.Commit();
                    }
                    using (Database newDb = db.Wblock(blockId))
                        newDb.SaveAs(plan.OutputPath, DwgVersion.Current);
                }
                RefreshThumbnail(BlockWriteService.CreateSavedBlockInfo(plan.OutputPath, plan.Category));
                RecordLocalChange(LocalChangeAction.Add, ToLibraryRelativePath(plan.OutputPath), "", null);
                ed.WriteMessage(string.Format("\n块 {0} 已导出到 [{1}]", plan.BlockName, plan.Category));
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
                BlockLibrary.RefreshActiveLibrary();
                if (BlockLibrary.ActiveLibrary != null && BlockLibrary.ActiveLibrary.IsAvailable && !Directory.Exists(BlockLibrary.LibraryPath))
                    Directory.CreateDirectory(BlockLibrary.LibraryPath);
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
            OpenBlockBrowserCore();
        }

        [CommandMethod("BB_PANEL", CommandFlags.Session)]
        public void OpenBlockBrowserPanel()
        {
            OpenBlockBrowserCore();
        }

        [CommandMethod("BBPANEL", CommandFlags.Session)]
        public void OpenBlockBrowserPanelCompat()
        {
            OpenBlockBrowserCore();
        }

        private void OpenBlockBrowserCore()
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

            var selectedBlocks = new List<string>();
            string selCategory = null;
            var categories = CategorySelectionService.GetUserCategories(BlockLibrary.GetCategories());
            using (var form = new ExportBlocksDialog(blockNames, categories))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    selectedBlocks.AddRange(form.SelectedBlocks);
                    selCategory = form.SelectedCategory;
                }
            }

            var request = ExportBlockRequestService.CreatePlan(selectedBlocks, selCategory, BlockLibrary.IsSafeLibraryName);
            if (request.Action == ExportBlockRequestAction.Cancel) { ed.WriteMessage("\n取消。"); return; }
            if (request.Action == ExportBlockRequestAction.InvalidCategory) { ed.WriteMessage("\n分类包含非法字符，取消。"); return; }
            int ok = 0, fail = 0;
            foreach (var blk in request.SelectedBlocks)
            {
                if (BlockLibrary.ExportBlockFromCurrentDrawing(blk, request.Category)) ok++; else fail++;
            }
            ed.WriteMessage("\n" + ExportBlockRequestService.FormatCompletion(ok, fail));
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
        [CommandMethod("BBSYNC", CommandFlags.Session)]
        public void SyncLocalChanges()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (!BlockLibrary.AllowNasSync)
                {
                    ed.WriteMessage("\n当前电脑未启用同步到 NAS。请联系指定维护人。");
                    return;
                }

                var preview = BlockLibrary.PreviewLocalSync();
                ed.WriteMessage("\n" + SyncSummaryMessageService.FormatPreviewCommand(preview));
                var confirm = ed.GetString("\n继续同步到 NAS? [Y/N] <N>: ");
                if (confirm.Status != PromptStatus.OK || !string.Equals((confirm.StringResult ?? "").Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                {
                    ed.WriteMessage("\n已取消同步。");
                    return;
                }

                var plan = BlockLibrary.SyncSafeUploadsToNas();
                SyncSummaryMessageService.AppendLog(BlockLibrary.SyncLogPath, plan);
                ed.WriteMessage("\n" + SyncSummaryMessageService.FormatCommand(plan));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n同步失败: " + ex.Message);
            }
        }
        [CommandMethod("BBMIRROR", CommandFlags.Session)]
        public void UpdateLocalMirror()
        {
            var ed = CadApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var result = BlockLibrary.UpdateLocalMirrorFromNas();
                ed.WriteMessage("\n" + MirrorSummaryMessageService.FormatCommand(result));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n更新本地图库失败: " + ex.Message);
            }
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
                foreach (var line in BlockBrowserInfoService.FormatLines(
                    BlockLibrary.AppVersion,
                    BlockLibrary.PlatformName,
                    BlockLibrary.LibraryPath))
                {
                    ed.WriteMessage("\n" + line);
                }
            }
            catch (System.Exception ex) { ed.WriteMessage("\n错误: " + ex.Message); }
        }
    }
}
