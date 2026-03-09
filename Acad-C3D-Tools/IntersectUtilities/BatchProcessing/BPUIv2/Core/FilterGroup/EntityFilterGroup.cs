using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

/// <summary>
/// A group of filter conditions AND-ed together.
/// All conditions must pass for the group to match.
/// </summary>
public class EntityFilterGroup
{
    [JsonPropertyName("conditions")]
    public List<EntityFilter> Conditions { get; set; } = new();

    public EntityFilterGroup() { }

    /// <summary>
    /// Returns true if ALL conditions pass.
    /// The <paramref name="propertyAccessor"/> maps a FilterProperty to the
    /// actual string value from an entity.
    /// </summary>
    public bool EvaluateAll(Func<FilterProperty, string> propertyAccessor)
    {
        return Conditions.All(c => c.Evaluate(propertyAccessor(c.Property)));
    }
}
