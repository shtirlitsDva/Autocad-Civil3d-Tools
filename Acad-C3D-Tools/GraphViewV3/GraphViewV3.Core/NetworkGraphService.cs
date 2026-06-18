namespace GraphViewV3.Core;

/// <summary>The Core module's single public entry point: snapshot in, laid-out graph +
/// statistics out. Everything heavy here is pure CPU work safe to run on a background
/// thread. This is the boundary the AutoCAD plugin consumes.</summary>
public sealed class NetworkGraphService
{
    private readonly ConnectivityBuilder _connectivity;
    private readonly ForceLayout _layout;

    public NetworkGraphService(double tolerance = 0.5)
    {
        _connectivity = new ConnectivityBuilder { Tolerance = tolerance };
        _layout = new ForceLayout();
    }

    public NetworkResult Build(NetworkSnapshot snapshot)
    {
        var graph = _connectivity.Build(snapshot);
        _layout.Apply(graph);
        var stats = NetworkStatistics.From(snapshot);
        return new NetworkResult(graph, stats, snapshot.ContentHash);
    }
}

public sealed record NetworkResult(NetworkGraph Graph, NetworkStatistics Stats, long ContentHash)
{
    public static readonly NetworkResult Empty =
        new(NetworkGraph.Empty, NetworkStatistics.Empty, 0);
}
