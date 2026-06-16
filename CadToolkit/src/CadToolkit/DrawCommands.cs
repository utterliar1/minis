using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections;
using CadToolkit.Core;
using CadToolkit.UI;
using LayerStandardRule = CadToolkit.Core.Config.LayerStandardRule;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using CadColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using GrxCAD.Runtime;
using CadColor = GrxCAD.Colors.Color;
using CadColorMethod = GrxCAD.Colors.ColorMethod;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using CadColor = ZwSoft.ZwCAD.Colors.Color;
using CadColorMethod = ZwSoft.ZwCAD.Colors.ColorMethod;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
[CommandMethod("CT_CENTERLINE")]
        public void DrawCenterLine()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            string ltName = "Continuous";
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var lt = (LinetypeTable)tr.GetObject(Db.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has("CENTER")) { ltName = "CENTER"; }
                else
                {
                    try
                    {
#if AUTOCAD
                        Db.LoadLineTypeFile("CENTER", "acad.lin");
#elif ZWCAD
                        Db.LoadLineTypeFile("CENTER", "zwcad.lin");
#elif GSTARCAD
                        Db.LoadLineTypeFile("CENTER", "gcad.lin");
#endif
                        ltName = "CENTER";
                    }
                    catch { Ed.WriteMessage("\n\u8b66\u544a\uff1a\u65e0\u6cd5\u52a0\u8f7d CENTER \u7ebf\u578b\uff0c\u5c06\u4f7f\u7528 Continuous\u3002"); }
                }
                tr.Commit();
            }
            int count = 0;
            double extRatio = 0.25;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (ObjectId id in selectedIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    Point3d center = Point3d.Origin;
                    double halfX = 0, halfY = 0;
                    bool ok = false;
                    if (ent is Circle)
                    {
                        var ci = (Circle)ent;
                        center = ci.Center;
                        double r = ci.Radius;
                        double ext = r * extRatio;
                        halfX = r + ext; halfY = r + ext;
                        ok = true;
                    }
                    else if (ent is Polyline)
                    {
                        var pl = (Polyline)ent;
                        if (pl.Closed && pl.NumberOfVertices == 4)
                        {
                            double mnx = double.MaxValue, mny = double.MaxValue;
                            double mxx = double.MinValue, mxy = double.MinValue;
                            for (int i = 0; i < 4; i++)
                            {
                                Point3d pt = pl.GetPoint3dAt(i);
                                if (pt.X < mnx) mnx = pt.X; if (pt.Y < mny) mny = pt.Y;
                                if (pt.X > mxx) mxx = pt.X; if (pt.Y > mxy) mxy = pt.Y;
                            }
                            center = new Point3d((mnx + mxx) / 2.0, (mny + mxy) / 2.0, 0);
                            halfX = (mxx - mnx) / 2.0 * (1.0 + extRatio);
                            halfY = (mxy - mny) / 2.0 * (1.0 + extRatio);
                            ok = true;
                        }
                    }
                    else if (ent is Hatch || ent is Solid3d)
                    {
                        Extents3d ext3d;
                        var e2 = (Entity)ent; try { ext3d = e2.GeometricExtents; } catch (System.Exception ex) { Log("CenterLine extents skipped: " + ex.Message); continue; }
                        center = new Point3d((ext3d.MinPoint.X + ext3d.MaxPoint.X) / 2.0, (ext3d.MinPoint.Y + ext3d.MaxPoint.Y) / 2.0, 0);
                        halfX = (ext3d.MaxPoint.X - ext3d.MinPoint.X) / 2.0 * (1.0 + extRatio);
                        halfY = (ext3d.MaxPoint.Y - ext3d.MinPoint.Y) / 2.0 * (1.0 + extRatio);
                        ok = true;
                    }
                    if (!ok) continue;
                    var l1 = new Line(new Point3d(center.X - halfX, center.Y, 0), new Point3d(center.X + halfX, center.Y, 0));
                    l1.Layer = "0"; l1.ColorIndex = 1;
                    try { l1.Linetype = ltName; } catch (System.Exception ex) { Log("Set centerline horizontal linetype failed: " + ex.Message); }
                    l1.LinetypeScale = 1.0;
                    msBtr.AppendEntity(l1); tr.AddNewlyCreatedDBObject(l1, true);
                    var l2 = new Line(new Point3d(center.X, center.Y - halfY, 0), new Point3d(center.X, center.Y + halfY, 0));
                    l2.Layer = "0"; l2.ColorIndex = 1;
                    try { l2.Linetype = ltName; } catch (System.Exception ex) { Log("Set centerline vertical linetype failed: " + ex.Message); }
                    l2.LinetypeScale = 1.0;
                    msBtr.AppendEntity(l2); tr.AddNewlyCreatedDBObject(l2, true);
                    count++;
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u4e3a {0} \u4e2a\u5bf9\u8c61\u7ed8\u5236\u4e2d\u5fc3\u7ebf\u3002", count));
        }

