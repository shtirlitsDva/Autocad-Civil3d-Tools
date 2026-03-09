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

public class SetProfileViewBandItemsOp : OperationBase
{
    public override string TypeId => "Profile.SetViewBandItems";
    public override string DisplayName => "Set Profile View Band Items";
    public override string Description => "Configures bottom band items on profile views with surface and top profile references.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "bandIndex",
            "Band Index",
            ParameterType.Int,
            "Index of the bottom band item to configure",
            defaultValue: 0),
        new ParameterDescriptor(
            "surfaceProfileSuffix",
            "Surface Profile Suffix",
            ParameterType.String,
            "Suffix appended to alignment name for the surface profile",
            defaultValue: "_surface_P"),
        new ParameterDescriptor(
            "topProfilePattern",
            "Top Profile Pattern",
            ParameterType.String,
            "Text pattern to match the top profile name",
            defaultValue: "TOP"),
        new ParameterDescriptor(
            "labelAtStart",
            "Label at Start",
            ParameterType.Bool,
            "Whether to show label at start station",
            defaultValue: true),
        new ParameterDescriptor(
            "labelAtEnd",
            "Label at End",
            ParameterType.Bool,
            "Whether to show label at end station",
            defaultValue: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        int bandIdx = GetParamOrDefault(parameterValues, "bandIndex", 0);
        string surfSuffix = GetParamOrDefault(parameterValues, "surfaceProfileSuffix", "_surface_P");
        string topPattern = GetParamOrDefault(parameterValues, "topProfilePattern", "TOP");
        bool labelStart = GetParamOrDefault(parameterValues, "labelAtStart", true);
        bool labelEnd = GetParamOrDefault(parameterValues, "labelAtEnd", true);

        var xTx = context.Database.TransactionManager.TopTransaction;

        var pvs = context.Database.HashSetOfType<ProfileView>(xTx);
        var als = context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);

        foreach (Autodesk.Civil.DatabaseServices.Alignment al in als)
        {
            ObjectIdCollection pIds = al.GetProfileIds();

            Oid surfaceProfileId = Oid.Null;
            Oid topProfileId = Oid.Null;

            foreach (Oid oid in pIds)
            {
                Autodesk.Civil.DatabaseServices.Profile p =
                    oid.Go<Autodesk.Civil.DatabaseServices.Profile>(xTx);
                if (p.Name == $"{al.Name}{surfSuffix}") surfaceProfileId = p.Id;
                if (p.Name.Contains(topPattern)) topProfileId = p.Id;
            }

            foreach (ProfileView pv in pvs)
            {
                ProfileViewBandSet pvbs = pv.Bands;
                ProfileViewBandItemCollection pvbic = pvbs.GetBottomBandItems();

                if (bandIdx < pvbic.Count)
                {
                    ProfileViewBandItem pvbi = pvbic[bandIdx];
                    pvbi.Profile1Id = surfaceProfileId;
                    pvbi.Profile2Id = topProfileId;
                    pvbi.LabelAtStartStation = labelStart;
                    pvbi.LabelAtEndStation = labelEnd;
                }

                pvbs.SetBottomBandItems(pvbic);
            }
        }

        return new Result();
    }
}
