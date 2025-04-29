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
                MapPropertyEnum.Bridge => "Bridges and non-bridges",
                MapPropertyEnum.CriticalPath => "Kritisk forbruger",
                MapPropertyEnum.SubGraphId => $"Sub-graphs",
                MapPropertyEnum.Pipe => "Rørdimensioner",

                //Gradient theme
                MapPropertyEnum.Bygninger => $"Bygninger forsynet [stk]",
                MapPropertyEnum.Units => $"Enheder forsynet [stk]",
                MapPropertyEnum.HeatingDemand => $"Estimeret varmebehov [MWh/år]",
                MapPropertyEnum.FlowSupply => $"Vandflow, frem [m³/h]",
                MapPropertyEnum.FlowReturn => $"Vandflow, retur [m³/h]",
                MapPropertyEnum.PressureGradientSupply => $"Trykgradient, frem [Pa/m]",
                MapPropertyEnum.PressureGradientReturn => $"Trykgradient, retur [Pa/m]",
                MapPropertyEnum.VelocitySupply => $"Hastighed, frem [m/s]",
                MapPropertyEnum.VelocityReturn => $"Hastighed, retur [m/s]",
                MapPropertyEnum.UtilizationRate => $"Udnyttelsesfaktor [%]",

                //Not used
                MapPropertyEnum.Default => "",
                MapPropertyEnum.Basic => "",

                _ => property.ToString(),
            };
        }
    }
}
