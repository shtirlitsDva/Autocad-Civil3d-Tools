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

public class CreatePreliminaryDetailingOp : OperationBase
{
    public override string TypeId => "Detailing.CreatePreliminary";
    public override string DisplayName => "Create Preliminary Detailing";
    public override string Description => "Creates preliminary detailing for the drawing using project data references.";
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
            return new Result(ResultStatus.FatalError, "DataReferencesOptions not set.");

        new Intersect().createdetailingpreliminarymethod(context.DataReferences, context.Database);

        return new Result();
    }
}
