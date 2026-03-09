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
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Profile;

public class SetProfileStyleOp : OperationBase
{
    public override string TypeId => "Profile.SetStyle";
    public override string DisplayName => "Set Profile Style";
    public override string Description => "Sets the style for profiles matching a name pattern.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "namePattern",
            "Name Pattern",
            ParameterType.String,
            "Regex pattern to match profile names"),
        new ParameterDescriptor(
            "styleName",
            "Style Name",
            ParameterType.String,
            "Name of the profile style to apply",
            supportsSampling: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string pattern = GetStringParam(parameterValues, "namePattern");
        string styleName = GetStringParam(parameterValues, "styleName");

        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        Oid styleId = cDoc.Styles.ProfileStyles[styleName];

        var als = context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);
        var regex = new System.Text.RegularExpressions.Regex(
            pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (Autodesk.Civil.DatabaseServices.Alignment al in als)
        {
            ObjectIdCollection pIds = al.GetProfileIds();
            foreach (Oid oid in pIds)
            {
                Autodesk.Civil.DatabaseServices.Profile p =
                    oid.Go<Autodesk.Civil.DatabaseServices.Profile>(xTx);
                if (regex.IsMatch(p.Name))
                {
                    p.CheckOrOpenForWrite();
                    p.StyleId = styleId;
                }
            }
        }

        return new Result();
    }
}
