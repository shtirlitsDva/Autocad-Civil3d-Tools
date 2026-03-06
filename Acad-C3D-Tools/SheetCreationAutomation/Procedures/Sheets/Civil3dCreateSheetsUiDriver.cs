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
                SetTextByClassNN(multiplePlotDialog, "Edit1", "50");
                SetTextByClassNN(multiplePlotDialog, "Edit2", "100");
                SetTextByClassNN(multiplePlotDialog, "Edit3", "100");
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
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            IntPtr saveConfirmDialog = IntPtr.Zero;
            WaitResult saveWait = await Waiter.WaitUntilAsync("Save confirmation dialog", () =>
            {
                saveConfirmDialog = Win32WindowTools.FindProcessDialogContainingText(
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