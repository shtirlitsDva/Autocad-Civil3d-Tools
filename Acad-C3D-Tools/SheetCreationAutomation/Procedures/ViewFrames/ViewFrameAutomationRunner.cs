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

namespace SheetCreationAutomation.Procedures.ViewFrames
{
    internal sealed class ViewFrameAutomationRunner : DrawingAutomationRunnerBase
    {
        private readonly ViewFrameCountService viewFrameCountService;
        private readonly Civil3dWizardUiDriver wizardUiDriver;

        public ViewFrameAutomationRunner(
            ViewFrameCountService viewFrameCountService,
            Civil3dWizardUiDriver wizardUiDriver,
            WaitPolicy waitPolicy,
            IWaitOverlayPresenter overlayPresenter)
            : base(waitPolicy, overlayPresenter)
        {
            this.viewFrameCountService = viewFrameCountService;
            this.wizardUiDriver = wizardUiDriver;
        }

        public async Task<AutomationRunResult> RunAsync(
            ViewFrameAutomationContext context,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            int nextCounter = context.NextViewFrameCounterNumber;
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

                    progress.Report($"[{index + 1}/{context.DrawingPaths.Count}] Opening {Path.GetFileName(drawingPath)}...");
                    Document doc = AcApp.DocumentManager.Open(drawingPath, forReadOnly: false);
                    RequestDocumentActivation(doc);
                    WaitResult waitResult = await WaitForDocumentActiveAsync(doc, cancellationToken);
                    if (!waitResult.IsCompleted)
                    {
                        if (waitResult.IsCancelled) cancelled = true;
                        else hardFailureMessage = waitResult.Message;
                        break;
                    }

                    activeStep = "Pre-check view frame count";
                    activeStepStopwatch.Restart();
                    int beforeCount = viewFrameCountService.GetViewFrameCount(doc.Database);
                    activeStepStopwatch.Stop();
                    progress.Report($"DEBUG: before view-frame count={beforeCount}");

                    if (beforeCount > 0)
                    {
                        hardFailureMessage =
                            $"Drawing already contains {beforeCount} view frame(s). " +
                            "This run requires zero pre-existing view frames.";
                        activeStep = "Pre-check view frame count";
                        break;
                    }

                    activeStep = "Launch _aecccreateviewframes";
                    activeStepStopwatch.Restart();
                    doc.SendStringToExecute("_aecccreateviewframes ", true, false, false);
                    activeStepStopwatch.Stop();

                    activeStep = "Drive Create View Frames wizard";
                    activeStepStopwatch.Restart();
                    WaitResult wizardResult = await wizardUiDriver.RunCreateViewFramesWizardAsync(
                        new WizardRunOptions
                        {
                            IsFirstFile = index == 0,
                            PlanOnly = context.PlanOnly,
                            TemplateFilePath = context.TemplateFilePath,
                            NextViewFrameCounterNumber = nextCounter,
                            ViewOverlap = context.ViewOverlap
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

                    activeStep = "Wait for command idle";
                    activeStepStopwatch.Restart();
                    WaitResult idleResult = await WaitForIdleAsync(cancellationToken);
                    if (!idleResult.IsCompleted)
                    {
                        if (idleResult.IsCancelled) cancelled = true;
                        else hardFailureMessage = idleResult.Message;
                        break;
                    }
                    activeStepStopwatch.Stop();

                    activeStep = "Post-check view frame count";
                    activeStepStopwatch.Restart();
                    int afterCount = viewFrameCountService.GetViewFrameCount(doc.Database);
                    activeStepStopwatch.Stop();
                    progress.Report($"DEBUG: after view-frame count={afterCount}");

                    int delta = afterCount - beforeCount;
                    progress.Report($"DEBUG: view-frame delta={delta}");
                    if (delta <= 0)
                    {
                        hardFailureMessage =
                            $"No new view frames detected in drawing '{Path.GetFileName(drawingPath)}'. " +
                            $"Before={beforeCount}, After={afterCount}.";
                        activeStep = "Post-check view frame count";
                        break;
                    }

                    nextCounter += delta;
                    progress.Report(
                        $"[{index + 1}/{context.DrawingPaths.Count}] " +
                        $"Created {delta} view frame(s), next counter = {nextCounter}.");

                    activeStep = "Save and close drawing";
                    activeStepStopwatch.Restart();
                    string fullPath = doc.Name;
                    doc.CloseAndSave(fullPath);
                    activeStepStopwatch.Stop();
                }

                if (cancelled)
                {
                    return new AutomationRunResult
                    {
                        Outcome = AutomationOutcome.Cancelled,
                        FinalNextViewFrameCounter = nextCounter
                    };
                }

                if (!string.IsNullOrWhiteSpace(hardFailureMessage))
                {
                    return new AutomationRunResult
                    {
                        Outcome = AutomationOutcome.Failed,
                        FinalNextViewFrameCounter = nextCounter,
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

                return new AutomationRunResult
                {
                    Outcome = AutomationOutcome.Succeeded,
                    FinalNextViewFrameCounter = nextCounter
                };
            }
            catch (Exception ex)
            {
                return new AutomationRunResult
                {
                    Outcome = AutomationOutcome.Failed,
                    FinalNextViewFrameCounter = nextCounter,
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

    }
}
