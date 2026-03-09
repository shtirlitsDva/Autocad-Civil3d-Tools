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

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.ViewFrame;

public class ListViewFrameNumbersOp : OperationBase
{
    public override string TypeId => "ViewFrame.ListNumbers";
    public override string DisplayName => "List ViewFrame Numbers";
    public override string Description =>
        "Lists all ViewFrame numbers and warns if they do not follow the expected sequence.";
    public override string Category => "ViewFrame";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "counter",
            "Counter",
            ParameterType.Counter,
            "Shared counter across drawings")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        Counter count = GetCounter(context);
        var xTx = context.Database.TransactionManager.TopTransaction;

        ViewFrameGroup vfg = context.Database.ListOfType<ViewFrameGroup>(xTx).FirstOrDefault();
        if (vfg != null)
        {
            var ids = vfg.GetViewFrameIds();
            var ents = ids.Entities<Autodesk.Civil.DatabaseServices.ViewFrame>(xTx);
            foreach (var item in ents)
            {
                count.counter++;
                int vfNumber = Convert.ToInt32(item.Name);
                if (count.counter != vfNumber)
                    prdDbg(item.Name + " <- Fejl! Skal være " + count.counter + ".");
                else
                    prdDbg(item.Name);
            }
        }

        return new Result();
    }
}
