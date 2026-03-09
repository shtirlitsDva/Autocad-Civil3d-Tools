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

public class SetProfileStylePropertiesOp : OperationBase
{
    public override string TypeId => "Profile.SetStyleProperties";
    public override string DisplayName => "Set Profile Style Properties";
    public override string Description => "Sets linetype scale and line weight on a profile style.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "styleName",
            "Style Name",
            ParameterType.String,
            "Name of the profile style to modify",
            supportsSampling: true),
        new ParameterDescriptor(
            "linetypeScale",
            "Linetype Scale",
            ParameterType.Double,
            "Linetype scale value",
            defaultValue: 10.0),
        new ParameterDescriptor(
            "lineWeight",
            "Line Weight",
            ParameterType.Int,
            "Line weight multiplier (multiplied by 5 for actual weight)",
            defaultValue: 0)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string styleName = GetStringParam(parameterValues, "styleName");
        double ltScale = GetParamOrDefault(parameterValues, "linetypeScale", 10.0);
        int lw = GetParamOrDefault(parameterValues, "lineWeight", 0);

        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        ProfileStyle ps = cDoc.Styles.ProfileStyles[styleName]
            .Go<ProfileStyle>(xTx);
        ps.CheckOrOpenForWrite();

        LineWeight weight = (LineWeight)(lw * 5);

        DisplayStyle ds;

        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Line);
        ds.LinetypeScale = ltScale;
        ds.Lineweight = weight;

        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
        ds.LinetypeScale = ltScale;
        ds.Lineweight = weight;

        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
        ds.LinetypeScale = ltScale;
        ds.Lineweight = weight;

        return new Result();
    }
}
