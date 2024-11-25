using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Dimensionering.DimensioneringV2.Geometry;

using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Dimensionering.DimensioneringV2.GraphModelRoads
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
        public double DistanceToSegment(SegmentNode other, SpatialIndex noCrossIndex)
        {
            var line1 = ToLineString();
            var line2 = other.ToLineString();

            if (noCrossIndex != null)
            {
                DistanceOp dOp = new DistanceOp(line1, line2);
                Coordinate[] closestPt = dOp.NearestPoints();
                //var nLine = new LineString([closestPt[0], closestPt[1]]);
                var seg = new SegmentNode(
                    new Point2D(closestPt[0].X, closestPt[0].Y),
                    new Point2D(closestPt[1].X, closestPt[1].Y));

                //Line line = new Line(
                //    new Point3d(closestPt[0].X, closestPt[0].Y, 0),
                //    new Point3d(closestPt[1].X, closestPt[1].Y, 0));
                //line.AddEntityToDbModelSpace(HostApplicationServices.WorkingDatabase);

                var nearestNoCross = noCrossIndex.FindNearest(seg);
                if (nearestNoCross != null)
                {
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

            if (lenSq == 0)
                return 0;

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
    }
}
