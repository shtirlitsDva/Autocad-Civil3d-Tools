using Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Xref;

public class DetachXrefOp : OperationBase
{
    public override string TypeId => "Xref.Detach";
    public override string DisplayName => "Detach Xref";
    public override string Description => "Detaches an external reference (xref) by name, outputting its path and layer.";
    public override string Category => "Xref";

    public override IReadOnlyList<ParameterDescriptor> Parameters =>
    [
        new ParameterDescriptor(
            "xrefName",
            "Xref Name",
            ParameterType.String,
            "Xref name without .dwg extension")
    ];

    public override IReadOnlyList<OutputDescriptor> Outputs =>
    [
        new OutputDescriptor("xrefPath", "Xref Path", ParameterType.String),
        new OutputDescriptor("xrefLayer", "Xref Layer", ParameterType.String)
    ];

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string xrefName = GetStringParam(parameterValues, "xrefName");
        Transaction xTx = context.Database.TransactionManager.TopTransaction;
        BlockTable bt = context.Database.BlockTableId.Go<BlockTable>(xTx, OpenMode.ForRead);

        foreach (Oid oid in bt)
        {
            BlockTableRecord btr = xTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
            if (btr.Name == xrefName && btr.IsFromExternalReference)
            {
                var refIds = btr.GetBlockReferenceIds(true, false);
                if (refIds.Count > 0)
                {
                    var br = refIds[0].Go<BlockReference>(xTx);
                    SetOutput(context, "xrefPath", btr.PathName);
                    SetOutput(context, "xrefLayer", br.Layer);
                }
                prdDbg($"Detaching xref: {btr.Name}");
                context.Database.DetachXref(btr.ObjectId);
                return new Result();
            }
        }
        prdDbg("Specified xref NOT found!");
        return new Result();
    }
}
