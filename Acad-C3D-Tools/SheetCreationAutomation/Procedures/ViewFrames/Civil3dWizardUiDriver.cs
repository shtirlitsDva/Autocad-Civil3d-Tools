using Autodesk.AutoCAD.ApplicationServices;

using SheetCreationAutomation.Models;
using SheetCreationAutomation.Procedures.Common;
using SheetCreationAutomation.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace SheetCreationAutomation.Procedures.ViewFrames
{
    internal sealed class Civil3dWizardUiDriver : WizardUiDriverBase
    {
        public Civil3dWizardUiDriver(IWaitOverlayPresenter overlayPresenter, WaitPolicy waitPolicy)
            : base(overlayPresenter, waitPolicy)
        {
        }

        public async Task<WaitResult> RunCreateViewFramesWizardAsync(
            WizardRunOptions options,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            ResetSelectorSnapshot();

            IntPtr mainHwnd = Application.MainWindow.Handle;

            var alignmentDialogResult = await WaitForDialogAsync(mainHwnd, "Create View Frames - Alignment", cancellationToken);
            if (!alignmentDialogResult.IsCompleted) return alignmentDialogResult.Discard();
            IntPtr alignmentDialog = alignmentDialogResult.Value;
            progress.Report("Wizard: Alignment -> Next");
            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            ClickButtonByClassNN(alignmentDialog, "Button2");

            var sheetsDialogResult = await WaitForDialogAsync(mainHwnd, "Create View Frames - Sheets", cancellationToken);
            if (!sheetsDialogResult.IsCompleted) return sheetsDialogResult.Discard();
            IntPtr sheetsDialog = sheetsDialogResult.Value;
            progress.Report("Wizard: Sheets");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            if (options.PlanOnly)
            {
                ClickButtonByClassNN(sheetsDialog, "Button15");
            }

            if (options.IsFirstFile)
            {
                ClickButtonByClassNN(sheetsDialog, "Button17");
                var selectLayoutDialogResult = await WaitForDialogAsync(
                    mainHwnd, "Select Layout as Sheet Template", cancellationToken);
                if (!selectLayoutDialogResult.IsCompleted) return selectLayoutDialogResult.Discard();
                IntPtr selectLayoutDialog = selectLayoutDialogResult.Value;
                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                SetTextAsUserInputByClassNN(selectLayoutDialog, "Edit1", options.TemplateFilePath);
                SendEnterByClassNN(selectLayoutDialog, "Edit1");
                var templateLoadResult = await WaitForTemplateLayoutsLoadedAsync(selectLayoutDialog, cancellationToken);
                if (!templateLoadResult.IsCompleted) return templateLoadResult;
                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                ClickButtonByClassNN(selectLayoutDialog, "Button1");
            }

            var nextButtonResult = await WaitForButtonEnabledAsync(
                mainHwnd,
                "Create View Frames - Sheets",
                "Button2",
                cancellationToken,
                "Next");
            if (!nextButtonResult.IsCompleted) return nextButtonResult;

            ClickButtonByClassNN(sheetsDialog, "Button2");

            var groupDialogResult = await WaitForDialogAsync(
                mainHwnd, "Create View Frames - View Frame Group", cancellationToken);
            if (!groupDialogResult.IsCompleted) return groupDialogResult.Discard();
            IntPtr groupDialog = groupDialogResult.Value;
            progress.Report("Wizard: View Frame Group");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            ClickButtonByClassNN(groupDialog, "Button28");

            var nameTemplateDialogResult = await WaitForDialogAsync(
                mainHwnd, "Name Template", cancellationToken);
            if (!nameTemplateDialogResult.IsCompleted) return nameTemplateDialogResult.Discard();
            IntPtr nameTemplateDialog = nameTemplateDialogResult.Value;
            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            ClickButtonByClassNN(nameTemplateDialog, "Button2");
            SetTextByClassNN(
                nameTemplateDialog, "Edit2", options.NextViewFrameCounterNumber.ToString());
            ClickButtonByClassNN(nameTemplateDialog, "Button4");

            var nameTemplateCloseResult = await Waiter.WaitUntilAsync("Name Template dialog to close", () =>
            {
                IntPtr hwnd = FindDialog(
                    mainHwnd,
                    new DialogSearchOptions("Name Template")
                    {
                        RequiredClassName = "#32770",
                        MatchMode = TitleMatchMode.ExactOrContains
                    });
                return hwnd == IntPtr.Zero;
            }, cancellationToken, continueOnCapturedContext: false);
            if (!nameTemplateCloseResult.IsCompleted) return nameTemplateCloseResult;

            SelectComboString(groupDialog, "ComboBox4", "Middle center");
            ClickButtonByClassNN(groupDialog, "Button2");

            var matchLinesDialogResult = await WaitForDialogAsync(
                mainHwnd, "Create View Frames - Match Lines", cancellationToken);
            if (!matchLinesDialogResult.IsCompleted) return matchLinesDialogResult.Discard();
            IntPtr matchLinesDialog = matchLinesDialogResult.Value;
            progress.Report("Wizard: Match Lines");

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            EnsureCheckBoxCheckedByName(matchLinesDialog, "Snap station value");
            SetTextByClassNN(matchLinesDialog, "Edit11", "20");
            EnsureCheckBoxCheckedByName(matchLinesDialog, "Allow additional distance");
            SetTextByClassNN(matchLinesDialog, "Edit12", options.ViewOverlap.ToString());

            IntPtr createActionDialog = matchLinesDialog;

            if (!options.PlanOnly)
            {
                ClickButtonByClassNN(matchLinesDialog, "Button2");
                var profileDialogResult = await WaitForDialogAsync(mainHwnd, "Create View Frames - Profile Views", cancellationToken);
                if (!profileDialogResult.IsCompleted) return profileDialogResult.Discard();
                IntPtr profileDialog = profileDialogResult.Value;
                progress.Report("Wizard: Profile Views");

                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                var combo10Result = await SelectComboOrPopupTreeItemAsync(profileDialog, "ComboBox10", "PROFILE VIEW L TO R NO SCALE", cancellationToken);
                if (!combo10Result.IsCompleted) return combo10Result;
                if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
                var combo11Result = await SelectComboOrPopupTreeItemAsync(profileDialog, "ComboBox11", "EG-FG Elevations and Stations", cancellationToken);
                if (!combo11Result.IsCompleted) return combo11Result;
                createActionDialog = profileDialog;
            }

            if (cancellationToken.IsCancellationRequested) return WaitResult.Cancelled;
            progress.Report("Wizard: Create View Frames");
            ClickButtonByClassNN(createActionDialog, "Button3");

            var wizardCloseResult = await Waiter.WaitUntilAsync("Create View Frames dialog to close", () =>
            {
                return FindDialog(mainHwnd, WizardDialog("Create View Frames - Alignment")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - Sheets")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - View Frame Group")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - Match Lines")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - Profile Views")) == IntPtr.Zero;
            }, cancellationToken, continueOnCapturedContext: false);
            if (!wizardCloseResult.IsCompleted) return wizardCloseResult;

            Trace("WIZARD_COMPLETE: create-clicked and dialogs closed.");
            return WaitResult.Completed;
        }

        private async Task<WaitResult> WaitForButtonEnabledAsync(
            IntPtr mainHwnd,
            string dialogTitle,
            string classNN,
            CancellationToken cancellationToken,
            string buttonDisplayName)
        {
            IntPtr button = IntPtr.Zero;
            WaitResult waitResult = await Waiter.WaitUntilAsync($"Button '{buttonDisplayName}' enabled on '{dialogTitle}'", () =>
            {
                IntPtr dialog = FindDialog(mainHwnd, WizardDialog(dialogTitle));
                if (dialog == IntPtr.Zero)
                {
                    return false;
                }

                button = Win32WindowTools.FindChildByClassNN(dialog, classNN);
                if (button == IntPtr.Zero)
                {
                    return false;
                }

                return Win32WindowTools.GetMetadata(button).IsEnabled;
            }, cancellationToken, continueOnCapturedContext: false);

            if (!waitResult.IsCompleted) return waitResult;

            if (button == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Button not found after wait. dialog='{dialogTitle}', classNN={classNN}");
            }

            string caption = Win32WindowTools.GetWindowTextRaw(button);
            Trace($"BUTTON_READY: {classNN} '{caption}' in '{dialogTitle}'");
            return WaitResult.Completed;
        }

        private void SendEnterByClassNN(IntPtr dialog, string classNN)
        {
            IntPtr edit = Win32WindowTools.FindChildByClassNN(dialog, classNN);
            if (edit == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Edit not found for Enter key. classNN={classNN}");
            }

            WindowMetadata meta = Win32WindowTools.GetMetadata(edit);
            if (!meta.IsEnabled)
            {
                throw new InvalidOperationException($"Edit is disabled for Enter key. classNN={classNN}");
            }

            Trace($"KEY: Enter -> {classNN} hwnd=0x{edit.ToInt64():X}");
            if (!Win32WindowTools.SendEnterToEditControl(dialog, edit))
            {
                throw new InvalidOperationException($"Failed to send Enter to edit. classNN={classNN}");
            }
        }

        private async Task<WaitResult> WaitForTemplateLayoutsLoadedAsync(IntPtr selectLayoutDialog, CancellationToken cancellationToken)
        {
            return await Waiter.WaitUntilAsync("Template layout list populated", () =>
            {
                int win32Count = Win32WindowTools.GetDialogListItemCount(selectLayoutDialog);
                if (win32Count > 0)
                {
                    Trace($"TEMPLATE_LAYOUTS_READY(HWND): count={win32Count}");
                    return true;
                }

                AutomationElement? root = SafeFromHandle(selectLayoutDialog);
                if (root == null)
                {
                    return false;
                }

                var itemCondition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));

                AutomationElementCollection items = root.FindAll(TreeScope.Descendants, itemCondition);
                if (items.Count > 0)
                {
                    Trace($"TEMPLATE_LAYOUTS_READY(UIA): count={items.Count}");
                    return true;
                }

                return false;
            }, cancellationToken, continueOnCapturedContext: false);
        }

        private void SelectComboString(IntPtr dialog, string classNN, string value)
        {
            IntPtr combo = Win32WindowTools.FindChildByClassNN(dialog, classNN);
            if (combo == IntPtr.Zero)
            {
                throw new InvalidOperationException($"ComboBox not found. classNN={classNN}");
            }

            if (!Win32WindowTools.SelectComboString(combo, value))
            {
                throw new InvalidOperationException($"Could not select '{value}' in {classNN}.");
            }

            Trace($"COMBOSELECT: {classNN}='{value}'");
        }

        private async Task<WaitResult> SelectComboOrPopupTreeItemAsync(IntPtr dialog, string classNN, string itemName, CancellationToken cancellationToken)
        {
            IntPtr combo = Win32WindowTools.FindChildByClassNN(dialog, classNN);
            if (combo == IntPtr.Zero)
            {
                throw new InvalidOperationException($"ComboBox not found. classNN={classNN}");
            }

            WindowMetadata comboMeta = Win32WindowTools.GetMetadata(combo);
            Trace($"COMBO_BEGIN: {classNN} target='{itemName}'");
            Trace(
                $"COMBO_META: {classNN} hwnd=0x{combo.ToInt64():X} class='{comboMeta.ClassName}' enabled={comboMeta.IsEnabled} visible={comboMeta.IsVisible}");
            Trace($"COMBO_OPEN: {classNN} (ExpandComboDropDown)");
            if (!Win32WindowTools.ExpandComboDropDown(combo))
            {
                throw new InvalidOperationException($"Failed to open combo dropdown. classNN={classNN}");
            }

            IntPtr mainHwnd = Application.MainWindow.Handle;
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            IntPtr treePopup = IntPtr.Zero;
            Trace($"COMBO_WAIT_TREE: {classNN} target='{itemName}'");
            var treePopupResult = await Waiter.WaitUntilAsync($"Popup tree for '{itemName}'", () =>
            {
                treePopup = FindPopupTree(mainHwnd, mainPid);
                return treePopup != IntPtr.Zero;
            }, cancellationToken, continueOnCapturedContext: false);
            if (!treePopupResult.IsCompleted) return treePopupResult;
            Trace($"COMBO_TREE_FOUND: {classNN} tree=0x{treePopup.ToInt64():X}");

            int treeCount = 0;
            var treePopulatedResult = await Waiter.WaitUntilAsync($"Popup tree populated for '{itemName}'", () =>
            {
                treeCount = Win32WindowTools.GetTreeItemCount(treePopup);
                return treeCount > 0;
            }, cancellationToken, continueOnCapturedContext: false);
            if (!treePopulatedResult.IsCompleted) return treePopulatedResult;
            Trace($"COMBO_TREE_COUNT: {classNN} count={treeCount}");

            foreach (string line in Win32WindowTools.DumpTreeItems(treePopup, 120))
            {
                Trace($"TREE_ITEM: {line}");
            }

            Trace($"COMBO_FIND_ITEM: {classNN} '{itemName}'");
            IntPtr treeItem = Win32WindowTools.FindTreeItemByText(treePopup, itemName, ignoreCase: true);
            if (treeItem == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Tree item '{itemName}' not found in popup tree. count={treeCount}");
            }
            Trace($"COMBO_ITEM_FOUND: {classNN} '{itemName}' hItem=0x{treeItem.ToInt64():X}");

            if (!Win32WindowTools.FocusWindow(treePopup))
            {
                throw new InvalidOperationException($"Could not focus tree popup for '{itemName}'.");
            }

            if (!Win32WindowTools.SelectTreeItem(treePopup, treeItem))
            {
                throw new InvalidOperationException(
                    $"Failed selecting tree item '{itemName}' (hItem=0x{treeItem.ToInt64():X}).");
            }
            Trace($"COMBO_SELECTED: {classNN} '{itemName}'");

            if (!TryGetTreeItemClickPoint(treePopup, itemName, out int clickX, out int clickY))
            {
                throw new InvalidOperationException(
                    $"Failed to resolve click point for tree item '{itemName}'.");
            }

            if (!Win32WindowTools.ClickAtScreenPoint(treePopup, clickX, clickY))
            {
                throw new InvalidOperationException(
                    $"Failed clicking tree item '{itemName}' at [{clickX},{clickY}].");
            }
            Trace($"COMBO_CLICK_ITEM: {classNN} '{itemName}' point=[{clickX},{clickY}]");

            if (!Win32WindowTools.SendEnterToWindow(treePopup))
            {
                throw new InvalidOperationException(
                    $"Failed committing tree item '{itemName}' with Enter.");
            }

            Trace(
                $"TREESELECT(WIN32+ENTER): '{itemName}' tree=0x{treePopup.ToInt64():X} hItem=0x{treeItem.ToInt64():X}");

            _ = Win32WindowTools.CollapseComboDropDown(combo);
            Trace($"COMBO_COLLAPSE: {classNN}");

            bool notifySent = Win32WindowTools.NotifyComboSelectionCommitted(combo);
            string comboText = Win32WindowTools.GetWindowTextRaw(combo);
            Trace($"COMBO_NOTIFY: {classNN} sent={notifySent}");
            Trace($"COMBO_TEXT_AFTER: {classNN} '{comboText}'");

            if (string.IsNullOrWhiteSpace(comboText))
            {
                Trace($"COMBO_TEXT_NOTE: {classNN} empty display text (owner-drawn/custom). Continuing.");
            }
            else if (!comboText.Contains(itemName, StringComparison.OrdinalIgnoreCase))
            {
                Trace($"COMBO_TEXT_NOTE: {classNN} display text mismatch. expected='{itemName}' actual='{comboText}'. Continuing.");
            }

            Trace($"COMBO_DONE: {classNN} '{itemName}'");
            return WaitResult.Completed;
        }

        private static IntPtr FindPopupTree(IntPtr mainHwnd, uint mainPid)
        {
            IntPtr treePopup = Win32WindowTools.FindDescendantByClass(mainHwnd, "SysTreeView32");
            if (treePopup != IntPtr.Zero)
            {
                WindowMetadata meta = Win32WindowTools.GetMetadata(treePopup);
                if (meta.ProcessId == mainPid && meta.IsVisible)
                {
                    return treePopup;
                }
            }

            foreach (IntPtr hwnd in Win32WindowTools.EnumerateTopLevelWindows())
            {
                WindowMetadata meta = Win32WindowTools.GetMetadata(hwnd);
                if (meta.ProcessId != mainPid || !meta.IsVisible)
                {
                    continue;
                }

                if (!string.Equals(meta.ClassName, "SysTreeView32", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return hwnd;
            }

            return IntPtr.Zero;
        }

        private void EnsureCheckBoxCheckedByName(IntPtr dialog, string nameFragment)
        {
            bool found = false;
            foreach (IntPtr hwnd in Win32WindowTools.EnumerateChildWindows(dialog))
            {
                string text = Win32WindowTools.GetWindowTextRaw(hwnd);
                if (!text.Contains(nameFragment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                found = true;
                if (!Win32WindowTools.IsChecked(hwnd))
                {
                    Win32WindowTools.Click(hwnd);
                }

                Trace($"CHECK(HWND): '{nameFragment}'");
                return;
            }

            if (!found)
            {
                throw new InvalidOperationException($"Checkbox not found by text fragment: '{nameFragment}'.");
            }
        }

        private static AutomationElement? SafeFromHandle(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return AutomationElement.FromHandle(hwnd);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetTreeItemClickPoint(IntPtr treeHwnd, string itemName, out int x, out int y)
        {
            x = 0;
            y = 0;

            AutomationElement? treeElement = SafeFromHandle(treeHwnd);
            if (treeElement == null)
            {
                return false;
            }

            AutomationElement? item = treeElement.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, itemName, PropertyConditionFlags.IgnoreCase));
            if (item == null)
            {
                return false;
            }

            var bounds = item.Current.BoundingRectangle;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            x = (int)Math.Round(bounds.Left + (bounds.Width / 2.0));
            y = (int)Math.Round(bounds.Top + (bounds.Height / 2.0));
            return true;
        }

    }
}
