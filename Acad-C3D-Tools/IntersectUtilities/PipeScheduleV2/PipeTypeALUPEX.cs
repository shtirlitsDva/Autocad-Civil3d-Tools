using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeTypeALUPEX : PipeTypeBase
    {
        public override double GetBuerorMinRadius(int dn, int std) => 0.0;

        public override string GetLabel(int DN, UtilsCommon.Utils.PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case UtilsCommon.Utils.PipeTypeEnum.Ukendt:
                    return "";
                case UtilsCommon.Utils.PipeTypeEnum.Twin:
                    return $"AluPex{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                case UtilsCommon.Utils.PipeTypeEnum.Frem:
                case UtilsCommon.Utils.PipeTypeEnum.Retur:
                case UtilsCommon.Utils.PipeTypeEnum.Enkelt:
                    return $"AluPex{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
    }
}
