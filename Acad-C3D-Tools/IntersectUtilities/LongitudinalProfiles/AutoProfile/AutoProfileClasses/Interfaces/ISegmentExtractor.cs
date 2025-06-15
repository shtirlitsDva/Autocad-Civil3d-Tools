using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Extracts segments from AutoCAD polylines
    /// </summary>
    public interface ISegmentExtractor
    {
        LinkedList<IPolylineSegment> ExtractSegments(Polyline polyline);
    }    
}
