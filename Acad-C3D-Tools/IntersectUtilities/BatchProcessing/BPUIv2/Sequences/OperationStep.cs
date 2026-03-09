using System.Text.Json.Serialization;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

public class OperationStep
{
    [JsonPropertyName("operationTypeId")]
    public string OperationTypeId { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, ParameterValue> Parameters { get; set; } = new();

    public OperationStep() { }

    public OperationStep(string operationTypeId, Dictionary<string, ParameterValue>? parameters = null)
    {
        OperationTypeId = operationTypeId;
        Parameters = parameters ?? new();
    }

    /// <summary>
    /// Resolves all parameter values into a flat dictionary suitable for Execute().
    /// </summary>
    public IReadOnlyDictionary<string, object> ResolveValues()
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in Parameters)
        {
            result[kvp.Key] = kvp.Value.ResolvedValue;
        }
        return result;
    }
}
