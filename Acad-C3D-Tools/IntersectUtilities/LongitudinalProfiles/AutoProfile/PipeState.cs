using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal record PipeState(double X, double Y, double ThetaDeg, double Cost, PipeState? Parent);
}
