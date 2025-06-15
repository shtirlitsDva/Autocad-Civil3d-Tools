using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{    
    public class SegmentExtractor : ISegmentExtractor
    {
        public LinkedList<IPolylineSegment> ExtractSegments(Polyline polyline)
        {
            var segments = new LinkedList<IPolylineSegment>();
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
                        segments.AddLast(new PolylineLineSegment(lineGeometry));
                        break;
                    case SegmentType.Arc:
                        var arcGeometry = polyline.GetArcSegment2dAt(i);
                        segments.AddLast(new PolylineArcSegment(arcGeometry));
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
}
