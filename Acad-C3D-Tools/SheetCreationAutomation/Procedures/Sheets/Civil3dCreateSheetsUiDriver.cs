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

        public async Task RunCreateSheetsWizardAsync(
            CreateSheetsWizardRunOptions options,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            ResetSelectorSnapshot();

            IntPtr mainHwnd = Application.MainWindow.Handle;

            IntPtr layoutsDialog = await WaitForDialogAsync(
                mainHwnd, "Create Sheets - View Frame Group and Layouts", cancellationToken);
            progress.Report("Wizard: View Frame Group and Layouts");

            ClickButtonByClassNN(layoutsDialog, "Button13");
            SetTextByClassNN(layoutsDialog, "Edit2",  options.LayoutNamePattern);
            SetTextByClassNN(layoutsDialog, "Edit3", options.NorthArrowBlockName);
            ClickButtonByClassNN(layoutsDialog, "Button2");

            IntPtr sheetSetDialog = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            progress.Report("Wizard: Sheet Set");

            ClickButtonByTitle(sheetSetDialog, "Add to existing sheet set:");
            Trace("DEBUG: clicked 'Add to existing sheet set:', now post-clicking Button20 (browse)...");
            PostClickButtonByClassNN(sheetSetDialog, "Button20");
            Trace("DEBUG: Button20 post-clicked, waiting for Browse dialog...");

            IntPtr browseDialog = await WaitForDialogAsync(mainHwnd, "Browse the Sheet set file", cancellationToken);
            Trace($"DEBUG: Browse dialog found => 0x{browseDialog.ToInt64():X}");
            progress.Report("Wizard: Browse Sheet Set");

            Trace($"DEBUG: Setting Edit1 to '{options.SheetSetFilePath}'...");
            SetTextAsUserInputByClassNN(browseDialog, "Edit1", options.SheetSetFilePath);
            Trace("DEBUG: Edit1 text set, clicking Button7 (Open)...");
            ClickButtonByClassNN(browseDialog, "Button7");
            Trace("DEBUG: Button7 clicked, waiting for Browse dialog to close...");
            await WaitForDialogClosedAsync(mainHwnd, "Browse the Sheet set file", cancellationToken);
            Trace("DEBUG: Browse dialog closed.");

            sheetSetDialog = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            SetTextByClassNN(sheetSetDialog, "Edit8", options.SheetFileNamePattern);            

            IntPtr createSheetsDialog = sheetSetDialog;
            if (!options.PlanOnly)
            {
                ClickButtonByClassNN(sheetSetDialog, "Button2");
                IntPtr profileDialog = await WaitForDialogAsync(mainHwnd, "Create Sheets - Profile Views", cancellationToken);
                progress.Report("Wizard: Profile Views");

                ClickButtonByClassNN(profileDialog, "Button36");
                ClickButtonByClassNN(profileDialog, "Button31");
                ClickButtonByClassNN(profileDialog, "Button33");

                IntPtr profileHeightDialog = await WaitForDialogAsync(
                    mainHwnd, "Create Multiple Profile Views - Profile View Height", cancellationToken);
                ClickButtonByClassNN(profileHeightDialog, "Button8");

                IntPtr multiplePlotDialog = await WaitForDialogAsync(
                    mainHwnd, "Create Multiple Profile Views - Multiple Plot Options", cancellationToken);
                progress.Report("Wizard: Multiple Plot Options");

                SetTextByClassNN(multiplePlotDialog, "Edit1", "50");
                SetTextByClassNN(multiplePlotDialog, "Edit2", "100");
                SetTextByClassNN(multiplePlotDialog, "Edit3", "100");
                ClickButtonByClassNN(multiplePlotDialog, "Button55");
                await WaitForDialogClosedAsync(
                    mainHwnd, "Create Multiple Profile Views - Multiple Plot Options", cancellationToken);

                createSheetsDialog = await WaitForDialogAsync(mainHwnd, "Create Sheets - Profile Views", cancellationToken);
            }

            progress.Report("Wizard: Create Sheets");
            ClickButtonByClassNN(createSheetsDialog, "Button3");

            IntPtr saveConfirmDialog = await WaitForDialogContainingTextAsync(
                mainHwnd,
                "To complete this process your current drawing will be saved.",
                cancellationToken);
            progress.Report("Wizard: Save confirmation");
            ClickButtonByClassNN(saveConfirmDialog, "Button1");

            Trace("WIZARD_COMPLETE: create-clicked and save-confirmed.");
        }

        private void SetValueByMsaaPath(IntPtr dialog, IReadOnlyList<int> path, string value, string fieldName)
        {
            if (!MsaaTools.TrySetValueByChildPath(dialog, path, value, out string error))
            {
                throw new InvalidOperationException(
                    $"Failed to set '{fieldName}' via MSAA path [{string.Join(",", path)}]. {error}");
            }

            Trace($"MSAA_SET: {fieldName}='{value}' path=[{string.Join(",", path)}]");
        }

    }
}
