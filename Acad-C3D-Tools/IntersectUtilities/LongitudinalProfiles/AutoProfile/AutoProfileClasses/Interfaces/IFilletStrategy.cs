namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Strategy interface for different fillet operations
    /// </summary>
    internal interface IFilletStrategy
    {
        bool CanHandle(IPolylineSegment segment1, IPolylineSegment segment2);
        IFilletResult CreateFillet(IPolylineSegment segment1, IPolylineSegment segment2, double radius);
    }
}