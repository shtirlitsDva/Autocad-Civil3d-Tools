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
/// Extracts all report data from a HydraulicNetwork into a ReportDataContext.
/// Called once before rendering; modules consume the pre-extracted context.
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

        var segments = ExtractSegments(hn);
        var nodes = ExtractNodes(hn);
        var consumers = ExtractConsumers(hn);
        var summary = ComputeSummary(hn, segments, consumers);
        var compliance = ComputeCompliance(hn, settings, summary);
        var supplyPoints = ExtractSupplyPoints(hn, settings);

        return new ReportDataContext
        {
            Network = hn,
            Settings = settings,
            HnSettings = hnSettings,
            Profile = profile,
            Summary = summary,
            Segments = segments,
            Nodes = nodes,
            Consumers = consumers,
            ComplianceChecks = compliance,
            SupplyPoints = supplyPoints,
        };
    }

    private static List<SegmentRow> ExtractSegments(HydraulicNetwork hn)
    {
        var rows = new List<SegmentRow>();

        foreach (var graph in hn.Graphs)
        {
            foreach (var edge in graph.Edges)
            {
                var f = edge.PipeSegment;
                if (f.NumberOfBuildingsSupplied == 0) continue;

                int srcId = edge.Source.NodeId;
                int tgtId = edge.Target.NodeId;
                string segId = srcId > 0 && tgtId > 0
                    ? $"{srcId}-{tgtId}"
                    : $"?-?";

                rows.Add(new SegmentRow(
                    SegmentId: segId,
                    LengthM: f.Length,
                    PipeType: f.Dim.PipeType.ToString(),
                    DimensionName: f.Dim.DimName,
                    VelocitySupply: f.VelocitySupply,
                    VelocityReturn: f.VelocityReturn,
                    VelocityUtilization: f.UtilizationRate,
                    PressureGradientSupply: f.PressureGradientSupply,
                    PressureGradientReturn: f.PressureGradientReturn,
                    PressureGradientUtilization: 0, // TODO: compute from accept criteria
                    PressureLossBar: f.PressureLossBAR));
            }
        }

        return rows;
    }

    private static List<NodeRow> ExtractNodes(HydraulicNetwork hn)
    {
        var rows = new List<NodeRow>();
        var seen = new HashSet<NodeJunction>();

        foreach (var graph in hn.Graphs)
        {
            foreach (var node in graph.Vertices)
            {
                if (node.NodeId < 0) continue;
                if (!seen.Add(node)) continue;

                // Find the max pressure loss to this node from its adjacent edges
                double pressureLoss = 0;
                double differentialPressure = 0;
                double effekt = 0;

                foreach (var edge in graph.AdjacentEdges(node))
                {
                    var f = edge.PipeSegment;
                    if (node.IsBuildingNode)
                    {
                        pressureLoss = f.PressureLossAtClientSupply + f.PressureLossAtClientReturn;
                        differentialPressure = f.DifferentialPressureAtClient;
                        effekt = f.Effekt;
                    }
                }

                rows.Add(new NodeRow(
                    NodeId: node.NodeId,
                    X: node.Location.X,
                    Y: node.Location.Y,
                    IsRoot: node.IsRootNode,
                    IsBuilding: node.IsBuildingNode,
                    Degree: node.Degree,
                    EffektKw: effekt,
                    PressureLossToNodeBar: pressureLoss,
                    AvailableDifferentialPressureBar: differentialPressure));
            }
        }

        return rows.OrderBy(n => n.NodeId).ToList();
    }

    private static List<ConsumerRow> ExtractConsumers(HydraulicNetwork hn)
    {
        var rows = new List<ConsumerRow>();

        foreach (var graph in hn.Graphs)
        {
            foreach (var edge in graph.Edges)
            {
                var f = edge.PipeSegment;
                if (f.SegmentType != SegmentType.Stikledning) continue;
                if (f.NumberOfBuildingsSupplied == 0) continue;

                rows.Add(new ConsumerRow(
                    Address: f.Adresse ?? "",
                    BuildingType: f.BygningsAnvendelseNyTekst ?? "",
                    BuildingCode: f.BygningsAnvendelseNyKode ?? "",
                    NumberOfProperties: f.NumberOfBuildingsConnected,
                    NumberOfUnitsWithHotWater: f.NumberOfUnitsConnected,
                    DimCoolingC: f.TempDeltaVarme,
                    BbrAreaM2: f.BeregningsAreal,
                    ConstructionYear: f.Opførelsesår,
                    EnergyConsumptionKwhYear: f.HeatingDemandConnected * 1000, // MWh -> kWh
                    ServiceLineLengthM: f.Length,
                    DimensionName: f.Dim.DimName,
                    PressureGradientPaM: f.PressureGradientSupply,
                    VelocityMs: f.VelocitySupply,
                    PressureLossServiceLineBar: f.PressureLossBAR,
                    RequiredDifferentialPressureBar: f.RequiredDifferentialPressure));
            }
        }

        return rows.OrderByDescending(c => c.RequiredDifferentialPressureBar).ToList();
    }

    private static SystemSummary ComputeSummary(
        HydraulicNetwork hn,
        List<SegmentRow> segments,
        List<ConsumerRow> consumers)
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

        // Critical consumer: the one with highest required differential pressure
        if (consumers.Count > 0)
            summary.CriticalConsumerAddress = consumers[0].Address; // already sorted desc

        return summary;
    }

    private static List<ComplianceRow> ComputeCompliance(
        HydraulicNetwork hn,
        HydraulicSettings settings,
        SystemSummary summary)
    {
        var rows = new List<ComplianceRow>();

        // Find worst-case velocity and pressure gradient across all active segments
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

            // Compute capacity from total heating demand of this graph
            double totalDemandMwh = graph.Edges
                .Where(e => e.PipeSegment.SegmentType == SegmentType.Stikledning
                         && e.PipeSegment.NumberOfBuildingsSupplied > 0)
                .Sum(e => e.PipeSegment.HeatingDemandConnected);

            // Total flow at root
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
