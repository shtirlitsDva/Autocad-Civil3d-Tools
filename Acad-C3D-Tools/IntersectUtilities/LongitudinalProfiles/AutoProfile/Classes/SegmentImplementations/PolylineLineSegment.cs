using System;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Line segment implementation using AutoCAD's native LineSegment2d
    /// </summary>
    public class PolylineLineSegment : IPolylineSegment
    {
        private readonly LineSegment2d _geometry;
        public SegmentType SegmentType => SegmentType.Line;

        public PolylineLineSegment(LineSegment2d geometry)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        public Point2d StartPoint => _geometry.StartPoint;
        public Point2d EndPoint => _geometry.EndPoint;
        public double Length => _geometry.Length;

        public Curve2d GetGeometry2d() => _geometry;
        public Vector2d GetStartTangent() => _geometry.Direction;
        public Vector2d GetEndTangent() => _geometry.Direction;

        public override string ToString() => $"Line: {StartPoint} -> {EndPoint}";
    }
    #endregion
}
