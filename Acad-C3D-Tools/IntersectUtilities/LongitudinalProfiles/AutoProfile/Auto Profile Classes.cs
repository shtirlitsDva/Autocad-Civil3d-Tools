using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.NTS;

using static IntersectUtilities.UtilsCommon.Utils;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Exception = System.Exception;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Dreambuild.AutoCAD;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AP_PipelineData
    {
        public AP_PipelineData(string name)
        {
            Name = name;
        }
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public AP_SurfaceProfileData? SurfaceProfile { get; set; } = null;
        [JsonIgnore]
        public IPipelineSizeArrayV2? SizeArray { get; set; } = null;
        [JsonIgnore]
        public List<HorizontalArc> HorizontalArcs { get; set; } = new();
        [JsonIgnore]
        public AP_ProfileViewData? ProfileView { get; set; } = null;
        [JsonInclude]
        public List<AP_Utility> Utility { get; set; } = new();
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
        internal void MergeAvoidancePolygonsForUtilities()
        {
            //Merge the avoidance polygons for each utility
            foreach (AP_Utility utility in Utility)
            {
                if (utility.AvoidancePolygon == null) continue;
                if (utility.HorizontalArcAvoidancePolygon == null)
                {
                    utility.MergedAvoidancePolygon = utility.AvoidancePolygon;
                    continue;
                }
                var merged = utility.AvoidancePolygon.Union(utility.HorizontalArcAvoidancePolygon);
                if (merged is Polygon poly)
                {
                    utility.MergedAvoidancePolygon = poly;
                }
                else throw new Exception($"Merged polygon is not a valid polygon!");
            }
        }
        public Polyline? UnfilletedPolyline { get; set; }
        public Polyline? FilletedPolyline { get; private set; } // To store the result

        internal void ProcessSelectedUtilitiesToCreateUnfilletedPolyline()
        {
            if (SurfaceProfile == null) throw new Exception("No surface profile found for the pipeline!");
            if (SurfaceProfile.OffsetCentrelines == null) throw new Exception("No offset centrelines found for the surface profile!");
            if (ProfileView == null) throw new Exception("No profile view found for the pipeline!");
            var pv = ProfileView.ProfileView;

            Region? mergedRegion = null;

            //1. Create a polygon from pipe centreline
            var poly = SurfaceProfile.OffsetCentrelines.Clone() as Polyline;
            if (poly == null) throw new Exception("No offset centrelines found for the surface profile!");
            var sp = poly.GetPoint2dAt(0);
            poly.AddVertexAt(0, new Point2d(sp.X, sp.Y + 20), 0, 0, 0);
            var ep = poly.GetPoint2dAt(poly.NumberOfVertices - 1);
            poly.AddVertexAt(poly.NumberOfVertices, new Point2d(ep.X, ep.Y + 20), 0, 0, 0);
            poly.Closed = true;
            DBObjectCollection regions = Region.CreateFromCurves(new DBObjectCollection { poly });
            if (regions.Count == 0) throw new Exception("No regions created from the offset centrelines!");
            Region mainRegion = regions[0] as Region;
            if (mainRegion == null) throw new Exception("No main region created from the offset centrelines!");

            //2. Merge pipe centreline polygon with selected utility polygons
            var query = Utility.Where(
                x => x.Status == AP_Status.Selected);

            if (query.Count() == 0) { mergedRegion = mainRegion; } //No selected utilities
            else
            {
                foreach (var utility in query)
                {
                    mainRegion.BooleanOperation(BooleanOperationType.BoolUnite, utility.AvoidanceRegion!);

                    if (utility.HorizontalArcAvoidanceRegion != null)
                    {
                        mainRegion.BooleanOperation(BooleanOperationType.BoolUnite, utility.HorizontalArcAvoidanceRegion);
                    }
                }

                //Trim
                var bbox = mainRegion.GeometricExtents;
                var origo = ProfileView.ProfileView.Location;

                //Left side
                Polyline polyline = new Polyline(4);
                polyline.AddVertexAt(0, origo.To2d(), 0, 0, 0);
                polyline.AddVertexAt(1, new Point2d(origo.X, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(2, new Point2d(bbox.MinPoint.X - 1, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(3, new Point2d(bbox.MinPoint.X - 1, origo.Y), 0, 0, 0);
                polyline.Closed = true;
                regions = Region.CreateFromCurves(new DBObjectCollection { polyline });
                if (regions.Count != 1) throw new Exception("Failed to create Region!");
                Region trimRegion = (Region)regions[0];
                mainRegion.BooleanOperation(BooleanOperationType.BoolSubtract, trimRegion);

                //Right side
                origo = new Point3d(origo.X + pv.StationEnd, origo.Y, 0.0);
                polyline = new Polyline(4);
                polyline.AddVertexAt(0, origo.To2d(), 0, 0, 0);
                polyline.AddVertexAt(1, new Point2d(origo.X, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(2, new Point2d(bbox.MaxPoint.X + 1, bbox.MaxPoint.Y + 1), 0, 0, 0);
                polyline.AddVertexAt(3, new Point2d(bbox.MaxPoint.X + 1, origo.Y), 0, 0, 0);
                polyline.Closed = true;
                regions = Region.CreateFromCurves(new DBObjectCollection { polyline });
                if (regions.Count != 1) throw new Exception("Failed to create Region!");
                trimRegion = (Region)regions[0];
                mainRegion.BooleanOperation(BooleanOperationType.BoolSubtract, trimRegion);
            }

            //test = mainRegion;

            //3.extract "lower" polyline
            if (mainRegion == null) throw new Exception("no merged polygon found!");

            using DBObjectCollection exploded = new DBObjectCollection();
            mainRegion.Explode(exploded);

            var ents = exploded.Cast<Entity>().ToList();
            List<Entity> filteredEntities = new();
            var pvBbox = pv.GeometricExtents;

            Point3d pvOrigo = new Point3d(pv.Location.X, pv.Location.Y, 0.0);
            Point3d pvEnd = new Point3d(pv.Location.X + pv.StationEnd, pv.Location.Y, 0.0);
            double tol = 0.01; //Tolerance for line ends

            foreach (var ent in ents)
            {
                switch (ent)
                {
                    case Line line:
                        {
                            //Do not take (vertical) lines at start and end
                            if ((line.StartPoint.X < pvOrigo.X + tol && line.EndPoint.X < pvOrigo.X + tol) ||
                                (line.StartPoint.X > pvEnd.X - tol && line.EndPoint.X > pvEnd.X - tol)) continue;

                            //Do not take lines that are outside the profile view
                            if (!pvBbox.IsPointIn(line.StartPoint) &&
                                !pvBbox.IsPointIn(line.EndPoint)) continue;

                            filteredEntities.Add(line);
                        }
                        break;
                    case Arc arc:
                        {
                            // Do not take(vertical) lines at start and end
                            if ((arc.StartPoint.X < pvOrigo.X + tol && arc.EndPoint.X < pvOrigo.X + tol) ||
                                (arc.StartPoint.X > pvEnd.X - tol && arc.EndPoint.X > pvEnd.X - tol)) continue;

                            //Do not take lines that are outside the profile view
                            if (!pvBbox.IsPointIn(arc.StartPoint) &&
                                !pvBbox.IsPointIn(arc.EndPoint)) continue;

                            filteredEntities.Add(arc);
                        }
                        break;
                    default:
                        break;
                }
            }

            var sorted = filteredEntities
                .OrderBy(x => x.GeometricExtents.MinPoint.X)
                .ToList();

            Polyline lowerpline = new Polyline();
            Point3d previousEnd = Point3d.Origin;
            for (int i = 0; i < sorted.Count; i++)
            {
                var ent = sorted[i];

                switch (ent)
                {
                    case Line line:
                        {
                            if (previousEnd.DistanceTo(line.StartPoint) > 0.00001 && i != 0)
                            {
                                //Check if the line is connected to the previous line
                                //If not, add a vertex at the end of the previous line
                                lowerpline.AddVertexAt(
                                    lowerpline.NumberOfVertices,
                                    previousEnd.To2d(), 0, 0, 0);
                            }

                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                line.StartPoint.To2d(), 0, 0, 0);

                            previousEnd = line.EndPoint;
                        }
                        break;
                    case Arc arc:
                        {
                            if (previousEnd.DistanceTo(arc.StartPoint) > 0.00001 && i != 0)
                            {
                                //Check if the arc is connected to the previous line
                                //If not, add a vertex at the end of the previous line
                                lowerpline.AddVertexAt(
                                    lowerpline.NumberOfVertices,
                                    previousEnd.To2d(), 0, 0, 0);
                            }

                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                arc.StartPoint.To2d(), arc.GetBulge(), 0, 0);

                            previousEnd = arc.EndPoint;
                        }
                        break;
                    default:
                        throw new Exception(
                            $"Unsupported entity type {ent.GetType()} found in the merged region!");
                }

                //Handle last point
                var lastPt = lowerpline.GetPoint2dAt(lowerpline.NumberOfVertices - 1);
                switch (ent)
                {
                    case Line line:
                        if (line.EndPoint.DistanceTo(lastPt.To3d()) > 0.00001)
                        {
                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                line.EndPoint.To2d(), 0, 0, 0);
                        }
                        previousEnd = line.EndPoint;
                        break;
                    case Arc arc:
                        if (arc.EndPoint.DistanceTo(lastPt.To3d()) > 0.00001)
                        {
                            lowerpline.AddVertexAt(
                                lowerpline.NumberOfVertices,
                                arc.EndPoint.To2d(), 0, 0, 0);
                        }
                        previousEnd = arc.EndPoint;
                        break;
                    default:
                        break;
                }
            }

            UnfilletedPolyline = lowerpline;
        }
        internal void FilletPolyline()
        {
            if (UnfilletedPolyline == null)
            {
                FilletedPolyline = null;
                return;
            }

            AutoProfileFilleter filleter = new AutoProfileFilleter();

            // Define the callback for getting the radius.
            // This is a placeholder. You'll need to implement the actual logic 
            // based on your application's requirements for how radius changes.
            Func<Point3d, double> getRadiusCallback = (cornerPoint) =>
            {
                // Example: Constant radius for now. 
                // Replace with your actual logic e.g. based on SizeArray and cornerPoint's station.
                return 5.0; // Placeholder constant radius
            };

            FilletedPolyline = filleter.FilletPolyline(UnfilletedPolyline, getRadiusCallback);
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
                if (polylinesToOffset[i].Item2 == null)
                    throw new Exception($"The polyline {i} is null for {Name}!");

                var size = polylinesToOffset[i].Item1;
                var pline = polylinesToOffset[i].Item2;

                double offset =
                    PipeScheduleV2.PipeScheduleV2.GetCoverDepth(size.DN, size.System, size.Type) +
                    size.Kod / 1000 / 2;

                Polyline offsetPline = (Polyline)pline.Clone();

                offsetPline.TransformBy(Matrix3d.Displacement(
                    new Vector3d(0, -offset, 0)));
                offsets.Add(offsetPline);
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
        internal double RotationOfSurfaceSimplifiedAtPointProjected(Point3d point)
        {
            if (SurfacePolylineSimplified == null) throw new Exception("No surface polyline found for the pipeline!");

            var pline = SurfacePolylineSimplified;
            var pt = pline.GetClosestPointTo(point, Vector3d.YAxis, false);
            var deriv = pline.GetFirstDerivative(pt);

            var rotation = Math.Atan2(deriv.Y, deriv.X);

            return rotation;
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
        public Polygon? MergedAvoidancePolygon { get; set; } = null!;
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
        public Region? AvoidanceRegion
        {
            get
            {
                if (AvoidanceArc == null) return null;

                var curves = new DBObjectCollection { AvoidanceArc.Clone() as Polyline };

                var regions = Region.CreateFromCurves(curves);
                if (regions.Count > 0) return regions[0] as Region;

                return null;
            }
        }
        [JsonIgnore]
        public Polyline? HorizontalArcAvoidancePolyline { get; private set; }
        [JsonIgnore]
        public Polygon? HorizontalArcAvoidancePolygon { get; set; } = null;
        [JsonIgnore]
        public Region? HorizontalArcAvoidanceRegion
        {
            get
            {
                if (HorizontalArcAvoidancePolyline == null) return null;

                var curves = new DBObjectCollection { HorizontalArcAvoidancePolyline.Clone() as Polyline };
                var regions = Region.CreateFromCurves(curves);
                if (regions.Count > 0) return regions[0] as Region;
                return null;
            }
        }
        public Relation RelateUtilityPolygonTo(Polygon other)
        {
            //var relation = this.UtilityPolygon.Relate(other);
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
            if (_pipeLine.SurfaceProfile == null)
                throw new Exception("No surface profile found for the pipeline!");
            if (_pipeLine.SurfaceProfile.SurfacePolylineSimplified == null)
                throw new Exception("No surface polyline found for the pipeline!");

            var center = GetCentreOfAvoidanceArc(radius).To3d();

            var arc = new Arc(center, radius,
                (BL.To3d() - center).AngleOnPlane(new Plane()),
                (BR.To3d() - center).AngleOnPlane(new Plane()));

            return arc;
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

            Point3d rpt = new Point3d(arc.Center.X, arc.Center.Y - radius, 0);

            //Determine the rotation angle of the arc
            var rotation = _pipeLine.SurfaceProfile.RotationOfSurfaceSimplifiedAtPointProjected(rpt);

            //Rotate the arc to match the surface profile
            polyline.TransformBy(Matrix3d.Rotation(rotation, Vector3d.ZAxis, rpt));

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

            //Rotate the polyline around the bottom of the utility
            double rx = extents.MinPoint.X + (extents.MaxPoint.X - extents.MinPoint.X) / 2;
            double ry = extents.MinPoint.Y;
            Point3d rpt = new Point3d(rx, ry, 0);

            //Determine the rotation angle of the arc
            var rotation = _pipeLine.SurfaceProfile.RotationOfSurfaceSimplifiedAtPointProjected(rpt);

            //Rotate the arc to match the surface profile
            harcPolyline.TransformBy(Matrix3d.Rotation(rotation, Vector3d.ZAxis, rpt));

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