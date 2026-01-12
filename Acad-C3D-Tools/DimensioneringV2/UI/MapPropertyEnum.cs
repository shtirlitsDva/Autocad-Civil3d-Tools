using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    public enum MapPropertyEnum
    {
        Default,
        Basic,
        [Description("Antal af bygninger")]
        Bygninger,
        [Description("Antal af enheder")]
        Units,
        [Description("Estimeret varmebehov")]
        HeatingDemand,
        [Description("Kar. flow for frem")]
        KarFlowSupply,
        [Description("Kar. flow for retur")]
        KarFlowReturn,
        [Description("Dim. flow for frem")]
        DimFlowSupply,
        [Description("Dim. flow for retur")]
        DimFlowReturn,
        [Description("Trykgradient for frem")]
        PressureGradientSupply,
        [Description("Trykgradient for retur")]
        PressureGradientReturn,
        [Description("Hastighed for frem")]
        VelocitySupply,
        [Description("Hastighed for retur")]
        VelocityReturn,
        [Description("Udnyttelsesfaktor")]
        UtilizationRate,
        [Description("Rørdimension")]
        Pipe,
        [Description("Vis non-bridges")]
        Bridge,
        [Description("Vis delgrafer")]
        SubGraphId,
        [Description("Vis kritisk kunde")]
        CriticalPath,
        [Description("Manuel dimension")]
        ManualDim,
        [Description("Afkøling rumvarme")]
        TempDeltaVarme,
        [Description("Afkøling brugsvand")]
        TempDeltaBV,
        [Description("Adresse")]
        Address,
        [Description("Anvendelse, tekst")]
        BygningsAnvendelseNyTekst,
        [Description("Anvendelse, kode")]
        BygningsAnvendelseNyKode,
        [Description("Nyttetimer")]
        Nyttetimer
    }
}
