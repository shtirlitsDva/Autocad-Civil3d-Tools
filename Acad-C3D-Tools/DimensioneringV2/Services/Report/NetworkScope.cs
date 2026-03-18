using DimensioneringV2.GraphFeatures;
using QuikGraph;
using System.Collections.Generic;

namespace DimensioneringV2.Services.Report;

internal class NetworkScope
{
    public IReadOnlyList<UndirectedGraph<NodeJunction, EdgePipeSegment>> Graphs { get; }
    public bool IsTotal { get; }
    public int NetworkIndex { get; } // 1-based, meaningful only when IsTotal == false
    public string? NetworkDisplayName { get; } // null in single-mode/total, "Fjernvarmenet X" in multi
    public bool IsSingleNetworkMode { get; }

    private NetworkScope(
        IReadOnlyList<UndirectedGraph<NodeJunction, EdgePipeSegment>> graphs,
        bool isTotal, int networkIndex, string? displayName, bool isSingleMode)
    {
        Graphs = graphs;
        IsTotal = isTotal;
        NetworkIndex = networkIndex;
        NetworkDisplayName = displayName;
        IsSingleNetworkMode = isSingleMode;
    }

    internal static NetworkScope Total(
        IReadOnlyList<UndirectedGraph<NodeJunction, EdgePipeSegment>> allGraphs,
        bool isSingleMode)
    {
        return new NetworkScope(allGraphs, isTotal: true, networkIndex: 0,
            displayName: null, isSingleMode: isSingleMode);
    }

    internal static NetworkScope ForGraph(
        UndirectedGraph<NodeJunction, EdgePipeSegment> graph,
        int oneBasedIndex, bool isSingleMode)
    {
        string? name = isSingleMode ? null : $"Fjernvarmenet {oneBasedIndex}";
        return new NetworkScope(
            new[] { graph }, isTotal: false, networkIndex: oneBasedIndex,
            displayName: name, isSingleMode: isSingleMode);
    }
}
