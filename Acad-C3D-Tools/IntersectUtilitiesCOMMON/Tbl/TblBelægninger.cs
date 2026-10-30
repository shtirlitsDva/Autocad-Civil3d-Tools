#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.Tbl
{
    /// <summary>
    /// A Belægning of a TBL stretch: the value filed in DriOmråder, the keyword TBLCRAWL asks with (STD's:
    /// Fortov's capital O makes its shortcut differ from Flisebelægning's), the layer TBLINIT creates for it
    /// and that layer's colour (ACI).
    /// </summary>
    public sealed record TblBelægning(string Name, string Keyword, string Layer, short ColorIndex);

    /// <summary>
    /// The one table of Belægninger, shared by TBLINIT and TBLCRAWL (IntersectUtilities) and NSTBL's line
    /// mode. A stretch is drawn on its Belægning's layer, but the layer only shows it: the DriOmråder
    /// Belægning is what the export counts by.
    /// Overkørsel and Belægningssten were proposed as 5 and 6, the FJV retur and twin pipe colours; they got
    /// the nearest hue that is no FJV pipe colour of PipeScheduleV2 and at least 30° from the pipe colours of
    /// a Fremtid (1, 5, 6, 190): 140 and 230.
    /// </summary>
    public static class TblBelægninger
    {
        /// <summary>The layer of the first stretches, still read, and the prefix of every Belægning's layer.</summary>
        public const string BareLayer = "0-TBL-STRÆK";

        public static readonly IReadOnlyList<TblBelægning> All = new[]
        {
            new TblBelægning("Vejbane", "Vejbane", "0-TBL-STRÆK-VEJBANE", 30),
            new TblBelægning("Cykelsti", "Cykelsti", "0-TBL-STRÆK-CYKELSTI", 4),
            new TblBelægning("Belægningssten", "Belægningssten", "0-TBL-STRÆK-BELÆGNINGSSTEN", 230),
            new TblBelægning("Flisebelægning", "Flisebelægning", "0-TBL-STRÆK-FLISEBELÆGNING", 2),
            new TblBelægning("Fortov", "FOrtov", "0-TBL-STRÆK-FORTOV", 3),
            new TblBelægning("Overkørsel", "Overkørsel", "0-TBL-STRÆK-OVERKØRSEL", 140),
            new TblBelægning("Ubefæstet", "Ubefæstet", "0-TBL-STRÆK-UBEFÆSTET", 8),
        };

        public static string[] Keywords => All.Select(b => b.Keyword).ToArray();

        public static TblBelægning FromKeyword(string keyword) => All.First(b => b.Keyword == keyword);

        /// <summary>The Belægning whose layer this is; null for the bare layer and any other.</summary>
        public static TblBelægning? OfLayer(string layer) =>
            All.FirstOrDefault(b => string.Equals(b.Layer, layer, StringComparison.OrdinalIgnoreCase));

        /// <summary>A layer stretches are read from: the bare 0-TBL-STRÆK or any 0-TBL-STRÆK-*.</summary>
        public static bool IsStretchLayer(string layer) =>
            string.Equals(layer, BareLayer, StringComparison.OrdinalIgnoreCase) ||
            layer.StartsWith(BareLayer + "-", StringComparison.OrdinalIgnoreCase);
    }
}
