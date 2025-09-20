using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Legend
{
    internal static class LegendTitleProvider
    {
        public static string GetTitle(MapPropertyEnum property)
        {
            return property switch
            {
                //Categorical theme
                MapPropertyEnum.Bridge => "Bridges/\nnon-bridges",
                MapPropertyEnum.CriticalPath => "Kritisk\nforbruger",
                MapPropertyEnum.SubGraphId => $"Sub-graphs",
                MapPropertyEnum.Pipe => "Rørdimensioner",

                //Gradient theme
                MapPropertyEnum.Bygninger => $"Bygninger\nforsynet\n[stk]",
                MapPropertyEnum.Units => $"Enheder\nforsynet\n[stk]",
                MapPropertyEnum.HeatingDemand => $"Estimeret\nvarmebehov\n[MWh/år]",
                MapPropertyEnum.FlowSupply => $"Vandflow\nfrem [m³/h]",
                MapPropertyEnum.FlowReturn => $"Vandflow\nretur [m³/h]",
                MapPropertyEnum.PressureGradientSupply => $"Trykgradient\nfrem [Pa/m]",
                MapPropertyEnum.PressureGradientReturn => $"Trykgradient\nretur [Pa/m]",
                MapPropertyEnum.VelocitySupply => $"Hastighed\nfrem [m/s]",
                MapPropertyEnum.VelocityReturn => $"Hastighed\nretur [m/s]",
                MapPropertyEnum.UtilizationRate => $"Udnyttelses-\nfaktor [%]",

                //Not used
                MapPropertyEnum.Default => "Ledninger",
                MapPropertyEnum.Basic => "",

                _ => property.ToString(),
            };
        }
    }
}
