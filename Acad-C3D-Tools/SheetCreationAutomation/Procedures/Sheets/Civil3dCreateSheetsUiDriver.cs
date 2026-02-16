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
        private static readonly int[] LayoutNameMsaaPath = { 4, 11, 4, 18, 4 };
        private static readonly int[] NorthArrowMsaaPath = { 4, 11, 4, 20, 4, 1, 4 };
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
            SetValueByMsaaPath(layoutsDialog, LayoutNameMsaaPath, options.LayoutNamePattern, "Layout name");
            SetValueByMsaaPath(layoutsDialog, NorthArrowMsaaPath, options.NorthArrowBlockName, "North arrow block");
            ClickButtonByClassNN(layoutsDialog, "Button2");

            IntPtr sheetSetDialog = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            progress.Report("Wizard: Sheet Set");

            ClickButtonByClassNN(sheetSetDialog, "Button19");
            ClickButtonByClassNN(sheetSetDialog, "Button20");

            IntPtr browseDialog = await WaitForDialogAsync(mainHwnd, "Browse the Sheet set file", cancellationToken);
            progress.Report("Wizard: Browse Sheet Set");
            SetTextAsUserInputByClassNN(browseDialog, "Edit1", options.SheetSetFilePath);
            ClickButtonByClassNN(browseDialog, "Button7");
            await WaitForDialogClosedAsync(mainHwnd, "Browse the Sheet set file", cancellationToken);

            sheetSetDialog = await WaitForDialogAsync(mainHwnd, "Create Sheets - Sheet Set", cancellationToken);
            SetValueByMsaaPath(sheetSetDialog, SheetFileNameMsaaPath, options.SheetFileNamePattern, "Sheet file name");

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
