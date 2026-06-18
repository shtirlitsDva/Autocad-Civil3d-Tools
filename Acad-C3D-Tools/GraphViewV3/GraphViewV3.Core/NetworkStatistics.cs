namespace GraphViewV3.Core;

public sealed record SizeLength(string Size, double Length, int Count);
public sealed record NamedCount(string Name, int Count);
public sealed record SystemShare(string Class, double Length, int PipeCount);

/// <summary>Dashboard statistics derived purely from a snapshot. Connectivity-independent
/// so it can refresh on a different (count-based) cadence than the graph.</summary>
public sealed class NetworkStatistics
{
    public int PipeCount { get; }
    public int ComponentCount { get; }
    public int ElementCount => PipeCount + ComponentCount;
    public double TotalLength { get; }
    public IReadOnlyList<SizeLength> LengthBySize { get; }
    public IReadOnlyList<SystemShare> BySystemClass { get; }
    public IReadOnlyList<NamedCount> ComponentsByType { get; }

    private NetworkStatistics(
        int pipeCount, int componentCount, double totalLength,
        IReadOnlyList<SizeLength> lengthBySize,
        IReadOnlyList<SystemShare> bySystemClass,
        IReadOnlyList<NamedCount> componentsByType)
    {
        PipeCount = pipeCount;
        ComponentCount = componentCount;
        TotalLength = totalLength;
        LengthBySize = lengthBySize;
        BySystemClass = bySystemClass;
        ComponentsByType = componentsByType;
    }

    public static readonly NetworkStatistics Empty = new(
        0, 0, 0,
        Array.Empty<SizeLength>(), Array.Empty<SystemShare>(), Array.Empty<NamedCount>());

    public static NetworkStatistics From(NetworkSnapshot s)
    {
        var lengthBySize = s.Pipes
            .GroupBy(p => string.IsNullOrEmpty(p.Size) ? "—" : p.Size)
            .Select(g => new SizeLength(g.Key, g.Sum(p => p.Length), g.Count()))
            .OrderByDescending(x => x.Length)
            .ToList();

        var bySystem = s.Pipes
            .GroupBy(p => FjvLayer.SystemClass(p.System))
            .Select(g => new SystemShare(g.Key, g.Sum(p => p.Length), g.Count()))
            .OrderByDescending(x => x.Length)
            .ToList();

        var byType = s.Components
            .GroupBy(c => c.Name)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return new NetworkStatistics(
            s.Pipes.Count, s.Components.Count, s.Pipes.Sum(p => p.Length),
            lengthBySize, bySystem, byType);
    }
}
