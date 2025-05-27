using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        [JsonIgnore]
        public IPipelineSizeArrayV2? SizeArray { get; set; } = null;
        [JsonIgnore]
        public List<HorizontalArc> HorizontalArcs { get; set; } = new();
        [JsonInclude]
        public List<AP_Utility> Utility { get; set; } = new();
        private STRtree<AP_Utility> utilityIndex = new();
        public void BuildUtilityIndex()
        {
            utilityIndex = new STRtree<AP_Utility>();
            foreach (var util in Utility)
            {
                var env = new Envelope(
                    util.StartStation, util.BottomElevation,
                    util.EndStation, util.TopElevation);
                utilityIndex.Insert(env, util);
            }
            utilityIndex.Build();
        }
        public void GenerateAvoidanceGeometryForUtilities() 
        {
            foreach (var utility in Utility)
            {
                //Avoidance arc
                double radius = SizeArray!.GetSizeAtStation(utility.MidStation).VerticalMinRadius;
                utility.BuildExtendedAvoidanceArc(radius, SurfaceProfile!.SurfacePolylineWithHangingEnds!);

                //Harc avoidance polyline
                //Find the horizontal arcs that cover this utility
                var harcsForThisUtility = HorizontalArcs
                    .Where(harc => harc.StartStation <= utility.EndStation &&
                                   harc.EndStation >= utility.StartStation)
                    .ToList();
                if (harcsForThisUtility.Count == 0) continue;
                double startStation = harcsForThisUtility.Min(x => x.StartStation);
                double endStation = harcsForThisUtility.Max(x => x.EndStation);
                utility.BuildHorizontalArcPolylineWithTrimmedArcs(startStation, endStation);
            }
        }
        internal void GenerateAvoidanceRegionsForUtilities()
        {
            foreach (var utility in Utility)
            {
                if (utility.AvoidanceArc == null || SurfaceProfile?.SurfacePolylineWithHangingEnds == null)
                    continue;

                #region Normal avoidance region
                using Point3dCollection pts1 = new Point3dCollection()
                {
                    SurfaceProfile.SurfacePolylineWithHangingEnds
                    .GetClosestPointTo(utility.AvoidanceArc.GetPoint3dAt(0), false),
                    SurfaceProfile.SurfacePolylineWithHangingEnds
                    .GetClosestPointTo(utility.AvoidanceArc.GetPoint3dAt(
                        utility.AvoidanceArc.NumberOfVertices - 1), false)
                };

                //Assume always three split curves
                var objs = SurfaceProfile.SurfacePolylineWithHangingEnds.GetSplitCurves(pts1);
                var pline = objs[1] as Polyline;


                // Create a Region from the closed loop
                using DBObjectCollection regionCurves1 = [utility.AvoidanceArc, pline];

                using DBObjectCollection regions1 = Region.CreateFromCurves(regionCurves1);
                if (regions1.Count == 0)
                    throw new InvalidOperationException($"Failed to create a region for utility {utility}.");

                // Process the created region (e.g., store it, add it to the drawing, etc.)
                Region region = regions1[0] as Region;
                if (region != null)
                {
                    utility.AvoidanceRegion = region;
                }
                #endregion

                #region Harc avoidance region
                if (utility.HorizontalArcAvoidancePolyline == null) continue;
                using Point3dCollection pts2 = new Point3dCollection()
                {
                    SurfaceProfile.SurfacePolylineWithHangingEnds
                    .GetClosestPointTo(utility.HorizontalArcAvoidancePolyline.GetPoint3dAt(0), false),
                    SurfaceProfile.SurfacePolylineWithHangingEnds
                    .GetClosestPointTo(utility.HorizontalArcAvoidancePolyline.GetPoint3dAt(
                        utility.HorizontalArcAvoidancePolyline.NumberOfVertices - 1), false)
                };

                //Assume always three split curves
                objs = SurfaceProfile.SurfacePolylineWithHangingEnds.GetSplitCurves(pts2);
                pline = objs[1] as Polyline;


                // Create a Region from the closed loop
                using DBObjectCollection regionCurves2 = [utility.HorizontalArcAvoidancePolyline, pline];

                try
                {
                    using DBObjectCollection regions2 = Region.CreateFromCurves(regionCurves2);
                    if (regions2.Count == 0)
                        throw new InvalidOperationException($"Failed to create a region for utility {utility}.");

                    // Process the created region (e.g., store it, add it to the drawing, etc.)
                    region = regions2[0] as Region;
                    if (region != null)
                    {
                        utility.HorizontalArcAvoidanceRegion = region;
                    }
                }
                catch (Exception ex)
                {                    
                    throw new DebugException(ex.Message, 
                        [utility.HorizontalArcAvoidancePolyline, pline, SurfaceProfile.SurfacePolylineWithHangingEnds]);
                }                
                #endregion
            }
        }
        public bool IsInsideAnyUtility(double station, double elevation)
        {
            if (utilityIndex == null) throw new Exception("Utility index is not built yet!");
            var env = new Envelope(new Coordinate(station, elevation));
            var query = utilityIndex.Query(env);
            return query.Count > 0;
        }
        [JsonIgnore]
        public AP_ProfileViewData? ProfileView { get; set; } = null;
        public AP_PipelineData(string name)
        {
            Name = name;
        }
        public void Serialize(string filename)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            options.Converters.Add(new PolylineJsonConverter());
            options.Converters.Add(new Extents2dJsonConverter());
            var json = JsonSerializer.Serialize(this, options);
            System.IO.File.WriteAllText(filename, json);
        }
    }
    internal abstract class PipelineData
    {
        protected AP_PipelineData _pipeLine;
        public PipelineData(AP_PipelineData pipeLine)
        {
            _pipeLine = pipeLine;
        }
    }
    internal class AP_SurfaceProfileData : PipelineData
    {
        private static double DouglaPeukerTolerance = 0.1;
        [JsonInclude]
        public string Name { get; set; }
        [JsonIgnore]
        public Profile? Profile { get; set; }
        [JsonIgnore]
        public Polyline? SurfacePolylineFull { get; set; } = null;
        [JsonInclude]
        public Polyline? SurfacePolylineSimplified { get; set; } = null;
        [JsonIgnore]
        public Polyline? SurfacePolylineWithHangingEnds { get; set; } = null;
        private Polyline? offsetCentrelines;
        [JsonIgnore]
        public Polyline? OffsetCentrelines => offsetCentrelines;
        public AP_SurfaceProfileData(string name, Profile p, AP_PipelineData pipeline) : base(pipeline)
        {
            Name = name;

            Profile = p;

            

            SurfacePolylineFull = p.ToPolyline(pipeline.ProfileView!.ProfileView);

            SurfacePolylineSimplified = SurfacePolylineFull.GetDouglasPeukerReducedCopy(DouglaPeukerTolerance);
            if (SurfacePolylineSimplified == null) throw new Exception($"No reduced polyline found for {Name}!");

            //Add hanging start and end segments to catch arcs that are too close
            //to the start and end of the profile view

            Polyline pline = SurfacePolylineSimplified.Clone() as Polyline;

            var start = pline.GetPoint2dAt(0);
            var addStart = new Point2d(start.X, start.Y - 50);
            pline.AddVertexAt(0, addStart, 0, 0, 0);

            var end = pline.GetPoint2dAt(pline.NumberOfVertices - 1);
            var addEnd = new Point2d(end.X, end.Y - 50);
            pline.AddVertexAt(pline.NumberOfVertices, addEnd, 0, 0, 0);

            SurfacePolylineWithHangingEnds = pline;

            BuildOffsetCentrelines(pipeline.SizeArray!);
        }

        private void BuildOffsetCentrelines(IPipelineSizeArrayV2 sizeArray)
        {
            if (SurfacePolylineFull == null) throw new Exception($"No surface polyline found for {Name}!");

            DoubleCollection splitDoubles = new();

            if (sizeArray.Length > 1)
            {
                for (int i = 0; i < sizeArray.Length - 1; i++)
                {//-1 to avoid adding a split point at the end of polyline
                    splitDoubles.Add(
                        SurfacePolylineSimplified.GetParameterAtStationX(
                            sizeArray[i].EndStation));
                }
            }

            List<(SizeEntryV2, Polyline)> polylinesToOffset = new();

            if (splitDoubles.Count == 0)
            {
                polylinesToOffset.Add((sizeArray[0], SurfacePolylineSimplified));
            }
            else
            {
                using var splitCurves = SurfacePolylineSimplified.GetSplitCurves(splitDoubles);
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
        /// <summary>
        /// x is station along the profile view. Returns the elevation at this station.
        /// </summary>
        internal double GetSurfaceYAtX(double x)
        {
            if (Profile == null) throw new Exception($"No profile found for {Name}!");
            return Profile.ElevationAt(x);
        }
    }
    internal class AP_Utility : PipelineData
    {
        private string devLyr = "AutoProfileTest";
        private Extents2d extents;
        [JsonIgnore]
        public Extents2d Extents { get => extents; set { extents = value; } }
        private double midStation;
        [JsonIgnore]
        public bool IsFloating { get; set; } = true;
        [JsonIgnore]
        public double MidStation => midStation;
        [JsonInclude]
        public double StartStation { get; private set; }
        [JsonInclude]
        public double EndStation { get; private set; }
        [JsonInclude]
        public double TopElevation { get; private set; }
        [JsonInclude]
        public double BottomElevation { get; private set; }

        /// <summary>
        /// Expects a bounding box of the utility to be passed in.
        /// </summary>        
        public AP_Utility(Geometry envelope, double station, AP_PipelineData pipeline) : base(pipeline)
        {
            var cs = envelope.Coordinates;
            extents = new Extents2d(cs[0].X, cs[0].Y, cs[2].X, cs[2].Y);
            this.midStation = station;
            StartStation = midStation - (extents.MaxPoint.X - extents.MinPoint.X) / 2;
            EndStation = midStation + (extents.MaxPoint.X - extents.MinPoint.X) / 2;
            double st = 0.0;
            double el = 0.0;
            pipeline.ProfileView!.ProfileView.FindStationAndElevationAtXY(
                extents.MinPoint.X, extents.MaxPoint.Y, ref st, ref el);
            TopElevation = el;
            pipeline.ProfileView!.ProfileView.FindStationAndElevationAtXY(
                extents.MinPoint.X, extents.MinPoint.Y, ref st, ref el);
            BottomElevation = el;


        }
        [JsonIgnore]
        public Point2d BL => extents.MinPoint;
        [JsonIgnore]
        public Point2d BR => new Point2d(extents.MaxPoint.X, extents.MinPoint.Y);
        [JsonIgnore]
        private Point2d TL => new Point2d(extents.MinPoint.X, extents.MaxPoint.Y);
        [JsonIgnore]
        private Point2d TR => extents.MaxPoint;
        [JsonInclude]
        public AP_Status Status { get; set; } = AP_Status.Unknown;
        [JsonIgnore]
        public Polyline? AvoidanceArc { get; private set; }
        [JsonIgnore]
        public Region? AvoidanceRegion { get; set; } = null;
        [JsonIgnore]
        public Polyline? HorizontalArcAvoidancePolyline { get; private set; }
        [JsonIgnore]
        public Region? HorizontalArcAvoidanceRegion { get; set; } = null;

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
        private Point2d GetCentreOfAvoidanceArc(double radius)
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
        private Point2d GetCentreOfTangencyArc(Point2d tangencyPoint, double radius)
        {
            //Use the tangency point to get the centre of the arc
            var C = tangencyPoint + new Vector2d(0, radius);
            return C;
        }
        public Arc BuildAvoidanceArc(double radius)
        {
            var center = GetCentreOfAvoidanceArc(radius).To3d();

            return new Arc(center, radius,
                (BL.To3d() - center).AngleOnPlane(new Plane()),
                (BR.To3d() - center).AngleOnPlane(new Plane()));
        }
        public Arc BuildExtendedAvoidanceArc(double radius, Polyline boundary)
        {
            var centre = GetCentreOfAvoidanceArc(radius).To3d();
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

            var arc = BuildAvoidanceArc(radius);
            //arc.Extend(true, start);
            //arc.Extend(false, end);

            arc.StartAngle = (start - centre).AngleOnPlane(new Plane());
            arc.EndAngle = (end - centre).AngleOnPlane(new Plane());

            //var startPt = boundary.GetClosestPointTo(arc.StartPoint, false).To2d();
            //var endPt = boundary.GetClosestPointTo(arc.EndPoint, false).To2d();

            Polyline polyline = new Polyline(2);
            //polyline.AddVertexAt(0, startPt, 0, 0, 0);
            polyline.AddVertexAt(polyline.NumberOfVertices, arc.StartPoint.To2d(), arc.GetBulge(), 0, 0);
            polyline.AddVertexAt(polyline.NumberOfVertices, arc.EndPoint.To2d(), 0, 0, 0);
            //polyline.AddVertexAt(3, endPt, 0, 0, 0);

            //(BL.To3d() - center).AngleOnPlane(new Plane()),
            //    (BR.To3d() - center).AngleOnPlane(new Plane())

            AvoidanceArc = polyline;

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
        internal Polyline BuildHorizontalArcPolylineWithTrimmedArcs(double startStation, double endStation)
        {
            var trimPolyline = _pipeLine.SurfaceProfile!.SurfacePolylineWithHangingEnds;

            //Start arc
            var startPointRadius = _pipeLine.SizeArray!.GetSizeAtStation(startStation).VerticalMinRadius;

            double x = 0.0, y = 0.0;
            _pipeLine.ProfileView!.ProfileView.FindXYAtStationAndElevation(
                startStation, BottomElevation, ref x, ref y);

            var startCentre = GetCentreOfTangencyArc(new Point2d(x, y), startPointRadius).To3d();

            Arc startArc = new Arc(startCentre, startPointRadius, Math.PI / 2, Math.PI * 3 / 2);
            using Point3dCollection startPts = new Point3dCollection();
            startArc.IntersectWith(trimPolyline,
                Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                new Plane(), startPts, 0, 0);

            var query = startPts
                .Cast<Point3d>()
                .Where(pt => trimPolyline!.GetClosestPointTo(pt, false)
                .DistanceHorizontalTo(pt) < 0.00000001).ToList();

            if (query.Count > 0)
            {
                var snitPt = query.MinBy(pt => pt.DistanceHorizontalTo(new Point3d(x, y, 0)));
                startArc = new Arc(startCentre, startPointRadius,
                    (snitPt - startCentre).AngleOnPlane(new Plane()),
                    Math.PI * 3 / 2);
            }

            //End arc
            var endPointRadius = _pipeLine.SizeArray!.GetSizeAtStation(endStation).VerticalMinRadius;

            _pipeLine.ProfileView!.ProfileView.FindXYAtStationAndElevation(
                endStation, BottomElevation, ref x, ref y);

            var endCentre = GetCentreOfTangencyArc(new Point2d(x, y), endPointRadius).To3d();

            Arc endArc = new Arc(endCentre, endPointRadius, Math.PI * 3 / 2, Math.PI / 2);
            using Point3dCollection endPts = new Point3dCollection();
            endArc.IntersectWith(trimPolyline,
                Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                new Plane(), endPts, 0, 0);

            query = endPts
                .Cast<Point3d>()
                .Where(pt => trimPolyline!.GetClosestPointTo(pt, false)
                .DistanceHorizontalTo(pt) < 0.00000001).ToList();

            if (query.Count > 0)
            {
                var snitPt = query.MinBy(pt => pt.DistanceHorizontalTo(new Point3d(x, y, 0))); ;
                endArc = new Arc(endCentre, endPointRadius,
                    Math.PI * 3 / 2,
                    (snitPt - endCentre).AngleOnPlane(new Plane())
                    );
            }

            //Construct the polyline
            Polyline harcPolyline = new Polyline(4);
            harcPolyline.AddVertexAt(0, startArc.StartPoint.To2d(), startArc.GetBulge(), 0, 0);
            harcPolyline.AddVertexAt(1, startArc.EndPoint.To2d(), 0, 0, 0);
            harcPolyline.AddVertexAt(2, endArc.StartPoint.To2d(), endArc.GetBulge(), 0, 0);
            harcPolyline.AddVertexAt(3, endArc.EndPoint.To2d(), 0, 0, 0);

            HorizontalArcAvoidancePolyline = harcPolyline;

            return harcPolyline;
        }
    }
    internal class AP_ProfileViewData : PipelineData
    {
        public string Name { get; set; }
        public ProfileView ProfileView { get; set; }
        public AP_ProfileViewData(string name, ProfileView profileView, AP_PipelineData pipeline) : base(pipeline)
        {
            Name = name;
            ProfileView = profileView;
        }
    }

    internal class HorizontalArc : PipelineData
    {
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public HorizontalArc(double start, double end, AP_PipelineData pipeline) : base(pipeline)
        {
            StartStation = start;
            EndStation = end;
        }
    }
    internal enum AP_Status
    {
        Unknown,
        Selected,
        Ignored
    }
}