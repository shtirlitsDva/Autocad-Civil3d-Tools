using Autodesk.AutoCAD.ApplicationServices;
using SheetCreationAutomation.Models;
using SheetCreationAutomation.Procedures.Common;
using SheetCreationAutomation.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SheetCreationAutomation.Procedures.Sheets
{
    internal sealed class Civil3dCreateSheetsUiDriver : WizardUiDriverBase
    {           
        private static readonly int[] SheetFileNameMsaaPath = { 4, 12, 4, 18, 4 };

        public Civil3dCreateSheetsUiDriver(IWaitOverlayPresenter overlayPresenter, WaitPolicy waitPolicy)
            : base(overlayPresenter, waitPolicy)
        {
        }

        public async Task<WaitResult> RunCreateSheetsWizardAsync(
            CreateSheetsWizardRunOptions options,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            ResetSelectorSnapshot();

            IntPtr mainHwnd = Application.MainWindow.Handle;

            var layoutsDialogResult = await WaitForDialogAsync(
                mainHwnd, "Create Sheets - View Frame Group and Layouts", cancellationToken);
            if (!layoutsDialogResult.IsCompleted) return layoutsDialogResult.Discard();
            IntPtr layoutsDialog = layoutsDialogResult.Value;
            progress.Report("Wizard: View Frame Group and Layouts");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            ClickButtonByClassNN(layoutsDialog, "Button13");
            SetTextByClassNN(layoutsDialog, "Edit2",  options.LayoutNamePattern);
            SetTextByClassNN(layoutsDialog, "Edit3", options.NorthArrowBlockName);
            ClickButtonByClassNN(layoutsDialog, "Button2");

            var sheetSetDialogResult = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            if (!sheetSetDialogResult.IsCompleted) return sheetSetDialogResult.Discard();
            IntPtr sheetSetDialog = sheetSetDialogResult.Value;
            progress.Report("Wizard: Sheet Set");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            ClickButtonByTitle(sheetSetDialog, "Add to existing sheet set:");
            PostClickButtonByClassNN(sheetSetDialog, "Button20");

            var browseDialogResult = await WaitForDialogAsync(mainHwnd, "Browse the Sheet set file", cancellationToken);
            if (!browseDialogResult.IsCompleted) return browseDialogResult.Discard();
            IntPtr browseDialog = browseDialogResult.Value;
            progress.Report("Wizard: Browse Sheet Set");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            SetTextAsUserInputByClassNN(browseDialog, "Edit1", options.SheetSetFilePath);
            ClickButtonByClassNN(browseDialog, "Button7");
            var browseCloseResult = await WaitForDialogClosedAsync(mainHwnd, "Browse the Sheet set file", cancellationToken);
            if (!browseCloseResult.IsCompleted) return browseCloseResult;

            var sheetSetDialogResult2 = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            if (!sheetSetDialogResult2.IsCompleted) return sheetSetDialogResult2.Discard();
            sheetSetDialog = sheetSetDialogResult2.Value;
            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            var editEnabledResult = await WaitForControlEnabledAsync(sheetSetDialog, "Edit8", cancellationToken);
            if (!editEnabledResult.IsCompleted) return editEnabledResult;
            SetTextAsUserInputByClassNN(sheetSetDialog, "Edit8", options.SheetFileNamePattern);

            IntPtr createSheetsDialog = sheetSetDialog;
            if (!options.PlanOnly)
            {
                ClickButtonByClassNN(sheetSetDialog, "Button2");
                var profileDialogResult = await WaitForDialogAsync(mainHwnd, "Create Sheets - Profile Views", cancellationToken);
                if (!profileDialogResult.IsCompleted) return profileDialogResult.Discard();
                IntPtr profileDialog = profileDialogResult.Value;
                progress.Report("Wizard: Profile Views");

                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                ClickButtonByClassNN(profileDialog, "Button36");
                ClickButtonByClassNN(profileDialog, "Button31");
                PostClickButtonByClassNN(profileDialog, "Button33");

                var profileHeightDialogResult = await WaitForDialogAsync(
                    mainHwnd, "Create Multiple Profile Views - Profile View Height", cancellationToken);
                if (!profileHeightDialogResult.IsCompleted) return profileHeightDialogResult.Discard();
                IntPtr profileHeightDialog = profileHeightDialogResult.Value;
                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                ClickControlCenterByTitle(profileHeightDialog, "Multiple Plot Options");

                var multiplePlotDialogResult = await WaitForDialogAsync(
                    mainHwnd, "Create Multiple Profile Views - Multiple Plot Options", cancellationToken);
                if (!multiplePlotDialogResult.IsCompleted) return multiplePlotDialogResult.Discard();
                IntPtr multiplePlotDialog = multiplePlotDialogResult.Value;
                progress.Report("Wizard: Multiple Plot Options");

                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                SetTextAsUserInputByClassNN(multiplePlotDialog, "Edit1", "50");
                SetTextAsUserInputByClassNN(multiplePlotDialog, "Edit2", "100");
                SetTextAsUserInputByClassNN(multiplePlotDialog, "Edit3", "100");
                PostClickButtonByClassNN(multiplePlotDialog, "Button55");
                var multiplePlotCloseResult = await WaitForDialogClosedAsync(
                    mainHwnd, "Create Multiple Profile Views - Multiple Plot Options", cancellationToken);
                if (!multiplePlotCloseResult.IsCompleted) return multiplePlotCloseResult;

                var createSheetsDialogResult = await WaitForDialogAsync(mainHwnd, "Create Sheets - Profile Views", cancellationToken);
                if (!createSheetsDialogResult.IsCompleted) return createSheetsDialogResult.Discard();
                createSheetsDialog = createSheetsDialogResult.Value;
            }

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            var buttonEnabledResult = await WaitForControlEnabledAsync(createSheetsDialog, "Button3", cancellationToken);
            if (!buttonEnabledResult.IsCompleted) return buttonEnabledResult;
            progress.Report("Wizard: Create Sheets");
            PostClickButtonByClassNN(createSheetsDialog, "Button3");
            // The save confirmation ("To complete this process your current drawing will be saved.")
            // is a standard MessageBox — an owned top-level popup (#32770), NOT a WS_CHILD of
            // the wizard. This means:
            //
            // 1) EnumChildWindows(wizard) cannot find it. Only EnumWindows (top-level search) can.
            //    FindParentOfTextControl walks ALL top-level process windows and their children,
            //    finds the Static containing the message text, then returns GetParent(static) —
            //    the confirmation dialog — regardless of where it sits in the hierarchy.
            //
            // 2) After PostClick(BM_CLICK) on "Create Sheets", AutoCAD does heavy synchronous work
            //    (saving the drawing) before showing the MessageBox. During this time the UI thread
            //    hasn't pumped messages, so Windows marks it "hung". GetTextSafe uses
            //    SendMessageTimeoutW with SMTO_ABORTIFHUNG, which returns empty immediately for
            //    any window on a hung thread — even after the modal dialog appears, it can take a
            //    moment for Windows to clear the hung status. The delay + ConfigureAwait(false)
            //    ensures we (a) give AutoCAD time to finish and show the dialog, and (b) move off
            //    the AutoCAD SynchronizationContext onto a thread pool thread so our polling never
            //    blocks AutoCAD's message pump.
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            IntPtr saveConfirmDialog = IntPtr.Zero;
            WaitResult saveWait = await Waiter.WaitUntilAsync("Save confirmation dialog", () =>
            {
                saveConfirmDialog = Win32WindowTools.FindParentOfTextControl(
                    mainPid, "To complete this process");
                return saveConfirmDialog != IntPtr.Zero;
            }, cancellationToken, continueOnCapturedContext: false);
            if (!saveWait.IsCompleted) return saveWait;
            progress.Report("Wizard: Save confirmation");
            PostClickButtonByClassNN(saveConfirmDialog, "Button1");

            

            Trace("WIZARD_COMPLETE: create-clicked and save-confirmed.");
            return WaitResult.Completed;
        }
    }
}