[CommandMethod("CT_QUICKDIM")]
        public void QuickDim()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    try
                    {
                        Extents3d ext = ent.GeometricExtents;
                        if (ext.MinPoint.X < minX) minX = ext.MinPoint.X;
                        if (ext.MinPoint.Y < minY) minY = ext.MinPoint.Y;
                        if (ext.MaxPoint.X > maxX) maxX = ext.MaxPoint.X;
                        if (ext.MaxPoint.Y > maxY) maxY = ext.MaxPoint.Y;
                    }
                    catch (System.Exception ex) { Log("Freeze non-isolated layer failed: " + ex.Message); }
                }
                tr.Commit();
            }
            if (minX >= maxX || minY >= maxY) { Ed.WriteMessage("\n\u65e0\u6cd5\u8ba1\u7b97\u5305\u56f4\u76d2\u3002"); return; }
            double offset = (maxY - minY) * 0.15;
            if (offset < 5) offset = 5;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                var dimH = new AlignedDimension();
                dimH.XLine1Point = new Point3d(minX, minY - offset, 0);
                dimH.XLine2Point = new Point3d(maxX, minY - offset, 0);
                dimH.DimLinePoint = new Point3d((minX + maxX) / 2.0, minY - offset * 2, 0);
                dimH.DimensionStyle = Db.Dimstyle;
                msBtr.AppendEntity(dimH); tr.AddNewlyCreatedDBObject(dimH, true);
                var dimV = new AlignedDimension();
                dimV.XLine1Point = new Point3d(maxX + offset, minY, 0);
                dimV.XLine2Point = new Point3d(maxX + offset, maxY, 0);
                dimV.DimLinePoint = new Point3d(maxX + offset * 2, (minY + maxY) / 2.0, 0);
                dimV.DimensionStyle = Db.Dimstyle;
                msBtr.AppendEntity(dimV); tr.AddNewlyCreatedDBObject(dimV, true);
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u521b\u5efa\u5feb\u901f\u6807\u6ce8: {0:F1} x {1:F1}", maxX - minX, maxY - minY));
        }

[CommandMethod("CT_INCCOPY")]
        public void IncCopy()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            string baseText = null;
            Point3d anchor = Point3d.Origin;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    if (ent is DBText) { baseText = ((DBText)ent).TextString; anchor = ((DBText)ent).Position; break; }
                    else if (ent is MText) { baseText = ((MText)ent).Contents; anchor = ((MText)ent).Location; break; }
                }
                tr.Commit();
            }
            if (baseText == null) { Ed.WriteMessage("\n\u9009\u62e9\u7684\u5bf9\u8c61\u4e2d\u6ca1\u6709\u6587\u5b57\u3002"); return; }
            int numEnd = baseText.Length;
            int numStart = numEnd;
            while (numStart > 0 && char.IsDigit(baseText[numStart - 1])) numStart--;
            string prefix = baseText.Substring(0, numStart);
            int num = 0;
            if (numStart < numEnd) int.TryParse(baseText.Substring(numStart), out num);
            int numLen = numEnd - numStart;
            if (numLen == 0) { numLen = 1; num = 1; }
            int copyCount = 0;
            while (true)
            {
                string curText = prefix + num.ToString().PadLeft(numLen, '0');
                var ppr = Ed.GetPoint(string.Format("\n\u6307\u5b9a\u590d\u5236\u57fa\u70b9\uff08\u5f53\u524d: {0} \uff0c\u56de\u8f66\u7ed3\u675f\uff09\uff1a", curText));
                if (ppr.Status != PromptStatus.OK) break;
                num++;
                string newText = prefix + num.ToString().PadLeft(numLen, '0');
                Point3d worldPoint = GetPointInWorld(ppr.Value);
                double dx = worldPoint.X - anchor.X;
                double dy = worldPoint.Y - anchor.Y;
                var transform = Matrix3d.Displacement(new Vector3d(dx, dy, 0));
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var msBtr = (BlockTableRecord)tr.GetObject(Db.CurrentSpaceId, OpenMode.ForWrite);
                    foreach (ObjectId id in selectedIds)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;
                        var clone = (Entity)ent.Clone();
                        clone.TransformBy(transform);
                        msBtr.AppendEntity(clone);
                        tr.AddNewlyCreatedDBObject(clone, true);
                        if (clone is DBText) ((DBText)clone).TextString = newText;
                        else if (clone is MText) ((MText)clone).Contents = newText;
                    }
                    tr.Commit();
                }
                copyCount++;
            }
            Ed.WriteMessage(string.Format("\n\u5df2\u9012\u589e\u590d\u5236 {0} \u6b21\u3002", copyCount));
        }

