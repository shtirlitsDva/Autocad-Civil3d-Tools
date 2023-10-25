using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeTypeCommon : IPipeType
    {
        private DataTable _data;
        public void Initialize(DataTable table) => _data = table;
    }
}
