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

        internal static readonly string LayerVejmidteTændt = "0-Vejmidte-tændt";
        internal static readonly string LayerVejmidteSlukket = "0-Vejmidte-slukket";

        internal static readonly string LayerNumbering = "0-Segments_Numbering";

        internal static readonly string LayerConnectionLine = "0-CONNECTION_LINE";
        internal static readonly Autodesk.AutoCAD.Colors.Color ConnectionLineColor = 
            IntersectUtilities.UtilsCommon.Utils.ColorByName("yellow");

        internal static readonly string LayerNoCross = "0-NOCROSS_LINE";

        internal static readonly string LayerDebugLines = "0-FJV_Debug";

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // BLOCK TYPE FILTERING
        // ═══════════════════════════════════════════════════════════════════════════════════════
        // AcceptedBlockTypes is now controlled via filter toggles in the Settings UI.
        // The filters are persisted as individual bool properties in HydraulicSettings:
        //   - FilterEl, FilterNaturgas, FilterVarmepumpe, FilterFastBrændsel,
        //     FilterOlie, FilterFjernvarme, FilterAndetIngenUdgår
        //
        // To get the effective accepted types at runtime, use:
        //   HydraulicSettingsService.Instance.Settings.GetAcceptedBlockTypes()
        // ═══════════════════════════════════════════════════════════════════════════════════════
        internal static readonly HashSet<string> AllBlockTypes = new HashSet<string>
        {
            "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet", "Fjernvarme", "Ingen", "UDGÅR"
        };
    }
}
