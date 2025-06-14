using System;
using System.Collections.Generic;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Result of a fillet operation with detailed error reporting
    /// </summary>
    public class FilletResultThreePart : FilletResultBase
    {
        public IPolylineSegment? OriginalFirstSegment { get; set; }
        public IPolylineSegment? OriginalSecondSegment { get; set; }
        public IPolylineSegment? TrimmedSegment1 { get; set; }
        public IPolylineSegment? FilletSegment { get; set; }
        public IPolylineSegment? TrimmedSegment2 { get; set; }
        /// <summary>
        /// Constructor for failure case, initializes all segments to null.
        /// </summary>
        public FilletResultThreePart() : base(false)
        {
            OriginalFirstSegment = null;
            OriginalSecondSegment = null;
            TrimmedSegment1 = null;
            FilletSegment = null;
            TrimmedSegment2 = null;
        }
        /// <summary>
        /// Constructor for success case, initializes all segments.
        /// </summary>        
        public FilletResultThreePart(IPolylineSegment originalFirstSegment,
            IPolylineSegment originalSecondSegment,
            IPolylineSegment trimmedSegment1,
            IPolylineSegment filletSegment,
            IPolylineSegment trimmedSegment2)
            : base(true)
        {
            OriginalFirstSegment = originalFirstSegment ?? throw new ArgumentNullException(nameof(originalFirstSegment));
            OriginalSecondSegment = originalSecondSegment ?? throw new ArgumentNullException(nameof(originalSecondSegment));
            TrimmedSegment1 = trimmedSegment1 ?? throw new ArgumentNullException(nameof(trimmedSegment1));
            FilletSegment = filletSegment ?? throw new ArgumentNullException(nameof(filletSegment));
            TrimmedSegment2 = trimmedSegment2 ?? throw new ArgumentNullException(nameof(trimmedSegment2));
        }

        public override void UpdateWithResults(
            LinkedList<IPolylineSegment> segments)
        {
            if (!Success)
                throw new InvalidOperationException("FilletResult not successful.");
            if (OriginalFirstSegment is null || OriginalSecondSegment is null)
                throw new InvalidOperationException("Original nodes not set.");
            if (TrimmedSegment1 == null || FilletSegment == null || TrimmedSegment2 == null)
                throw new InvalidOperationException("Fillet segments not set.");

            var first = segments.Find(OriginalFirstSegment);
            if (first is null)
                throw new InvalidOperationException("Original first segment not found in segments.");
            var second = segments.Find(OriginalSecondSegment);
            if (second is null)
                throw new InvalidOperationException("Original second segment not found in segments.");

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
