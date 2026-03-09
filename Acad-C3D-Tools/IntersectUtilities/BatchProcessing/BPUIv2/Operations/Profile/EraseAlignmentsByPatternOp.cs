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

public class EraseAlignmentsByPatternOp : OperationBase
{
    public override string TypeId => "Profile.EraseAlignmentsByPattern";
    public override string DisplayName => "Erase Alignments by Pattern";
    public override string Description => "Erases alignments whose names contain a specified text pattern.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "pattern",
            "Pattern",
            ParameterType.String,
            "Text pattern to match in alignment names",
            defaultValue: "(")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string pattern = GetParamOrDefault(parameterValues, "pattern", "(");

        var xTx = context.Database.TransactionManager.TopTransaction;

        var als = context.Database.ListOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);

        foreach (var item in als)
        {
            if (item.Name.Contains(pattern))
            {
                prdDbg(item.Name);
                item.CheckOrOpenForWrite();
                item.Erase(true);
            }
        }

        return new Result();
    }
}
