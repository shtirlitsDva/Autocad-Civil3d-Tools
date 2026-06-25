using System;
using System.Collections.Generic;
using System.Linq;

namespace NorsynDistrictZones.Pricing;

/// <summary>One display row of a zone's breakdown table (grouped by system + DN).</summary>
public readonly record struct BreakdownRow(
    string Dimension, double Length, double PipeCost, int StikCount, double StikCost)
{
    public double Total => PipeCost + StikCost;
}

/// <summary>
/// Projects a priced zone into the rows shown in the Excel report: one row per
/// (display system, DN), collapsing the NHS FL/SL axis and summing each column. The row
/// totals add up to <see cref="PriceCalculator.ZonePrice.Total"/> — same source as the label.
/// Pure: no AutoCAD, no I/O.
/// </summary>
public static class PriceBreakdown
{
    public static List<BreakdownRow> Rows(PriceCalculator.ZonePrice price) =>
        price.Lines
            .GroupBy(l => (System: PipeDisplayName.System(l.Type), l.Dn))
            .OrderBy(g => g.Key.System, StringComparer.Ordinal).ThenBy(g => g.Key.Dn)
            .Select(g => new BreakdownRow(
                PipeDisplayName.Label(g.Key.System, g.Key.Dn),
                g.Sum(l => l.Length),
                g.Sum(l => l.PipeCost),
                g.Sum(l => l.StikCount),
                g.Sum(l => l.StikCost)))
            .ToList();
}
