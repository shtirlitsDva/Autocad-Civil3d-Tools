using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;
using DimensioneringV2.Services.Report.DataModels;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.LookupData;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services.Report;

/// <summary>
/// Extracts computed/aggregated report data from a HydraulicNetwork.
/// Builds scoped data (total + per-network) for multi-network support.
/// </summary>
internal static class ReportDataExtractor
{
    internal static ReportDataContext Extract(HydraulicNetwork hn, ReportProfile profile)
    {
        var settings = hn.FrozenSettings
            ?? throw new InvalidOperationException("Cannot generate report: FrozenSettings is null.");

        var hnSettings = hn.ReportSettings ?? new ReportHnSettings();

        // Order graphs by edge count descending (largest network first)
        var orderedGraphs = hn.Graphs
            .OrderByDescending(g => g.EdgeCount)
            .ThenByDescending(g => g.Vertices.FirstOrDefault(v => v.IsRootNode)?.Location.X ?? 0)
            .ToList();

        bool isMultiNetwork = orderedGraphs.Count > 1;

        // Re-assign node IDs with proper ordering
        NodeNumberingService.AssignNodeIds(hn, orderedGraphs);

        // Build total scoped data (across all graphs)
        var totalSummary = ComputeSummary(orderedGraphs, hn.TotalPrice, settings);
        var totalCompliance = ComputeCompliance(orderedGraphs, settings);
        var totalSupplyPoints = ExtractSupplyPoints(orderedGraphs, settings, isMultiNetwork);
        var totalData = new ScopedReportData(totalSummary, totalCompliance, totalSupplyPoints);

        // Build per-network scoped data
        var perNetworkData = new List<ScopedReportData>();
        for (int i = 0; i < orderedGraphs.Count; i++)
        {
            var graphList = new List<UndirectedGraph<NodeJunction, EdgePipeSegment>> { orderedGraphs[i] };
            double graphPrice = graphList[0].Edges
                .Select(e => e.PipeSegment)
                .Where(f => f.NumberOfBuildingsSupplied > 0)
                .Sum(f => f.Dim.Price_m * f.Length + f.Dim.Price_stk_calc(f.SegmentType));

            var gSummary = ComputeSummary(graphList, graphPrice, settings);
            var gCompliance = ComputeCompliance(graphList, settings);
            var gSupplyPoints = ExtractSupplyPoints(graphList, settings, isMultiNetwork);
            perNetworkData.Add(new ScopedReportData(gSummary, gCompliance, gSupplyPoints));
        }

        // Initialize context with total scope active
        bool isSingleMode = orderedGraphs.Count == 1;
        return new ReportDataContext
        {
            Network = hn,
            Settings = settings,
            HnSettings = hnSettings,
            Profile = profile,
            OrderedGraphs = orderedGraphs,
            TotalData = totalData,
            PerNetworkData = perNetworkData,
            // Set active scope to total
            Summary = totalSummary,
            ComplianceChecks = totalCompliance,
            SupplyPoints = totalSupplyPoints,
            Scope = NetworkScope.Total(orderedGraphs, isSingleMode),
        };
    }

    private static SystemSummary ComputeSummary(
        IReadOnlyList<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs,
        double totalPrice,
        HydraulicSettings settings)
    {
        var summary = new SystemSummary();

        // Get fluid properties at forward temperature for power calculation
        // Φ = qv · ρ · cp · ΔT  [kW]
        var lookupData = LookupDataFactory.GetLookupData(settings.MedieType);
        double tempFrem = settings.TempFrem;
        double deltaT = settings.AfkølingVarme;
        double rho = lookupData.rho(tempFrem);   // kg/m³
        double cp = lookupData.cp(tempFrem);      // kJ/(kg·K)

        foreach (var graph in graphs)
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

            // Critical path pressure loss (per-graph, accumulates if multiple graphs)
            foreach (var edge in graph.Edges)
            {
                if (edge.PipeSegment.IsCriticalPath)
                {
                    summary.CriticalPathPressureLossBar +=
                        edge.PipeSegment.PressureGradientSupply * edge.PipeSegment.Length / 100_000;
                }
            }
        }

        summary.TotalPriceDkk = totalPrice;

        // Power: Φ = qv · ρ · cp · ΔT
        // qv [m³/h] / 3600 → [m³/s], ρ [kg/m³], cp [kJ/(kg·K)], ΔT [K]
        // Result: [m³/s · kg/m³ · kJ/(kg·K) · K] = [kJ/s] = [kW]
        summary.TotalPowerDemandKw = (summary.TotalFlowM3H / 3600.0) * rho * cp * deltaT;

        // Critical consumer: stikledning with highest required differential pressure
        double maxReqDP = 0;
        foreach (var graph in graphs)
        {
            foreach (var edge in graph.Edges)
            {
                var f = edge.PipeSegment;
                if (f.SegmentType == SegmentType.Stikledning
                    && f.NumberOfBuildingsSupplied > 0
                    && f.RequiredDifferentialPressure > maxReqDP)
                {
                    maxReqDP = f.RequiredDifferentialPressure;
                    summary.CriticalConsumerAddress = f.Adresse;
                }
            }
        }

        return summary;
    }

    private static List<ComplianceRow> ComputeCompliance(
        IReadOnlyList<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs,
        HydraulicSettings settings)
    {
        var rows = new List<ComplianceRow>();

        double maxVelocity = 0;
        double maxPressureGradient = 0;
        double minDifferentialPressure = double.MaxValue;

        foreach (var graph in graphs)
        {
            foreach (var edge in graph.Edges)
            {
                var f = edge.PipeSegment;
                if (f.NumberOfBuildingsSupplied == 0) continue;
                maxVelocity = Math.Max(maxVelocity, f.VelocitySupply);
                maxPressureGradient = Math.Max(maxPressureGradient, f.PressureGradientSupply);
                if (f.SegmentType == SegmentType.Stikledning)
                {
                    minDifferentialPressure = Math.Min(
                        minDifferentialPressure, f.DifferentialPressureAtClient);
                }
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
        IReadOnlyList<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs,
        HydraulicSettings settings,
        bool isMultiNetwork)
    {
        var rows = new List<SupplyPointRow>();

        for (int i = 0; i < graphs.Count; i++)
        {
            var graph = graphs[i];
            var rootNode = graph.Vertices.FirstOrDefault(v => v.IsRootNode);
            if (rootNode == null || string.IsNullOrEmpty(rootNode.NodeId)) continue;

            double totalDemandMwh = graph.Edges
                .Where(e => e.PipeSegment.SegmentType == SegmentType.Stikledning
                         && e.PipeSegment.NumberOfBuildingsSupplied > 0)
                .Sum(e => e.PipeSegment.HeatingDemandConnected);

            var rootEdge = graph.Edges
                .FirstOrDefault(e =>
                    ReferenceEquals(e.Source, rootNode) ||
                    ReferenceEquals(e.Target, rootNode));
            double flow = rootEdge?.PipeSegment.DimFlowSupply ?? 0;

            string? networkName = isMultiNetwork ? $"Fjernvarmenet {i + 1}" : null;

            rows.Add(new SupplyPointRow(
                NodeId: rootNode.NodeId,
                Type: "Fra",
                KoteM: null,
                DifferentialPressureBar: 0,
                TForwardC: settings.TempFrem,
                TReturnC: settings.TempFrem - settings.AfkølingVarme,
                CapacityMw: totalDemandMwh > 0 ? totalDemandMwh / 1000.0 : 0,
                NetworkName: networkName));
        }

        return rows;
    }
}
