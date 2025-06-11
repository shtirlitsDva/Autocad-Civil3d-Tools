using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    #region Core Interfaces

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

    /// <summary>
    /// Extracts segments from AutoCAD polylines
    /// </summary>
    public interface ISegmentExtractor
    {
        IList<IPolylineSegment> ExtractSegments(Polyline polyline);
    }

    /// <summary>
    /// Provides radius calculations based on geometric context
    /// </summary>
    public interface IFilletRadiusProvider
    {
        double GetRadiusAtPoint(Point2d point);
    }

    /// <summary>
    /// Strategy interface for different fillet operations
    /// </summary>
    public interface IFilletStrategy
    {
        bool CanHandle(IPolylineSegment segment1, IPolylineSegment segment2);
        FilletResult CreateFillet(IPolylineSegment segment1, IPolylineSegment segment2, double radius);
    }

    /// <summary>
    /// Builds AutoCAD polylines from segments
    /// </summary>
    public interface IPolylineBuilder
    {
        Polyline BuildPolyline(IList<IPolylineSegment> segments);
    }

    #endregion

    #region Core Data Structures

    /// <summary>
    /// Result of a fillet operation with detailed error reporting
    /// </summary>
    public class FilletResult
    {
        public bool Success { get; set; }
        public IPolylineSegment? TrimmedSegment1 { get; set; }
        public IPolylineSegment? FilletSegment { get; set; }
        public IPolylineSegment? TrimmedSegment2 { get; set; }
        public FilletFailureReason FailureReason { get; set; }
        public string? ErrorMessage { get; set; }

        public FilletResult(bool success = false)
        {
            Success = success;
            FailureReason = FilletFailureReason.None;
        }
    }

    /// <summary>
    /// Detailed failure reasons for debugging and error handling
    /// </summary>
    public enum FilletFailureReason
    {
        None,
        Seg1TooShort,
        Seg2TooShort,
        RadiusTooLarge,
        SegmentsDoNotIntersect,
        SegmentsAreTangential,
        UnsupportedSegmentTypes,
        CalculationError,
        InvalidRadius
    }

    #endregion

    #region Segment Implementations

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

    #endregion

    #region Service Implementations

    /// <summary>
    /// Extracts segments using AutoCAD's native API methods
    /// </summary>
    public class SegmentExtractor : ISegmentExtractor
    {
        public IList<IPolylineSegment> ExtractSegments(Polyline polyline)
        {
            var segments = new List<IPolylineSegment>();

            if (polyline == null || polyline.NumberOfVertices < 2)
                return segments;

            int numSegments = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;

            for (int i = 0; i < numSegments; i++)
            {
                var segmentType = polyline.GetSegmentType(i);

                switch (segmentType)
                {
                    case SegmentType.Line:
                        var lineGeometry = polyline.GetLineSegment2dAt(i);
                        segments.Add(new PolylineLineSegment(lineGeometry));
                        break;

                    case SegmentType.Arc:
                        var arcGeometry = polyline.GetArcSegment2dAt(i);
                        segments.Add(new PolylineArcSegment(arcGeometry));
                        break;

                    case SegmentType.Coincident:
                    case SegmentType.Point:
                    case SegmentType.Empty:
                        // Skip degenerate segments
                        break;

                    default:
                        throw new NotSupportedException($"Segment type {segmentType} is not supported");
                }
            }

            return segments;
        }
    }

    /// <summary>
    /// Simple radius provider using callback function
    /// </summary>
    public class RadiusProvider : IFilletRadiusProvider
    {
        private readonly Func<Point2d, double> _radiusCallback;

        public RadiusProvider(Func<Point2d, double> radiusCallback)
        {
            _radiusCallback = radiusCallback ?? throw new ArgumentNullException(nameof(radiusCallback));
        }

        public double GetRadiusAtPoint(Point2d point)
        {
            return _radiusCallback(point);
        }
    }

    /// <summary>
    /// Builds AutoCAD polylines from segments with proper bulge calculations
    /// </summary>
    public class AutoCadPolylineBuilder : IPolylineBuilder
    {
        public Polyline BuildPolyline(IList<IPolylineSegment> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Count == 0)
                throw new ArgumentException("At least one segment is required", nameof(segments));

            var polyline = new Polyline();
            int vertexIndex = 0;

            foreach (var segment in segments)
            {
                if (segment.SegmentType == SegmentType.Line)
                {
                    var lineSegment = (PolylineLineSegment)segment;
                    var lineGeom = (LineSegment2d)lineSegment.GetGeometry2d();

                    if (vertexIndex == 0)
                    {
                        polyline.AddVertexAt(vertexIndex++, lineGeom.StartPoint, 0.0, 0.0, 0.0);
                    }
                    polyline.AddVertexAt(vertexIndex++, lineGeom.EndPoint, 0.0, 0.0, 0.0);
                }
                else if (segment.SegmentType == SegmentType.Arc)
                {
                    var arcSegment = (PolylineArcSegment)segment;
                    var arcGeom = (CircularArc2d)arcSegment.GetGeometry2d();

                    if (vertexIndex == 0)
                    {
                        double bulge = CalculateBulgeFromArc(arcGeom);
                        polyline.AddVertexAt(vertexIndex++, arcGeom.StartPoint, bulge, 0.0, 0.0);
                    }
                    else
                    {
                        double bulge = CalculateBulgeFromArc(arcGeom);
                        polyline.SetBulgeAt(vertexIndex - 1, bulge);
                    }
                    polyline.AddVertexAt(vertexIndex++, arcGeom.EndPoint, 0.0, 0.0, 0.0);
                }
            }
            return polyline;
        }

        private double CalculateBulgeFromArc(CircularArc2d arc)
        {
            double sweepAngle = Math.Abs(arc.EndAngle - arc.StartAngle);
            if (sweepAngle > Math.PI)
                sweepAngle = 2 * Math.PI - sweepAngle;

            double bulge = Math.Tan(sweepAngle / 4.0);
            if (arc.IsClockWise)
                bulge = -bulge;

            return bulge;
        }
    }

    #endregion

    #region Fillet Strategies

    /// <summary>
    /// Strategy for filleting two line segments
    /// </summary>
    public class LineToLineFilletStrategy : IFilletStrategy
    {
        public bool CanHandle(IPolylineSegment segment1, IPolylineSegment segment2)
        {
            return segment1.SegmentType == SegmentType.Line && segment2.SegmentType == SegmentType.Line;
        }

        public FilletResult CreateFillet(IPolylineSegment segment1, IPolylineSegment segment2, double radius)
        {
            if (!CanHandle(segment1, segment2))
                return new FilletResult(false) { FailureReason = FilletFailureReason.UnsupportedSegmentTypes };

            if (radius <= 0)
                return new FilletResult(false) { FailureReason = FilletFailureReason.InvalidRadius };

            try
            {
                var line1 = (PolylineLineSegment)segment1;
                var line2 = (PolylineLineSegment)segment2;

                var geom1 = (LineSegment2d)line1.GetGeometry2d();
                var geom2 = (LineSegment2d)line2.GetGeometry2d();

                if (geom1.IsParallelTo(geom2) || geom1.IsColinearTo(geom2))
                    return new FilletResult(false) { FailureReason = FilletFailureReason.SegmentsAreTangential };

                double angle = geom1.Direction.GetAngleTo(geom2.Direction);
                if (angle > Math.PI) angle = 2 * Math.PI - angle;

                double tanDist = radius / Math.Tan(angle / 2.0);

                if (tanDist >= geom1.Length)
                    return new FilletResult(false) { FailureReason = FilletFailureReason.Seg1TooShort };

                if (tanDist >= geom2.Length)
                    return new FilletResult(false) { FailureReason = FilletFailureReason.Seg2TooShort };

                Point2d tangentPoint1 = geom1.EndPoint - geom1.Direction * tanDist;
                Point2d tangentPoint2 = geom2.StartPoint + geom2.Direction * tanDist;

                // Create simple fillet arc using three points
                Point2d midPoint = new Point2d((tangentPoint1.X + tangentPoint2.X) / 2, 
                                              (tangentPoint1.Y + tangentPoint2.Y) / 2);
                var filletArc = new CircularArc2d(tangentPoint1, midPoint, tangentPoint2);

                var trimmedLine1 = new LineSegment2d(geom1.StartPoint, tangentPoint1);
                var trimmedLine2 = new LineSegment2d(tangentPoint2, geom2.EndPoint);

                var trimmedSeg1 = new PolylineLineSegment(trimmedLine1);
                var trimmedSeg2 = new PolylineLineSegment(trimmedLine2);
                var filletSegment = new PolylineArcSegment(filletArc);

                return new FilletResult(true)
                {
                    TrimmedSegment1 = trimmedSeg1,
                    FilletSegment = filletSegment,
                    TrimmedSegment2 = trimmedSeg2
                };
            }
            catch (Exception ex)
            {
                return new FilletResult(false)
                {
                    FailureReason = FilletFailureReason.CalculationError,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    #endregion

    #region Strategy Manager

    /// <summary>
    /// Manages all fillet strategies using Strategy pattern
    /// </summary>
    public class FilletStrategyManager
    {
        private readonly List<IFilletStrategy> _strategies;

        public FilletStrategyManager()
        {
            _strategies = new List<IFilletStrategy>
            {
                new LineToLineFilletStrategy()
                // Additional strategies can be added here
            };
        }

        public void RegisterStrategy(IFilletStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            
            _strategies.Add(strategy);
        }

        public IFilletStrategy? GetStrategy(IPolylineSegment segment1, IPolylineSegment segment2)
        {
            return _strategies.FirstOrDefault(s => s.CanHandle(segment1, segment2));
        }

        public IReadOnlyList<IFilletStrategy> GetAllStrategies()
        {
            return _strategies.AsReadOnly();
        }
    }

    #endregion

    #region Main Filleter Class

    public class SolidAutoProfileFilleter
    {
        private readonly ISegmentExtractor _segmentExtractor;
        private readonly IFilletRadiusProvider _radiusProvider;
        private readonly IPolylineBuilder _polylineBuilder;
        private readonly FilletStrategyManager _strategyManager;

        public SolidAutoProfileFilleter(
            ISegmentExtractor segmentExtractor,
            IFilletRadiusProvider radiusProvider,
            IPolylineBuilder polylineBuilder,
            FilletStrategyManager? strategyManager = null)
        {
            _segmentExtractor = segmentExtractor ?? throw new ArgumentNullException(nameof(segmentExtractor));
            _radiusProvider = radiusProvider ?? throw new ArgumentNullException(nameof(radiusProvider));
            _polylineBuilder = polylineBuilder ?? throw new ArgumentNullException(nameof(polylineBuilder));
            _strategyManager = strategyManager ?? new FilletStrategyManager();
        }

        public static SolidAutoProfileFilleter CreateDefault(
            Func<Point2d, double> radiusCallback)
        {
            var segmentExtractor = new SegmentExtractor();
            
            var radiusProvider = new RadiusProvider(radiusCallback);
            
            var polylineBuilder = new AutoCadPolylineBuilder();

            return new SolidAutoProfileFilleter(segmentExtractor, radiusProvider, polylineBuilder);
        }

        public Polyline PerformFilleting(Polyline polyline)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));

            try
            {
                var segments = _segmentExtractor.ExtractSegments(polyline);
                var segmentList = segments.ToList();

                if (segmentList.Count < 2)
                    return (Polyline)polyline.Clone();

                var resultSegments = new List<IPolylineSegment>();

                for (int i = 0; i < segmentList.Count - 1; i++)
                {
                    var segment1 = segmentList[i];
                    var segment2 = segmentList[i + 1];

                    double radius = _radiusProvider.GetRadiusAtPoint(segment1.EndPoint);

                    var strategy = _strategyManager.GetStrategy(segment1, segment2);
                    if (strategy == null)
                    {
                        if (i == 0) resultSegments.Add(segment1);
                        resultSegments.Add(segment2);
                        continue;
                    }

                    var filletResult = strategy.CreateFillet(segment1, segment2, radius);
                    
                    if (filletResult.Success && filletResult.TrimmedSegment1 != null && 
                        filletResult.FilletSegment != null && filletResult.TrimmedSegment2 != null)
                    {
                        if (i == 0) resultSegments.Add(filletResult.TrimmedSegment1);
                        resultSegments.Add(filletResult.FilletSegment);
                        
                        if (i < segmentList.Count - 2)
                        {
                            segmentList[i + 1] = filletResult.TrimmedSegment2;
                        }
                        else
                        {
                            resultSegments.Add(filletResult.TrimmedSegment2);
                        }
                    }
                    else
                    {
                        if (i == 0) resultSegments.Add(segment1);
                        resultSegments.Add(segment2);
                    }
                }

                return _polylineBuilder.BuildPolyline(resultSegments);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Filleting operation failed: {ex.Message}", ex);
            }
        }
    }

    #endregion
}
