namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer;

public record AvailableOutput(
    string StepId, string StepDisplayName,
    string OutputName, string OutputDisplayName,
    Core.ParameterType Type)
{
    public string DisplayLabel => $"{StepDisplayName} \u00B7 {OutputDisplayName}";
}
