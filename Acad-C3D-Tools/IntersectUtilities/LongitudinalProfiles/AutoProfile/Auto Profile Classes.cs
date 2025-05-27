using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.NTS;

using static IntersectUtilities.UtilsCommon.Utils;

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
                utility.BuildExtendedAvoidanceArc(radius);

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
        internal void GenerateAvoidancePolygonsForUtilities()
        {
            foreach (var utility in Utility)
            {
                if (utility.AvoidanceArc == null) continue;

                #region Normal avoidance region
                Polygon polygon = NTSConversion.ConvertClosedPlineToNTSPolygonWithCurveApproximation(
                    utility.AvoidanceArc);
                if (polygon == null) throw new Exception($"No polygon created for utility!");
                utility.AvoidancePolygon = polygon;
                #endregion

                #region Harc avoidance region
                if (utility.HorizontalArcAvoidancePolyline == null) continue;
                
                Polygon poly = NTSConversion.ConvertClosedPlineToNTSPolygonWithCurveApproximation(
                    utility.HorizontalArcAvoidancePolyline);

                if (poly == null) throw new Exception($"No polygon created for utility!");

                utility.HorizontalArcAvoidancePolygon = poly;
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
        [JsonIgnore]
        public Polygon UtilityPolygon { get; private set; }
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

            Polyline pline = new Polyline(4);
            pline.AddVertexAt(0, extents.MinPoint, 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(extents.MinPoint.X, extents.MaxPoint.Y), 0, 0, 0);
            pline.AddVertexAt(2, extents.MaxPoint, 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(extents.MaxPoint.X, extents.MinPoint.Y), 0, 0, 0);
            pline.Closed = true;

            UtilityPolygon = NTSConversion.ConvertClosedPlineToNTSPolygon(pline);
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
        public Polygon? AvoidancePolygon { get; set; } = null;
        [JsonIgnore]
        public Polyline? HorizontalArcAvoidancePolyline { get; private set; }
        [JsonIgnore]
        public Polygon? HorizontalArcAvoidancePolygon { get; set; } = null;

        public Relation IntersectWithPolygon(Polygon other)
        {
            var relation = other.Relate(this.UtilityPolygon);

            if (relation.IsContains()) return Relation.Inside;
            if (relation.IsDisjoint()) return Relation.Outside;
            return Relation.Overlaps;
        }

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
        public Arc BuildExtendedAvoidanceArc(double radius)
        {
            if (_pipeLine.SurfaceProfile == null)
                throw new Exception("No surface profile found for the pipeline!");
            if (_pipeLine.ProfileView == null)
                throw new Exception("No profile view found for the pipeline!");

            var centre = GetCentreOfAvoidanceArc(radius).To3d();

            var arc = BuildAvoidanceArc(radius);

            Point3d lp = new Point3d(arc.Center.X, arc.Center.Y - radius, 0.0);
            var surfaceElevation = _pipeLine.SurfaceProfile.GetSurfaceYAtX(this.MidStation);

            //Limit the arc to be no more than a set distance above surface
            double station = 0;
            double elevation = 0;
            _pipeLine.ProfileView.ProfileView.FindStationAndElevationAtXY(lp.X, lp.Y, ref station, ref elevation);
            var trimPoint = new Point3d(lp.X, lp.Y + (surfaceElevation - elevation) + 2.0, 0);

            double dy = trimPoint.Y - arc.Center.Y;
            double dx = Math.Sqrt(arc.Radius * arc.Radius - dy * dy);

            var leftPt = new Point3d(arc.Center.X - dx, trimPoint.Y, arc.Center.Z);
            var rightPt = new Point3d(arc.Center.X + dx, trimPoint.Y, arc.Center.Z);

            double angle1 = Math.Atan2(leftPt.Y - arc.Center.Y, leftPt.X - arc.Center.X);
            double angle2 = Math.Atan2(rightPt.Y - arc.Center.Y, rightPt.X - arc.Center.X);

            if (angle2 < angle1) (angle1, angle2) = (angle2, angle1);

            arc.StartAngle = angle1;
            arc.EndAngle = angle2;

            Polyline polyline = new Polyline(2);
            polyline.AddVertexAt(polyline.NumberOfVertices, arc.StartPoint.To2d(), arc.GetBulge(), 0, 0);
            polyline.AddVertexAt(polyline.NumberOfVertices, arc.EndPoint.To2d(), 0, 0, 0);

            polyline.Closed = true;

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
            if (_pipeLine.SizeArray == null)
                throw new Exception("No size array found for the pipeline!");
            if (_pipeLine.ProfileView == null)
                throw new Exception("No profile view found for the pipeline!");
            if (_pipeLine.SurfaceProfile == null)
                throw new Exception("No surface profile found for the pipeline!");

            double x = 0.0, y = 0.0, station = 0.0, elevation = 0.0;

            var pv = _pipeLine.ProfileView.ProfileView;
            var p = _pipeLine.SurfaceProfile.Profile;

            //Start arc
            var startPointRadius = _pipeLine.SizeArray.GetSizeAtStation(startStation).VerticalMinRadius;
            
            _pipeLine.ProfileView.ProfileView.FindXYAtStationAndElevation(
                startStation, BottomElevation, ref x, ref y);

            var startCentre = GetCentreOfTangencyArc(new Point2d(x, y), startPointRadius).To3d();

            Arc startArc = new Arc(startCentre, startPointRadius, Math.PI / 2, Math.PI * 3 / 2);
            
            Point3d lp = new Point3d(startArc.Center.X, startArc.Center.Y - startPointRadius, 0.0);
            var surfaceElevation = _pipeLine.SurfaceProfile.GetSurfaceYAtX(startStation);

            pv.FindStationAndElevationAtXY(lp.X, lp.Y, ref station, ref elevation);
            var trimPoint = new Point3d(lp.X, lp.Y + (surfaceElevation - elevation) + 2.0, 0);

            double dy = trimPoint.Y - startArc.Center.Y;
            double dx = Math.Sqrt(startArc.Radius * startArc.Radius - dy * dy);

            var leftPt = new Point3d(startArc.Center.X - dx, trimPoint.Y, startArc.Center.Z);            

            double angle1 = Math.Atan2(leftPt.Y - startArc.Center.Y, leftPt.X - startArc.Center.X);

            startArc.StartAngle = angle1;

            //End arc
            var endPointRadius = _pipeLine.SizeArray!.GetSizeAtStation(endStation).VerticalMinRadius;

            _pipeLine.ProfileView!.ProfileView.FindXYAtStationAndElevation(
                endStation, BottomElevation, ref x, ref y);

            var endCentre = GetCentreOfTangencyArc(new Point2d(x, y), endPointRadius).To3d();

            Arc endArc = new Arc(endCentre, endPointRadius, Math.PI * 3 / 2, Math.PI / 2);

            lp = new Point3d(endArc.Center.X, endArc.Center.Y - endPointRadius, 0.0);
            surfaceElevation = _pipeLine.SurfaceProfile.GetSurfaceYAtX(endStation);

            pv.FindStationAndElevationAtXY(lp.X, lp.Y, ref station, ref elevation);
            trimPoint = new Point3d(lp.X, lp.Y + (surfaceElevation - elevation) + 2.0, 0);

            dy = trimPoint.Y - endArc.Center.Y;
            dx = Math.Sqrt(endArc.Radius * endArc.Radius - dy * dy);

            var rightPt = new Point3d(endArc.Center.X + dx, trimPoint.Y, endArc.Center.Z);
            double angle2 = Math.Atan2(rightPt.Y - endArc.Center.Y, rightPt.X - endArc.Center.X);

            endArc.EndAngle = angle2;

            //Construct the polyline
            Polyline harcPolyline = new Polyline(4);
            harcPolyline.AddVertexAt(0, startArc.StartPoint.To2d(), startArc.GetBulge(), 0, 0);
            harcPolyline.AddVertexAt(1, startArc.EndPoint.To2d(), 0, 0, 0);
            harcPolyline.AddVertexAt(2, endArc.StartPoint.To2d(), endArc.GetBulge(), 0, 0);
            harcPolyline.AddVertexAt(3, endArc.EndPoint.To2d(), 0, 0, 0);

            harcPolyline.Closed = true;

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
    internal enum Relation
    {
        Unknown,
        Inside,
        Outside,
        Overlaps
    }
}