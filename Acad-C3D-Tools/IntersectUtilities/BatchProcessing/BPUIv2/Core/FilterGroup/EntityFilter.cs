using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public class EntityFilter
{
    [JsonPropertyName("property")]
    public FilterProperty Property { get; set; }

    [JsonPropertyName("operator")]
    public FilterOperator Operator { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public EntityFilter() { }

    public bool Evaluate(string actualValue)
    {
        actualValue ??= string.Empty;

        return Operator switch
        {
            FilterOperator.Equals =>
                string.Equals(actualValue, Value, StringComparison.OrdinalIgnoreCase),

            FilterOperator.NotEquals =>
                !string.Equals(actualValue, Value, StringComparison.OrdinalIgnoreCase),

            FilterOperator.Contains =>
                actualValue.Contains(Value, StringComparison.OrdinalIgnoreCase),

            FilterOperator.StartsWith =>
                actualValue.StartsWith(Value, StringComparison.OrdinalIgnoreCase),

            FilterOperator.EndsWith =>
                actualValue.EndsWith(Value, StringComparison.OrdinalIgnoreCase),

            FilterOperator.Regex =>
                Regex.IsMatch(actualValue, Value, RegexOptions.IgnoreCase),

            _ => false
        };
    }
}
