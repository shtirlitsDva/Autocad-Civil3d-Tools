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

public class PlaceBlockOnPaperspaceOp : OperationBase
{
    public override string TypeId => "Block.PlaceOnPaperspace";
    public override string DisplayName => "Place Block on Paperspace";
    public override string Description =>
        "Places a block on every layout's paperspace at the specified position, with rotation from the viewport twist angle.";
    public override string Category => "Block";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "blockName",
            "Block Name",
            ParameterType.String,
            "Name of the block to place",
            supportsSampling: true),
        new ParameterDescriptor(
            "blockX",
            "Block X",
            ParameterType.Int,
            "X coordinate for block placement"),
        new ParameterDescriptor(
            "blockY",
            "Block Y",
            ParameterType.Int,
            "Y coordinate for block placement"),
        new ParameterDescriptor(
            "viewportCenterX",
            "Viewport Center X",
            ParameterType.Int,
            "Viewport center X coordinate (integer)"),
        new ParameterDescriptor(
            "viewportCenterY",
            "Viewport Center Y",
            ParameterType.Int,
            "Viewport center Y coordinate (integer)")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string blockName = GetStringParam(parameterValues, "blockName");
        int brX = GetIntParam(parameterValues, "blockX");
        int brY = GetIntParam(parameterValues, "blockY");
        int X = GetIntParam(parameterValues, "viewportCenterX");
        int Y = GetIntParam(parameterValues, "viewportCenterY");
        var xTx = context.Database.TransactionManager.TopTransaction;

        BlockTable bt = xTx.GetObject(context.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
        Oid btrId = bt[blockName];

        DBDictionary layoutDict = context.Database.LayoutDictionaryId.Go<DBDictionary>(xTx);

        foreach (DBDictionaryEntry item in layoutDict)
        {
            if (item.Key == "Model") continue;
            Layout layout = item.Value.Go<Layout>(xTx);
            BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

            foreach (Oid id in layBlock)
            {
                if (id.IsDerivedFrom<Autodesk.AutoCAD.DatabaseServices.Viewport>())
                {
                    var vp = id.Go<Autodesk.AutoCAD.DatabaseServices.Viewport>(xTx);
                    int centerX = (int)vp.CenterPoint.X;
                    int centerY = (int)vp.CenterPoint.Y;
                    if (centerX == X && centerY == Y)
                    {
                        prdDbg($"Found main viewport, placing {blockName}!");

                        var br = new BlockReference(new Point3d(brX, brY, 0), btrId);
                        layBlock.CheckOrOpenForWrite();
                        layBlock.AppendEntity(br);
                        xTx.AddNewlyCreatedDBObject(br, true);

                        br.Rotation = vp.TwistAngle;
                    }
                }
            }
        }

        return new Result();
    }
}
