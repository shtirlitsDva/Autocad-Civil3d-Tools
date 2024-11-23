using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2
{
    internal static class CommonVariables
    {
        public static readonly string LayerEndPoint = "0-End_Point";
        public static readonly string BlockEndPointName = "NS_End_Point";
        public static readonly string LayerSupplyPoint = "0-Supply_Point";
        public static readonly string BlockSupplyPointName = "NS_Supply_Point";

        public static readonly string LayerVejmidteTændt = "Vejmidte-tændt";
        public static readonly string LayerVejmidteSlukket = "Vejmidte-slukket";

        public static readonly string LayerNumbering = "0-Segments_Numbering";

        public static readonly string LayerConnectionLine = "0-CONNECTION_LINE";
        public static readonly Autodesk.AutoCAD.Colors.Color ConnectionLineColor = 
            IntersectUtilities.UtilsCommon.Utils.ColorByName("yellow");
    }
}
