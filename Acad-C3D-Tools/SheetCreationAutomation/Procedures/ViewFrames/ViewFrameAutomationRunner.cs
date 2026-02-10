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

            try
            {
                await AcApp.DocumentManager.ExecuteInCommandContextAsync(async _ =>
                {
                    for (int index = 0; index < context.DrawingPaths.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string drawingPath = context.DrawingPaths[index];
                        activeFile = drawingPath;

                        progress.Report($"[{index + 1}/{context.DrawingPaths.Count}] Opening {Path.GetFileName(drawingPath)}...");
                        Document doc = AcApp.DocumentManager.Open(drawingPath, forReadOnly: false);
                        RequestDocumentActivation(doc);
                        await WaitForDocumentActiveAsync(doc, cancellationToken);

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
                            return;
                        }

                        activeStep = "Launch _aecccreateviewframes";
                        activeStepStopwatch.Restart();
                        doc.SendStringToExecute("_aecccreateviewframes ", true, false, false);
                        activeStepStopwatch.Stop();

                        activeStep = "Drive Create View Frames wizard";
                        activeStepStopwatch.Restart();
                        await wizardUiDriver.RunCreateViewFramesWizardAsync(
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

                        activeStep = "Wait for command idle";
                        activeStepStopwatch.Restart();
                        await WaitForIdleAsync(cancellationToken);
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
                            return;
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
                }, null);

                if (!string.IsNullOrWhiteSpace(hardFailureMessage))
                {
                    return new AutomationRunResult
                    {
                        Succeeded = false,
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
                    Succeeded = true,
                    FinalNextViewFrameCounter = nextCounter
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new AutomationRunResult
                {
                    Succeeded = false,
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
