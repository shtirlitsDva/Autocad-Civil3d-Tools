using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal static class LinkedListExtensions
    {
        internal static bool TryGetFilletCandidate(
        this LinkedList<IPolylineSegment> segs,
        HashSet<VertexKey> skipped,
        FilletStrategyManager manager,
        out (LinkedListNode<IPolylineSegment> firstNode,
             LinkedListNode<IPolylineSegment> secondNode) nodes)
        {
            nodes = default;

            for (var n = segs.First; n?.Next != null; n = n.Next)
            {
                var first = n.Value;
                var second = n.Next.Value;

                if (first.IsTangentialTo(second)) continue;
                if (skipped.Contains(VertexKey.From(first.EndPoint))) continue;
                if (manager.GetStrategy(first, second) is null) continue;
                nodes = (n, n.Next);
                return true;
            }
            return false;
        }
    }
}
