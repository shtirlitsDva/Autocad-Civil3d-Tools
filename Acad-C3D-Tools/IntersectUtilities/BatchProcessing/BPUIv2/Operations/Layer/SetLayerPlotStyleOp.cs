using Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Layer;

public class SetLayerPlotStyleOp : OperationBase
{
    public override string TypeId => "Layer.SetPlotStyle";
    public override string DisplayName => "Set Layer Plot Style";
    public override string Description => "Sets the plot style for all layers belonging to a specific xref.";
    public override string Category => "Layer";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "xrefName",
            "Xref Name",
            ParameterType.String,
            "Name of the xref whose layers to modify",
            supportsSampling: true),
        new ParameterDescriptor(
            "plotStyleName",
            "Plot Style Name",
            ParameterType.String,
            "Name of the plot style to assign",
            defaultValue: "Nedtonet 50%")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string xrefName = GetStringParam(parameterValues, "xrefName");
        string plotStyleName = GetParamOrDefault(parameterValues, "plotStyleName", "Nedtonet 50%");

        Transaction xTx = context.Database.TransactionManager.TopTransaction;
        LayerTable extLt = context.Database.LayerTableId.Go<LayerTable>(xTx);

        foreach (Oid oid in extLt)
        {
            LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
            if (ltr.Name.Contains("|"))
            {
                var split = ltr.Name.Split('|');
                string xName = split[0];
                if (xName == xrefName)
                {
                    prdDbg(ltr.Name);
                    ltr.CheckOrOpenForWrite();
                    ltr.PlotStyleName = plotStyleName;
                }
            }
        }

        return new Result();
    }
}
