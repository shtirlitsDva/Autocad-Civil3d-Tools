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

public class SetProfileViewStyleOp : OperationBase
{
    public override string TypeId => "Profile.SetViewStyle";
    public override string DisplayName => "Set Profile View Style";
    public override string Description => "Sets the style for all profile views in the drawing.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "styleName",
            "Style Name",
            ParameterType.String,
            "Name of the profile view style to apply",
            supportsSampling: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string styleName = GetStringParam(parameterValues, "styleName");

        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        Oid sId = cDoc.Styles.ProfileViewStyles[styleName];

        var pvs = context.Database.HashSetOfType<ProfileView>(xTx);

        foreach (ProfileView pv in pvs)
        {
            pv.CheckOrOpenForWrite();
            pv.StyleId = sId;
        }

        return new Result();
    }
}
