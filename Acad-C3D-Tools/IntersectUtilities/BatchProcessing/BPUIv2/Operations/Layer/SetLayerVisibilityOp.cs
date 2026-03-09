using Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Layer;

public class SetLayerVisibilityOp : OperationBase
{
    public override string TypeId => "Layer.SetVisibility";
    public override string DisplayName => "Set Layer Visibility";
    public override string Description => "Sets frozen and off state for one or more layers, optionally scoped to an xref.";
    public override string Category => "Layer";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "layerNames",
            "Layer Names",
            ParameterType.String,
            "Semicolon-delimited list of layer names"),
        new ParameterDescriptor(
            "xrefName",
            "Xref Name",
            ParameterType.String,
            "Optional xref name to prefix layers with (leave empty for local layers)",
            defaultValue: ""),
        new ParameterDescriptor(
            "frozen",
            "Frozen",
            ParameterType.Bool,
            "Whether to freeze the layers",
            defaultValue: true),
        new ParameterDescriptor(
            "off",
            "Off",
            ParameterType.Bool,
            "Whether to turn the layers off",
            defaultValue: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string layerNames = GetStringParam(parameterValues, "layerNames");
        string xrefName = GetParamOrDefault(parameterValues, "xrefName", "");
        bool frozen = GetParamOrDefault(parameterValues, "frozen", true);
        bool off = GetParamOrDefault(parameterValues, "off", true);

        Transaction xTx = context.Database.TransactionManager.TopTransaction;
        LayerTable extLt = context.Database.LayerTableId.Go<LayerTable>(xTx);

        var split = layerNames.Split(';');
        foreach (string layName in split)
        {
            string targetName = string.IsNullOrEmpty(xrefName) ? layName : $"{xrefName}|{layName}";

            foreach (Oid oid in extLt)
            {
                LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
                if (ltr.Name == targetName)
                {
                    ltr.CheckOrOpenForWrite();
                    ltr.IsFrozen = frozen;
                    ltr.IsOff = off;
                }
            }
        }

        return new Result();
    }
}
