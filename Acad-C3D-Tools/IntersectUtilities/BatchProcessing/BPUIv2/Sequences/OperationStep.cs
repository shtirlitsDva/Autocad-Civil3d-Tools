using System.Text.Json.Serialization;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

public class OperationStep
{
    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = Guid.NewGuid().ToString("N")[..8];

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

    public IReadOnlyDictionary<string, object> ResolveValues(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> stepOutputStore)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in Parameters)
        {
            if (kvp.Value.IsBound)
            {
                var binding = kvp.Value.Binding!;
                if (stepOutputStore.TryGetValue(binding.SourceStepId, out var outputs)
                    && outputs.TryGetValue(binding.OutputName, out var val))
                {
                    result[kvp.Key] = val;
                    continue;
                }
            }
            result[kvp.Key] = kvp.Value.ResolvedValue;
        }
        return result;
    }
}
