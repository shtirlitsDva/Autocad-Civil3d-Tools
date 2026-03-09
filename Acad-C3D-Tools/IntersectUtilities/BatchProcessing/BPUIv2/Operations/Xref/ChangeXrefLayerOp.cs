using Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Xref;

public class ChangeXrefLayerOp : OperationBase
{
    public override string TypeId => "Xref.ChangeLayer";
    public override string DisplayName => "Change Xref Layer";
    public override string Description => "Changes the layer of an xref block reference matched by partial name.";
    public override string Category => "Xref";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "xrefPartialName",
            "Xref Partial Name",
            ParameterType.String,
            "Partial name to match against xref block names"),
        new ParameterDescriptor(
            "layerName",
            "Layer Name",
            ParameterType.String,
            "Target layer name for the xref",
            supportsSampling: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string partialName = GetStringParam(parameterValues, "xrefPartialName");
        string layerName = GetStringParam(parameterValues, "layerName");
        Transaction xTx = context.Database.TransactionManager.TopTransaction;
        BlockTable bt = context.Database.BlockTableId.Go<BlockTable>(xTx, OpenMode.ForRead);

        foreach (Oid oid in bt)
        {
            BlockTableRecord btr = xTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
            if (btr.Name.Contains(partialName) && btr.IsFromExternalReference)
            {
                prdDbg($"Found specified xref: {btr.Name}");
                var ids = btr.GetBlockReferenceIds(true, false);
                foreach (Oid id in ids)
                {
                    var br = id.Go<BlockReference>(xTx, OpenMode.ForWrite);
                    br.Layer = layerName;
                }
                return new Result();
            }
        }
        prdDbg("Specified xref NOT found!");
        return new Result();
    }
}
