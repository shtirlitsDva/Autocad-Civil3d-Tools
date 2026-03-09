namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public class OutputDescriptor(string name, string displayName, ParameterType type)
{
    public string Name { get; } = name;
    public string DisplayName { get; } = displayName;
    public ParameterType Type { get; } = type;
}
