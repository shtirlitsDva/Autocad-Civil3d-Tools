using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DimensioneringV2.Geometry;

using IntersectUtilities.UtilsCommon;
using dbg = IntersectUtilities.UtilsCommon.Utils.DebugHelper;
using utils = IntersectUtilities.UtilsCommon.Utils;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace DimensioneringV2.GraphModelRoads
{
    internal class SegmentNode : IBoundable<Envelope, SegmentNode>
    {
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public List<SegmentNode> Neighbors { get; set; }
        public bool IsBuildingConnection { get; internal set; }
        public ObjectId BuildingId { get; internal set; }
        public Envelope Bounds {
            get
            {
                var envelope = new Envelope(StartPoint.X, EndPoint.X, StartPoint.Y, EndPoint.Y);
                envelope.ExpandToInclude(EndPoint.X, EndPoint.Y);
                return envelope;
            }
        }
        public SegmentNode Item => this;

        public bool IsRoot { get; internal set; }

        public SegmentNode(Point2d startPoint, Point2d endPoint)
        {
            StartPoint = new Point2D(startPoint.X, startPoint.Y);
            EndPoint = new Point2D(endPoint.X, endPoint.Y);
            Neighbors = new List<SegmentNode>();
            IsBuildingConnection = false;
        }
        public SegmentNode(Point2D startPoint, Point2D endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            Neighbors = new List<SegmentNode>();
            IsBuildingConnection = false;
        }
        public void AddNeighbor(SegmentNode neighbor)
        {
            if (!Neighbors.Contains(neighbor))
            {
                Neighbors.Add(neighbor);
            }
        }
        public Point2D GetMidpoint()
        {
            return new Point2D(
                (StartPoint.X + EndPoint.X) / 2,
                (StartPoint.Y + EndPoint.Y) / 2
            );
        }
        public LineString ToLineString()
        {
            return new LineString(
            [
                new Coordinate(StartPoint.X, StartPoint.Y),
                new Coordinate(EndPoint.X, EndPoint.Y)
            ]);
        }
        // Calculate distance squared from a point to the segment
        public double DistanceSquaredToPoint(Point2D point)
        {
            // Vector from start to end
            double dx = EndPoint.X - StartPoint.X;
            double dy = EndPoint.Y - StartPoint.Y;

            // Vector from start to point
            double tdx = point.X - StartPoint.X;
            double tdy = point.Y - StartPoint.Y;

            // Project point onto the segment, clamping t between 0 and 1
            double dot = tdx * dx + tdy * dy;
            double lenSq = dx * dx + dy * dy;
            double t = lenSq > 0 ? dot / lenSq : 0;
            t = Math.Max(0, Math.Min(1, t));

            // Closest point on the segment
            double closestX = StartPoint.X + t * dx;
            double closestY = StartPoint.Y + t * dy;

            // Distance squared from the point to the closest point
            double distSq = (point.X - closestX) * (point.X - closestX) + (point.Y - closestY) * (point.Y - closestY);

            return distSq;
        }
        public double DistanceToPoint(Point2D point)
        {
            var pointGeometry = new Point(point.X, point.Y);
            var line = ToLineString();
            return line.Distance(pointGeometry);
        }
        /// <summary>
        /// Calculates the distance between this segment and another, with optional filtering
        /// for projection lines that cross forbidden segments.
        /// </summary>
        /// <param name="other">The other segment to calculate distance to.</param>
        /// <param name="noCrossIndex">
        /// Optional spatial index containing forbidden segments. If provided, the method checks
        /// whether the "projection line" (the line connecting the nearest points between this
        /// segment and <paramref name="other"/>) intersects any segment in this index.
        /// </param>
        /// <returns>
        /// The geometric distance between the two segments, or <see cref="double.MaxValue"/> if
        /// the projection line crosses any segment in <paramref name="noCrossIndex"/>.
        /// Returning MaxValue effectively disqualifies this candidate during R-tree nearest
        /// neighbor searches, causing the algorithm to find the next nearest valid segment.
        /// </returns>
        /// <remarks>
        /// The crossing check works by:
        /// 1. Computing the nearest points between the two line segments using DistanceOp
        /// 2. Creating a temporary segment from these two closest points (the "projection line")
        /// 3. Finding the nearest segment in noCrossIndex to this projection line
        /// 4. If distance to nearest forbidden segment is 0 (they touch/cross), return MaxValue
        /// </remarks>
        public double DistanceToSegment(SegmentNode other, SpatialIndex noCrossIndex)
        {
            var line1 = ToLineString();
            var line2 = other.ToLineString();

            if (noCrossIndex != null)
            {
                // Step 1: Find the nearest points between the two segments
                DistanceOp dOp = new DistanceOp(line1, line2);
                Coordinate[] closestPt = dOp.NearestPoints();

                // Step 2: Create a "projection line" segment from query to candidate
                var seg = new SegmentNode(
                    new Point2D(closestPt[0].X, closestPt[0].Y),
                    new Point2D(closestPt[1].X, closestPt[1].Y));

                // Step 3: Check if projection line crosses any forbidden segment
                var nearestNoCross = noCrossIndex.FindNearest(seg);
                if (nearestNoCross != null)
                {
                    // Step 4: Distance of 0 means they touch/cross - disqualify this candidate
                    if (nearestNoCross.DistanceToSegment(seg, null) == 0)
                    {
                        return double.MaxValue;
                    }
                }
            }

            return line1.Distance(line2);
        }
        public double GetParameterAtPoint(Point2D point)
        {
            double dx = EndPoint.X - StartPoint.X;
            double dy = EndPoint.Y - StartPoint.Y;
            double lenSq = dx * dx + dy * dy;

            if (lenSq == 0) return 0;

            double t = ((point.X - StartPoint.X) * dx + (point.Y - StartPoint.Y) * dy) / lenSq;
            return t;
        }
        public Point2D GetNearestPoint(Point2D pt)
        {
            double dx = EndPoint.X - StartPoint.X;
            double dy = EndPoint.Y - StartPoint.Y;

            double tdx = pt.X - StartPoint.X;
            double tdy = pt.Y - StartPoint.Y;

            double dot = tdx * dx + tdy * dy;
            double lenSq = dx * dx + dy * dy;
            double t = lenSq > 0 ? dot / lenSq : 0;
            t = Math.Max(0, Math.Min(1, t));

            double closestX = StartPoint.X + t * dx;
            double closestY = StartPoint.Y + t * dy;

            return new Point2D(closestX, closestY);
        }
        public override string ToString()
        {
            return $"Start:\n{StartPoint}\nEnd:\n{EndPoint}";
        }
        public string Print()
        {
            return $"n: {Neighbors.Count}";
        }
        public Point2D GetOtherEnd(Point2D point)
        {
            if (point.Equals(StartPoint))
            {
                return EndPoint;
            }
            else if (point.Equals(EndPoint))
            {
                return StartPoint;
            }
            else
            {
                dbg.CreateDebugLine(
                    StartPoint.To3d(), utils.ColorByName("red"));
                dbg.CreateDebugLine(
                    EndPoint.To3d(), utils.ColorByName("red"));
                dbg.CreateDebugLine(
                    point.To3d(), utils.ColorByName("blue"));
                throw new ArgumentException("DBG: Point is not an end of the segment");
            }
        }
        public bool HasPoint(Point2D point)
        {
            return StartPoint.Equals(point) || EndPoint.Equals(point);
        }
        public void MakePointStart(Point2D point)
        {
            if (EndPoint.Equals(point))
            {
                var temp = StartPoint;
                StartPoint = EndPoint;
                EndPoint = temp;
            }
        }
    }
}
