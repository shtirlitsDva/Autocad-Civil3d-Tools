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

public class CreateProfileCurveLabelsOp : OperationBase
{
    public override string TypeId => "Profile.CreateCurveLabels";
    public override string DisplayName => "Create Profile Curve Labels";
    public override string Description => "Creates crest and sag curve label groups for profiles matching a name pattern.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "namePattern",
            "Name Pattern",
            ParameterType.String,
            "Regex pattern to match profile names"),
        new ParameterDescriptor(
            "crestStyleName",
            "Crest Style Name",
            ParameterType.String,
            "Name of the crest curve label style"),
        new ParameterDescriptor(
            "sagStyleName",
            "Sag Style Name",
            ParameterType.String,
            "Name of the sag curve label style")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string pattern = GetStringParam(parameterValues, "namePattern");
        string crestName = GetStringParam(parameterValues, "crestStyleName");
        string sagName = GetStringParam(parameterValues, "sagStyleName");

        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        Oid crestId = cDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles[crestName];
        Oid sagId = cDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles[sagName];

        var pvs = context.Database.HashSetOfType<ProfileView>(xTx);
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
                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();
                        ProfileCrestCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, crestId);
                        ProfileSagCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, sagId);
                    }
                }
            }
        }

        return new Result();
    }
}
