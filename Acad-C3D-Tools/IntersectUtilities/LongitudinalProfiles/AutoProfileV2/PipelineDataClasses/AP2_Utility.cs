using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.NTS;
using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Exception = System.Exception;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal class AP2_Utility : PipelineDataBase
    {
        private string devLyr = "AutoProfileTest";

        private Extents2d _extents;
        private int _counter = 0;

        public double[] Box => [_extents.MinPoint.X, _extents.MinPoint.Y, _extents.MaxPoint.X, _extents.MaxPoint.Y];

        /// <summary>
        /// Expects a bounding box of the utility to be passed in.
        /// The coords are station, elevation in the profile view context.
        /// </summary>        
        public AP2_Utility(Extents2d extents, int counter, AP2_PipelineData pipeline) : base(pipeline)
        {
            _extents = extents;
            _counter = counter;
        }

        public string Name => $"{_pipeLine.Name}_utility_{_counter.ToString("D3")}";

        public Hatch GetUtilityHatch(ProfileView pv)
        {
            Point2d bl = StationElevationToXY(pv, _extents.MinPoint.X, _extents.MinPoint.Y);
            Point2d tr = StationElevationToXY(pv, _extents.MaxPoint.X, _extents.MaxPoint.Y);
            Point2d tl = StationElevationToXY(pv, _extents.MinPoint.X, _extents.MaxPoint.Y);
            Point2d br = StationElevationToXY(pv, _extents.MaxPoint.X, _extents.MinPoint.Y);

            Hatch hatch = new Hatch();
            hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
            hatch.Elevation = 0.0;
            hatch.PatternScale = 1.0;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");

            hatch.AppendLoop(HatchLoopTypes.Default,
                [bl, tl, tr, br, bl],
                [0.0, 0.0, 0.0, 0.0, 0.0]);
            hatch.EvaluateHatch(true);

            return hatch;
        }

        private static Point2d StationElevationToXY(ProfileView pv, double station, double elevation)
        {
            double x = 0.0, y = 0.0;
            pv.FindXYAtStationAndElevation(station, elevation, ref x, ref y);
            return new Point2d(x, y);
        }

        public Hatch GetUtilityHatch()
        {
            Hatch hatch = new Hatch();
            hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
            hatch.Elevation = 0.0;
            hatch.PatternScale = 1.0;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");

            Point2d bl = _extents.MinPoint;
            Point2d tr = _extents.MaxPoint;
            Point2d tl = new Point2d(bl.X, tr.Y);
            Point2d br = new Point2d(tr.X, bl.Y);

            hatch.AppendLoop(HatchLoopTypes.Default,
                [bl, tl, tr, br, bl],
                [0.0, 0.0, 0.0, 0.0, 0.0]);
            hatch.EvaluateHatch(true);

            return hatch;
        }
    }
}