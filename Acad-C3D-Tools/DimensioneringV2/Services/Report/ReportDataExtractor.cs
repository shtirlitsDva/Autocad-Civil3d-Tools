using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;

using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Extracts computed/aggregated report data from a HydraulicNetwork.
/// Only produces data that isn't directly on the graph model (SystemSummary,
/// ComplianceChecks, SupplyPoints). Modules access graph data directly.
/// </summary>
internal static class ReportDataExtractor
{
    internal static ReportDataContext Extract(HydraulicNetwork hn, ReportProfile profile)
    {
        var settings = hn.FrozenSettings
            ?? throw new InvalidOperationException("Cannot generate report: FrozenSettings is null.");

        var hnSettings = hn.ReportSettings ?? new ReportHnSettings();

        // Ensure node IDs are assigned (they should be from FinalizeCalculation,
        // but legacy networks might not have them)
        bool hasNodeIds = hn.Graphs
            .SelectMany(g => g.Vertices)
            .Any(v => v.NodeId > 0);
        if (!hasNodeIds)
            NodeNumberingService.AssignNodeIds(hn);

        var summary = ComputeSummary(hn);
        var compliance = ComputeCompliance(hn, settings, summary);
        var supplyPoints = ExtractSupplyPoints(hn, settings);

        return new ReportDataContext
        {
            Network = hn,
            Settings = settings,
            HnSettings = hnSettings,
            Profile = profile,
            Summary = summary,
            ComplianceChecks = compliance,
            SupplyPoints = supplyPoints,
        };
    }

    private static SystemSummary ComputeSummary(HydraulicNetwork hn)
    {
        var summary = new SystemSummary();

        foreach (var graph in hn.Graphs)
        {
            foreach (var edge in graph.Edges)
            {
                var f = edge.PipeSegment;
                if (f.NumberOfBuildingsSupplied == 0) continue;

                if (f.SegmentType == SegmentType.Stikledning)
                {
                    summary.ServiceLineLengthM += f.Length;
                    summary.TotalBuildings += f.NumberOfBuildingsConnected;
                    summary.TotalUnits += f.NumberOfUnitsConnected;
                    summary.TotalHeatingDemandMwh += f.HeatingDemandConnected;
                    summary.TotalPowerDemandMw += f.Effekt / 1000.0; // kW -> MW
                }
                else
                {
                    summary.DistributionLineLengthM += f.Length;
                }
            }

            // Flow at root
            var rootEdge = graph.Edges
                .FirstOrDefault(e => e.Source.IsRootNode || e.Target.IsRootNode);
            if (rootEdge != null)
                summary.TotalFlowM3H += rootEdge.PipeSegment.DimFlowSupply;

            // Critical path pressure loss
            foreach (var edge in graph.Edges)
            {
                if (edge.PipeSegment.IsCriticalPath)
                {
                    summary.CriticalPathPressureLossBar +=
                        edge.PipeSegment.PressureGradientSupply * edge.PipeSegment.Length / 100_000;
                    // Pa/m * m = Pa, convert to bar: / 100_000
                }
            }
        }

        summary.TotalPriceDkk = hn.TotalPrice;

        // Critical consumer: stikledning with highest required differential pressure
        double maxReqDP = 0;
        foreach (var f in hn.AllFeatures)
        {
            if (f.SegmentType == SegmentType.Stikledning
                && f.NumberOfBuildingsSupplied > 0
                && f.RequiredDifferentialPressure > maxReqDP)
            {
                maxReqDP = f.RequiredDifferentialPressure;
                summary.CriticalConsumerAddress = f.Adresse;
            }
        }

        return summary;
    }

    private static List<ComplianceRow> ComputeCompliance(
        HydraulicNetwork hn,
        HydraulicSettings settings,
        SystemSummary summary)
    {
        var rows = new List<ComplianceRow>();

        double maxVelocity = 0;
        double maxPressureGradient = 0;
        double minDifferentialPressure = double.MaxValue;

        foreach (var f in hn.AllFeatures)
        {
            if (f.NumberOfBuildingsSupplied == 0) continue;
            maxVelocity = Math.Max(maxVelocity, f.VelocitySupply);
            maxPressureGradient = Math.Max(maxPressureGradient, f.PressureGradientSupply);
            if (f.SegmentType == SegmentType.Stikledning)
            {
                minDifferentialPressure = Math.Min(
                    minDifferentialPressure, f.DifferentialPressureAtClient);
            }
        }

        if (minDifferentialPressure == double.MaxValue)
            minDifferentialPressure = 0;

        rows.Add(new ComplianceRow(
            "Min. differenstryk over hovedhaner",
            $"≥ {settings.MinDifferentialPressureOverHovedHaner:F2} bar",
            $"{minDifferentialPressure:F2} bar",
            minDifferentialPressure >= settings.MinDifferentialPressureOverHovedHaner));

        return rows;
    }

    private static List<SupplyPointRow> ExtractSupplyPoints(
        HydraulicNetwork hn,
        HydraulicSettings settings)
    {
        var rows = new List<SupplyPointRow>();

        foreach (var graph in hn.Graphs)
        {
            var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
            if (rootNode == null || rootNode.NodeId < 0) continue;

            double totalDemandMwh = graph.Edges
                .Where(e => e.PipeSegment.SegmentType == SegmentType.Stikledning
                         && e.PipeSegment.NumberOfBuildingsSupplied > 0)
                .Sum(e => e.PipeSegment.HeatingDemandConnected);

            var rootEdge = graph.Edges
                .FirstOrDefault(e =>
                    ReferenceEquals(e.Source, rootNode) ||
                    ReferenceEquals(e.Target, rootNode));
            double flow = rootEdge?.PipeSegment.DimFlowSupply ?? 0;

            rows.Add(new SupplyPointRow(
                NodeId: rootNode.NodeId,
                Type: "Fra",
                KoteM: null, // TODO: GDAL elevation lookup
                DifferentialPressureBar: 0, // TODO: compute
                TForwardC: settings.TempFrem,
                TReturnC: settings.TempFrem - settings.AfkølingVarme,
                CapacityMw: totalDemandMwh > 0 ? totalDemandMwh / 1000.0 : 0));
        }

        return rows;
    }
}
