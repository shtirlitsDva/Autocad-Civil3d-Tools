using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using Dreambuild.AutoCAD;

using IntersectUtilities.UtilsCommon;

using Exception = System.Exception;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal class AP2_SurfaceProfileData : PipelineDataBase
    {
        private static double DouglasPeukerTolerance = 0.1;
        public string Name { get; set; }
        private Profile? _profile { get; set; }
        private Polyline? _surfacePolylineFull { get; set; } = null;
        private Polyline? _surfacePolylineSimplified { get; set; } = null;
        public AP2_SurfaceProfileData(string name, Profile p, AP2_PipelineData pipeline) : base(pipeline)
        {
            Name = name;

            _profile = p;

            _surfacePolylineFull = p.ToPolyline(pipeline.ProfileView!.ProfileView);

            _surfacePolylineSimplified = _surfacePolylineFull.GetDouglasPeukerReducedCopy(DouglasPeukerTolerance);
            if (_surfacePolylineSimplified == null) throw new Exception($"No reduced polyline found for {Name}!");
        }

        public List<double[]> GetSimplifiedProfileDTO()
        {
            if (_profile == null) throw new Exception($"No profile found for {Name}!");
            if (_surfacePolylineSimplified == null) throw new Exception($"No surface polyline found for {Name}!");

            //We will normalize the sampling to account for if profile
            //starting station is not 0
            var profileStartStation = _profile.StartingStation;

            List<double[]> profileData = new();

            double startX = 0;
            double curStation = 0;
            for (int i = 0; i < _surfacePolylineSimplified.NumberOfVertices; i++)
            {
                var pt = _surfacePolylineSimplified.GetPoint3dAt(i);
                if (i == 0) startX = pt.X;
                curStation = pt.X - startX + profileStartStation;

                double elevation = 0;
                try
                {
                    elevation = _profile.ElevationAt(curStation);
                    profileData.Add([curStation, elevation]);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Elevation sampling failed at station {curStation} for surface profile {Name}!");
                }
            }

            return profileData;
        }
    }
}