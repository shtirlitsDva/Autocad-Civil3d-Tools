using System;
using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Result of a fillet operation with detailed error reporting
    /// </summary>
    public class FilletResultThreePart : FilletResultBase
    {
        
        public IPolylineSegment? TrimmedSegment1 { get; set; }
        public IPolylineSegment? FilletSegment { get; set; }
        public IPolylineSegment? TrimmedSegment2 { get; set; }
        public FilletResultThreePart(bool success = false) : base(success)
        {
            TrimmedSegment1 = null;
            FilletSegment = null;
            TrimmedSegment2 = null;
        }

        public override void UpdateWithResults(
            LinkedList<IPolylineSegment> segments, 
            (LinkedListNode<IPolylineSegment> firstNode, 
            LinkedListNode<IPolylineSegment> secondNode) originalNodes)
        {
            if (!Success)
                throw new InvalidOperationException("FilletResult not successful.");
            if (TrimmedSegment1 == null || FilletSegment == null || TrimmedSegment2 == null)
                throw new InvalidOperationException("Fillet segments not set.");

            var first = originalNodes.firstNode;
            var second = originalNodes.secondNode;

            // verify that first precedes second—no goto needed
            bool inOrder = false;
            for (var n = first; n is not null && !inOrder; n = n.Next)
                inOrder = n == second;

            if (!inOrder)
                throw new ArgumentException("firstNode must precede secondNode.");            

            // replace boundary segments
            first.Value = TrimmedSegment1;
            second.Value = TrimmedSegment2;

            // remove any nodes strictly between first and second
            for (var cur = first.Next; cur != second;)
            {
                if (cur is null)
                    throw new InvalidOperationException(
                        "LinkedList structure corrupted: no node found between first and second.");

                var toRemove = cur;                
                cur = cur.Next;
                segments.Remove(toRemove);
            }

            // insert the fillet arc between the two trimmed segments
            segments.AddAfter(first, FilletSegment);
        }
    }    
}
