using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal interface IFilletResult
    {
        public bool Success { get; }
        public FilletFailureReason FailureReason { get; }        
        public string? ErrorMessage { get; }
        public void UpdateWithResults(
            LinkedList<IPolylineSegment> segments);
    }
}
