using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models.Nyttetimer;

using QuikGraph;

using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Models;

internal enum HnState { Empty, Nascent, Calculating, Calculated }
internal enum HnEvent { NewCalc, StartCalc, CalcSuccess, CalcError, CalcCancel, LoadHn }
internal enum NewCalcSource { Civil, CloneCurrent }

internal class HydraulicNetwork
{
    public string? Id { get; set; }
    public string? Description { get; set; }

    public List<UndirectedGraph<NodeJunction, EdgePipeSegment>> Graphs { get; }

    public HydraulicSettings? FrozenSettings { get; private set; }

    public DateTime? CalculatedAt { get; private set; }
    public TimeSpan? CalculationDuration { get; private set; }
    public double TotalPrice { get; set; }
    public bool IsSaved { get; set; }

    /// <summary>
    /// Nyttetimer configuration snapshot taken at calculation time.
    /// Null for legacy networks calculated before this feature.
    /// </summary>
    public NyttetimerConfigurationData? FrozenNyttetimerConfig { get; set; }

    /// <summary>
    /// BBR building features captured at graph-creation time.
    /// Stored in EPSG:25832; reprojected to EPSG:3857 on map display.
    /// Null means no BBR data was captured (legacy/old saved networks).
    /// </summary>
    public List<BBRMapFeature>? BbrFeatures { get; set; }

    public IEnumerable<AnalysisFeature> AllFeatures =>
        Graphs.SelectMany(g => g.Edges.Select(e => e.PipeSegment));

    public HydraulicNetwork(List<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs)
    {
        Graphs = graphs;
    }

    public void Freeze(HydraulicSettings settings)
    {
        FrozenSettings = new HydraulicSettings();
        FrozenSettings.CopyFrom(settings);
        DeepCopyPipeConfigs(FrozenSettings);
    }

    public void FinalizeCalculation(TimeSpan duration)
    {
        CalculatedAt = DateTime.Now;
        CalculationDuration = duration;
        RecalculatePrice();
    }

    private static void DeepCopyPipeConfigs(HydraulicSettings target)
    {
        var clonedFL = new Dictionary<MediumTypeEnum, PipeTypeConfiguration>();
        foreach (var kvp in target.AllPipeConfigsFL)
            clonedFL[kvp.Key] = kvp.Value.Clone();
        target.AllPipeConfigsFL = clonedFL;

        var clonedSL = new Dictionary<MediumTypeEnum, PipeTypeConfiguration>();
        foreach (var kvp in target.AllPipeConfigsSL)
            clonedSL[kvp.Key] = kvp.Value.Clone();
        target.AllPipeConfigsSL = clonedSL;
    }

    public void RecalculatePrice()
    {
        TotalPrice = AllFeatures.Sum(f => f.Dim.Price_m * f.Length + f.Dim.Price_stk_calc(f.SegmentType));
    }

    public void ResetResults()
    {
        foreach (var f in AllFeatures) f.ResetHydraulicResults();
        FrozenSettings = null;
        CalculatedAt = null;
        CalculationDuration = null;
        TotalPrice = 0;
    }

    internal static HydraulicNetwork Restore(
        string? id,
        List<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs,
        HydraulicSettings? frozenSettings,
        DateTime? calculatedAt,
        TimeSpan? calculationDuration,
        double totalPrice,
        string? description = null,
        List<BBRMapFeature>? bbrFeatures = null,
        NyttetimerConfigurationData? frozenNyttetimerConfig = null)
    {
        var hn = new HydraulicNetwork(graphs);
        hn.Id = id;
        hn.Description = description;
        hn.FrozenSettings = frozenSettings;
        hn.FrozenNyttetimerConfig = frozenNyttetimerConfig;
        hn.CalculatedAt = calculatedAt;
        hn.CalculationDuration = calculationDuration;
        hn.TotalPrice = totalPrice;
        hn.BbrFeatures = bbrFeatures;
        hn.IsSaved = true;
        return hn;
    }
}
