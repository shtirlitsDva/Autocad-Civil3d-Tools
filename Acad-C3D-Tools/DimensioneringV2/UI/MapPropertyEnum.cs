using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    enum MapPropertyEnum
    {
        Default,
        Basic,
        [Description("Antal af bygninger")]
        Bygninger,
        [Description("Antal af enheder")]
        Units,
        [Description("Estimeret varmebehov")]
        HeatingDemand,
    }
}
