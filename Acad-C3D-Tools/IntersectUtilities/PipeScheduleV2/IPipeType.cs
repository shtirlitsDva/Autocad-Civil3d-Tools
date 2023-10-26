using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public interface IPipeType
    {
        void Initialize(DataTable table);
        double GetPipeOd(int dn);
        UtilsCommon.Utils.PipeSeriesEnum GetPipeSeries(
            int dn, UtilsCommon.Utils.PipeTypeEnum type, double realKod);
        double GetPipeKOd(int dn, UtilsCommon.Utils.PipeTypeEnum type, UtilsCommon.Utils.PipeSeriesEnum pipeSeries);
        double GetMinElasticRadius(int dn, UtilsCommon.Utils.PipeTypeEnum type, UtilsCommon.Utils.PipeSeriesEnum series);
        double GetBuerorMinRadius(int dn, int std);
    }
}
