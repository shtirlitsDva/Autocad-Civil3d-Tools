using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2
{
    internal static class CommonVariables
    {
        internal static readonly string LayerEndPoint = "0-End_Point";
        internal static readonly string BlockEndPointName = "NS_End_Point";
        internal static readonly string LayerSupplyPoint = "0-Supply_Point";
        internal static readonly string BlockSupplyPointName = "NS_Supply_Point";

        internal static readonly string LayerVejmidteTændt = "Vejmidte-tændt";
        internal static readonly string LayerVejmidteSlukket = "Vejmidte-slukket";

        internal static readonly string LayerNumbering = "0-Segments_Numbering";

        internal static readonly string LayerConnectionLine = "0-CONNECTION_LINE";
        internal static readonly Autodesk.AutoCAD.Colors.Color ConnectionLineColor = 
            IntersectUtilities.UtilsCommon.Utils.ColorByName("yellow");

        internal static readonly string LayerNoCross = "0-NOCROSS_LINE";

        internal static readonly string LayerDebugLines = "0-FJV_Debug";

        internal static HashSet<string> AcceptedBlockTypes =
            new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet" };
        internal static HashSet<string> AllBlockTypes =
            new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet", "Fjernvarme", "Ingen", "UDGÅR" };
    }
}
