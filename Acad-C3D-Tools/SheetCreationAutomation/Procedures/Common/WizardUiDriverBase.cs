using SheetCreationAutomation.Models;
using SheetCreationAutomation.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SheetCreationAutomation.Procedures.Common
{
    internal abstract class WizardUiDriverBase
    {
        private readonly StringBuilder selectorSnapshot = new StringBuilder();

        protected WizardUiDriverBase(IWaitOverlayPresenter overlayPresenter, WaitPolicy waitPolicy)
        {
            Waiter = new AutomationWaiter(waitPolicy, overlayPresenter);
        }

        protected AutomationWaiter Waiter { get; }
        public string LastSelectorSnapshot => selectorSnapshot.ToString();

        protected void ResetSelectorSnapshot() => selectorSnapshot.Clear();

        protected void Trace(string message)
        {
            selectorSnapshot.AppendLine(message);
            AutomationRunLog.Append(message);
        }

        protected async Task<IntPtr> WaitForDialogAsync(IntPtr mainHwnd, string title, CancellationToken cancellationToken)
            => await WaitForDialogAsync(mainHwnd, WizardDialog(title), cancellationToken);

        protected async Task<IntPtr> WaitForDialogAsync(
            IntPtr mainHwnd,
            DialogSearchOptions options,
            CancellationToken cancellationToken)
        {
            IntPtr dialog = IntPtr.Zero;
            await Waiter.WaitUntilAsync($"Dialog '{options.ExpectedTitle}'", () =>
            {
                dialog = FindDialog(mainHwnd, options);
                if (dialog != IntPtr.Zero)
                {
                    Trace($"FOUND: {options.ExpectedTitle} => 0x{dialog.ToInt64():X}");
                }

                return dialog != IntPtr.Zero;
            }, cancellationToken, continueOnCapturedContext: false);

            return dialog;
        }

        protected async Task<IntPtr> WaitForDialogContainingTextAsync(
            IntPtr mainHwnd,
            string textFragment,
            CancellationToken cancellationToken)
        {
            IntPtr dialog = IntPtr.Zero;
            await Waiter.WaitUntilAsync($"Dialog contains '{textFragment}'", () =>
            {
                dialog = FindDialogContainingText(mainHwnd, textFragment);
                if (dialog != IntPtr.Zero)
                {
                    Trace($"FOUND_TEXT: '{textFragment}' => 0x{dialog.ToInt64():X}");
                }

                return dialog != IntPtr.Zero;
            }, cancellationToken, continueOnCapturedContext: false);

            return dialog;
        }

        protected async Task WaitForDialogClosedAsync(IntPtr mainHwnd, string title, CancellationToken cancellationToken)
        {
            await Waiter.WaitUntilAsync($"Dialog '{title}' to close", () =>
            {
                IntPtr dialog = FindDialog(mainHwnd, WizardDialog(title));
                return dialog == IntPtr.Zero;
            }, cancellationToken, continueOnCapturedContext: false);
        }

        protected void ClickButtonByClassNN(IntPtr dialog, string classNN)
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
            Trace($"CLICK: {classNN} '{caption}' on 0x{dialog.ToInt64():X}");
            Win32WindowTools.Click(button);
        }

        protected void SetTextByClassNN(IntPtr dialog, string classNN, string value)
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

            Trace($"SETTEXT: {classNN}='{value}'");
            if (!Win32WindowTools.SetText(edit, value))
            {
                throw new InvalidOperationException($"Failed to set text. classNN={classNN}");
            }
        }

        protected void SetTextAsUserInputByClassNN(IntPtr dialog, string classNN, string value)
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

            Trace($"SETTEXT_USERINPUT: {classNN}='{value}'");
            if (!Win32WindowTools.SetEditTextLikeUserInput(dialog, edit, value))
            {
                throw new InvalidOperationException($"Failed to set edit text as user input. classNN={classNN}");
            }
        }

        protected static DialogSearchOptions WizardDialog(string title) =>
            new DialogSearchOptions(title)
            {
                RequiredClassName = "#32770",
                MatchMode = TitleMatchMode.ExactOrContains
            };

        protected static IntPtr FindDialog(IntPtr mainHwnd, DialogSearchOptions options)
        {
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            var candidates = new List<IntPtr>();

            candidates.AddRange(Win32WindowTools.EnumerateChildWindows(mainHwnd, includeRoot: true));
            candidates.AddRange(Win32WindowTools.EnumerateTopLevelWindows());

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

        protected static IntPtr FindDialogContainingText(IntPtr mainHwnd, string textFragment)
        {
            uint mainPid = Win32WindowTools.GetMetadata(mainHwnd).ProcessId;
            var candidates = new List<IntPtr>();
            candidates.AddRange(Win32WindowTools.EnumerateChildWindows(mainHwnd, includeRoot: true));
            candidates.AddRange(Win32WindowTools.EnumerateTopLevelWindows());

            foreach (IntPtr hwnd in candidates.Distinct())
            {
                WindowMetadata meta = Win32WindowTools.GetMetadata(hwnd);
                if (meta.ProcessId != mainPid || !meta.IsVisible)
                {
                    continue;
                }

                if (!string.Equals(meta.ClassName, "#32770", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (DialogContainsText(hwnd, textFragment))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        protected static bool DialogContainsText(IntPtr dialog, string textFragment)
        {
            foreach (IntPtr hwnd in Win32WindowTools.EnumerateChildWindows(dialog, includeRoot: true))
            {
                string text = Win32WindowTools.GetWindowTextRaw(hwnd);
                if (text.Contains(textFragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        protected static bool TitleMatches(string actualTitle, DialogSearchOptions options)
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

        protected enum TitleMatchMode
        {
            Exact = 0,
            ExactOrContains = 1,
            PrefixAndToken = 2
        }

        protected sealed class DialogSearchOptions
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
    }
}
