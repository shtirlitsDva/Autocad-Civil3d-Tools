using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal interface ITriageStrategy
    {
        bool CanHandle(IFilletStrategy filletStrategy, FilletFailureReason reason);
        IFilletResult Triage(
            (LinkedListNode<IPolylineSegment> firstNode,
             LinkedListNode<IPolylineSegment> secondNode) nodes,
            double radius);
    }
}
