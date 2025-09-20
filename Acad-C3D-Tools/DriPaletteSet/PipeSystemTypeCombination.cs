using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntersectUtilities.UtilsCommon;

namespace DriPaletteSet
{
    public class PipeSystemTypeCombination
    {
        public Utils.PipeSystemEnum System { get; set; }
        public Utils.PipeTypeEnum Type { get; set; }
        public PipeSystemTypeCombination(Utils.PipeSystemEnum system, Utils.PipeTypeEnum type)
        {
            System = system;
            Type = type;
        }
        public override string ToString() => $"{System} - {Type}";
    }
}
