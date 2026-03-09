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

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Style;

public class ImportCivilStylesOp : OperationBase
{
    public override string TypeId => "Style.ImportCivilStyles";
    public override string DisplayName => "Import Civil Styles";
    public override string Description => "Imports Civil 3D styles into the drawing.";
    public override string Category => "Style";

    public override IReadOnlyList<ParameterDescriptor> Parameters => Array.Empty<ParameterDescriptor>();

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        new Intersect().importcivilstylesmethod(context.Database);

        return new Result();
    }
}
