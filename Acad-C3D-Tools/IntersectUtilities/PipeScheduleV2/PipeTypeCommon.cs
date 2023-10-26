using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeTypeCommon : PipeTypeBase
    {
        public override double GetBuerorMinRadius(int dn, int std) => 0.0;
    }
}
