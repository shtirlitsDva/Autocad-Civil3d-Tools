using Microsoft.Office.Interop.Excel;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeTypeDN : PipeTypeBase
    {
        public override double GetBuerorMinRadius(int dn, int std)
        {
            DataRow[] results = _data.Select($"DN = {dn}");

            if (results != null && results.Length > 0)
            {
                double vpMax12 = (double)results[0]["VpMax12"];
                if (vpMax12 == 0) return 0;
                return (180 * std) / (Math.PI * vpMax12);
            }
            return 0;
        }
    }
}
