using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

namespace DriPaletteSet
{
    public class PipeSystemTypeCombination
    {
        public PipeSystemEnum System { get; set; }
        public PipeTypeEnum Type { get; set; }
        public PipeSystemTypeCombination(PipeSystemEnum system, PipeTypeEnum type)
        {
            System = system;
            Type = type;
        }
        public override string ToString() => $"{System} - {Type}";
    }
}