[CommandMethod("CT_FLATTEN")]
        public void FlattenZ()
        {
            ObjectId[] selectedIds = GetSelectionOrAbort();
            if (selectedIds == null) return;
            int count = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    if (ent is Line)
                    {
                        var ln = (Line)ent;
                        ln.UpgradeOpen();
                        ln.StartPoint = new Point3d(ln.StartPoint.X, ln.StartPoint.Y, 0);
                        ln.EndPoint = new Point3d(ln.EndPoint.X, ln.EndPoint.Y, 0);
                        count++;
                    }
                    else if (ent is Circle)
                    {
                        var ci = (Circle)ent;
                        ci.UpgradeOpen();
                        ci.Center = new Point3d(ci.Center.X, ci.Center.Y, 0);
                        count++;
                    }
                    else if (ent is Arc)
                    {
                        var ar = (Arc)ent;
                        ar.UpgradeOpen();
                        ar.Center = new Point3d(ar.Center.X, ar.Center.Y, 0);
                        count++;
                    }
                    else if (ent is Polyline)
                    {
                        var pl = (Polyline)ent;
                        pl.UpgradeOpen();
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var pt = pl.GetPoint3dAt(i);
                            pl.SetPointAt(i, new Point2d(pt.X, pt.Y));
                        }
                        pl.Elevation = 0;
                        count++;
                    }
                    else if (ent is Polyline2d)
                    {
                        var pl2 = (Polyline2d)ent;
                        pl2.UpgradeOpen();
                        pl2.Elevation = 0;
                        count++;
                    }
                    else if (ent is Polyline3d)
                    {
                        var pl3 = (Polyline3d)ent;
                        pl3.UpgradeOpen();
                        foreach (ObjectId vId in pl3)
                        {
                            var v = tr.GetObject(vId, OpenMode.ForWrite) as PolylineVertex3d;
                            if (v != null)
                                v.Position = new Point3d(v.Position.X, v.Position.Y, 0);
                        }
                        count++;
                    }
                    else if (ent is DBText)
                    {
                        var dt = (DBText)ent;
                        dt.UpgradeOpen();
                        dt.Position = new Point3d(dt.Position.X, dt.Position.Y, 0);
                        count++;
                    }
                    else if (ent is MText)
                    {
                        var mt = (MText)ent;
                        mt.UpgradeOpen();
                        mt.Location = new Point3d(mt.Location.X, mt.Location.Y, 0);
                        count++;
                    }
                    else if (ent is BlockReference)
                    {
                        var br = (BlockReference)ent;
                        br.UpgradeOpen();
                        br.Position = new Point3d(br.Position.X, br.Position.Y, 0);
                        count++;
                    }
                    else if (ent is Dimension)
                    {
                        var dim = (Dimension)ent;
                        dim.UpgradeOpen();
                        dim.TextPosition = new Point3d(dim.TextPosition.X, dim.TextPosition.Y, 0);
                        count++;
                    }
                    else if (ent is Spline)
                    {
                        var sp = (Spline)ent;
                        sp.UpgradeOpen();
                        var pts = new Point3dCollection();
                        for (int i = 0; i < sp.NumControlPoints; i++)
                        {
                            var cp = sp.GetControlPointAt(i);
                            pts.Add(new Point3d(cp.X, cp.Y, 0));
                        }
                        for (int i = 0; i < sp.NumControlPoints; i++)
                            sp.SetControlPointAt(i, pts[i]);
                        count++;
                    }
                    else
                    {
                        try
                        {
                            var ext = ent.GeometricExtents;
                            if (ext.MinPoint.Z != 0 || ext.MaxPoint.Z != 0)
                            {
                                double dz = 0 - (ext.MinPoint.Z + ext.MaxPoint.Z) / 2.0;
                                var xf = Matrix3d.Displacement(new Vector3d(0, 0, dz));
                                ent.UpgradeOpen();
                                ent.TransformBy(xf);
                                count++;
                            }
                        }
                    catch (System.Exception ex) { Log("QuickDim extents skipped: " + ex.Message); }
                    }
                }
                tr.Commit();
            }
            Ed.WriteMessage(string.Format("\n已将 {0} 个对象 Z 轴归零。", count));
        }
    }
}



