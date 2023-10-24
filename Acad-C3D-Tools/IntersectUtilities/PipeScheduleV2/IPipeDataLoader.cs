using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public interface IPipeDataLoader
    {
        void Load(IEnumerable<string> paths);
    }
}
