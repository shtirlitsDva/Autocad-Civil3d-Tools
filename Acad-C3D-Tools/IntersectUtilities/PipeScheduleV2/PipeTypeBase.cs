using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public abstract class PipeTypeBase : IPipeType
    {
        private DataTable _data;
        public void Initialize(DataTable table) => _data = table;
    }
}
