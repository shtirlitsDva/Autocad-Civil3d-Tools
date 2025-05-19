using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AP_PipelineData
    {
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public AP_SurfaceProfileData? SurfaceProfile { get; set; } = null;
        public IPipelineSizeArrayV2? SizeArray { get; set; } = null;
        public double[][]? HorizontalArcs { get; set; } = null;
        public List<AP_Utility> Utility { get; set; } = new();
        public AP_ProfileViewData? ProfileView { get; set; } = null;
        public AP_PipelineData(string name)
        {
            Name = name;
        }
    }
    internal class AP_SurfaceProfileData
    {
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public double[][]? ProfilePoints { get; set; }
        public AP_SurfaceProfileData(string name)
        {
            Name = name;
        }
    }
    internal class AP_ProfileViewData
    {
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public double[] Origin { get; set; } = [0, 0];
        [JsonInclude]
        public double ElevationAtOrigin { get; set; } = 0;
        public AP_ProfileViewData(string name)
        {
            Name = name;
        }
    }
    internal class AP_Utility
    {
        private Extents2d extents;
        private double station;
        public double Station => station;
        /// <summary>
        /// Expects a bounding box of the utility to be passed in.
        /// </summary>        
        public AP_Utility(Geometry envelope, double station)
        {
            var cs = envelope.Coordinates;
            extents = new Extents2d(cs[0].X, cs[0].Y, cs[2].X, cs[2].Y);
            this.station = station;
        }
        public Point2d BL => extents.MinPoint;
        public Point2d BR => new Point2d(extents.MaxPoint.X, extents.MinPoint.Y);
        private Point2d TL => new Point2d(extents.MinPoint.X, extents.MaxPoint.Y);
        private Point2d TR => extents.MaxPoint;
        public Hatch GetUtilityHatch()
        {
            Hatch hatch = new Hatch();
            hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
            hatch.Elevation = 0.0;
            hatch.PatternScale = 1.0;
            hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");

            hatch.AppendLoop(HatchLoopTypes.Default,
                [BL, TL, TR, BR, BL],
                [0.0, 0.0, 0.0, 0.0, 0.0]);
            hatch.EvaluateHatch(true);

            return hatch;
        }
        public Arc GetTangencyArc(double radius)
        {
            var M = new LineSegment2d(BL, BR).MidPoint;

            double halfDist = BL.GetDistanceTo(BR) / 2;

            if (halfDist > radius)
                throw new ArgumentException(
                    $"The radius {radius} is too small for the distance between" +
                    $" the left and right points {BL} and {BR}.");

            double h = Math.Sqrt(radius * radius - halfDist * halfDist);
            var center = new Point3d(M.X, M.Y + h, 0);

            return new Arc(center, radius,
                (BL.To3d() - center).AngleOnPlane(new Plane()),
                (BR.To3d() - center).AngleOnPlane(new Plane()));
        }
    }
}