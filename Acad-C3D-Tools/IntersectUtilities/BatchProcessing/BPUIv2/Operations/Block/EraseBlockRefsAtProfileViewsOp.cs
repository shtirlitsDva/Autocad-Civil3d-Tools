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

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Block;

public class EraseBlockRefsAtProfileViewsOp : OperationBase
{
    public override string TypeId => "Block.EraseAtProfileViews";
    public override string DisplayName => "Erase Block References at Profile Views";
    public override string Description =>
        "Erases all block references whose position matches a ProfileView location.";
    public override string Category => "Block";

    public override IReadOnlyList<ParameterDescriptor> Parameters =>
        Array.Empty<ParameterDescriptor>();

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        var xTx = context.Database.TransactionManager.TopTransaction;
        var brs = context.Database.ListOfType<BlockReference>(xTx);
        var pvs = context.Database.ListOfType<ProfileView>(xTx);

        foreach (BlockReference br in brs)
        {
            if (pvs.Any(pv => br.Position.HorizontalEqualz(pv.Location)))
            {
                br.UpgradeOpen();
                br.Erase();
            }
        }

        return new Result();
    }
}
