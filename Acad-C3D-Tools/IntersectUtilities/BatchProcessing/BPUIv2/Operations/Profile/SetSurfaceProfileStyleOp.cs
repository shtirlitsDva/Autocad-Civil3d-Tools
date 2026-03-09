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

public class SetSurfaceProfileStyleOp : OperationBase
{
    public override string TypeId => "Profile.SetSurfaceProfileStyle";
    public override string DisplayName => "Set Surface Profile Style";
    public override string Description => "Sets the style for surface profiles (named {alignment}_surface_P).";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "profileStyleName",
            "Profile Style Name",
            ParameterType.String,
            "Name of the profile style to apply to surface profiles",
            supportsSampling: true,
            defaultValue: "Terr\u00e6n")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string styleName = GetStringParam(parameterValues, "profileStyleName");

        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        Oid profileStyleId = cDoc.Styles.ProfileStyles[styleName];

        var als = context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);

        foreach (Autodesk.Civil.DatabaseServices.Alignment al in als)
        {
            var pIds = al.GetProfileIds();
            foreach (Oid oid in pIds)
            {
                Autodesk.Civil.DatabaseServices.Profile p =
                    oid.Go<Autodesk.Civil.DatabaseServices.Profile>(xTx);
                if (p.Name == $"{al.Name}_surface_P")
                {
                    p.CheckOrOpenForWrite();
                    p.StyleId = profileStyleId;
                }
            }
        }

        return new Result();
    }
}
