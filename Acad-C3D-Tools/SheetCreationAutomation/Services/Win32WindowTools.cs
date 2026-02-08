using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SheetCreationAutomation.Services
{
    internal static class Win32WindowTools
    {
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        private const uint BM_CLICK = 0x00F5;
        private const uint BM_GETCHECK = 0x00F0;
        private const uint BST_CHECKED = 0x0001;
        private const uint WM_SETTEXT = 0x000C;
        private const uint CB_SELECTSTRING = 0x014D;

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

        public static IEnumerable<IntPtr> EnumerateChildWindows(IntPtr parent, bool includeRoot = false)
        {
            var handles = new List<IntPtr>();
            if (includeRoot && parent != IntPtr.Zero)
            {
                handles.Add(parent);
            }

            EnumChildWindows(parent, (hwnd, _) =>
            {
                handles.Add(hwnd);
                return true;
            }, IntPtr.Zero);

            return handles;
        }

        public static IEnumerable<IntPtr> EnumerateDirectChildWindows(IntPtr parent)
        {
            var direct = new List<IntPtr>();
            foreach (IntPtr child in EnumerateChildWindows(parent))
            {
                if (GetParent(child) == parent)
                {
                    direct.Add(child);
                }
            }

            return direct;
        }

        public static IEnumerable<IntPtr> EnumerateTopLevelWindows()
        {
            var handles = new List<IntPtr>();
            EnumWindows((hwnd, _) =>
            {
                handles.Add(hwnd);
                return true;
            }, IntPtr.Zero);

            return handles;
        }

        public static IntPtr FindDescendantByTitle(IntPtr root, string windowTitle)
        {
            foreach (IntPtr hwnd in EnumerateChildWindows(root, includeRoot: true))
            {
                if (!IsWindow(hwnd))
                {
                    continue;
                }

                string title = GetWindowTextRaw(hwnd);
                if (string.Equals(title, windowTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        public static IntPtr FindTopLevelByTitle(string windowTitle, uint processId = 0)
        {
            foreach (IntPtr hwnd in EnumerateTopLevelWindows())
            {
                if (!IsWindow(hwnd))
                {
                    continue;
                }

                if (processId != 0)
                {
                    _ = GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid != processId)
                    {
                        continue;
                    }
                }

                string title = GetWindowTextRaw(hwnd);
                if (string.Equals(title, windowTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        public static IntPtr FindDescendantByClass(IntPtr root, string className)
        {
            foreach (IntPtr hwnd in EnumerateChildWindows(root, includeRoot: true))
            {
                if (!IsWindow(hwnd))
                {
                    continue;
                }

                string cls = GetClassNameRaw(hwnd);
                if (string.Equals(cls, className, StringComparison.OrdinalIgnoreCase))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        public static IntPtr FindTopLevelByClass(string className, uint processId = 0)
        {
            foreach (IntPtr hwnd in EnumerateTopLevelWindows())
            {
                if (!IsWindow(hwnd))
                {
                    continue;
                }

                if (processId != 0)
                {
                    _ = GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid != processId)
                    {
                        continue;
                    }
                }

                string cls = GetClassNameRaw(hwnd);
                if (string.Equals(cls, className, StringComparison.OrdinalIgnoreCase))
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        public static IntPtr FindChildByClassNN(IntPtr dialog, string classNN)
        {
            if (string.IsNullOrWhiteSpace(classNN))
            {
                return IntPtr.Zero;
            }

            int splitIndex = classNN.Length - 1;
            while (splitIndex >= 0 && char.IsDigit(classNN[splitIndex]))
            {
                splitIndex--;
            }

            if (splitIndex <= 0 || splitIndex >= classNN.Length - 1)
            {
                return IntPtr.Zero;
            }

            string className = classNN.Substring(0, splitIndex + 1);
            if (!int.TryParse(classNN.Substring(splitIndex + 1), out int ordinal) || ordinal <= 0)
            {
                return IntPtr.Zero;
            }

            int current = 0;
            foreach (IntPtr hwnd in EnumerateChildWindows(dialog))
            {
                if (!string.Equals(GetClassNameRaw(hwnd), className, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                current++;
                if (current == ordinal)
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
        }

        public static bool Click(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            SendMessage(hwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        public static bool SetText(IntPtr hwnd, string value)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            SendMessage(hwnd, WM_SETTEXT, IntPtr.Zero, value);
            return true;
        }

        public static bool SelectComboString(IntPtr hwnd, string value)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            IntPtr result = SendMessage(hwnd, CB_SELECTSTRING, new IntPtr(-1), value);
            return result.ToInt64() >= 0;
        }

        public static bool IsChecked(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            IntPtr result = SendMessage(hwnd, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero);
            return (uint)result.ToInt64() == BST_CHECKED;
        }

        public static string GetWindowTextRaw(IntPtr hwnd)
        {
            var sb = new StringBuilder(512);
            _ = GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string GetClassNameRaw(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            _ = GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static WindowMetadata GetMetadata(IntPtr hwnd)
        {
            uint tid = GetWindowThreadProcessId(hwnd, out uint pid);
            return new WindowMetadata
            {
                Handle = hwnd,
                Title = GetWindowTextRaw(hwnd),
                ClassName = GetClassNameRaw(hwnd),
                ProcessId = pid,
                ThreadId = tid,
                IsVisible = IsWindowVisible(hwnd),
                IsEnabled = IsWindowEnabled(hwnd)
            };
        }
    }

    internal sealed class WindowMetadata
    {
        public IntPtr Handle { get; init; }
        public string Title { get; init; } = string.Empty;
        public string ClassName { get; init; } = string.Empty;
        public uint ProcessId { get; init; }
        public uint ThreadId { get; init; }
        public bool IsVisible { get; init; }
        public bool IsEnabled { get; init; }
    }
}
