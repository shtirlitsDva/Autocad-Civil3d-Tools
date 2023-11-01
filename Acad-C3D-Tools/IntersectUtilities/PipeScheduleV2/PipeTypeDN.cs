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
        public override string GetLabel(int DN, UtilsCommon.Utils.PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case UtilsCommon.Utils.PipeTypeEnum.Ukendt:
                    return "";
                case UtilsCommon.Utils.PipeTypeEnum.Twin:
                    return $"DN{DN}-ø{od.ToString("N1")}+ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                case UtilsCommon.Utils.PipeTypeEnum.Frem:
                case UtilsCommon.Utils.PipeTypeEnum.Retur:
                case UtilsCommon.Utils.PipeTypeEnum.Enkelt:
                    return $"DN{DN}-ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
    }
}
