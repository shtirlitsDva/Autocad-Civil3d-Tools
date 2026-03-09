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

public class RenumberViewFramesOp : OperationBase
{
    public override string TypeId => "ViewFrame.Renumber";
    public override string DisplayName => "Renumber ViewFrames";
    public override string Description =>
        "Renumbers all ViewFrame numbers to follow the expected sequence.";
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

            Dictionary<Oid, string> oNames = new Dictionary<Oid, string>();

            Random rnd = new Random();
            foreach (var item in ents)
            {
                oNames.Add(item.Id, item.Name);

                item.CheckOrOpenForWrite();
                item.Name = rnd.Next(1, 999999).ToString("000000");
            }

            foreach (var item in ents)
            {
                count.counter++;
                string previousName = item.Name;
                item.CheckOrOpenForWrite();
                item.Name = count.counter.ToString("000");
                prdDbg($"{oNames[item.Id]} -> {item.Name}");
            }
        }

        return new Result();
    }
}
