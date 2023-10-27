using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    internal class PipeTypePEXU : PipeTypeBase
    {
        public override double GetBuerorMinRadius(int dn, int std) => 0;
    }
}
