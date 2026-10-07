namespace IntersectUtilities.LerProbe;

internal sealed record LerProbeProperty(
    string PropertySet, string Property, string Value, string DataType,
    string Description, bool Automatic, bool Unavailable);

internal readonly record struct LerProbeSnapshot(
    string XrefNames, string SourceFile, string Layer, string Handle,
    int PropertySetCount, IReadOnlyList<LerProbeProperty> Properties);
