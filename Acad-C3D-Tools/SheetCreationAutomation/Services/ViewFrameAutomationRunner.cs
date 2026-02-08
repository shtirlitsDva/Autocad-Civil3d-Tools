using Autodesk.AutoCAD.ApplicationServices;
using SheetCreationAutomation.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SheetCreationAutomation.Services
{
    internal sealed class ViewFrameAutomationRunner : IViewFrameAutomationRunner
    {
        private readonly IViewFrameCountService viewFrameCountService;
        private readonly IWizardUiDriver wizardUiDriver;
        private readonly AutomationWaiter waiter;

        public ViewFrameAutomationRunner(
            IViewFrameCountService viewFrameCountService,
            IWizardUiDriver wizardUiDriver,
            WaitPolicy waitPolicy,
            IWaitOverlayPresenter overlayPresenter)
        {
            this.viewFrameCountService = viewFrameCountService;
            this.wizardUiDriver = wizardUiDriver;
            waiter = new AutomationWaiter(waitPolicy, overlayPresenter);
        }

        public async Task<AutomationRunResult> RunAsync(
            ViewFrameAutomationContext context,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            // TEMP DEBUG MODE:
            // - Run only the first drawing in the list
            // - Close drawing without saving changes
            const bool debugFirstDrawingOnly = true;
            const bool debugCloseWithoutSaving = true;

            int nextCounter = context.NextViewFrameCounterNumber;
            string activeFile = string.Empty;
            string activeStep = string.Empty;
            Stopwatch activeStepStopwatch = new Stopwatch();

            try
            {
                await AcApp.DocumentManager.ExecuteInCommandContextAsync(async _ =>
                {
                    for (int index = 0; index < context.DrawingPaths.Count; index++)
                    {
                        if (debugFirstDrawingOnly && index > 0)
                        {
                            progress.Report("[DEBUG] First-drawing mode active. Stopping before next drawing.");
                            break;
                        }

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

                        if (beforeCount > 0)
                        {
                            throw new InvalidOperationException(
                                $"Drawing already contains {beforeCount} view frame(s). " +
                                "This run requires zero pre-existing view frames.");
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
                                TemplateFileName = context.TemplateFileName,
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

                        int delta = afterCount - beforeCount;
                        if (delta <= 0)
                        {
                            throw new InvalidOperationException(
                                $"No new view frames detected in drawing '{Path.GetFileName(drawingPath)}'. " +
                                $"Before={beforeCount}, After={afterCount}.");
                        }

                        nextCounter += delta;
                        progress.Report(
                            $"[{index + 1}/{context.DrawingPaths.Count}] " +
                            $"Created {delta} view frame(s), next counter = {nextCounter}.");

                        activeStep = debugCloseWithoutSaving
                            ? "Close drawing without saving (debug)"
                            : "Save and close drawing";
                        activeStepStopwatch.Restart();
                        if (debugCloseWithoutSaving)
                        {
                            doc.CloseAndDiscard();
                            progress.Report("[DEBUG] Closed drawing without saving.");
                        }
                        else
                        {
                            string fullPath = doc.Name;
                            doc.CloseAndSave(fullPath);
                        }
                        activeStepStopwatch.Stop();
                    }
                }, null);

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

        private async Task WaitForDocumentActiveAsync(Document doc, CancellationToken cancellationToken)
        {
            RequestDocumentActivation(doc);

            await waiter.WaitUntilAsync($"Document active: {Path.GetFileName(doc.Name)}", () =>
            {
                if (AcApp.DocumentManager.MdiActiveDocument == doc)
                {
                    return true;
                }

                RequestDocumentActivation(doc);
                return false;
            }, cancellationToken);
        }

        private static void RequestDocumentActivation(Document doc)
        {
            if (AcApp.DocumentManager.MdiActiveDocument == doc)
            {
                return;
            }

            AcApp.DocumentManager.MdiActiveDocument = doc;
        }

        private async Task WaitForIdleAsync(CancellationToken cancellationToken)
        {
            await waiter.WaitUntilAsync("AutoCAD command idle", () =>
            {
                object? cmdNames = AcApp.GetSystemVariable("CMDNAMES");
                string activeCommands = cmdNames?.ToString() ?? string.Empty;
                return string.IsNullOrWhiteSpace(activeCommands);
            }, cancellationToken);
        }
    }
}
