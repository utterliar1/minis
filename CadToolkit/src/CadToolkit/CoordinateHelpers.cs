using System;

#if AUTOCAD
using Autodesk.AutoCAD.Geometry;
#elif GSTARCAD
using GrxCAD.Geometry;
#elif ZWCAD
using ZwSoft.ZwCAD.Geometry;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        static Point3d GetPointInWorld(Point3d point)
        {
            try
            {
                return ConvertPointFromUcsToWcs(point, GetCurrentUserCoordinateSystem());
            }
            catch (Exception ex)
            {
                Log("Transform UCS point to world failed: " + ex.Message);
                return point;
            }
        }

        static Matrix3d GetCurrentUserCoordinateSystem()
        {
            try
            {
                var editor = Ed;
                var property = editor.GetType().GetProperty("CurrentUserCoordinateSystem");
                if (property != null)
                {
                    object value = property.GetValue(editor, null);
                    if (value is Matrix3d)
                        return (Matrix3d)value;
                }
            }
            catch (Exception ex)
            {
                Log("Read editor UCS failed: " + ex.Message);
            }

            return Matrix3d.Identity;
        }

        static Point3d ConvertPointFromUcsToWcs(Point3d point, Matrix3d ucsMatrix)
        {
            try
            {
                return point.TransformBy(ucsMatrix);
            }
            catch (Exception ex)
            {
                Log("Transform UCS point by matrix failed: " + ex.Message);
            }

            try
            {
                object ucsOrg = null;
                object ucsXDir = null;
                object ucsYDir = null;
                var getSystemVariable = Db.GetType().GetMethod("GetSystemVariable");
                if (getSystemVariable != null)
                {
                    ucsOrg = getSystemVariable.Invoke(Db, new object[] { "UCSORG" });
                    ucsXDir = getSystemVariable.Invoke(Db, new object[] { "UCSXDIR" });
                    ucsYDir = getSystemVariable.Invoke(Db, new object[] { "UCSYDIR" });
                }

                if (ucsOrg is Point3d && ucsXDir is Vector3d && ucsYDir is Vector3d)
                {
                    var origin = (Point3d)ucsOrg;
                    var xAxis = (Vector3d)ucsXDir;
                    var yAxis = (Vector3d)ucsYDir;
                    return new Point3d(
                        origin.X + point.X * xAxis.X + point.Y * yAxis.X,
                        origin.Y + point.X * xAxis.Y + point.Y * yAxis.Y,
                        origin.Z + point.X * xAxis.Z + point.Y * yAxis.Z);
                }
            }
            catch (Exception ex)
            {
                Log("Convert UCS point with system variables failed: " + ex.Message);
            }

            return point;
        }
    }
}
