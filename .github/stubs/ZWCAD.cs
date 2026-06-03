// ZWCAD API stubs for CI compilation
using System;
using System.Collections;
using System.Collections.Generic;

namespace ZwSoft.ZwCAD.Runtime
{
    [AttributeUsage(AttributeTargets.Method)] public class CommandMethodAttribute : Attribute { public CommandMethodAttribute(string name) {} public CommandMethodAttribute(string name, CommandFlags flags) {} }
    [Flags] public enum CommandFlags { Session = 1 }
    public interface IExtensionApplication { void Initialize(); void Terminate(); }
}
namespace ZwSoft.ZwCAD.ApplicationServices
{
    public static class Application
    {
        public static DocumentManager DocumentManager { get; set; }
        public static void ShowModalDialog(System.Windows.Forms.Form form) {}
    }
    public class DocumentManager { public Document MdiActiveDocument { get; set; } }
    public class Document
    {
        public Database Database { get; set; }
        public EditorInput.Editor Editor { get; set; }
        public DocumentLock LockDocument() { return new DocumentLock(); }
    }
    public class DocumentLock : IDisposable { public void Dispose() {} }
}
namespace ZwSoft.ZwCAD.DatabaseServices
{
    public class Database : IDisposable
    {
        public Database(bool build, bool noInit) {}
        public ObjectId BlockTableId { get; set; }
        public TransactionManager TransactionManager { get; set; }
        public void ReadDwgFile(string file, FileOpenMode mode, bool allow, string pwd) {}
        public void Insert(string name, Database db, bool b) {}
        public Database Wblock(ObjectId id) { return new Database(false, true); }
        public Database Wblock(ObjectIdCollection ids, Geometry.Point3d pt) { return new Database(false, true); }
        public void SaveAs(string path, DwgVersion ver) {}
        public void Dispose() {}
    }
    public enum FileOpenMode { OpenForReadAndAllShare }
    public enum DwgVersion { Current }
    public class TransactionManager { public Transaction StartTransaction() { return new Transaction(); } }
    public class Transaction : IDisposable
    {
        public DBObject GetObject(ObjectId id, OpenMode mode) { return null; }
        public void Commit() {}
        public void AddNewlyCreatedDBObject(DBObject obj, bool b) {}
        public void Dispose() {}
    }
    public enum OpenMode { ForRead, ForWrite }
    public class DBObject { public ObjectId ObjectId { get; set; } }
    public class ObjectId
    {
        public bool IsNull { get; set; }
        public bool Equals(ObjectId other) { return false; }
        public override bool Equals(object obj) { return obj is ObjectId o && Equals(o); }
        public override int GetHashCode() { return 0; }
        public static bool operator ==(ObjectId a, ObjectId b) => false;
        public static bool operator !=(ObjectId a, ObjectId b) => true;
    }
    public class ObjectIdCollection : IEnumerable<ObjectId>
    {
        public ObjectIdCollection() {}
        public ObjectIdCollection(ObjectId[] ids) {}
        public ObjectId[] GetObjectIds() { return new ObjectId[0]; }
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class BlockTable : DBObject, IEnumerable<ObjectId>
    {
        public bool Has(string name) { return false; }
        public ObjectId this[string name] => new ObjectId();
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class BlockTableRecord : DBObject, IEnumerable<ObjectId>
    {
        public string Name { get; set; }
        public bool IsLayout { get; set; }
        public bool HasPreviewIcon { get; set; }
        public System.Drawing.Bitmap PreviewIcon { get; set; }
        public static readonly string ModelSpace = "*Model_Space";
        public void AppendEntity(Entity ent) {}
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class Entity : DBObject
    {
        public Geometry.Extents3d GeometricExtents { get; set; }
    }
    public class BlockReference : Entity
    {
        public BlockReference(Geometry.Point3d pt, ObjectId id) {}
        public ObjectId BlockTableRecord { get; set; }
        public Geometry.Matrix3d BlockTransform { get; set; }
        public Geometry.Scale3d ScaleFactors { get; set; }
        public double Rotation { get; set; }
    }
    public class Line : Entity { public Geometry.Point3d StartPoint { get; set; } public Geometry.Point3d EndPoint { get; set; } }
    public class Circle : Entity { public Geometry.Point3d Center { get; set; } public double Radius { get; set; } }
    public class Arc : Entity { public Geometry.Point3d Center { get; set; } public double Radius { get; set; } public double StartAngle { get; set; } public double EndAngle { get; set; } }
    public class Polyline : Entity
    {
        public int NumberOfVertices { get; set; }
        public bool Closed { get; set; }
        public Geometry.Point3d GetPoint3dAt(int i) => new Geometry.Point3d();
    }
    public class Spline : Entity { public Entity ToPolyline() { return null; } }
    public class Ellipse : Entity { public Geometry.Point3d Center { get; set; } public Geometry.Vector3d MajorAxis { get; set; } public Geometry.Vector3d MinorAxis { get; set; } }
    public class DBPoint : Entity { public Geometry.Point3d Position { get; set; } }
}
namespace ZwSoft.ZwCAD.EditorInput
{
    public class Editor
    {
        public PromptPointResult GetPoint(string msg) { return new PromptPointResult(); }
        public PromptSelectionResult GetSelection() { return new PromptSelectionResult(); }
        public void WriteMessage(string msg) {}
    }
    public enum PromptStatus { OK, Cancel, None, Error }
    public class PromptPointResult { public PromptStatus Status { get; set; } public Geometry.Point3d Value { get; set; } }
    public class PromptSelectionResult { public PromptStatus Status { get; set; } public SelectionSet Value { get; set; } }
    public class SelectionSet { public DatabaseServices.ObjectId[] GetObjectIds() { return new DatabaseServices.ObjectId[0]; } }
}
namespace ZwSoft.ZwCAD.Geometry
{
    public struct Point3d { public Point3d(double x, double y, double z) {} public double X { get; set; } public double Y { get; set; } public double Z { get; set; } public Point3d TransformBy(Matrix3d m) => this; }
    public struct Vector3d { public double Length { get; set; } }
    public struct Matrix3d { public static Matrix3d Identity => new Matrix3d(); public static Matrix3d operator *(Matrix3d a, Matrix3d b) => a; }
    public struct Scale3d { public Scale3d(double x, double y, double z) {} }
    public struct Extents3d { public Point3d MinPoint { get; set; } public Point3d MaxPoint { get; set; } }
}
