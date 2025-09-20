using System;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Arc segment implementation using AutoCAD's native CircularArc2d
    /// </summary>
    public class PolylineArcSegment : IPolylineSegment
    {
        private readonly CircularArc2d _geometry;
        public SegmentType SegmentType => SegmentType.Arc;

        public PolylineArcSegment(CircularArc2d geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        public Point2d StartPoint => _geometry.StartPoint;
        public Point2d EndPoint => _geometry.EndPoint;

        public double Length 
        { 
            get
            {
                var interval = _geometry.GetInterval();
                return _geometry.GetLength(interval.LowerBound, interval.UpperBound);
            }
        }

        public Curve2d GetGeometry2d() => _geometry;

        public Vector2d GetStartTangent()
        {
            var tangentLine = _geometry.GetTangent(_geometry.StartPoint);
            return tangentLine.Direction;
        }

        public Vector2d GetEndTangent()
        {
            var tangentLine = _geometry.GetTangent(_geometry.EndPoint);
            return tangentLine.Direction;
        }

        public override string ToString() => $"Arc: {StartPoint} -> {EndPoint}";
    }
}