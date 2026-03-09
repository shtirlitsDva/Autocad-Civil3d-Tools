using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.BatchProcessing.BPUIv2.DrawingList;
using IntersectUtilities.BatchProcessing.BPUIv2.Registry;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using static IntersectUtilities.UtilsCommon.Utils;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Execution;

public class BatchRunner
{
    private readonly OperationRegistry _registry;
    private bool _cancelRequested;

    public event Action<BatchRunProgress>? ProgressChanged;
    public event Action<string>? LogMessage;

    public BatchRunner()
    {
        _registry = OperationRegistry.Instance;
    }

    public void RequestCancel() => _cancelRequested = true;

    public Result Run(
        IReadOnlyList<DrawingListItem> drawings,
        SequenceDefinition sequence,
        BatchRunOptions options,
        DataReferencesOptions? dataReferences = null)
    {
        _cancelRequested = false;
        var stopwatch = Stopwatch.StartNew();

        var sharedState = new Dictionary<string, object>();

        bool needsCounter = sequence.Steps.Any(s =>
            s.Parameters.Values.Any(p => p.Type == ParameterType.Counter));
        if (needsCounter)
            sharedState["_counter"] = new Counter();

        Log($"Starting batch: \"{sequence.Name}\" on {drawings.Count} drawings");
        Log($"Steps: {sequence.Steps.Count}");

        for (int drawingIdx = 0; drawingIdx < drawings.Count; drawingIdx++)
        {
            if (_cancelRequested)
            {
                Log("Batch cancelled by user.");
                break;
            }

            var item = drawings[drawingIdx];
            string file = item.FilePath;

            if (!File.Exists(file))
            {
                Log($"[{drawingIdx + 1}/{drawings.Count}] SKIP (not found): {item.FileName}");
                continue;
            }

            Log($"[{drawingIdx + 1}/{drawings.Count}] Processing: {item.FileName}");
            bool drawingFailed = false;

            var stepOutputStore = new Dictionary<string, IReadOnlyDictionary<string, object>>();

            using (Database xDb = new Database(false, true))
            {
                xDb.ReadDwgFile(file, FileShare.ReadWrite, false, "");

                using (Transaction xTx = xDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        CivilDocument civilDoc = CivilDocument.GetCivilDocument(xDb);

                        var context = new OperationContext(
                            xDb, xTx, civilDoc, sharedState);
                        context.DataReferences = dataReferences;

                        for (int opIdx = 0; opIdx < sequence.Steps.Count; opIdx++)
                        {
                            if (_cancelRequested)
                            {
                                Log("Cancel requested — aborting current drawing.");
                                break;
                            }

                            var step = sequence.Steps[opIdx];
                            var operation = _registry.GetOperation(step.OperationTypeId);

                            if (operation == null)
                            {
                                Log($"  WARNING: Operation '{step.OperationTypeId}' not found, skipping.");
                                continue;
                            }

                            ReportProgress(drawingIdx, drawings.Count,
                                item.FileName, operation.DisplayName,
                                opIdx, sequence.Steps.Count);

                            try
                            {
                                context.StepOutputs.Clear();
                                var paramValues = step.ResolveValues(stepOutputStore);
                                Result result = operation.Execute(context, paramValues);

                                if (context.StepOutputs.Count > 0)
                                    stepOutputStore[step.StepId] = new Dictionary<string, object>(context.StepOutputs);

                                switch (result.Status)
                                {
                                    case ResultStatus.OK:
                                        break;

                                    case ResultStatus.FatalError:
                                        Log($"  FATAL: {operation.DisplayName} — {result.ErrorMsg}");
                                        if (options.AbortDrawingOnFatal)
                                        {
                                            drawingFailed = true;
                                            Log("  Aborting remaining operations for this drawing.");
                                        }
                                        break;

                                    case ResultStatus.SoftError:
                                        Log($"  WARN: {operation.DisplayName} — {result.ErrorMsg}");
                                        break;
                                }

                                if (drawingFailed) break;
                            }
                            catch (Exception ex)
                            {
                                Log($"  EXCEPTION in {operation.DisplayName}: {ex.Message}");
                                if (options.AbortAllOnException)
                                {
                                    xTx.Abort();
                                    stopwatch.Stop();
                                    Log($"Batch aborted after exception. Elapsed: {stopwatch.Elapsed}");
                                    return new Result(ResultStatus.FatalError,
                                        $"Exception in {operation.DisplayName}: {ex.Message}");
                                }
                                drawingFailed = true;
                                break;
                            }

                            System.Windows.Forms.Application.DoEvents();
                        }

                        if (drawingFailed || _cancelRequested)
                        {
                            xTx.Abort();
                        }
                        else
                        {
                            xTx.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"  EXCEPTION processing {item.FileName}: {ex.Message}");
                        xTx.Abort();
                        if (options.AbortAllOnException)
                        {
                            stopwatch.Stop();
                            Log($"Batch aborted. Elapsed: {stopwatch.Elapsed}");
                            return new Result(ResultStatus.FatalError, ex.ToString());
                        }
                        drawingFailed = true;
                    }
                }

                if (!drawingFailed && !_cancelRequested)
                {
                    xDb.SaveAs(xDb.Filename, true, DwgVersion.Newest, xDb.SecurityParameters);
                    Log($"  Saved: {item.FileName}");
                }
                else if (drawingFailed)
                {
                    Log($"  NOT saved (errors): {item.FileName}");
                }
            }

            System.Windows.Forms.Application.DoEvents();
        }

        stopwatch.Stop();
        Log($"Batch complete. Total time: {stopwatch.Elapsed}");

        return new Result();
    }

    private void Log(string message)
    {
        prdDbg(message);
        LogMessage?.Invoke(message);
    }

    private void ReportProgress(
        int drawingIndex, int totalDrawings,
        string drawingName, string operationName,
        int operationIndex, int totalOperations)
    {
        ProgressChanged?.Invoke(new BatchRunProgress(
            drawingIndex, totalDrawings,
            drawingName, operationName,
            operationIndex, totalOperations));
    }
}
