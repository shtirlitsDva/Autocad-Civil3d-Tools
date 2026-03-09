using System.Collections.Generic;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Registry;

/// <summary>
/// Immutable data class holding metadata about one operation for UI display.
/// </summary>
public class OperationCatalogEntry
{
    public string TypeId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }
    public IReadOnlyList<ParameterDescriptor> Parameters { get; }
    public IReadOnlyList<OutputDescriptor> Outputs { get; }

    public OperationCatalogEntry(
        string typeId,
        string displayName,
        string description,
        string category,
        IReadOnlyList<ParameterDescriptor> parameters,
        IReadOnlyList<OutputDescriptor> outputs)
    {
        TypeId = typeId;
        DisplayName = displayName;
        Description = description;
        Category = category;
        Parameters = parameters;
        Outputs = outputs;
    }
}
