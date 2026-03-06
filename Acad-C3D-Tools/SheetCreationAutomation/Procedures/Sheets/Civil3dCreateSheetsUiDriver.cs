using Autodesk.AutoCAD.ApplicationServices;
using SheetCreationAutomation.Models;
using SheetCreationAutomation.Procedures.Common;
using SheetCreationAutomation.Services;
using System;
using System.Collections.Generic;
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
            if (layoutsDialogResult.IsCancelled) return WaitResult.Cancelled;
            IntPtr layoutsDialog = layoutsDialogResult.Value;
            progress.Report("Wizard: View Frame Group and Layouts");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            ClickButtonByClassNN(layoutsDialog, "Button13");
            SetTextByClassNN(layoutsDialog, "Edit2",  options.LayoutNamePattern);
            SetTextByClassNN(layoutsDialog, "Edit3", options.NorthArrowBlockName);
            ClickButtonByClassNN(layoutsDialog, "Button2");

            var sheetSetDialogResult = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            if (sheetSetDialogResult.IsCancelled) return WaitResult.Cancelled;
            IntPtr sheetSetDialog = sheetSetDialogResult.Value;
            progress.Report("Wizard: Sheet Set");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            ClickButtonByTitle(sheetSetDialog, "Add to existing sheet set:");
            Trace("DEBUG: clicked 'Add to existing sheet set:', now post-clicking Button20 (browse)...");
            PostClickButtonByClassNN(sheetSetDialog, "Button20");
            Trace("DEBUG: Button20 post-clicked, waiting for Browse dialog...");

            var browseDialogResult = await WaitForDialogAsync(mainHwnd, "Browse the Sheet set file", cancellationToken);
            if (browseDialogResult.IsCancelled) return WaitResult.Cancelled;
            IntPtr browseDialog = browseDialogResult.Value;
            Trace($"DEBUG: Browse dialog found => 0x{browseDialog.ToInt64():X}");
            progress.Report("Wizard: Browse Sheet Set");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            Trace($"DEBUG: Setting Edit1 to '{options.SheetSetFilePath}'...");
            SetTextAsUserInputByClassNN(browseDialog, "Edit1", options.SheetSetFilePath);
            Trace("DEBUG: Edit1 text set, clicking Button7 (Open)...");
            ClickButtonByClassNN(browseDialog, "Button7");
            Trace("DEBUG: Button7 clicked, waiting for Browse dialog to close...");
            if ((await WaitForDialogClosedAsync(mainHwnd, "Browse the Sheet set file", cancellationToken)).IsCancelled) return WaitResult.Cancelled;
            Trace("DEBUG: Browse dialog closed.");

            var sheetSetDialogResult2 = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            if (sheetSetDialogResult2.IsCancelled) return WaitResult.Cancelled;
            sheetSetDialog = sheetSetDialogResult2.Value;
            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            if ((await WaitForControlEnabledAsync(sheetSetDialog, "Edit8", cancellationToken)).IsCancelled) return WaitResult.Cancelled;
            SetTextAsUserInputByClassNN(sheetSetDialog, "Edit8", options.SheetFileNamePattern);

            IntPtr createSheetsDialog = sheetSetDialog;
            if (!options.PlanOnly)
            {
                ClickButtonByClassNN(sheetSetDialog, "Button2");
                var profileDialogResult = await WaitForDialogAsync(mainHwnd, "Create Sheets - Profile Views", cancellationToken);
                if (profileDialogResult.IsCancelled) return WaitResult.Cancelled;
                IntPtr profileDialog = profileDialogResult.Value;
                progress.Report("Wizard: Profile Views");

                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                ClickButtonByClassNN(profileDialog, "Button36");
                ClickButtonByClassNN(profileDialog, "Button31");
                PostClickButtonByClassNN(profileDialog, "Button33");

                var profileHeightDialogResult = await WaitForDialogAsync(
                    mainHwnd, "Create Multiple Profile Views - Profile View Height", cancellationToken);
                if (profileHeightDialogResult.IsCancelled) return WaitResult.Cancelled;
                IntPtr profileHeightDialog = profileHeightDialogResult.Value;
                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                ClickControlCenterByTitle(profileHeightDialog, "Multiple Plot Options");

                var multiplePlotDialogResult = await WaitForDialogAsync(
                    mainHwnd, "Create Multiple Profile Views - Multiple Plot Options", cancellationToken);
                if (multiplePlotDialogResult.IsCancelled) return WaitResult.Cancelled;
                IntPtr multiplePlotDialog = multiplePlotDialogResult.Value;
                progress.Report("Wizard: Multiple Plot Options");

                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                Thread.Sleep(3000);
                SetTextByClassNN(multiplePlotDialog, "Edit1", "50");
                Thread.Sleep(2000);
                SetTextByClassNN(multiplePlotDialog, "Edit2", "100");
                Thread.Sleep(2000);
                SetTextByClassNN(multiplePlotDialog, "Edit3", "100");
                Thread.Sleep(3000);
                ClickButtonByClassNN(multiplePlotDialog, "Button55");
                if ((await WaitForDialogClosedAsync(
                    mainHwnd, "Create Multiple Profile Views - Multiple Plot Options", cancellationToken)).IsCancelled) return WaitResult.Cancelled;

                var createSheetsDialogResult = await WaitForDialogAsync(mainHwnd, "Create Sheets - Profile Views", cancellationToken);
                if (createSheetsDialogResult.IsCancelled) return WaitResult.Cancelled;
                createSheetsDialog = createSheetsDialogResult.Value;
            }

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            progress.Report("Wizard: Create Sheets");
            ClickButtonByClassNN(createSheetsDialog, "Button3");

            var saveConfirmDialogResult = await WaitForDialogContainingTextAsync(
                mainHwnd,
                "To complete this process your current drawing will be saved.",
                cancellationToken);
            if (saveConfirmDialogResult.IsCancelled) return WaitResult.Cancelled;
            IntPtr saveConfirmDialog = saveConfirmDialogResult.Value;
            progress.Report("Wizard: Save confirmation");
            ClickButtonByClassNN(saveConfirmDialog, "Button1");

            Trace("WIZARD_COMPLETE: create-clicked and save-confirmed.");
            return WaitResult.Completed;
        }
    }
}