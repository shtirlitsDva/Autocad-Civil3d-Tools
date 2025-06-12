namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Strategy interface for different fillet operations
    /// </summary>
    public interface IFilletStrategy
    {
        bool CanHandle(IPolylineSegment segment1, IPolylineSegment segment2);
        FilletResult CreateFillet(IPolylineSegment segment1, IPolylineSegment segment2, double radius);
    }
    #endregion
}
