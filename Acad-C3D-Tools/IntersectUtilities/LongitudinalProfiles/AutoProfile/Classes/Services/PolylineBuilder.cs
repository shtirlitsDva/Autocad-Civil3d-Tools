using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Builds AutoCAD polylines from segments with proper bulge calculations
    /// </summary>
    internal class PolylineBuilder : IPolylineBuilder
    {
        public Polyline BuildPolyline(ICollection<IPolylineSegment> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Count == 0)
                throw new ArgumentException("At least one segment is required", nameof(segments));

            var polyline = new Polyline();            

            foreach (var segment in segments)
            {
                if (segment.SegmentType == SegmentType.Line)
                {
                    var lineSegment = (PolylineLineSegment)segment;
                    var lineGeom = (LineSegment2d)lineSegment.GetGeometry2d();

                    if (polyline.NumberOfVertices == 0)
                    {
                        polyline.AddVertexAt(polyline.NumberOfVertices, lineGeom.StartPoint, 0.0, 0.0, 0.0);
                    }
                    polyline.AddVertexAt(polyline.NumberOfVertices, lineGeom.EndPoint, 0.0, 0.0, 0.0);
                }
                else if (segment.SegmentType == SegmentType.Arc)
                {
                    var arcSegment = (PolylineArcSegment)segment;
                    var arcGeom = (CircularArc2d)arcSegment.GetGeometry2d();
                    double bulge = CalculateBulgeFromArc(arcGeom);
                    
                    if (polyline.NumberOfVertices == 0)
                    {                        
                        polyline.AddVertexAt(polyline.NumberOfVertices, arcGeom.StartPoint, bulge, 0.0, 0.0);
                    }
                    else
                    {
                        polyline.SetBulgeAt(polyline.NumberOfVertices - 1, bulge);
                    }
                    polyline.AddVertexAt(polyline.NumberOfVertices, arcGeom.EndPoint, 0.0, 0.0, 0.0);
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
            if (arc.IsClockWise) bulge = -bulge;

            return bulge;
        }
    }
}
