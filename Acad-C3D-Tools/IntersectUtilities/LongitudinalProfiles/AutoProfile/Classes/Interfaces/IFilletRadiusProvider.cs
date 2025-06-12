using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Provides radius calculations based on geometric context
    /// </summary>
    public interface IFilletRadiusProvider
    {
        double GetRadiusAtPoint(Point2d point);
    }
    #endregion
}
