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
                            AutomationRunLog.Append(
                                $"RETRY: attempt {attempt}/{MaxWizardAttemptsPerDrawing} — restarting wizard");
                            progress.Report("Retrying Create Sheets from start for this drawing...");
                            continue;
                        }

                        activeStep = "Send profile view origin";
                        activeStepStopwatch.Restart();
                        AutomationRunLog.Append($"SEND_COORDS: '{context.ProfileViewOrigin}'");
                        foreach (char c in context.ProfileViewOrigin)
                        {
                            Win32WindowTools.TypeChar(c);
                            await Task.Delay(100).ConfigureAwait(false);
                        }
                        await Task.Delay(500).ConfigureAwait(false);
                        Win32WindowTools.TypeEnter();
                        await Task.Delay(1000).ConfigureAwait(false);
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

            await Task.Delay(2000).ConfigureAwait(false);

            AutomationRunLog.Append("DYNINPUT_TREE_DUMP:");
            AutomationRunLog.Append(DumpAllProcessWindows(mainHwnd, mainPid));

            Stopwatch sw = Stopwatch.StartNew();

            while (sw.Elapsed < DynamicInputTimeout)
            {
                if (cancellationToken.IsCancellationRequested)
                    return WaitResult<IntPtr>.Cancel();

                IntPtr dynamicWindow = FindDynamicInputWindow(mainHwnd, mainPid);
                if (dynamicWindow != IntPtr.Zero)
                {
                    AutomationRunLog.Append(
                        $"DYNAMIC_INPUT_FOUND: hwnd=0x{dynamicWindow.ToInt64():X} after {sw.Elapsed.TotalSeconds:F1}s");
                    return WaitResult<IntPtr>.Of(dynamicWindow);
                }

                await Task.Delay(WaitPolicy.PollInterval).ConfigureAwait(false);
            }

            AutomationRunLog.Append(
                $"DYNAMIC_INPUT_TIMEOUT: not found after {DynamicInputTimeout.TotalSeconds:0}s (cancelled by AutoCAD?)");
            return WaitResult<IntPtr>.Of(IntPtr.Zero);
        }

        private static string DumpAllProcessWindows(IntPtr mainHwnd, uint mainPid)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("--- Top-level windows ---");
            foreach (IntPtr hwnd in Win32WindowTools.EnumerateTopLevelWindows())
            {
                WindowMetadata meta = Win32WindowTools.GetMetadata(hwnd);
                if (meta.ProcessId != mainPid) continue;
                sb.AppendLine(
                    $"  TOP 0x{hwnd.ToInt64():X} cls='{meta.ClassName}' vis={meta.IsVisible} title='{meta.Title}'");
            }

            sb.AppendLine("--- Children of mainHwnd with 'DynInput' or 'Dyn' in class ---");
            foreach (IntPtr hwnd in Win32WindowTools.EnumerateChildWindows(mainHwnd))
            {
                string cls = Win32WindowTools.GetClassNameRaw(hwnd);
                if (cls.Contains("Dyn", StringComparison.OrdinalIgnoreCase)
                    || cls.Contains("Input", StringComparison.OrdinalIgnoreCase))
                {
                    bool vis = Win32WindowTools.GetMetadata(hwnd).IsVisible;
                    sb.AppendLine($"  CHILD 0x{hwnd.ToInt64():X} cls='{cls}' vis={vis}");
                }
            }

            return sb.ToString();
        }

        private static IntPtr FindDynamicInputWindow(IntPtr mainHwnd, uint mainPid)
        {
            // The Win32 class of the dynamic input window is an MFC-generated name
            // like "Afx:00007FF7...", NOT "CAcDynInputWndControl".
            // "CAcDynInputWndControl" is the window TITLE.
            // The AHK WinWait("CAcDynInputWndControl") matched by title — we must do the same.
            foreach (IntPtr hwnd in Win32WindowTools.EnumerateTopLevelWindows())
            {
                WindowMetadata meta = Win32WindowTools.GetMetadata(hwnd);
                if (meta.ProcessId != mainPid) continue;
                if (!meta.IsVisible) continue;
                if (string.Equals(meta.Title, "CAcDynInputWndControl", StringComparison.OrdinalIgnoreCase))
                    return hwnd;
            }

            foreach (IntPtr hwnd in Win32WindowTools.EnumerateChildWindows(mainHwnd))
            {
                WindowMetadata meta = Win32WindowTools.GetMetadata(hwnd);
                if (meta.ProcessId != mainPid) continue;
                if (!meta.IsVisible) continue;
                if (string.Equals(meta.Title, "CAcDynInputWndControl", StringComparison.OrdinalIgnoreCase))
                    return hwnd;
            }

            return IntPtr.Zero;
        }

    }
}
