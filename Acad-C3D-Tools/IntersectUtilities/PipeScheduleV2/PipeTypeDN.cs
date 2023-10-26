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
            switch (std)
            {
                case 12:
                    if (results != null && results.Length > 0) return (double)results[0]["VpMax12"];
                    return 0;
                case 16:
                    if (results != null && results.Length > 0) return (double)results[0]["VpMax16"];
                    return 0;
            }
            return 0;
        }
    }
}
