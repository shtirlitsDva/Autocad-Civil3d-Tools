using System.Linq;

using IntersectUtilities.UtilsCommon.Enums;

using NorsynHydraulicCalc;

using NorsynDistrictZones.Model;

using NhsPipeType = NorsynHydraulicCalc.PipeType;

namespace NorsynDistrictZones.Pricing;

/// <summary>Pure cost math: price the portion of pipes that falls inside a zone.</summary>
public static class PriceCalculator
{
    /// <summary>
    /// One priced line of a zone: all in-zone pipe of a single authoritative (NHS type, DN),
    /// with its total length, total pipe cost, the number of Stikledning pipes in it, and their
    /// total fitting (stik) cost. <see cref="Total"/> = pipe + stik. This is the atom both the
    /// model-space label total and the Excel breakdown are built from — one source, no drift.
    /// </summary>
    public readonly record struct ZoneLine(
        NhsPipeType Type, int Dn, double Length, double PipeCost, int StikCount, double StikCost)
    {
        public double Total => PipeCost + StikCost;
    }

    /// <summary>
    /// Result of pricing all pipes inside one zone. <see cref="Total"/> is the sum of
    /// <see cref="Lines"/>. <see cref="AnyProvisional"/> flags an unstamped (unidentifiable)
    /// pipe; <see cref="MissingEntries"/> lists the distinct (type, DN) the active catalog had
    /// no price for — both are reasons the total is untrusted. Provisional and missing pipes
    /// contribute no line (they cannot be priced) but are still counted in PipeCount/LengthInside.
    /// </summary>
    public readonly record struct ZonePrice(
        double Total, double LengthInside, int PipeCount, bool AnyProvisional,
        IReadOnlyList<(NhsPipeType Type, int Dn)> MissingEntries,
        IReadOnlyList<ZoneLine> Lines);

    private struct LineAcc
    {
        public double Length;
        public double PipeCost;
        public int StikCount;
        public double StikCost;
    }

    /// <summary>
    /// Price every (already in-memory-clipped) pipe contribution in a zone, aggregated into one
    /// line per authoritative (NHS type, DN). <c>AnyProvisional</c> flags an unstamped pipe;
    /// <c>MissingEntries</c> collects stamped pipes the catalog couldn't price — the caller
    /// reports both. The grand total is the sum of the line totals (single pricing source).
    /// </summary>
    public static ZonePrice PriceZone(
        PipePriceCatalog catalog,
        IEnumerable<(PipeSegment Pipe, double LengthInside)> contributions)
    {
        var acc = new Dictionary<(NhsPipeType Type, int Dn), LineAcc>();
        double len = 0;
        int count = 0;
        bool provisional = false;
        var missing = new List<(NhsPipeType Type, int Dn)>();

        foreach (var (pipe, lengthInside) in contributions)
        {
            if (lengthInside <= 0) continue;
            len += lengthInside;
            count++;

            if (pipe.NhsType is null) { provisional = true; continue; }
            var key = (pipe.NhsType.Value, pipe.Dn);

            PipePriceEntry? entry = catalog.Find(key.Item1, key.Item2);
            if (entry is null) { missing.Add(key); continue; }

            bool isStik = pipe.Segment == SegmentType.Stikledning;
            acc.TryGetValue(key, out LineAcc a);
            a.Length += lengthInside;
            a.PipeCost += entry.PricePerMeter * lengthInside;
            if (isStik) { a.StikCount++; a.StikCost += entry.PricePerFitting; }
            acc[key] = a;
        }

        var lines = acc
            .Select(kv => new ZoneLine(
                kv.Key.Type, kv.Key.Dn, kv.Value.Length, kv.Value.PipeCost, kv.Value.StikCount, kv.Value.StikCost))
            .ToList();
        double total = lines.Sum(l => l.Total);
        return new ZonePrice(total, len, count, provisional, missing.Distinct().ToList(), lines);
    }
}
