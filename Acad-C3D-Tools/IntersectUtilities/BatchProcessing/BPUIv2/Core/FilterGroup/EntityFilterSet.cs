using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

/// <summary>
/// OR-collection of filter groups.
/// Returns true if ANY group passes. An empty Groups list matches everything.
/// </summary>
public class EntityFilterSet
{
    [JsonPropertyName("groups")]
    public List<EntityFilterGroup> Groups { get; set; } = new();

    public EntityFilterSet() { }

    /// <summary>
    /// Returns true if ANY group passes (OR logic).
    /// An empty Groups list matches everything.
    /// </summary>
    public bool Evaluate(Func<FilterProperty, string> propertyAccessor)
    {
        if (Groups.Count == 0)
            return true;

        return Groups.Any(g => g.EvaluateAll(propertyAccessor));
    }
}
