using IntersectUtilities.UtilsCommon.Enums;

using NorsynHydraulicCalc;

using NorsynDistrictZones.Model;

namespace NorsynDistrictZones.Pricing;

/// <summary>Pure cost math: price the portion of pipes that falls inside a zone.</summary>
public static class PriceCalculator
{
    /// <summary>
    /// Price one clipped pipe length. <paramref name="lengthInside"/> is already the
    /// in-zone portion (clipped in memory). The per-fitting (stik) surcharge is added
    /// once when the pipe is a service line — but FL/SL is provisional until P12, so
    /// today this only fires if a caller has authoritatively set Stikledning.
    /// </summary>
    public static double PriceClippedPipe(
        PipePriceCatalog catalog, PipeSystemEnum system, int dn, SegmentType segment,
        double lengthInside, bool addFittingForServiceLine)
    {
        NorsynHydraulicCalc.PipeType? nhs = PipeTypeTranslator.ToNhs(system, segment);
        if (nhs is null) return 0.0;

        PipePriceEntry? entry = catalog.Find(nhs.Value, dn);
        if (entry is null) return 0.0;

        double price = entry.PricePerMeter * lengthInside;
        if (addFittingForServiceLine && segment == SegmentType.Stikledning)
            price += entry.PricePerFitting;
        return price;
    }

    /// <summary>Result of pricing all pipes inside one zone.</summary>
    public readonly record struct ZonePrice(double Total, double LengthInside, int PipeCount, bool AnyProvisional);

    /// <summary>
    /// Sum the cost of every (already in-memory-clipped) pipe contribution in a zone.
    /// Each contribution is (segment, clipped length). <paramref name="anyProvisional"/>
    /// flags that at least one pipe's FL/SL was a provisional default → price is an estimate.
    /// </summary>
    public static ZonePrice PriceZone(
        PipePriceCatalog catalog,
        IEnumerable<(PipeSegment Pipe, double LengthInside)> contributions)
    {
        double total = 0, len = 0;
        int count = 0;
        bool provisional = false;
        foreach (var (pipe, lengthInside) in contributions)
        {
            if (lengthInside <= 0) continue;
            total += PriceClippedPipe(
                catalog, pipe.System, pipe.Dn, pipe.Segment, lengthInside,
                addFittingForServiceLine: !pipe.SegmentIsProvisional);
            len += lengthInside;
            count++;
            provisional |= pipe.SegmentIsProvisional;
        }
        return new ZonePrice(total, len, count, provisional);
    }
}
