using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Builds AutoCAD polylines from segments
    /// </summary>
    public interface IPolylineBuilder
    {
        Polyline BuildPolyline(IList<IPolylineSegment> segments);
    }
    #endregion
}
