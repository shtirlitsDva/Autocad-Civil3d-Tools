using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.PipelineNetworkSystem;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile.DataGatherers
{
    internal static class SurfaceProfileDataGatherer
    {
        public static AP_SurfaceProfileData GatherData(Alignment al, IPipelineSizeArrayV2 sizeArray)
        {
            Transaction tx = al.Database.TransactionManager.TopTransaction;
            
            ObjectIdCollection pids = al.GetProfileIds();
            if (pids.Count == 0) throw new Exception($"Alignment {al.Name} has no profiles!");                
            
            Profile? p = null;
            foreach (Oid pid in pids)
            {
                Profile ptemp = pid.Go<Profile>(tx);
                if (ptemp.Name.EndsWith("surface_P"))
                {
                    p = ptemp;
                    break;
                }
            }

            if (p == null) throw new Exception($"No surface profile found for {al.Name}!");

            ProfilePVICollection pvis = p.PVIs;

            var query = pvis.Select(
                pvis => new { pvis.RawStation, pvis.Elevation })
                .OrderBy(x => x.RawStation);

            var spd = new AP_SurfaceProfileData(p.Name);
            spd.ProfilePoints = query.Select(
                x => new double[] { x.RawStation, x.Elevation })
                .ToArray();

            //Create a polyline from the profile
            var pvs = al.GetProfileViewIds().Entities<ProfileView>(tx).ToList();
            if (pvs.Count != 1) throw new Exception(
                $"Alignment {al.Name} has more than one profile view!");

            ProfileView pv = pvs[0];

            var pline = p.ToPolyline(pv);

            spd.SurfacePolylineFull = pline.Clone() as Polyline;

            //Add hanging start and end segments to catch arcs that are too close
            //to the start and end of the profile view

            var start = pline.GetPoint2dAt(0);
            var addStart = new Point2d(start.X, start.Y - 50);
            pline.AddVertexAt(0, addStart, 0, 0, 0);

            var end = pline.GetPoint2dAt(pline.NumberOfVertices - 1);
            var addEnd = new Point2d(end.X, end.Y - 50);
            pline.AddVertexAt(pline.NumberOfVertices, addEnd, 0, 0, 0);

            spd.SurfacePolylineWithHangingEnds = pline;            

            spd.BuildOffsetCentrelines(sizeArray);

            return spd;
        }
    }
}