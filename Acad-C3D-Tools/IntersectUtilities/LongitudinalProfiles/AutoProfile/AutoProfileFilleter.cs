using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Enum to represent segment types in the polyline
    /// </summary>
    internal enum SegmentGeometryType
    {
        Line,
        Arc
    }

    /// <summary>
    /// Reasons why a filleting operation might fail
    /// </summary>
    internal enum FilletFailureReason
    {
        None,
        Seg1TooShort,
        Seg2TooShort,
        RadiusTooLarge,
        SegmentsDoNotIntersect,
        SegmentsAreTangential,
        UnsupportedSegmentTypes,
        CalculationError
    }

    /// <summary>
    /// Class to hold information about an individual segment of a polyline
    /// </summary>
    internal class PolylineSegmentAdapter
    {
        public Point3d StartPoint { get; }
        public Point3d EndPoint { get; }
        public SegmentGeometryType SegmentType { get; }
        public double Bulge { get; } // Relevant for arcs

        // Constructor for a line segment
        public PolylineSegmentAdapter(Point3d startPoint, Point3d endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            SegmentType = SegmentGeometryType.Line;
            Bulge = 0;
        }

        // Constructor for an arc segment
        public PolylineSegmentAdapter(Point3d startPoint, Point3d endPoint, double bulge)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            SegmentType = SegmentGeometryType.Arc;
            Bulge = bulge;
        }

        /// <summary>
        /// Gets the length of the segment
        /// </summary>
        public double Length
        {
            get
            {
                if (SegmentType == SegmentGeometryType.Line)
                {
                    return StartPoint.DistanceTo(EndPoint);
                }
                else // Arc
                {
                    // Arc length calculation based on bulge
                    if (Math.Abs(Bulge) < 1e-9) return StartPoint.DistanceTo(EndPoint);

                    double chord = StartPoint.DistanceTo(EndPoint);
                    double includedAngle = 4 * Math.Atan(Math.Abs(Bulge));
                    double radius = (chord / 2) / Math.Sin(includedAngle / 2);
                    
                    return radius * includedAngle;
                }
            }
        }

        /// <summary>
        /// For Line segments, returns the direction vector
        /// </summary>
        public Vector3d Direction
        {
            get
            {
                if (SegmentType != SegmentGeometryType.Line)
                    throw new InvalidOperationException("Direction is only valid for Line segments.");
                
                return (EndPoint - StartPoint).GetNormal();
            }
        }
        
        public override string ToString()
        {
            return $"{SegmentType}: {StartPoint} -> {EndPoint}{(SegmentType == SegmentGeometryType.Arc ? $", Bulge: {Bulge}" : "")}";
        }
    }

    /// <summary>
    /// Result of a filleting operation with detailed information
    /// </summary>
    internal class FilletOperationResult
    {
        public bool Success { get; set; }
        public PolylineSegmentAdapter TrimmedSegment1 { get; set; }
        public PolylineSegmentAdapter FilletArcSegment { get; set; }
        public PolylineSegmentAdapter TrimmedSegment2 { get; set; }
        public FilletFailureReason FailureReason { get; set; }

        public FilletOperationResult(bool success = false)
        {
            Success = success;
            FailureReason = FilletFailureReason.None;
        }
    }

    /// <summary>
    /// Class for filleting polylines with variable radius
    /// </summary>
    internal class AutoProfileFilleter
    {
        /// <summary>
        /// Main method to fillet a polyline.
        /// </summary>
        /// <param name="sourcePolyline">Input polyline with lines and arcs</param>
        /// <param name="getRadiusAtCorner">Callback function to get the fillet radius at a given corner point</param>
        /// <returns>A new filleted polyline</returns>
        public Polyline FilletPolyline(Polyline sourcePolyline, Func<Point3d, double> getRadiusAtCorner)
        {
            if (sourcePolyline == null || sourcePolyline.NumberOfVertices < 2)
            {
                return sourcePolyline?.Clone() as Polyline ?? new Polyline();
            }

            // Extract segments from the source polyline
            List<PolylineSegmentAdapter> segments = ExtractSegments(sourcePolyline);
            
            if (segments.Count < 2)
            {
                return sourcePolyline.Clone() as Polyline;
            }

            // Process segments to create fillets
            List<PolylineSegmentAdapter> resultSegments = ProcessSegmentsWithFillets(segments, getRadiusAtCorner);
            
            // Build a new polyline from the processed segments
            return BuildPolylineFromSegments(resultSegments);
        }

        /// <summary>
        /// Process the segments and insert fillets where needed
        /// </summary>
        private List<PolylineSegmentAdapter> ProcessSegmentsWithFillets(
            List<PolylineSegmentAdapter> segments, 
            Func<Point3d, double> getRadiusAtCorner)
        {
            List<PolylineSegmentAdapter> resultSegments = new List<PolylineSegmentAdapter>();
            
            if (segments.Count == 0) return resultSegments;
            
            // Add the first segment to start
            PolylineSegmentAdapter currentSegment = segments[0];
            
            for (int i = 0; i < segments.Count - 1; i++)
            {
                PolylineSegmentAdapter nextSegment = segments[i + 1];
                Point3d cornerPoint = currentSegment.EndPoint; // This is where segments meet
                
                // Check if the segments are tangential (no need for fillet)
                if (AreSegmentsTangential(currentSegment, nextSegment))
                {
                    // No fillet needed, add the current segment and continue
                    resultSegments.Add(currentSegment);
                    currentSegment = nextSegment;
                    continue;
                }
                
                // Get the radius for this corner
                double radius = getRadiusAtCorner(cornerPoint);
                
                // Try to create a fillet
                FilletOperationResult filletResult = CreateFilletArcAndTrimSegments(
                    currentSegment, nextSegment, radius, cornerPoint);
                
                if (filletResult.Success)
                {
                    // If successful, add the trimmed first segment and the fillet arc
                    resultSegments.Add(filletResult.TrimmedSegment1);
                    resultSegments.Add(filletResult.FilletArcSegment);
                    
                    // The trimmed second segment becomes the current segment for the next iteration
                    currentSegment = filletResult.TrimmedSegment2;
                }
                else
                {
                    // If filleting failed, add the current segment as is
                    resultSegments.Add(currentSegment);
                    currentSegment = nextSegment;
                }
            }
            
            // Add the last segment
            resultSegments.Add(currentSegment);
            
            return resultSegments;
        }

        /// <summary>
        /// Check if two segments are tangential at their junction point
        /// </summary>
        private bool AreSegmentsTangential(PolylineSegmentAdapter seg1, PolylineSegmentAdapter seg2)
        {
            // For simplicity in this initial implementation, we'll just check if both segments are lines
            // and their directions are the same or opposite (collinear)
            if (seg1.SegmentType == SegmentGeometryType.Line && 
                seg2.SegmentType == SegmentGeometryType.Line)
            {
                Vector3d dir1 = seg1.Direction;
                Vector3d dir2 = seg2.Direction;
                
                // Check if directions are parallel (same or opposite)
                double dot = Math.Abs(dir1.DotProduct(dir2));
                return Math.Abs(dot - 1.0) < 1e-6;
            }
            
            // For line-arc, arc-line, arc-arc combinations, 
            // we would need more sophisticated tangency checks
            // For now, assume they're not tangential
            return false;
        }

        /// <summary>
        /// Create a fillet arc between two segments and trim the segments accordingly
        /// </summary>
        private FilletOperationResult CreateFilletArcAndTrimSegments(
            PolylineSegmentAdapter seg1, 
            PolylineSegmentAdapter seg2, 
            double radius, 
            Point3d cornerPoint)
        {
            // For this initial implementation, we'll handle the simplest case: line-line filleting
            // More complex cases would be added later
            if (seg1.SegmentType != SegmentGeometryType.Line || 
                seg2.SegmentType != SegmentGeometryType.Line)
            {
                return new FilletOperationResult(false) 
                { 
                    FailureReason = FilletFailureReason.UnsupportedSegmentTypes 
                };
            }

            // Check if radius is valid
            if (radius <= 0)
            {
                return new FilletOperationResult(false) 
                { 
                    FailureReason = FilletFailureReason.RadiusTooLarge 
                };
            }

            try
            {
                // Get directions of the two lines
                Vector3d dir1 = seg1.Direction;
                Vector3d dir2 = seg2.Direction;
                
                // Calculate the angle between the two lines
                double angle = dir1.GetAngleTo(dir2);
                
                // If lines are nearly parallel, no fillet is possible
                if (angle < 1e-6 || Math.Abs(angle - Math.PI) < 1e-6)
                {
                    return new FilletOperationResult(false) 
                    { 
                        FailureReason = FilletFailureReason.SegmentsAreTangential 
                    };
                }

                // Calculate tangent distances from corner point
                double tanDist = radius / Math.Tan(angle / 2.0);
                
                // Check if segments are long enough
                if (tanDist >= seg1.Length)
                {
                    return new FilletOperationResult(false) 
                    { 
                        FailureReason = FilletFailureReason.Seg1TooShort 
                    };
                }
                
                if (tanDist >= seg2.Length)
                {
                    return new FilletOperationResult(false) 
                    { 
                        FailureReason = FilletFailureReason.Seg2TooShort 
                    };
                }

                // Calculate tangent points on both lines
                Point3d tangentPoint1 = cornerPoint - dir1 * tanDist;
                Point3d tangentPoint2 = cornerPoint + dir2 * tanDist;
                
                // Calculate the fillet arc center
                // The center is at the intersection of two lines perpendicular to 
                // the segments at the tangent points
                Vector3d perpDir1 = new Vector3d(-dir1.Y, dir1.X, 0).GetNormal();
                Vector3d perpDir2 = new Vector3d(-dir2.Y, dir2.X, 0).GetNormal();
                
                // Ensure the perpendicular vectors point toward the inside of the corner
                if (perpDir1.DotProduct(dir2) < 0) perpDir1 = -perpDir1;
                if (perpDir2.DotProduct(-dir1) < 0) perpDir2 = -perpDir2;
                
                Point3d centerPoint = FindIntersection(
                    tangentPoint1, perpDir1, 
                    tangentPoint2, perpDir2);

                // Calculate the start and end angles for the fillet arc
                double startAngle = (tangentPoint1 - centerPoint).GetAngleToPlan(Vector3d.ZAxis);
                double endAngle = (tangentPoint2 - centerPoint).GetAngleToPlan(Vector3d.ZAxis);
                
                // Determine the appropriate bulge for the fillet arc
                bool isCCW = IsCounterClockwise(centerPoint, tangentPoint1, tangentPoint2);
                double bulge = Math.Tan((endAngle - startAngle) / 4.0);
                if (!isCCW) bulge = -bulge;
                
                // Create the trimmed segments and the fillet arc
                PolylineSegmentAdapter trimmedSeg1 = new PolylineSegmentAdapter(
                    seg1.StartPoint, tangentPoint1);
                
                PolylineSegmentAdapter filletArc = new PolylineSegmentAdapter(
                    tangentPoint1, tangentPoint2, bulge);
                
                PolylineSegmentAdapter trimmedSeg2 = new PolylineSegmentAdapter(
                    tangentPoint2, seg2.EndPoint);
                
                return new FilletOperationResult(true)
                {
                    TrimmedSegment1 = trimmedSeg1,
                    FilletArcSegment = filletArc,
                    TrimmedSegment2 = trimmedSeg2
                };
            }
            catch (Exception)
            {
                return new FilletOperationResult(false) 
                { 
                    FailureReason = FilletFailureReason.CalculationError 
                };
            }
        }

        /// <summary>
        /// Find the intersection point of two lines defined by a point and direction
        /// </summary>
        private Point3d FindIntersection(Point3d p1, Vector3d v1, Point3d p2, Vector3d v2)
        {
            // Convert to 2D for simplicity
            Point2d p1_2d = new Point2d(p1.X, p1.Y);
            Point2d p2_2d = new Point2d(p2.X, p2.Y);
            Vector2d v1_2d = new Vector2d(v1.X, v1.Y);
            Vector2d v2_2d = new Vector2d(v2.X, v2.Y);
            
            // Create line segments
            Line2d line1 = new Line2d(p1_2d, v1_2d);
            Line2d line2 = new Line2d(p2_2d, v2_2d);
            
            // Get intersection
            Point2d? intersection = line1.IntersectWith(line2);
            if (!intersection.HasValue)
                throw new InvalidOperationException("Lines do not intersect.");
            
            return new Point3d(intersection.Value.X, intersection.Value.Y, p1.Z);
        }

        /// <summary>
        /// Determine if three points are in counter-clockwise order
        /// </summary>
        private bool IsCounterClockwise(Point3d center, Point3d p1, Point3d p2)
        {
            // Convert to 2D for simplicity
            Vector2d v1 = new Vector2d(p1.X - center.X, p1.Y - center.Y);
            Vector2d v2 = new Vector2d(p2.X - center.X, p2.Y - center.Y);
            
            // Cross product sign indicates direction
            return (v1.X * v2.Y - v1.Y * v2.X) > 0;
        }

        /// <summary>
        /// Extract individual segments from a polyline
        /// </summary>
        private List<PolylineSegmentAdapter> ExtractSegments(Polyline polyline)
        {
            List<PolylineSegmentAdapter> segments = new List<PolylineSegmentAdapter>();
            
            if (polyline == null || polyline.NumberOfVertices < 2) 
                return segments;

            // For each vertex except the last, extract the segment starting from it
            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                Point2d ptStart = polyline.GetPoint2dAt(i);
                Point2d ptEnd = polyline.GetPoint2dAt(i + 1);
                double bulge = polyline.GetBulgeAt(i);
                
                Point3d pt1 = new Point3d(ptStart.X, ptStart.Y, polyline.Elevation);
                Point3d pt2 = new Point3d(ptEnd.X, ptEnd.Y, polyline.Elevation);
                
                if (Math.Abs(bulge) < 1e-9) // Treat very small bulges as lines
                {
                    segments.Add(new PolylineSegmentAdapter(pt1, pt2));
                }
                else
                {
                    segments.Add(new PolylineSegmentAdapter(pt1, pt2, bulge));
                }
            }
            
            // If polyline is closed, add the segment from last to first vertex
            if (polyline.Closed && polyline.NumberOfVertices > 1)
            {
                Point2d ptLast = polyline.GetPoint2dAt(polyline.NumberOfVertices - 1);
                Point2d ptFirst = polyline.GetPoint2dAt(0);
                double bulge = polyline.GetBulgeAt(polyline.NumberOfVertices - 1);
                
                Point3d pLast = new Point3d(ptLast.X, ptLast.Y, polyline.Elevation);
                Point3d pFirst = new Point3d(ptFirst.X, ptFirst.Y, polyline.Elevation);
                
                if (Math.Abs(bulge) < 1e-9)
                {
                    segments.Add(new PolylineSegmentAdapter(pLast, pFirst));
                }
                else
                {
                    segments.Add(new PolylineSegmentAdapter(pLast, pFirst, bulge));
                }
            }
            
            return segments;
        }

        /// <summary>
        /// Build an AutoCAD polyline from a list of segments
        /// </summary>
        private Polyline BuildPolylineFromSegments(List<PolylineSegmentAdapter> segments)
        {
            Polyline newPline = new Polyline();
            
            if (segments == null || segments.Count == 0)
                return newPline;

            // Add the first point
            newPline.AddVertexAt(0, segments[0].StartPoint.ToPoint2d(), 0, 0, 0);
            
            // Add each segment
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                double bulge = (segment.SegmentType == SegmentGeometryType.Arc) ? segment.Bulge : 0;
                
                // If this is the first point of the polyline, we've already added it
                if (i == 0)
                {
                    // Set the bulge for the first segment
                    newPline.SetBulgeAt(0, bulge);
                }
                
                // Add the endpoint of this segment
                newPline.AddVertexAt(newPline.NumberOfVertices, segment.EndPoint.ToPoint2d(), 
                                    (i < segments.Count - 1) ? segments[i + 1].Bulge : 0, 0, 0);
            }
            
            return newPline;
        }
    }
}