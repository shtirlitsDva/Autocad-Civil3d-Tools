using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.DrawingList;
using IntersectUtilities.BatchProcessing.BPUIv2.Execution;
using IntersectUtilities.BatchProcessing.BPUIv2.Registry;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;
using IntersectUtilities.UtilsCommon;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI;

public partial class BatchProcessingViewModel : ObservableObject
{
    private readonly DrawingListService _drawingListService;
    private readonly OperationRegistry _registry;
    private BatchRunner? _runner;

    [ObservableProperty]
    private ObservableCollection<SequenceDefinition> availableSequences = new();

    [ObservableProperty]
    private SequenceDefinition? selectedSequence;

    [ObservableProperty]
    private string drawingListSummary = "No drawings loaded";

    [ObservableProperty]
    private int progressPercent;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string logText = string.Empty;

    [ObservableProperty]
    private bool isLogExpanded;

    public BatchProcessingViewModel()
    {
        _drawingListService = DrawingListService.Instance;
        _registry = OperationRegistry.Instance;

        LoadSequences();
        RefreshDrawingListSummary();
    }

    private void LoadSequences()
    {
        AvailableSequences.Clear();

        // Load predefined sequences
        foreach (var seq in PredefinedSequences.GetAll())
            AvailableSequences.Add(seq);

        // TODO: Phase 8 will add User and Shared sequences here
    }

    public void RefreshDrawingListSummary()
    {
        DrawingListSummary = _drawingListService.GetSummary();
    }

    [RelayCommand(CanExecute = nameof(CanRunBatch))]
    private void RunBatch()
    {
        if (SelectedSequence == null) return;

        var activeDrawings = _drawingListService.GetActiveItems();
        if (activeDrawings.Count == 0) return;

        IsRunning = true;
        StatusText = "Running...";
        ProgressPercent = 0;
        LogText = string.Empty;

        _runner = new BatchRunner();
        _runner.ProgressChanged += OnProgressChanged;
        _runner.LogMessage += OnLogMessage;

        try
        {
            var options = new BatchRunOptions
            {
                AbortDrawingOnFatal = true,
                AbortAllOnException = false
            };

            Result result = _runner.Run(
                activeDrawings, SelectedSequence, options);

            StatusText = result.Status == ResultStatus.OK
                ? "Completed successfully"
                : $"Completed with errors: {result.ErrorMsg}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendLog($"EXCEPTION: {ex.Message}");
        }
        finally
        {
            _runner.ProgressChanged -= OnProgressChanged;
            _runner.LogMessage -= OnLogMessage;
            _runner = null;
            IsRunning = false;
            ProgressPercent = 100;
            RunBatchCommand.NotifyCanExecuteChanged();
            CancelBatchCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunBatch() => !IsRunning && SelectedSequence != null;

    [RelayCommand(CanExecute = nameof(CanCancelBatch))]
    private void CancelBatch()
    {
        _runner?.RequestCancel();
        StatusText = "Cancelling...";
    }

    private bool CanCancelBatch() => IsRunning;

    partial void OnIsRunningChanged(bool value)
    {
        RunBatchCommand.NotifyCanExecuteChanged();
        CancelBatchCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSequenceChanged(SequenceDefinition? value)
    {
        RunBatchCommand.NotifyCanExecuteChanged();
    }

    private void OnProgressChanged(BatchRunProgress progress)
    {
        ProgressPercent = progress.PercentComplete;
        StatusText = $"[{progress.DrawingIndex + 1}/{progress.TotalDrawings}] " +
                     $"{progress.DrawingName} — {progress.OperationName}";
    }

    private void OnLogMessage(string message)
    {
        AppendLog(message);
    }

    private void AppendLog(string message)
    {
        LogText += message + Environment.NewLine;
    }
}
