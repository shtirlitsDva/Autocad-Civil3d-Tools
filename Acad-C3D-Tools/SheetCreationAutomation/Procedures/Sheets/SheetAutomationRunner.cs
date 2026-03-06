using Autodesk.AutoCAD.ApplicationServices;
using SheetCreationAutomation.Models;
using SheetCreationAutomation.Procedures.Common;
using SheetCreationAutomation.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SheetCreationAutomation.Procedures.Sheets
{
    internal sealed class SheetAutomationRunner : DrawingAutomationRunnerBase
    {
        private const int MaxWizardAttemptsPerDrawing = 3;
        private static readonly TimeSpan DynamicInputTimeout = TimeSpan.FromSeconds(15);

        private readonly Civil3dCreateSheetsUiDriver wizardUiDriver;

        public SheetAutomationRunner(
            Civil3dCreateSheetsUiDriver wizardUiDriver,
            WaitPolicy waitPolicy,
            IWaitOverlayPresenter overlayPresenter)
            : base(waitPolicy, overlayPresenter)
        {
            this.wizardUiDriver = wizardUiDriver;
        }

        public async Task<SheetAutomationRunResult> RunAsync(
            SheetAutomationContext context,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            string activeFile = string.Empty;
            string activeStep = string.Empty;
            Stopwatch activeStepStopwatch = new Stopwatch();
            string? hardFailureMessage = null;
            bool cancelled = false;

            try
            {
                ExceptionDispatchInfo? lambdaException = null;

                await AcApp.DocumentManager.ExecuteInCommandContextAsync(async _ =>
                {
                    try
                    {
                        for (int index = 0; index < context.DrawingPaths.Count; index++)
                        {
                            if (cancellationToken.IsCancellationRequested) { cancelled = true; return; }

                            string drawingPath = context.DrawingPaths[index];
                            activeFile = drawingPath;

                            if (cancellationToken.IsCancellationRequested) { cancelled = true; return; }
                            progress.Report($"[{index + 1}/{context.DrawingPaths.Count}] Opening {Path.GetFileName(drawingPath)}...");
                            Document doc = AcApp.DocumentManager.Open(drawingPath, forReadOnly: false);
                            RequestDocumentActivation(doc);
                            WaitResult docActiveResult = await WaitForDocumentActiveAsync(doc, cancellationToken);
                            if (docActiveResult.IsCancelled) { cancelled = true; return; }

                            bool completed = false;
                            for (int attempt = 1; attempt <= MaxWizardAttemptsPerDrawing; attempt++)
                            {
                                if (cancellationToken.IsCancellationRequested) { cancelled = true; return; }
                                progress.Report(
                                    $"[{index + 1}/{context.DrawingPaths.Count}] " +
                                    $"Create Sheets attempt {attempt}/{MaxWizardAttemptsPerDrawing}...");

                                activeStep = "Launch _aecccreatesheets";
                                activeStepStopwatch.Restart();
                                doc.SendStringToExecute("_aecccreatesheets ", true, false, false);
                                activeStepStopwatch.Stop();

                                activeStep = "Drive Create Sheets wizard";
                                activeStepStopwatch.Restart();
                                WaitResult wizardResult = await wizardUiDriver.RunCreateSheetsWizardAsync(
                                    new CreateSheetsWizardRunOptions
                                    {
                                        PlanOnly = context.PlanOnly,
                                        SheetSetFilePath = context.SheetSetFilePath,
                                        LayoutNamePattern =
                                            "<[View Frame Group Alignment Name]> ST <[View Frame Start Station Value]> - <[View Frame End Station Value]>",
                                        SheetFileNamePattern = "<[View Frame Group Alignment Name]>_SHT",
                                        NorthArrowBlockName = "Nordpil2"
                                    },
                                    progress,
                                    cancellationToken);
                                activeStepStopwatch.Stop();
                                if (wizardResult.IsCancelled) { cancelled = true; return; }

                                if (context.PlanOnly)
                                {
                                    completed = true;
                                    break;
                                }

                                activeStep = "Wait for dynamic input prompt";
                                activeStepStopwatch.Restart();
                                WaitResult<IntPtr> dynResult = await WaitForDynamicInputWindowAsync(cancellationToken);
                                activeStepStopwatch.Stop();
                                if (dynResult.IsCancelled) { cancelled = true; return; }
                                IntPtr dynamicInput = dynResult.Value;

                                if (dynamicInput == IntPtr.Zero)
                                {
                                    progress.Report(
                                        $"Dynamic input prompt not detected within {DynamicInputTimeout.TotalSeconds:0}s.");
                                    if (attempt >= MaxWizardAttemptsPerDrawing)
                                    {
                                        hardFailureMessage =
                                            "Create Sheets did not reach dynamic input coordinate prompt. " +
                                            $"Attempts exhausted ({MaxWizardAttemptsPerDrawing}).";
                                        return;
                                    }

                                    if ((await WaitForIdleAsync(cancellationToken)).IsCancelled) { cancelled = true; return; }
                                    progress.Report("Retrying Create Sheets from start for this drawing...");
                                    continue;
                                }

                                activeStep = "Send profile view origin";
                                activeStepStopwatch.Restart();
                                doc.SendStringToExecute(context.ProfileViewOrigin + " ", true, false, false);
                                activeStepStopwatch.Stop();
                                completed = true;
                                break;
                            }

                            if (cancelled) return;

                            if (!completed)
                            {
                                hardFailureMessage ??= "Create Sheets was not completed.";
                                return;
                            }

                            activeStep = "Wait for command idle";
                            activeStepStopwatch.Restart();
                            if ((await WaitForIdleAsync(cancellationToken)).IsCancelled) { cancelled = true; return; }
                            activeStepStopwatch.Stop();

                            activeStep = "Close drawing without save";
                            activeStepStopwatch.Restart();
                            doc.CloseAndDiscard();
                            activeStepStopwatch.Stop();

                            progress.Report($"[{index + 1}/{context.DrawingPaths.Count}] Completed {Path.GetFileName(drawingPath)}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        lambdaException = ExceptionDispatchInfo.Capture(ex);
                    }
                }, null);

                lambdaException?.Throw();

                if (cancelled)
                {
                    return new SheetAutomationRunResult { Outcome = AutomationOutcome.Cancelled };
                }

                if (!string.IsNullOrWhiteSpace(hardFailureMessage))
                {
                    return new SheetAutomationRunResult
                    {
                        Outcome = AutomationOutcome.Failed,
                        Failure = new AutomationFailureInfo
                        {
                            DrawingPath = activeFile,
                            StepName = activeStep,
                            Elapsed = activeStepStopwatch.Elapsed,
                            Message = hardFailureMessage,
                            SelectorSnapshot = wizardUiDriver.LastSelectorSnapshot
                        }
                    };
                }

                return new SheetAutomationRunResult
                {
                    Outcome = AutomationOutcome.Succeeded
                };
            }
            catch (Exception ex)
            {
                return new SheetAutomationRunResult
                {
                    Outcome = AutomationOutcome.Failed,
                    Failure = new AutomationFailureInfo
                    {
                        DrawingPath = activeFile,
                        StepName = activeStep,
                        Elapsed = activeStepStopwatch.Elapsed,
                        Message = ex.Message,
                        SelectorSnapshot = wizardUiDriver.LastSelectorSnapshot
                    }
                };
            }
        }

        private async Task<WaitResult<IntPtr>> WaitForDynamicInputWindowAsync(CancellationToken cancellationToken)
        {
            IntPtr mainHwnd = AcApp.MainWindow.Handle;
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.Elapsed < DynamicInputTimeout)
            {
                if (cancellationToken.IsCancellationRequested)
                    return WaitResult<IntPtr>.Cancel();

                IntPtr dynamicWindow = FindDynamicInputWindow(mainHwnd, mainPid);
                if (dynamicWindow != IntPtr.Zero)
                {
                    return WaitResult<IntPtr>.Of(dynamicWindow);
                }

                try
                {
                    await Task.Delay(WaitPolicy.PollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return WaitResult<IntPtr>.Cancel();
                }
            }

            return WaitResult<IntPtr>.Of(IntPtr.Zero);
        }

        private static IntPtr FindDynamicInputWindow(IntPtr mainHwnd, uint mainPid)
        {
            IntPtr child = Win32WindowTools.FindDescendantByClass(mainHwnd, "CAcDynInputWndControl");
            if (child != IntPtr.Zero)
            {
                WindowMetadata childMeta = Win32WindowTools.GetMetadata(child);
                if (childMeta.ProcessId == mainPid && childMeta.IsVisible)
                {
                    return child;
                }
            }

            IntPtr topLevel = Win32WindowTools.FindTopLevelByClass("CAcDynInputWndControl", mainPid);
            if (topLevel != IntPtr.Zero)
            {
                WindowMetadata topMeta = Win32WindowTools.GetMetadata(topLevel);
                if (topMeta.IsVisible)
                {
                    return topLevel;
                }
            }

            return IntPtr.Zero;
        }

    }
}
