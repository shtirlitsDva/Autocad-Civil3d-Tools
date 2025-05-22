using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipeScheduleV2;
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
        private static double DouglaPeukerTolerance = 0.1;
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public double[][]? ProfilePoints { get; set; }
        public Polyline? SurfacePolyline { get; set; } = null;
        public Polyline? SurfacePolylineWithHangingEnds { get; set; } = null;
        private Polyline? offsetCentrelines;
        public Polyline? OffsetCentrelines => offsetCentrelines;
        public AP_SurfaceProfileData(string name)
        {
            Name = name;
        }

        internal void BuildOffsetCentrelines(IPipelineSizeArrayV2 sizeArray)
        {
            if (SurfacePolyline == null) throw new Exception($"No surface polyline found for {Name}!");

            Polyline? reducedPolyline = SurfacePolyline.GetDouglasPeukerReducedCopy(DouglaPeukerTolerance);
            if (reducedPolyline == null) throw new Exception($"No reduced polyline found for {Name}!");

            DoubleCollection splitDoubles = new();

            if (sizeArray.Length > 1)
            {
                for (int i = 0; i < sizeArray.Length - 1; i++)
                {//-1 to avoid adding a split point at the end of polyline
                    splitDoubles.Add(
                        reducedPolyline.GetParameterAtStationX(
                            sizeArray[i].EndStation));
                }
            }

            List<(SizeEntryV2, Polyline)> polylinesToOffset = new();

            if (splitDoubles.Count == 0)
            {
                polylinesToOffset.Add((sizeArray[0], reducedPolyline));
            }
            else
            {
                using var splitCurves = reducedPolyline.GetSplitCurves(splitDoubles);
                //Check for sanity
                if (!(splitCurves.Count == sizeArray.Length))
                    throw new Exception($"The number of split curves {splitCurves.Count} does not match" +
                        $" the number of sizes {sizeArray.Length}!");

                for (int i = 0; i < splitCurves.Count; i++)
                {
                    var current = splitCurves[i] as Polyline;
                    if (current == null) throw new Exception($"The split curve {i} is not a polyline!");

                    var size = sizeArray[i];

                    polylinesToOffset.Add((size, current));
                }
            }

            List<Polyline> offsets = new();
            for (int i = 0; i < polylinesToOffset.Count; i++)
            {
                var size = polylinesToOffset[i].Item1;
                var pline = polylinesToOffset[i].Item2;

                double offset =
                    PipeScheduleV2.PipeScheduleV2.GetCoverDepth(size.DN, size.System, size.Type) +
                    size.Kod / 1000 / 2;

                using var offsetCurves = pline.GetOffsetCurves(offset);

                foreach (var ent in offsetCurves)
                {
                    if (ent is Polyline poly)
                    {
                        offsets.Add(poly);
                    }
                }
            }

            if (offsets.Count == 0) throw new Exception($"No offset polylines found for {Name}!");
            if (offsets.Count == 1) offsetCentrelines = offsets[0];
            else
            {
                Polyline combined = new Polyline();
                for (int i = 0; i < offsets.Count; i++)
                {
                    var cur = offsets[i];
                    for (int j = 0; j < cur.NumberOfVertices; j++)
                        combined.AddVertexAt(combined.NumberOfVertices, cur.GetPoint2dAt(j), 0, 0, 0);                    
                }
                offsetCentrelines = combined;
            }
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
        private string devLyr = "AutoProfileTest";
        private Extents2d extents;
        private double midStation;
        public bool IsFloating { get; set; } = true;
        public double MidStation => midStation;
        public double MinStation => midStation - (extents.MaxPoint.X - extents.MinPoint.X) / 2;
        public double MaxStation => midStation + (extents.MaxPoint.X - extents.MinPoint.X) / 2;

        /// <summary>
        /// Expects a bounding box of the utility to be passed in.
        /// </summary>        
        public AP_Utility(Geometry envelope, double station)
        {
            var cs = envelope.Coordinates;
            extents = new Extents2d(cs[0].X, cs[0].Y, cs[2].X, cs[2].Y);
            this.midStation = station;
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
        private Point2d GetCentreOfTangencyArc(double radius)
        {
            var M = new LineSegment2d(BL, BR).MidPoint;
            double halfDist = BL.GetDistanceTo(BR) / 2;
            if (halfDist > radius)
                throw new ArgumentException(
                    $"The radius {radius} is too small for the distance between" +
                    $" the left and right points {BL} and {BR}.");
            double h = Math.Sqrt(radius * radius - halfDist * halfDist);
            return new Point2d(M.X, M.Y + h);
        }
        public Arc GetTangencyArc(double radius)
        {
            var center = GetCentreOfTangencyArc(radius).To3d();

            return new Arc(center, radius,
                (BL.To3d() - center).AngleOnPlane(new Plane()),
                (BR.To3d() - center).AngleOnPlane(new Plane()));
        }
        public Arc GetExtendedArc(double radius, Polyline boundary)
        {
            var centre = GetCentreOfTangencyArc(radius).To3d();
            Circle circle = new Circle(centre, Vector3d.ZAxis, radius);
            using Point3dCollection pts = new Point3dCollection();
            circle.IntersectWith(
                boundary, Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                new Plane(), pts, 0, 0);

            //Assume 2 intersection points until proven otherwise
            if (pts.Count != 2)
                throw new ArgumentException(
                    $"The circle at {centre} with radius {radius} does not intersect the boundary " +
                    $"at 2 points. It intersects at {pts.Count} points.");

            var ptsSorted = pts.Cast<Point3d>()
                .OrderBy(x => x.X)
                .ToList();

            var start = ptsSorted[0];
            var end = ptsSorted[1];

            var arc = GetTangencyArc(radius);
            arc.Extend(true, start);
            arc.Extend(false, end);

            return arc;
        }
        public void TestFloatingStatus(Polyline profile)
        {
            //Define utility as a polyline
            Polyline utility = new Polyline(4);
            utility.AddVertexAt(0, BL, 0, 0, 0);
            utility.AddVertexAt(1, TL, 0, 0, 0);
            utility.AddVertexAt(2, TR, 0, 0, 0);
            utility.AddVertexAt(3, BR, 0, 0, 0);
            utility.Closed = true;

            using Point3dCollection pts = new Point3dCollection();
            profile.IntersectWith(
                utility, Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                new Plane(), pts, 0, 0);

            //Neeed to test IntersectWith as it is very incorrect at large coordinates
            var query = pts
                .Cast<Point3d>()
                .Where(x => utility.GetClosestPointTo(x, false)
                .DistanceHorizontalTo(x) < 0.00000001);            

            if (query.Count() != 0) IsFloating = false;
            else
            {
                Point3d left = profile.GetClosestPointTo(BL.To3d(), Vector3d.YAxis, true);
                Point3d right = profile.GetClosestPointTo(BR.To3d(), Vector3d.YAxis, true);                

                if (BL.Y > left.Y && BR.Y > right.Y)
                {
                    IsFloating = false;
                }
                else IsFloating = true;                
            }
        }        
    }
}