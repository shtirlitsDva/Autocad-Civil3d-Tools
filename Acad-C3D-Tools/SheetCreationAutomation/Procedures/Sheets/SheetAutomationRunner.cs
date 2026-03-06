using Autodesk.AutoCAD.ApplicationServices;
using SheetCreationAutomation.Models;
using SheetCreationAutomation.Procedures.Common;
using SheetCreationAutomation.Services;
using System;
using System.Diagnostics;
using System.IO;
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
                for (int index = 0; index < context.DrawingPaths.Count; index++)
                {
                    if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }

                    string drawingPath = context.DrawingPaths[index];
                    activeFile = drawingPath;

                    if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }
                    progress.Report($"[{index + 1}/{context.DrawingPaths.Count}] Opening {Path.GetFileName(drawingPath)}...");
                    Document doc = AcApp.DocumentManager.Open(drawingPath, forReadOnly: false);
                    RequestDocumentActivation(doc);
                    WaitResult docActiveResult = await WaitForDocumentActiveAsync(doc, cancellationToken);
                    if (!docActiveResult.IsCompleted)
                    {
                        if (docActiveResult.IsCancelled) cancelled = true;
                        else hardFailureMessage = docActiveResult.Message;
                        break;
                    }

                    bool completed = false;
                    for (int attempt = 1; attempt <= MaxWizardAttemptsPerDrawing; attempt++)
                    {
                        if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }
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
                        if (!wizardResult.IsCompleted)
                        {
                            if (wizardResult.IsCancelled) cancelled = true;
                            else hardFailureMessage = wizardResult.Message;
                            break;
                        }

                        if (context.PlanOnly)
                        {
                            completed = true;
                            break;
                        }

                        activeStep = "Wait for dynamic input prompt";
                        activeStepStopwatch.Restart();
                        WaitResult<IntPtr> dynResult = await WaitForDynamicInputWindowAsync(cancellationToken);
                        activeStepStopwatch.Stop();
                        if (!dynResult.IsCompleted)
                        {
                            WaitResult dynOutcome = dynResult.Discard();
                            if (dynOutcome.IsCancelled) cancelled = true;
                            else hardFailureMessage = dynOutcome.Message;
                            break;
                        }
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
                                break;
                            }

                            WaitResult idleResult = await WaitForIdleAsync(cancellationToken);
                            if (!idleResult.IsCompleted)
                            {
                                if (idleResult.IsCancelled) cancelled = true;
                                else hardFailureMessage = idleResult.Message;
                                break;
                            }
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

                    if (cancelled) break;

                    if (!completed)
                    {
                        hardFailureMessage ??= "Create Sheets was not completed.";
                        break;
                    }

                    activeStep = "Wait for command idle";
                    activeStepStopwatch.Restart();
                    WaitResult idleResult2 = await WaitForIdleAsync(cancellationToken);
                    if (!idleResult2.IsCompleted)
                    {
                        if (idleResult2.IsCancelled) cancelled = true;
                        else hardFailureMessage = idleResult2.Message;
                        break;
                    }
                    activeStepStopwatch.Stop();

                    activeStep = "Close drawing without save";
                    activeStepStopwatch.Restart();
                    doc.CloseAndDiscard();
                    activeStepStopwatch.Stop();

                    progress.Report($"[{index + 1}/{context.DrawingPaths.Count}] Completed {Path.GetFileName(drawingPath)}.");
                }

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

                await Task.Delay(WaitPolicy.PollInterval).ConfigureAwait(false);
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
