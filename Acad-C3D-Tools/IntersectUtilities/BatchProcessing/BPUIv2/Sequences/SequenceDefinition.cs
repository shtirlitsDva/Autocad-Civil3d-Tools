using System.Text.Json.Serialization;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

public class SequenceDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = Environment.UserName;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("modifiedAt")]
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("steps")]
    public List<OperationStep> Steps { get; set; } = new();

    [JsonIgnore]
    public SequenceStorageLevel StorageLevel { get; set; } = SequenceStorageLevel.User;

    public SequenceDefinition() { }

    public SequenceDefinition(
        string name,
        string description,
        string category,
        SequenceStorageLevel storageLevel,
        params OperationStep[] steps)
    {
        Name = name;
        Description = description;
        Category = category;
        StorageLevel = storageLevel;
        Steps = steps.ToList();
    }
}
