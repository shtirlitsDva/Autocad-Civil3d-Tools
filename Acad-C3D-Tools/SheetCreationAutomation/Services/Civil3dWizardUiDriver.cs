using Autodesk.AutoCAD.ApplicationServices;
using SheetCreationAutomation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace SheetCreationAutomation.Services
{
    internal sealed class Civil3dWizardUiDriver : IWizardUiDriver
    {
        private readonly AutomationWaiter waiter;
        private readonly StringBuilder selectorSnapshot = new StringBuilder();

        public Civil3dWizardUiDriver(IWaitOverlayPresenter overlayPresenter, WaitPolicy waitPolicy)
        {
            waiter = new AutomationWaiter(waitPolicy, overlayPresenter);
        }

        public string LastSelectorSnapshot => selectorSnapshot.ToString();

        public async Task RunCreateViewFramesWizardAsync(
            WizardRunOptions options,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            selectorSnapshot.Clear();

            IntPtr mainHwnd = Application.MainWindow.Handle;

            IntPtr alignmentDialog = await WaitForDialogAsync(mainHwnd, "Create View Frames - Alignment", cancellationToken);
            progress.Report("Wizard: Alignment -> Next");
            ClickButtonByClassNN(alignmentDialog, "Button2");

            IntPtr sheetsDialog = await WaitForDialogAsync(mainHwnd, "Create View Frames - Sheets", cancellationToken);
            progress.Report("Wizard: Sheets");

            if (options.PlanOnly)
            {
                EnsureCheckBoxCheckedByName(sheetsDialog, "Plan");
            }

            if (options.IsFirstFile)
            {
                // First step for template workflow: open the template picker button ("...") on Sheets.
                ClickButtonByClassNN(sheetsDialog, "Button18");
                _ = await WaitForDialogAsync(mainHwnd, "Select Layout as Sheet Template", cancellationToken);
                ClickButtonByClassNN(sheetsDialog, "Button4");
            }

            await WaitForButtonEnabledAsync(
                mainHwnd,
                "Create View Frames - Sheets",
                "Button2",
                cancellationToken,
                "Next");

            ClickButtonByClassNN(sheetsDialog, "Button2");

            IntPtr groupDialog = await WaitForDialogAsync(mainHwnd, "Create View Frames - View Frame Group", cancellationToken);
            progress.Report("Wizard: View Frame Group");

            ClickButtonByClassNN(groupDialog, "Button28");

            IntPtr nameTemplateDialog = await WaitForDialogAsync(mainHwnd, "Name Template", cancellationToken);
            ClickButtonByClassNN(nameTemplateDialog, "Button2");
            SetTextByClassNN(nameTemplateDialog, "Edit2", options.NextViewFrameCounterNumber.ToString());
            ClickButtonByClassNN(nameTemplateDialog, "Button4");

            await waiter.WaitUntilAsync("Name Template dialog to close", () =>
            {
                IntPtr hwnd = FindDialog(
                    mainHwnd,
                    new DialogSearchOptions("Name Template")
                    {
                        RequiredClassName = "#32770",
                        MatchMode = TitleMatchMode.ExactOrContains
                    });
                return hwnd == IntPtr.Zero;
            }, cancellationToken);

            groupDialog = await WaitForDialogAsync(mainHwnd, "Create View Frames - View Frame Group", cancellationToken);
            SelectComboString(groupDialog, "ComboBox4", "Middle center");
            ClickButtonByClassNN(groupDialog, "Button2");

            IntPtr matchLinesDialog = await WaitForDialogAsync(mainHwnd, "Create View Frames - Match Lines", cancellationToken);
            progress.Report("Wizard: Match Lines");

            EnsureCheckBoxCheckedByName(matchLinesDialog, "Snap station value");
            SetTextByClassNN(matchLinesDialog, "Edit11", "20");
            EnsureCheckBoxCheckedByName(matchLinesDialog, "Allow additional distance");
            SetTextByClassNN(matchLinesDialog, "Edit12", options.ViewOverlap.ToString());

            IntPtr createActionDialog = matchLinesDialog;

            if (!options.PlanOnly)
            {
                ClickButtonByClassNN(matchLinesDialog, "Button2");
                IntPtr profileDialog = await WaitForDialogAsync(mainHwnd, "Create View Frames - Profile Views", cancellationToken);
                progress.Report("Wizard: Profile Views");

                await SelectComboOrPopupTreeItemAsync(profileDialog, "ComboBox10", "PROFILE VIEW L TO R NO SCALE", cancellationToken);
                await SelectComboOrPopupTreeItemAsync(profileDialog, "ComboBox11", "EG-FG Elevations and Stations", cancellationToken);
                createActionDialog = profileDialog;
            }

            progress.Report("Wizard: Create View Frames");
            ClickButtonByClassNN(createActionDialog, "Button1");

            await waiter.WaitUntilAsync("Create View Frames dialog to close", () =>
            {
                return FindDialog(mainHwnd, WizardDialog("Create View Frames - Alignment")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - Sheets")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - View Frame Group")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - Match Lines")) == IntPtr.Zero
                    && FindDialog(mainHwnd, WizardDialog("Create View Frames - Profile Views")) == IntPtr.Zero;
            }, cancellationToken);
        }

        private async Task<IntPtr> WaitForDialogAsync(IntPtr mainHwnd, string title, CancellationToken cancellationToken)
            => await WaitForDialogAsync(mainHwnd, WizardDialog(title), cancellationToken);

        private async Task<IntPtr> WaitForDialogAsync(
            IntPtr mainHwnd,
            DialogSearchOptions options,
            CancellationToken cancellationToken)
        {
            IntPtr dialog = IntPtr.Zero;
            await waiter.WaitUntilAsync($"Dialog '{options.ExpectedTitle}'", () =>
            {
                dialog = FindDialog(mainHwnd, options);
                if (dialog != IntPtr.Zero)
                {
                    selectorSnapshot.AppendLine($"FOUND: {options.ExpectedTitle} => 0x{dialog.ToInt64():X}");
                }
                return dialog != IntPtr.Zero;
            }, cancellationToken);

            return dialog;
        }

        private void ClickButtonByClassNN(IntPtr dialog, string classNN)
        {
            IntPtr button = Win32WindowTools.FindChildByClassNN(dialog, classNN);
            if (button == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Button not found. classNN={classNN}");
            }

            WindowMetadata meta = Win32WindowTools.GetMetadata(button);
            if (!meta.IsEnabled)
            {
                string captionDisabled = Win32WindowTools.GetWindowTextRaw(button);
                throw new InvalidOperationException(
                    $"Button is disabled. classNN={classNN}, caption='{captionDisabled}'");
            }

            string caption = Win32WindowTools.GetWindowTextRaw(button);
            selectorSnapshot.AppendLine($"CLICK: {classNN} '{caption}' on 0x{dialog.ToInt64():X}");
            Win32WindowTools.Click(button);
        }

        private async Task WaitForButtonEnabledAsync(
            IntPtr mainHwnd,
            string dialogTitle,
            string classNN,
            CancellationToken cancellationToken,
            string buttonDisplayName)
        {
            IntPtr button = IntPtr.Zero;
            await waiter.WaitUntilAsync($"Button '{buttonDisplayName}' enabled on '{dialogTitle}'", () =>
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
            }, cancellationToken);

            if (button == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"Button not found after wait. dialog='{dialogTitle}', classNN={classNN}");
            }

            string caption = Win32WindowTools.GetWindowTextRaw(button);
            selectorSnapshot.AppendLine($"BUTTON_READY: {classNN} '{caption}' in '{dialogTitle}'");
        }

        private void SetTextByClassNN(IntPtr dialog, string classNN, string value)
        {
            IntPtr edit = Win32WindowTools.FindChildByClassNN(dialog, classNN);
            if (edit == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Edit not found. classNN={classNN}");
            }

            WindowMetadata meta = Win32WindowTools.GetMetadata(edit);
            if (!meta.IsEnabled)
            {
                throw new InvalidOperationException($"Edit is disabled. classNN={classNN}");
            }

            selectorSnapshot.AppendLine($"SETTEXT: {classNN}='{value}'");
            Win32WindowTools.SetText(edit, value);
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

            selectorSnapshot.AppendLine($"COMBOSELECT: {classNN}='{value}'");
        }

        private async Task SelectComboOrPopupTreeItemAsync(IntPtr dialog, string classNN, string itemName, CancellationToken cancellationToken)
        {
            IntPtr combo = Win32WindowTools.FindChildByClassNN(dialog, classNN);
            if (combo == IntPtr.Zero)
            {
                throw new InvalidOperationException($"ComboBox not found. classNN={classNN}");
            }
            Win32WindowTools.Click(combo);

            IntPtr mainHwnd = Application.MainWindow.Handle;
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            IntPtr treePopup = IntPtr.Zero;
            await waiter.WaitUntilAsync($"Popup tree for '{itemName}'", () =>
            {
                treePopup = Win32WindowTools.FindDescendantByClass(mainHwnd, "SysTreeView32");
                if (treePopup == IntPtr.Zero)
                {
                    treePopup = Win32WindowTools.FindTopLevelByClass("SysTreeView32", mainPid);
                }
                return treePopup != IntPtr.Zero;
            }, cancellationToken);

            AutomationElement? treeElement = SafeFromHandle(treePopup);
            if (treeElement == null)
            {
                throw new InvalidOperationException($"Popup tree not available for item '{itemName}'.");
            }

            AutomationElement? item = treeElement.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, itemName, PropertyConditionFlags.IgnoreCase));
            if (item == null)
            {
                throw new InvalidOperationException($"Tree item '{itemName}' not found.");
            }

            if (!item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? selectionObj))
            {
                throw new InvalidOperationException($"Tree item '{itemName}' does not expose SelectionItem pattern.");
            }

            ((SelectionItemPattern)selectionObj).Select();
            selectorSnapshot.AppendLine($"TREESELECT(UIA): '{itemName}'");
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

                selectorSnapshot.AppendLine($"CHECK(HWND): '{nameFragment}'");
                return;
            }

            if (!found)
            {
                throw new InvalidOperationException($"Checkbox not found by text fragment: '{nameFragment}'.");
            }
        }

        private static DialogSearchOptions WizardDialog(string title) =>
            new DialogSearchOptions(title)
            {
                RequiredClassName = "#32770",
                MatchMode = TitleMatchMode.ExactOrContains
            };

        private static IntPtr FindDialog(IntPtr mainHwnd, DialogSearchOptions options)
        {
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            var candidates = new List<IntPtr>();

            candidates.AddRange(Win32WindowTools.EnumerateChildWindows(mainHwnd, includeRoot: true));
            candidates.AddRange(Win32WindowTools.EnumerateTopLevelWindows());

            // Prefer visible modal-style dialogs from the AutoCAD process first.
            foreach (IntPtr hwnd in candidates.Distinct())
            {
                WindowMetadata meta = Win32WindowTools.GetMetadata(hwnd);
                if (meta.ProcessId != mainPid || !meta.IsVisible)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(options.RequiredClassName)
                    && !string.Equals(meta.ClassName, options.RequiredClassName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TitleMatches(meta.Title, options))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        private static bool TitleMatches(string actualTitle, DialogSearchOptions options)
        {
            string actual = (actualTitle ?? string.Empty).Trim();
            string expected = (options.ExpectedTitle ?? string.Empty).Trim();

            if (actual.Length == 0 || expected.Length == 0)
            {
                return false;
            }

            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (options.MatchMode == TitleMatchMode.ExactOrContains
                && actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (options.MatchMode == TitleMatchMode.PrefixAndToken
                && !string.IsNullOrWhiteSpace(options.TitlePrefix)
                && expected.StartsWith(options.TitlePrefix, StringComparison.OrdinalIgnoreCase)
                && actual.StartsWith(options.TitlePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string expectedSuffix = expected.Substring(options.TitlePrefix.Length).Trim();
                return actual.Contains(expectedSuffix, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private enum TitleMatchMode
        {
            Exact = 0,
            ExactOrContains = 1,
            PrefixAndToken = 2
        }

        private sealed class DialogSearchOptions
        {
            public DialogSearchOptions(string expectedTitle)
            {
                ExpectedTitle = expectedTitle;
            }

            public string ExpectedTitle { get; }
            public string? RequiredClassName { get; init; }
            public string? TitlePrefix { get; init; }
            public TitleMatchMode MatchMode { get; init; } = TitleMatchMode.Exact;
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

    }
}
