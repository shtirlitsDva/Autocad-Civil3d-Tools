using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    internal class PipeTypeCU : PipeTypeBase
    {
        public override double GetBuerorMinRadius(int dn, int std) => 0.0;

        public override string GetLabel(int DN, UtilsCommon.Utils.PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case UtilsCommon.Utils.PipeTypeEnum.Ukendt:
                    return "";
                case UtilsCommon.Utils.PipeTypeEnum.Twin:
                    return $"CU{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                case UtilsCommon.Utils.PipeTypeEnum.Frem:
                case UtilsCommon.Utils.PipeTypeEnum.Retur:
                case UtilsCommon.Utils.PipeTypeEnum.Enkelt:
                    return $"CU{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }

        public new double GetPipeKOd(int dn, UtilsCommon.Utils.PipeTypeEnum type, UtilsCommon.Utils.PipeSeriesEnum series)
        {
            if (series == UtilsCommon.Utils.PipeSeriesEnum.S3) series = UtilsCommon.Utils.PipeSeriesEnum.S2;
            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["kOd"];
            return 0;
        }
    }
}
