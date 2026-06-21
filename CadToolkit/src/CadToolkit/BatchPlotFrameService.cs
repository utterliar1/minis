using System;
using System.Collections.Generic;

#if AUTOCAD
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
#elif GSTARCAD
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
#elif ZWCAD
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        static ObjectId[] CollectBatchPlotFrameBlockIds(BatchPlotFrameBlockKey frameBlockKey)
        {
            var pso = new PromptSelectionOptions();
            pso.MessageForAdding = "\n框选要打印的范围：";
            var filter = new TypedValue[] { new TypedValue(0, "INSERT") };
            var sf = new SelectionFilter(filter);
            var psr = Ed.GetSelection(pso, sf);
            if (psr.Status == PromptStatus.Cancel) return null;
            if (psr.Status != PromptStatus.OK || psr.Value == null) return new ObjectId[0];

            var ids = new List<ObjectId>();
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in psr.Value.GetObjectIds())
                {
                    try
                    {
                        if (IsBatchPlotSameFrameBlock(id, frameBlockKey, tr))
                            ids.Add(id);
                    }
                    catch (System.Exception ex)
                    {
                        Log("BatchPlot skipped block candidate: " + ex.Message);
                    }
                }
                tr.Commit();
            }
            return ids.ToArray();
        }

        static bool IsBatchPlotSameFrameBlock(ObjectId blockReferenceId, BatchPlotFrameBlockKey frameBlockKey, Transaction tr)
        {
            if (frameBlockKey == null) return false;
            BatchPlotFrameBlockKey candidate = GetBatchPlotFrameBlockKey(blockReferenceId, tr);
            if (candidate == null) return false;
            if (!candidate.DefinitionId.IsNull && !frameBlockKey.DefinitionId.IsNull)
                return candidate.DefinitionId == frameBlockKey.DefinitionId;
            return SafeStr(candidate.Name).Equals(SafeStr(frameBlockKey.Name), StringComparison.OrdinalIgnoreCase);
        }

        static BatchPlotFrameBlockKey GetBatchPlotFrameBlockKey(ObjectId blockReferenceId, Transaction tr)
        {
            var br = tr.GetObject(blockReferenceId, OpenMode.ForRead) as BlockReference;
            if (br == null) return null;

            ObjectId definitionId = ObjectId.Null;
            try
            {
                ObjectId dynamicId = br.DynamicBlockTableRecord;
                if (!dynamicId.IsNull) definitionId = dynamicId;
            }
            catch { }
            if (definitionId.IsNull) definitionId = br.BlockTableRecord;
            if (definitionId.IsNull) return null;

            var btr = tr.GetObject(definitionId, OpenMode.ForRead) as BlockTableRecord;
            string name = btr == null ? "" : btr.Name;
            if (string.IsNullOrEmpty(name)) name = Convert.ToString(definitionId);

            return new BatchPlotFrameBlockKey
            {
                DefinitionId = definitionId,
                Name = name,
                DisplayName = name
            };
        }

        static List<BatchPlotFrame> CollectPlotFrames(ObjectId[] selectedIds)
        {
            var frames = new List<BatchPlotFrame>();
            int selectionOrder = 0;
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selectedIds)
                {
                    try
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;
                        Extents3d ext = ent.GeometricExtents;
                        if (ext.MaxPoint.X <= ext.MinPoint.X || ext.MaxPoint.Y <= ext.MinPoint.Y) continue;

                        var frame = new BatchPlotFrame();
                        frame.Id = id;
                        frame.MinX = ext.MinPoint.X;
                        frame.MinY = ext.MinPoint.Y;
                        frame.MaxX = ext.MaxPoint.X;
                        frame.MaxY = ext.MaxPoint.Y;
                        frame.SelectionOrder = selectionOrder++;
                        ReadBatchPlotTitleBlockAttributes(ent as BlockReference, frame, tr);
                        frames.Add(frame);
                    }
                    catch (System.Exception ex)
                    {
                        Log("BatchPlot skipped frame: " + ex.Message);
                    }
                }
                tr.Commit();
            }
            return frames;
        }

        static void ReadBatchPlotTitleBlockAttributes(BlockReference br, BatchPlotFrame frame, Transaction tr)
        {
            if (br == null || frame == null || tr == null) return;
            try
            {
                foreach (ObjectId attrId in br.AttributeCollection)
                {
                    var ar = tr.GetObject(attrId, OpenMode.ForRead) as AttributeReference;
                    if (ar == null) continue;
                    string tag = SafeStr(ar.Tag).Trim();
                    string value = SafeStr(ar.TextString).Trim();
                    if (string.IsNullOrEmpty(value)) continue;
                    if (string.IsNullOrEmpty(frame.SheetNumber) && IsBatchPlotSheetNumberTag(tag))
                        frame.SheetNumber = value;
                    else if (string.IsNullOrEmpty(frame.SheetName) && IsBatchPlotSheetNameTag(tag))
                        frame.SheetName = value;
                }
            }
            catch (System.Exception ex)
            {
                Log("BatchPlot read title block attributes failed: " + ex.Message);
            }
        }

        static bool IsBatchPlotSheetNumberTag(string tag)
        {
            string normalized = NormalizeBatchPlotAttributeTag(tag);
            return normalized == "图号"
                || normalized == "图纸编号"
                || normalized == "图纸号"
                || normalized == "SHEETNO"
                || normalized == "SHEETNUMBER"
                || normalized == "DRAWINGNO"
                || normalized == "DRAWINGNUMBER"
                || normalized == "DWGNO";
        }

        static bool IsBatchPlotSheetNameTag(string tag)
        {
            string normalized = NormalizeBatchPlotAttributeTag(tag);
            return normalized == "图名"
                || normalized == "图纸名称"
                || normalized == "图纸名"
                || normalized == "SHEETNAME"
                || normalized == "SHEETTITLE"
                || normalized == "DRAWINGNAME"
                || normalized == "DRAWINGTITLE"
                || normalized == "TITLE";
        }

        static string NormalizeBatchPlotAttributeTag(string tag)
        {
            return SafeStr(tag).Replace(" ", "").Replace("_", "").Replace("-", "").Replace("：", "").Replace(":", "").ToUpperInvariant();
        }
    }
}
