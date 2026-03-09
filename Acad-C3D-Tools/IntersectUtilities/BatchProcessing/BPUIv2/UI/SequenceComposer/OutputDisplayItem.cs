namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer;

public class OutputDisplayItem(string name, string displayName, Core.ParameterType type)
{
    public string Name { get; } = name;
    public string DisplayName { get; } = displayName;
    public Core.ParameterType Type { get; } = type;
}
