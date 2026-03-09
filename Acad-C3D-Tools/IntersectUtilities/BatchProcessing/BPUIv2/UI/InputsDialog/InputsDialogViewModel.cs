using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.BatchProcessing.BPUIv2.Registry;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.InputsDialog;

public partial class InputsDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<InputItemViewModel> inputs = new();

    private readonly SequenceDefinition _sequence;

    public InputsDialogViewModel(SequenceDefinition sequence)
    {
        _sequence = sequence;
        LoadInputs();
    }

    private void LoadInputs()
    {
        var registry = OperationRegistry.Instance;

        var stepDisplayNames = new Dictionary<string, string>();
        foreach (var step in _sequence.Steps)
        {
            var entry = registry.Catalog.FirstOrDefault(e => e.TypeId == step.OperationTypeId);
            stepDisplayNames[step.StepId] = entry?.DisplayName ?? step.OperationTypeId;
        }

        foreach (var step in _sequence.Steps)
        {
            var entry = registry.Catalog.FirstOrDefault(e => e.TypeId == step.OperationTypeId);
            if (entry == null) continue;
            string stepName = entry.DisplayName;

            foreach (var paramDesc in entry.Parameters)
            {
                step.Parameters.TryGetValue(paramDesc.Name, out var existingValue);

                bool isBound = existingValue?.IsBound == true;
                string bindingText = "";
                if (isBound)
                {
                    var binding = existingValue!.Binding!;
                    stepDisplayNames.TryGetValue(binding.SourceStepId, out var srcName);

                    var srcStep = _sequence.Steps.FirstOrDefault(s => s.StepId == binding.SourceStepId);
                    string outputDisplay = binding.OutputName;
                    if (srcStep != null)
                    {
                        var srcEntry = registry.Catalog.FirstOrDefault(e => e.TypeId == srcStep.OperationTypeId);
                        var outputDesc = srcEntry?.Outputs.FirstOrDefault(o => o.Name == binding.OutputName);
                        if (outputDesc != null) outputDisplay = outputDesc.DisplayName;
                    }
                    bindingText = $"\u2190 {srcName ?? binding.SourceStepId} \u00b7 {outputDisplay}";
                }

                var item = new InputItemViewModel
                {
                    StepId = step.StepId,
                    StepDisplayName = stepName,
                    ParamName = paramDesc.Name,
                    ParamDisplayName = paramDesc.DisplayName,
                    ParamType = paramDesc.Type,
                    IsBound = isBound,
                    BindingDisplayText = bindingText,
                    EnumChoices = paramDesc.EnumChoices ?? Array.Empty<string>(),
                };

                if (existingValue != null && !isBound)
                {
                    switch (paramDesc.Type)
                    {
                        case ParameterType.Bool:
                            item.BoolValue = existingValue.Value is true;
                            break;
                        case ParameterType.EnumChoice:
                            item.SelectedEnumValue = existingValue.Value?.ToString() ?? "";
                            break;
                        default:
                            item.StringValue = existingValue.Value?.ToString() ?? "";
                            break;
                    }
                }

                Inputs.Add(item);
            }
        }
    }

    public void ApplyToSequence()
    {
        foreach (var item in Inputs)
        {
            if (item.IsBound) continue;

            var step = _sequence.Steps.FirstOrDefault(s => s.StepId == item.StepId);
            if (step == null) continue;

            if (!step.Parameters.TryGetValue(item.ParamName, out var pv))
            {
                pv = new ParameterValue { Type = item.ParamType };
                step.Parameters[item.ParamName] = pv;
            }

            switch (item.ParamType)
            {
                case ParameterType.Bool:
                    pv.Value = item.BoolValue;
                    break;
                case ParameterType.Int:
                    pv.Value = int.TryParse(item.StringValue, out var i) ? i : 0;
                    break;
                case ParameterType.Double:
                    pv.Value = double.TryParse(item.StringValue, out var d) ? d : 0.0;
                    break;
                case ParameterType.EnumChoice:
                    pv.Value = item.SelectedEnumValue;
                    break;
                case ParameterType.String:
                default:
                    pv.Value = item.StringValue;
                    break;
            }
        }
    }
}
