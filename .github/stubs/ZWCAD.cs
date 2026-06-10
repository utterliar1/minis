// ZWCAD API stubs for CI compilation
using System;
using System.Collections;
using System.Collections.Generic;

namespace ZwSoft.ZwCAD.Runtime
{
    [AttributeUsage(AttributeTargets.Method)] public class CommandMethodAttribute : Attribute { public CommandMethodAttribute(string name) {} public CommandMethodAttribute(string name, CommandFlags flags) {} }
    [AttributeUsage(AttributeTargets.Assembly)] public class CommandClassAttribute : Attribute { public CommandClassAttribute(Type t) {} }
    [AttributeUsage(AttributeTargets.Assembly)] public class ExtensionApplicationAttribute : Attribute { public ExtensionApplicationAttribute(Type t) {} }
    [Flags] public enum CommandFlags { Session = 1, UsePickSet = 2 }
    public interface IExtensionApplication { void Initialize(); void Terminate(); }
}
namespace ZwSoft.ZwCAD.ApplicationServices
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
        public DocumentLock LockDocument() { return new DocumentLock(); }
    }
    public class DocumentLock : IDisposable { public void Dispose() {} }
}
namespace ZwSoft.ZwCAD.Colors
{
    public enum ColorMethod { ByAci }
    public class Color
    {
        public static Color FromColorIndex(ColorMethod method, short colorIndex) { return new Color(); }
    }
}
namespace ZwSoft.ZwCAD.DatabaseServices
{
    public class Database : IDisposable
    {
        public ObjectId BlockTableId { get; set; }
        public ObjectId CurrentSpaceId { get; set; }
        public ObjectId LayerTableId { get; set; }
        public ObjectId LinetypeTableId { get; set; }
        public ObjectId TextStyleTableId { get; set; }
        public TransactionManager TransactionManager { get; set; }
        public ObjectId Dimstyle { get; set; }
        public ObjectId Textstyle { get; set; }
        public ObjectId Clayer { get; set; }
        public ObjectId Celtype { get; set; }
        public LayerTable Layers { get; set; }
        public LinetypeTable Linetypes { get; set; }
        public void LoadLineTypeFile(string name, string file) {}
        public void DeepCloneObjects(ObjectIdCollection ids, ObjectId owner, IdMapping mapping, bool b) {}
        public ObjectId GetObjectId(bool createIfNotFound, Handle handle, long version) { return new ObjectId(); }
        public Database() {}
        public Database(bool buildDefaultDrawing, bool noDocument) {}
        public void ReadDwgFile(string fileName, FileOpenMode mode, bool allowCPConversion, string password) {}
        public void Insert(string blockName, Database sourceDatabase, bool preserveSourceDatabase) {}
        public Database Wblock(ObjectIdCollection ids, ZwSoft.ZwCAD.Geometry.Point3d basePoint) { return new Database(); }
        public Database Wblock(ObjectId blockId) { return new Database(); }
        public Database Wblock() { return new Database(); }
        public void SaveAs(string fileName, DwgVersion version) {}
        public string Filename { get; set; }
        public void Dispose() {}
    }
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
    public enum LineWeight { ByLayer = -1, ByBlock = -2, ByLineWeightDefault = -3 }
    public enum FileOpenMode { OpenForReadAndAllShare, OpenForReadAndWriteNoShare, OpenForReadAndAllShareWithConversions }
    public enum DwgVersion { Current, AC1015, AC1018, AC1021, AC1024, AC1027, AC1032 }
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
        public bool IsValid { get { return true; } }
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
    public struct Handle
    {
        public Handle(long value) {}
        public long Value { get; set; }
    }
    public struct IdPair { public ObjectId Key { get; set; } public ObjectId Value { get; set; } }
    public class BlockTable : DBObject, IEnumerable<ObjectId>
    {
        public void Add(BlockTableRecord btr) {}
        public bool Has(string name) { return false; }
        public ObjectId this[string name] => new ObjectId();
        public ObjectId this[ObjectId id] => new ObjectId();
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class BlockTableRecord : DBObject, IEnumerable<ObjectId>
    {
        public string Name { get; set; }
        public bool IsLayout { get; set; }
        public bool IsAnonymous { get; set; }
        public bool IsDependent { get; set; }
        public bool IsFromExternalReference { get; set; }
        public bool IsFromOverlayReference { get; set; }
        public bool HasPreviewIcon { get { return false; } }
        public System.Drawing.Bitmap PreviewIcon { get { return null; } }
        public Geometry.Point3d Origin { get; set; }
        public static readonly ObjectId ModelSpace = new ObjectId();
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
        public LineWeight LineWeight { get; set; }
        public Geometry.Extents3d GeometricExtents { get; set; }
        public Entity Clone() { return null; }
        public void TransformBy(Geometry.Matrix3d mat) {}
    }
    public class BlockReference : Entity
    {
        public BlockReference() {}
        public BlockReference(Geometry.Point3d pt, ObjectId id) {}
        public string Name { get; set; }
        public ObjectId BlockTableRecord { get; set; }
        public AttributeCollection AttributeCollection { get; set; }
        public Geometry.Point3d Position { get; set; }
        public Geometry.Scale3d ScaleFactors { get; set; }
        public double Rotation { get; set; }
        public Geometry.Matrix3d BlockTransform { get { return Geometry.Matrix3d.Identity; } }
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
        public string TextStyleName { get; set; }
    }
    public enum AttachmentPoint { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight }
    public class MText : Entity
    {
        public string Contents { get; set; }
        public Geometry.Point3d Location { get; set; }
        public double TextHeight { get; set; }
        public double Width { get; set; }
        public double Rotation { get; set; }
        public AttachmentPoint Attachment { get; set; }
        public string TextStyleName { get; set; }
    }
    public class AttributeReference : DBText { public string Tag { get; set; } }
    public class AttributeDefinition : DBText { public string Tag { get; set; } }
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
        public bool IsDependent { get; set; }
        public ZwSoft.ZwCAD.Colors.Color Color { get; set; }
        public LineWeight LineWeight { get; set; }
        public bool IsPlottable { get; set; }
        public ObjectId LinetypeObjectId { get; set; }
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
        public void Add(TextStyleTableRecord record) {}
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
        public double TextSize { get; set; }
        public double XScale { get; set; }
        public double ObliquingAngle { get; set; }
        public bool IsDependent { get; set; }
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
        public double LinetypeScale { get; set; }
    }
    public class Polyline : Entity
    {
        public Polyline() {}
        public bool Closed { get; set; }
        public int NumberOfVertices { get; }
        public Geometry.Point3d GetPoint3dAt(int index) { return new Geometry.Point3d(); }
        public void SetPointAt(int index, Geometry.Point2d point) {}
        public double Elevation { get; set; }
    }
    public class Polyline2d : Entity { public double Elevation { get; set; } }
    public class Polyline3d : Entity, IEnumerable<ObjectId>
    {
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class PolylineVertex3d : Entity { public Geometry.Point3d Position { get; set; } }
    public class Arc : Entity
    {
        public Geometry.Point3d Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
    }
    public class Spline : Entity
    {
        public Entity ToPolyline() { return null; }
        public int NumControlPoints { get; set; }
        public Geometry.Point3d GetControlPointAt(int index) { return new Geometry.Point3d(); }
        public void SetControlPointAt(int index, Geometry.Point3d point) {}
    }
    public class Ellipse : Entity
    {
        public Geometry.Point3d Center { get; set; }
        public Geometry.Vector3d MajorAxis { get; set; }
        public Geometry.Vector3d MinorAxis { get; set; }
    }
    public class DBPoint : Entity
    {
        public Geometry.Point3d Position { get; set; }
    }
    public class Hatch : Entity { public string PatternName { get; set; } }
    public class Solid3d : Entity { public void CreateBox(double x, double y, double z) {} }
    public class AlignedDimension : Entity
    {
        public AlignedDimension() {}
        public Geometry.Point3d XLine1Point { get; set; }
        public Geometry.Point3d XLine2Point { get; set; }
        public Geometry.Point3d DimLinePoint { get; set; }
        public ObjectId DimensionStyle { get; set; }
    }
    public class Dimension : Entity { public Geometry.Point3d TextPosition { get; set; } }
}
namespace ZwSoft.ZwCAD.EditorInput
{
    public class Editor
    {
        public PromptPointResult GetPoint(string msg) { return new PromptPointResult(); }
        public PromptPointResult GetPoint(PromptPointOptions opts) { return new PromptPointResult(); }
        public PromptSelectionResult GetSelection() { return new PromptSelectionResult(); }
        public PromptSelectionResult GetSelection(SelectionFilter filter) { return new PromptSelectionResult(); }
        public PromptSelectionResult SelectImplied() { return new PromptSelectionResult(); }
        public void SetImpliedSelection(DatabaseServices.ObjectId[] ids) {}
        public PromptSelectionResult GetSelection(PromptSelectionOptions opts) { return new PromptSelectionResult(); }
        public PromptSelectionResult SelectAll(SelectionFilter filter) { return new PromptSelectionResult(); }
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
    public class PromptPointOptions { public string Message { get; set; } public bool AllowNone { get; set; } }
    public class PromptPointResult { public PromptStatus Status { get; set; } public Geometry.Point3d Value { get; set; } }
    public class PromptEntityOptions
    {
        public PromptEntityOptions(string msg) {}
        public string Message { get; set; }
        public bool AllowNone { get; set; }
        public void SetRejectMessage(string msg) {}
        public void AddAllowedClass(Type type, bool exactMatch) {}
    }
    public class PromptEntityResult { public PromptStatus Status { get; set; } public DatabaseServices.ObjectId ObjectId { get; set; } public Geometry.Point3d PickPoint { get; set; } }
    public class PromptSelectionResult { public PromptStatus Status { get; set; } public SelectionSet Value { get; set; } }
    public class PromptSelectionOptions { public string MessageForAdding { get; set; } }
    public class SelectionSet { public DatabaseServices.ObjectId[] GetObjectIds() { return new DatabaseServices.ObjectId[0]; } public int Count { get; set; } }
    public class SelectionFilter { public SelectionFilter(TypedValue[] tv) {} }
    public struct TypedValue { public TypedValue(int code) { Code = code; Value = null; } public TypedValue(int code, object value) { Code = code; Value = value; } public int Code { get; set; } public object Value { get; set; } }
    public class PromptStringResult { public PromptStatus Status { get; set; } public string StringResult { get; set; } }
    public class PromptStringOptions { public PromptStringOptions(string msg) {} public string Message { get; set; } public bool AllowSpaces { get; set; } public string DefaultValue { get; set; } }
    public class PromptDoubleResult { public PromptStatus Status { get; set; } public double Value { get; set; } }
}
namespace ZwSoft.ZwCAD.Geometry
{
    public struct Point2d { public Point2d(double x, double y) {} public double X { get; set; } public double Y { get; set; } }
    public struct Point3d { public Point3d(double x, double y, double z) {} public double X { get; set; } public double Y { get; set; } public double Z { get; set; } public static Point3d Origin { get { return new Point3d(0,0,0); } }
        public Point3d TransformBy(Matrix3d xf) { return this; } }
    public class Point3dCollection : List<Point3d> {}
    public struct Vector3d { public Vector3d(double x, double y, double z) {} public double X { get; set; } public double Y { get; set; } public double Z { get; set; } public static Vector3d XAxis { get { return new Vector3d(1,0,0); } } public static Vector3d YAxis { get { return new Vector3d(0,1,0); } } public static Vector3d ZAxis { get { return new Vector3d(0,0,1); } }
        public double Length { get { return 0; } } }
    public struct Matrix3d { public static Matrix3d Displacement(Vector3d vec) { return new Matrix3d(); } public static Matrix3d Identity { get { return new Matrix3d(); } }
        public static Matrix3d operator *(Matrix3d a, Matrix3d b) { return new Matrix3d(); } }
    public struct Extents3d { public Extents3d(Point3d min, Point3d max) {} public Point3d MinPoint { get; set; } public Point3d MaxPoint { get; set; } }
    public struct Scale3d { public Scale3d(double uniformScale) {} public Scale3d(double sx, double sy, double sz) {} }
}
