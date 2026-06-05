// AutoCAD API stubs for CI compilation
using System;
using System.Collections;
using System.Collections.Generic;

namespace Autodesk.AutoCAD.Runtime
{
    [AttributeUsage(AttributeTargets.Method)] public class CommandMethodAttribute : Attribute { public CommandMethodAttribute(string name) {} public CommandMethodAttribute(string name, CommandFlags flags) {} }
    [AttributeUsage(AttributeTargets.Assembly)] public class CommandClassAttribute : Attribute { public CommandClassAttribute(Type t) {} }
    [AttributeUsage(AttributeTargets.Assembly)] public class ExtensionApplicationAttribute : Attribute { public ExtensionApplicationAttribute(Type t) {} }
    [Flags] public enum CommandFlags { Session = 1 }
    
}
namespace Autodesk.AutoCAD.ApplicationServices
{
    public static class Application
    {
        public static DocumentManager DocumentManager { get; set; }
        public static event System.EventHandler Idle;
        public static void ShowModalDialog(System.Windows.Forms.Form form) {}
    }
    public class DocumentManager { public Document MdiActiveDocument { get; set; } }
    public class Document
    {
        public DatabaseServices.Database Database { get; set; }
        public EditorInput.Editor Editor { get; set; }
        public void SendStringToExecute(string cmd, bool b1, bool b2, bool b3) {}
    }
}
namespace Autodesk.AutoCAD.DatabaseServices
{
    public class Database : IDisposable
    {
        public ObjectId BlockTableId { get; set; }
        public ObjectId CurrentSpaceId { get; set; }
        public ObjectId LayerTableId { get; set; }
        public ObjectId LinetypeTableId { get; set; }
        public ObjectId TextStyleTableId { get; set; }
        public ObjectId DimstyleTableId { get; set; }
        public TransactionManager TransactionManager { get; set; }
        public ObjectId Dimstyle { get; set; }
        public ObjectId Textstyle { get; set; }
        public ObjectId Clayer { get; set; }
        public ObjectId Celtype { get; set; }
        public LayerTable Layers { get; set; }
        public LinetypeTable Linetypes { get; set; }
        public void LoadLineTypeFile(string name, string file) {}
        public void DeepCloneObjects(ObjectIdCollection ids, ObjectId owner, IdMapping mapping, bool b) {}
        public void Wblock(Database dest, ObjectIdCollection ids, Geometry.Point3d pt, DuplicateRecordCloning mode) {}
        public void Insert(string name, Database source, bool b) {}
        public void Dispose() {}
    }
    public enum DuplicateRecordCloning { Replace, MangleExisting, Ignore, UnmangleName, UseCloneCallback }
    public class TransactionManager { public Transaction StartTransaction() { return new Transaction(); } }
    public class Transaction : IDisposable
    {
        public DBObject GetObject(ObjectId id, OpenMode mode) { return null; }
        public DBObject GetObject(ObjectId id, OpenMode mode, bool openErased) { return null; }
        public void Commit() {}
        public void AddNewlyCreatedDBObject(DBObject obj, bool b) {}
        public void Dispose() {}
    }
    public enum OpenMode { ForRead, ForWrite }
    public class DBObject
    {
        public ObjectId ObjectId { get; set; }
        public ObjectId Id { get { return ObjectId; } }
        public ObjectId OwnerId { get; set; }
        public void Erase() {}
        public void UpgradeOpen() {}
    }
    public class ObjectId
    {
        public override bool Equals(object obj) { return obj is ObjectId; }
        public override int GetHashCode() { return 0; }
        public static bool operator ==(ObjectId a, ObjectId b) => false;
        public static bool operator !=(ObjectId a, ObjectId b) => true;
        public bool IsNull { get; set; }
        public bool IsValid { get; set; }
    }
    public class ObjectIdCollection : IEnumerable<ObjectId>
    {
        public ObjectIdCollection() {}
        public ObjectIdCollection(ObjectId[] ids) {}
        public ObjectId[] GetObjectIds() { return new ObjectId[0]; }
        public int Count { get; set; }
        public void Add(ObjectId id) {}
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class IdMapping
    {
        public bool Contains(ObjectId id) { return false; }
        public IdPair this[ObjectId id] { get { return new IdPair(); } }
        public IEnumerator GetEnumerator() { yield break; }
    }
    public struct IdPair
    {
        public ObjectId Key { get; set; }
        public ObjectId Value { get; set; }
    }
    public class BlockTable : DBObject, IEnumerable<ObjectId>
    {
        public void Add(BlockTableRecord btr) {}
        public bool Has(string name) { return false; }
        public ObjectId this[string name] => new ObjectId();
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class BlockTableRecord : DBObject, IEnumerable<ObjectId>
    {
        public string Name { get; set; }
        public Geometry.Point3d Origin { get; set; }
        public bool IsFromExternalReference { get; set; }
        public void AppendEntity(Entity ent) {}
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class Entity : DBObject
    {
        public string Layer { get; set; }
        public System.Drawing.Color Color { get; set; }
        public int ColorIndex { get; set; }
        public string Linetype { get; set; }
        public ObjectId LayerId { get; set; }
        public ObjectId LinetypeId { get; set; }
        public ObjectId TextStyleId { get; set; }
        public Geometry.Extents3d GeometricExtents { get; set; }
        public Entity Clone() { return null; }
        public void TransformBy(Geometry.Matrix3d mat) {}
        public void UpgradeOpen() {}
        public void Erase() {}
    }
    public class BlockReference : Entity
    {
        public BlockReference() {}
        public BlockReference(Geometry.Point3d pt, ObjectId id) {}
        public string Name { get; set; }
        public ObjectId BlockTableRecord { get; set; }
        public ObjectId BlockId { get; set; }
        public AttributeCollection AttributeCollection { get; set; }
        public Geometry.Point3d Position { get; set; }
        public Geometry.Scale3d ScaleFactors { get; set; }
        public double Rotation { get; set; }
    }
    public struct Scale3d
    {
        public Scale3d(double uniformScale) {}
        public Scale3d(double sx, double sy, double sz) {}
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public enum TextHorizontalMode { TextLeft, TextCenter, TextRight, TextAlign, TextMid, TextFit }
    public enum TextVerticalMode { TextBase, TextBottom, TextVerticalMid, TextTop }
    public class DBText : Entity
    {
        public string TextString { get; set; }
        public Geometry.Point3d Position { get; set; }
        public double Height { get; set; }
        public double WidthFactor { get; set; }
        public double Rotation { get; set; }
        public TextHorizontalMode HorizontalMode { get; set; }
        public TextVerticalMode VerticalMode { get; set; }
        public Geometry.Point3d AlignmentPoint { get; set; }
    }
    public enum AttachmentPoint { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight, BaseLeft, BaseCenter, BaseRight, BaseAlign, BaseFit, BaseMid, BaseStart, BaseEnd }
    public class MText : Entity
    {
        public string Contents { get; set; }
        public string Text { get; set; }
        public Geometry.Point3d Location { get; set; }
        public double TextHeight { get; set; }
        public double Width { get; set; }
        public double Rotation { get; set; }
        public AttachmentPoint Attachment { get; set; }
    }
    public class AttributeReference : Entity
    {
        public string TextString { get; set; }
        public string Tag { get; set; }
    }
    public class AttributeCollection : IEnumerable<ObjectId>
    {
        public int Count { get; set; }
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class LayerTable : DBObject, IEnumerable<ObjectId>
    {
        public void Add(LayerTableRecord ltr) {}
        public bool Has(string name) { return false; }
        public ObjectId this[string name] => new ObjectId();
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class LayerTableRecord : DBObject
    {
        public string Name { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsOff { get; set; }
        public bool IsLocked { get; set; }
    }
    public class LinetypeTable : DBObject, IEnumerable<ObjectId>
    {
        public bool Has(string name) { return false; }
        public ObjectId this[string name] => new ObjectId();
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class TextStyleTable : DBObject, IEnumerable<ObjectId>
    {
        public bool Has(string name) { return false; }
        public ObjectId this[string name] => new ObjectId();
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class TextStyleTableRecord : DBObject
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string BigFontFileName { get; set; }
    }
    public class Circle : Entity
    {
        public Circle() {}
        public Circle(Geometry.Point3d center, Geometry.Vector3d normal, double radius) {}
        public Geometry.Point3d Center { get; set; }
        public double Radius { get; set; }
    }
    public class Line : Entity
    {
        public Line() {}
        public Line(Geometry.Point3d start, Geometry.Point3d end) {}
        public Geometry.Point3d StartPoint { get; set; }
        public Geometry.Point3d EndPoint { get; set; }
    }
    public class Polyline : Entity
    {
        public Polyline() {}
        public int NumberOfVertices { get; }
        public Geometry.Point3d GetPoint3dAt(int index) { return new Geometry.Point3d(); }
        public void AddVertexAt(int index, Geometry.Point2d pt, double bulge, double startWidth, double endWidth) {}
        public void SetDatabaseDefaults() {}
    }
    public class Solid3d : Entity
    {
        public void CreateBox(double x, double y, double z) {}
    }
    public class AlignedDimension : Entity
    {
        public AlignedDimension() {}
        public Geometry.Point3d XLine1Point { get; set; }
        public Geometry.Point3d XLine2Point { get; set; }
        public Geometry.Point3d DimLinePoint { get; set; }
        public ObjectId DimensionStyle { get; set; }
    }
}
namespace Autodesk.AutoCAD.EditorInput
{
    public class Editor
    {
        public PromptPointResult GetPoint(string msg) { return new PromptPointResult(); }
        public PromptPointResult GetPoint(PromptPointOptions opts) { return new PromptPointResult(); }
        public PromptSelectionResult GetSelection() { return new PromptSelectionResult(); }
        public PromptSelectionResult GetSelection(SelectionFilter filter) { return new PromptSelectionResult(); }
        public PromptSelectionResult SelectImplied() { return new PromptSelectionResult(); }
        public void SetImpliedSelection(ObjectId[] ids) {}
        public PromptEntityResult GetEntity(string msg) { return new PromptEntityResult(); }
        public PromptEntityResult GetEntity(PromptEntityOptions opts) { return new PromptEntityResult(); }
        public PromptStringResult GetString(string msg) { return new PromptStringResult(); }
        public PromptStringResult GetString(PromptStringOptions opts) { return new PromptStringResult(); }
        public PromptDoubleResult GetDouble(string msg) { return new PromptDoubleResult(); }
        public PromptDoubleResult GetDistance(string msg) { return new PromptDoubleResult(); }
        public void WriteMessage(string msg) {}
        public void WriteMessage(string fmt, params object[] args) {}
    }
    public enum PromptStatus { OK, Cancel, None, Error }
    public class PromptPointOptions
    {
        public string Message { get; set; }
        public bool AllowNone { get; set; }
    }
    public class PromptPointResult { public PromptStatus Status { get; set; } public Geometry.Point3d Value { get; set; } }
    public class PromptEntityOptions
    {
        public PromptEntityOptions(string msg) {}
        public string Message { get; set; }
        public bool AllowNone { get; set; }
    }
    public class PromptEntityResult
    {
        public PromptStatus Status { get; set; }
        public DatabaseServices.ObjectId ObjectId { get; set; }
        public Geometry.Point3d PickPoint { get; set; }
    }
    public class PromptSelectionResult { public PromptStatus Status { get; set; } public SelectionSet Value { get; set; } }
    public class SelectionSet
    {
        public DatabaseServices.ObjectId[] GetObjectIds() { return new DatabaseServices.ObjectId[0]; }
        public int Count { get; set; }
    }
    public class SelectionFilter
    {
        public SelectionFilter(TypedValue[] tv) {}
    }
    public struct TypedValue
    {
        public TypedValue(int code) { Code = code; Value = null; }
        public TypedValue(int code, object value) { Code = code; Value = value; }
        public int Code { get; set; }
        public object Value { get; set; }
    }
    public class PromptStringResult
    {
        public PromptStatus Status { get; set; }
        public string StringResult { get; set; }
    }
    public class PromptStringOptions
    {
        public PromptStringOptions(string msg) {}
        public string Message { get; set; }
        public bool AllowSpaces { get; set; }
        public string DefaultValue { get; set; }
    }
    public class PromptDoubleResult
    {
        public PromptStatus Status { get; set; }
        public double Value { get; set; }
    }
}
namespace Autodesk.AutoCAD.Geometry
{
    public struct Point2d
    {
        public Point2d(double x, double y) {}
        public double X { get; set; }
        public double Y { get; set; }
    }
    public struct Point3d
    {
        public Point3d(double x, double y, double z) {}
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }
    public struct Vector3d
    {
        public Vector3d(double x, double y, double z) {}
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public static Vector3d XAxis { get { return new Vector3d(1,0,0); } }
        public static Vector3d YAxis { get { return new Vector3d(0,1,0); } }
        public static Vector3d ZAxis { get { return new Vector3d(0,0,1); } }
    }
    public struct Matrix3d
    {
        public static Matrix3d Displacement(Vector3d vec) { return new Matrix3d(); }
        public static Matrix3d Identity { get { return new Matrix3d(); } }
    }
    public struct Extents3d
    {
        public Extents3d(Point3d min, Point3d max) {}
        public Point3d MinPoint { get; set; }
        public Point3d MaxPoint { get; set; }
    }
    public struct Scale3d
    {
        public Scale3d(double uniformScale) {}
        public Scale3d(double sx, double sy, double sz) {}
    }
}