using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using Dreambuild.AutoCAD;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Detailing;

public class DeleteDetailingOp : OperationBase
{
    public override string TypeId => "Detailing.Delete";
    public override string DisplayName => "Delete Detailing";
    public override string Description => "Deletes all detailing from the drawing.";
    public override string Category => "Detailing";

    public override IReadOnlyList<ParameterDescriptor> Parameters => Array.Empty<ParameterDescriptor>();

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        new Intersect().deletedetailingmethod(context.Database);

        return new Result();
    }
}
