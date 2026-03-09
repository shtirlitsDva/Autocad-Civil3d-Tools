using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using Dreambuild.AutoCAD;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Profile;

public class EraseAllProfilesOp : OperationBase
{
    public override string TypeId => "Profile.EraseAll";
    public override string DisplayName => "Erase All Profiles";
    public override string Description => "Erases all profiles in the drawing.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => Array.Empty<ParameterDescriptor>();

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        var xTx = context.Database.TransactionManager.TopTransaction;

        var profs = context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Profile>(xTx);

        foreach (Autodesk.Civil.DatabaseServices.Profile p in profs)
        {
            p.CheckOrOpenForWrite();
            p.Erase(true);
        }

        return new Result();
    }
}
