using DimensioneringV2.UI;

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
                MapPropertyEnum.ManualDim => "Manuelt\ndimensionerede\nrør",
                MapPropertyEnum.Address => "Adresser",
                MapPropertyEnum.BygningsAnvendelseNyTekst => "Anvendelse, tekst",
                MapPropertyEnum.BygningsAnvendelseNyKode => "Anvendelse, kode",

                //Gradient theme
                MapPropertyEnum.Bygninger => $"Bygninger\nforsynet\n[stk]",
                MapPropertyEnum.Units => $"Enheder\nforsynet\n[stk]",
                MapPropertyEnum.HeatingDemand => $"Estimeret\nvarmebehov\n[MWh/år]",
                MapPropertyEnum.DimFlowSupply => $"Vandflow\nfrem [m³/h]",
                MapPropertyEnum.DimFlowReturn => $"Vandflow\nretur [m³/h]",
                MapPropertyEnum.PressureGradientSupply => $"Trykgradient\nfrem [Pa/m]",
                MapPropertyEnum.PressureGradientReturn => $"Trykgradient\nretur [Pa/m]",
                MapPropertyEnum.VelocitySupply => $"Hastighed\nfrem [m/s]",
                MapPropertyEnum.VelocityReturn => $"Hastighed\nretur [m/s]",
                MapPropertyEnum.UtilizationRate => $"Udnyttelses-\nfaktor [%]",
                MapPropertyEnum.TempDeltaVarme => "Afkøling rumvarme [°C]",
                MapPropertyEnum.TempDeltaBV => "Afkøling brugsvand [°C]",

                //Not used
                MapPropertyEnum.Default => "Ledninger",
                MapPropertyEnum.Basic => "",

                _ => property.ToString(),
            };
        }
    }
}
