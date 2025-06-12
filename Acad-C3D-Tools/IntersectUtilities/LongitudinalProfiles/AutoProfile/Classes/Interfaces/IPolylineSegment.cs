using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Represents a segment of a polyline with native AutoCAD geometry support
    /// </summary>
    public interface IPolylineSegment
    {
        SegmentType SegmentType { get; }
        Point2d StartPoint { get; }
        Point2d EndPoint { get; }
        double Length { get; }

        /// <summary>
        /// Gets the native 2D geometry for AutoCAD calculations
        /// </summary>
        Curve2d GetGeometry2d();

        /// <summary>
        /// Gets tangent vector at start using AutoCAD's native methods
        /// </summary>
        Vector2d GetStartTangent();

        /// <summary>
        /// Gets tangent vector at end using AutoCAD's native methods
        /// </summary>
        Vector2d GetEndTangent();
    }
    #endregion
}
