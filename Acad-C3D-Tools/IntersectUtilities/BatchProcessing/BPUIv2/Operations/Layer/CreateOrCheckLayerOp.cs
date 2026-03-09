using Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Layer;

public class CreateOrCheckLayerOp : OperationBase
{
    public override string TypeId => "Layer.CreateOrCheck";
    public override string DisplayName => "Create or Check Layer";
    public override string Description => "Creates a layer if it does not exist, or verifies it exists.";
    public override string Category => "Layer";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "layerName",
            "Layer Name",
            ParameterType.String,
            "Name of the layer to create or check"),
        new ParameterDescriptor(
            "colorIndex",
            "Color Index",
            ParameterType.Int,
            "AutoCAD color index for the layer",
            defaultValue: 0)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string layerName = GetStringParam(parameterValues, "layerName");
        int colorIndex = GetParamOrDefault(parameterValues, "colorIndex", 0);

        context.Database.CheckOrCreateLayer(layerName, (short)colorIndex);

        return new Result();
    }
}
