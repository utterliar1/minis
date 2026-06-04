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
    public interface IExtensionApplication { void Initialize(); void Terminate(); }
}
namespace Autodesk.AutoCAD.ApplicationServices
{
    public static class Application
    {
        public static DocumentManager DocumentManager { get; set; }
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
        public Database(bool build, bool noInit) {}
        public ObjectId BlockTableId { get; set; }
        public ObjectId CurrentSpaceId { get; set; }
        public ObjectId LayerTableId { get; set; }
        public TransactionManager TransactionManager { get; set; }
        public void DeepCloneObjects(ObjectIdCollection ids, ObjectId owner, IdMapping mapping, bool b) {}
        public void Dispose() {}
    }
    public class TransactionManager { public Transaction StartTransaction() { return new Transaction(); } }
    public class Transaction : IDisposable
    {
        public DBObject GetObject(ObjectId id, OpenMode mode) { return null; }
        public void Commit() {}
        public void AddNewlyCreatedDBObject(DBObject obj, bool b) {}
        public void Dispose() {}
    }
    public enum OpenMode { ForRead, ForWrite }
    public class DBObject { public ObjectId ObjectId { get; set; } public ObjectId Id { get { return ObjectId; } } public void Erase() {} public void UpgradeOpen() {} }
    public class ObjectId
    {
        public bool IsNull { get; set; }
        public override bool Equals(object obj) { return obj is ObjectId; }
        public override int GetHashCode() { return 0; }
        public static bool operator ==(ObjectId a, ObjectId b) => false;
        public static bool operator !=(ObjectId a, ObjectId b) => true;
    }
    public class ObjectIdCollection : IEnumerable<ObjectId>
    {
        public ObjectIdCollection() {}
        public ObjectIdCollection(ObjectId[] ids) {}
        public ObjectId[] GetObjectIds() { return new ObjectId[0]; }
        public int Count { get; set; }
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class IdMapping {}
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
        public void AppendEntity(Entity ent) {}
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class Entity : DBObject
    {
        public string Layer { get; set; }
        public System.Drawing.Color Color { get; set; }
        public void UpgradeOpen() {}
        public void Erase() {}
    }
    public class BlockReference : Entity
    {
        public BlockReference(Geometry.Point3d pt, ObjectId id) {}
        public ObjectId BlockTableRecord { get; set; }
        public AttributeCollection AttributeCollection { get; set; }
    }
    public class DBText : Entity
    {
        public string TextString { get; set; }
        public Geometry.Point3d Position { get; set; }
        public double Height { get; set; }
        public double WidthFactor { get; set; }
        public double Rotation { get; set; }
    }
    public class MText : Entity
    {
        public string Contents { get; set; }
        public Geometry.Point3d Location { get; set; }
        public double TextHeight { get; set; }
        public double Rotation { get; set; }
    }
    public class AttributeReference : Entity
    {
        public string TextString { get; set; }
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
        public IEnumerator<ObjectId> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() { yield break; }
    }
    public class LayerTableRecord : DBObject
    {
        public string Name { get; set; }
    }
}
namespace Autodesk.AutoCAD.EditorInput
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
namespace Autodesk.AutoCAD.Geometry
{
    public struct Point3d { public Point3d(double x, double y, double z) {} public double X { get; set; } public double Y { get; set; } public double Z { get; set; } }
}
