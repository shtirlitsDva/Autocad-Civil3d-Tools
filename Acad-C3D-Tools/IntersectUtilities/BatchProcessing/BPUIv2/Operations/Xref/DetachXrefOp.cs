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
    public override string Description => "Detaches an external reference (xref) by name, saving its path and layer to SharedState.";
    public override string Category => "Xref";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "xrefName",
            "Xref Name",
            ParameterType.String,
            "Xref name without .dwg extension")
    };

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
                // Capture path and layer before detaching
                var refIds = btr.GetBlockReferenceIds(true, false);
                if (refIds.Count > 0)
                {
                    var br = refIds[0].Go<BlockReference>(xTx);
                    context.SharedState["_detached_xref_path"] = btr.PathName;
                    context.SharedState["_detached_xref_layer"] = br.Layer;
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
