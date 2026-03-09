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

public class SetProfileViewElevationRangeOp : OperationBase
{
    public override string TypeId => "Profile.SetViewElevationRange";
    public override string DisplayName => "Set Profile View Elevation Range";
    public override string Description => "Automatically sets elevation range on profile views based on surface profile data.";
    public override string Category => "Profile";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "minDepth",
            "Minimum Depth",
            ParameterType.Double,
            "Depth below minimum surface elevation for the view bottom",
            defaultValue: 3.0)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        double minDepth = GetParamOrDefault(parameterValues, "minDepth", 3.0);

        var xTx = context.Database.TransactionManager.TopTransaction;

        var als = context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);

        foreach (Autodesk.Civil.DatabaseServices.Alignment al in als)
        {
            var pIds = al.GetProfileIds();
            var pvIds = al.GetProfileViewIds();

            Autodesk.Civil.DatabaseServices.Profile pSurface = null;

            foreach (Oid oid in pIds)
            {
                Autodesk.Civil.DatabaseServices.Profile pt =
                    oid.Go<Autodesk.Civil.DatabaseServices.Profile>(xTx);
                if (pt.Name == $"{al.Name}_surface_P") pSurface = pt;
            }

            if (pSurface == null) continue;

            prdDbg($"\nProfile {pSurface.Name} found!");

            foreach (ProfileView pv in pvIds.Entities<ProfileView>(xTx))
            {
                double pvStStart = pv.StationStart;
                double pvStEnd = pv.StationEnd;

                int nrOfIntervals = (int)((pvStEnd - pvStStart) / 0.25);
                double delta = (pvStEnd - pvStStart) / nrOfIntervals;

                HashSet<double> topElevs = new HashSet<double>();

                for (int j = 0; j < nrOfIntervals + 1; j++)
                {
                    try { topElevs.Add(pSurface.ElevationAt(pvStStart + delta * j)); }
                    catch { continue; }
                }

                if (topElevs.Count == 0) continue;

                double maxEl = topElevs.Max();
                double minEl = topElevs.Min();

                pv.CheckOrOpenForWrite();
                pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                pv.ElevationMax = Math.Ceiling(maxEl);
                pv.ElevationMin = Math.Floor(minEl) - minDepth;
            }
        }

        return new Result();
    }
}
