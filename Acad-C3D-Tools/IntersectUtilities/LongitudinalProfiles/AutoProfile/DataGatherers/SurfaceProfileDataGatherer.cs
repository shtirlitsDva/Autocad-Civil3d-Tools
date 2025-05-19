using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile.DataGatherers
{
    internal static class SurfaceProfileDataGatherer
    {
        public static AP_SurfaceProfileData GatherData(Alignment al)
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

            return spd;
        }
    }
}