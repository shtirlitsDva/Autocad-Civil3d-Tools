using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public interface IPipeTypeRepository
    {
        void Initialize(Dictionary<string, IPipeType> pipeTypeDict);
        IPipeType GetPipeType(string type);
        IEnumerable<string> ListAllPipeTypes();
    }
}
