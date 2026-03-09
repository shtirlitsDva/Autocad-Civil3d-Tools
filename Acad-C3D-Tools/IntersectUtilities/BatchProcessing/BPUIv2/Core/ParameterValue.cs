using System.Text.Json.Serialization;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public class ParameterValue
{
    [JsonPropertyName("type")]
    public ParameterType Type { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("filterSet")]
    public EntityFilterSet? FilterSet { get; set; }

    [JsonPropertyName("binding")]
    public OutputBinding? Binding { get; set; }

    [JsonIgnore]
    public bool IsBound => Binding != null;

    [JsonIgnore]
    public object ResolvedValue => Type == ParameterType.FilterSet
        ? FilterSet!
        : Value!;
}
