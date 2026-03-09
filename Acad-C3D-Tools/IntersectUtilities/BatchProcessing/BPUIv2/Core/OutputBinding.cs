using System.Text.Json.Serialization;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public class OutputBinding
{
    [JsonPropertyName("sourceStepId")]
    public string SourceStepId { get; set; } = string.Empty;

    [JsonPropertyName("outputName")]
    public string OutputName { get; set; } = string.Empty;
}
