namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public class ParameterDescriptor
{
    public string Name { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ParameterType Type { get; }
    public bool SupportsSampling { get; }
    public string[]? EnumChoices { get; }
    public object? DefaultValue { get; }

    public ParameterDescriptor(
        string name,
        string displayName,
        ParameterType type,
        string description = "",
        bool supportsSampling = false,
        string[]? enumChoices = null,
        object? defaultValue = null)
    {
        Name = name;
        DisplayName = displayName;
        Type = type;
        Description = description;
        SupportsSampling = supportsSampling;
        EnumChoices = enumChoices;
        DefaultValue = defaultValue;
    }
}
