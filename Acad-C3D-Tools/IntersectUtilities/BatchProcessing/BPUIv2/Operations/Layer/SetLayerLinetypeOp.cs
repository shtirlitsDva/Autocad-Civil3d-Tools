using Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Layer;

public class SetLayerLinetypeOp : OperationBase
{
    public override string TypeId => "Layer.SetLinetype";
    public override string DisplayName => "Set Layer Linetype";
    public override string Description => "Sets the linetype for one or more layers.";
    public override string Category => "Layer";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "layerNames",
            "Layer Names",
            ParameterType.String,
            "Semicolon-delimited list of layer names"),
        new ParameterDescriptor(
            "linetypeName",
            "Linetype Name",
            ParameterType.String,
            "Name of the linetype to assign",
            supportsSampling: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string layerNames = GetStringParam(parameterValues, "layerNames");
        string linetypeName = GetStringParam(parameterValues, "linetypeName");

        Transaction xTx = context.Database.TransactionManager.TopTransaction;
        LayerTable extLt = context.Database.LayerTableId.Go<LayerTable>(xTx);
        LinetypeTable ltt = context.Database.LinetypeTableId.Go<LinetypeTable>(xTx);

        if (!ltt.Has(linetypeName))
            return new Result(ResultStatus.FatalError, $"Linetype {linetypeName} not found!");

        Oid ltypeId = ltt[linetypeName];

        var split = layerNames.Split(';');
        foreach (string layName in split)
        {
            foreach (Oid oid in extLt)
            {
                LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
                if (ltr.Name == layName)
                {
                    ltr.CheckOrOpenForWrite();
                    ltr.LinetypeObjectId = ltypeId;
                }
            }
        }

        return new Result();
    }
}
