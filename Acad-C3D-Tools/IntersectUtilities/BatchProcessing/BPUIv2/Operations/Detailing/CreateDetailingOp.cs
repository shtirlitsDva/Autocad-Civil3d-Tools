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

public class CreateDetailingOp : OperationBase
{
    public override string TypeId => "Detailing.Create";
    public override string DisplayName => "Create Detailing";
    public override string Description => "Creates detailing for the drawing using project data references.";
    public override string Category => "Detailing";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "dro",
            "Data References",
            ParameterType.DataReferencesOptions,
            "Project and phase selection")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        if (context.DataReferences == null)
            return new Result(ResultStatus.FatalError, "DataReferencesOptions not set. Configure DRO before running.");

        new Intersect().createdetailingmethod(context.DataReferences, context.Database);

        return new Result();
    }
}
