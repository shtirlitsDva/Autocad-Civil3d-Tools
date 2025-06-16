using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.NTS;
using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Exception = System.Exception;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AP_Utility : PipelineDataBase
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

            List<Point3d> pts = new();
            profile.IntersectWithValidation(utility, pts);

            if (pts.Count != 0) IsFloating = false;
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
}