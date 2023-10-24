using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public interface IPipeRepository
    {
        void Initialize(string type, DataTable pipeTypes);
        //PipeType GetPipeType(string layerName);
    }
}
