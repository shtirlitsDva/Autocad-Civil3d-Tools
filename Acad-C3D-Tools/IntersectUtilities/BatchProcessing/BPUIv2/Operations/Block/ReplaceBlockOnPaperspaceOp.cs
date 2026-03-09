using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using Dreambuild.AutoCAD;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Block;

public class ReplaceBlockOnPaperspaceOp : OperationBase
{
    public override string TypeId => "Block.ReplaceOnPaperspace";
    public override string DisplayName => "Replace Block on Paperspace";
    public override string Description =>
        "Deletes an old block, imports a new block from a library DWG, and places it on all layouts with attributes.";
    public override string Category => "Block";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "oldBlockName",
            "Old Block Name",
            ParameterType.String,
            "Name of the block to replace"),
        new ParameterDescriptor(
            "libraryPath",
            "Library Path",
            ParameterType.String,
            "Full path to the DWG file containing the new block"),
        new ParameterDescriptor(
            "newBlockName",
            "New Block Name",
            ParameterType.String,
            "Name of the new block to import"),
        new ParameterDescriptor(
            "blockX",
            "Block X",
            ParameterType.Int,
            "X coordinate for block placement"),
        new ParameterDescriptor(
            "blockY",
            "Block Y",
            ParameterType.Int,
            "Y coordinate for block placement")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string blockToReplace = GetStringParam(parameterValues, "oldBlockName");
        string pathToBlockLibrary = GetStringParam(parameterValues, "libraryPath");
        string blockReplacement = GetStringParam(parameterValues, "newBlockName");
        int brX = GetIntParam(parameterValues, "blockX");
        int brY = GetIntParam(parameterValues, "blockY");
        var xTx = context.Database.TransactionManager.TopTransaction;
        BlockTable bt = xTx.GetObject(context.Database.BlockTableId, OpenMode.ForRead) as BlockTable;

        #region Delete old block
        using (Transaction dxTx = context.Database.TransactionManager.StartTransaction())
        {
            if (bt.Has(blockToReplace))
            {
                Oid btrId = bt[blockToReplace];
                BlockTableRecord btr = btrId.Go<BlockTableRecord>(dxTx);
                var refIds = btr.GetBlockReferenceIds(true, false);
                var brs = refIds.Entities<BlockReference>(dxTx);
                foreach (var br in brs)
                {
                    br.UpgradeOpen();
                    br.Erase();
                }
                btr.UpgradeOpen();
                btr.Erase();
                dxTx.Commit();
            }
            else dxTx.Abort();
        }
        #endregion

        #region Import new block
        context.Database.CheckOrImportBlockRecord(pathToBlockLibrary, blockReplacement);
        if (!bt.Has(blockReplacement))
            return new Result(
                ResultStatus.FatalError, $"{System.IO.Path.GetFileName(context.Database.Filename)} " +
                $"failed to import {blockReplacement} from {pathToBlockLibrary}!");
        BlockTableRecord newBtr = bt[blockReplacement].Go<BlockTableRecord>(xTx);
        #endregion

        DBDictionary layoutDict = context.Database.LayoutDictionaryId.Go<DBDictionary>(xTx);

        foreach (DBDictionaryEntry item in layoutDict)
        {
            if (item.Key == "Model") continue;
            Layout layout = item.Value.Go<Layout>(xTx);
            BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

            var br = new BlockReference(new Point3d(brX, brY, 0), newBtr.Id);
            layBlock.CheckOrOpenForWrite();
            layBlock.AppendEntity(br);
            xTx.AddNewlyCreatedDBObject(br, true);

            foreach (Oid arOid in newBtr)
            {
                if (arOid.IsDerivedFrom<AttributeDefinition>())
                {
                    AttributeDefinition at = arOid.Go<AttributeDefinition>(xTx);
                    if (!at.Constant)
                    {
                        using (AttributeReference atRef = new AttributeReference())
                        {
                            atRef.SetAttributeFromBlock(at, br.BlockTransform);
                            atRef.Position = at.Position.TransformBy(br.BlockTransform);
                            atRef.TextString = at.getTextWithFieldCodes();
                            br.AttributeCollection.AppendAttribute(atRef);
                            xTx.AddNewlyCreatedDBObject(atRef, true);
                        }
                    }
                }
            }

            br.AttSync();
        }

        return new Result();
    }
}
