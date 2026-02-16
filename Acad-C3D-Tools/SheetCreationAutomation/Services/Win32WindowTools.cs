using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const uint WM_NEXTDLGCTL = 0x0028;
        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint CB_SELECTSTRING = 0x014D;
        private const uint CB_SHOWDROPDOWN = 0x014F;
        private const uint EM_SETSEL = 0x00B1;
        private const uint EM_REPLACESEL = 0x00C2;
        private const uint LB_GETCOUNT = 0x018B;
        private const uint LVM_FIRST = 0x1000;
        private const uint LVM_GETITEMCOUNT = LVM_FIRST + 4;
        private const uint TV_FIRST = 0x1100;
        private const uint TVM_GETNEXTITEM = TV_FIRST + 10;
        private const uint TVM_SELECTITEM = TV_FIRST + 11;
        private const uint TVM_GETCOUNT = TV_FIRST + 5;
        private const uint TVM_ENSUREVISIBLE = TV_FIRST + 20;
        private const uint TVM_GETITEMW = TV_FIRST + 62;
        private const int TVGN_ROOT = 0x0000;
        private const int TVGN_NEXT = 0x0001;
        private const int TVGN_CHILD = 0x0004;
        private const int TVGN_CARET = 0x0009;
        private const uint TVIF_TEXT = 0x0001;
        private const int EN_KILLFOCUS = 0x0200;
        private const int EN_CHANGE = 0x0300;
        private const int EN_UPDATE = 0x0400;
        private const int CBN_SELCHANGE = 1;
        private const int CBN_CLOSEUP = 8;
        private const int CBN_SELENDOK = 9;
        private const int VK_RETURN = 0x0D;
        private const uint MAPVK_VK_TO_VSC = 0;
        private const uint GA_ROOT = 2;
        private const uint MK_LBUTTON = 0x0001;

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
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

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

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern int GetDlgCtrlID(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

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

        public static bool FocusWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            IntPtr root = GetAncestor(hwnd, GA_ROOT);
            if (root == IntPtr.Zero)
            {
                root = hwnd;
            }

            uint currentTid = GetCurrentThreadId();
            uint targetTid = GetWindowThreadProcessId(root, out _);
            bool attached = false;

            try
            {
                if (currentTid != targetTid && targetTid != 0)
                {
                    attached = AttachThreadInput(currentTid, targetTid, true);
                }

                _ = SetForegroundWindow(root);
                _ = SetActiveWindow(root);
                _ = SetFocus(hwnd);
                return true;
            }
            finally
            {
                if (attached)
                {
                    _ = AttachThreadInput(currentTid, targetTid, false);
                }
            }
        }

        public static bool ClickAtScreenPoint(IntPtr hwnd, int screenX, int screenY)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            POINT clientPoint = new POINT { X = screenX, Y = screenY };
            if (!ScreenToClient(hwnd, ref clientPoint))
            {
                return false;
            }

            IntPtr lParam = MakeMouseLParam(clientPoint.X, clientPoint.Y);
            _ = SendMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
            _ = SendMessage(hwnd, WM_LBUTTONDOWN, new IntPtr(MK_LBUTTON), lParam);
            _ = SendMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
            return true;
        }

        public static bool IsWindowHandle(IntPtr hwnd) => hwnd != IntPtr.Zero && IsWindow(hwnd);

        public static bool NotifyComboSelectionCommitted(IntPtr comboHwnd)
        {
            if (comboHwnd == IntPtr.Zero)
            {
                return false;
            }

            IntPtr parent = GetParent(comboHwnd);
            if (parent == IntPtr.Zero)
            {
                return false;
            }

            int controlId = GetDlgCtrlID(comboHwnd);
            if (controlId <= 0)
            {
                return false;
            }

            _ = SendMessage(parent, WM_COMMAND, MakeWParam(controlId, CBN_SELCHANGE), comboHwnd);
            _ = SendMessage(parent, WM_COMMAND, MakeWParam(controlId, CBN_SELENDOK), comboHwnd);
            _ = SendMessage(parent, WM_COMMAND, MakeWParam(controlId, CBN_CLOSEUP), comboHwnd);
            return true;
        }

        public static bool ExpandComboDropDown(IntPtr comboHwnd)
        {
            if (comboHwnd == IntPtr.Zero)
            {
                return false;
            }

            _ = FocusWindow(comboHwnd);

            // Preferred path for standard ComboBox controls.
            _ = SendMessage(comboHwnd, CB_SHOWDROPDOWN, new IntPtr(1), IntPtr.Zero);

            // MFC/custom owner-drawn combos may still require an actual click.
            _ = ClickCenterClient(comboHwnd);
            return true;
        }

        public static bool CollapseComboDropDown(IntPtr comboHwnd)
        {
            if (comboHwnd == IntPtr.Zero)
            {
                return false;
            }

            _ = SendMessage(comboHwnd, CB_SHOWDROPDOWN, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        public static bool SendEnterToWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (!FocusWindow(hwnd))
            {
                return false;
            }

            uint scanCode = MapVirtualKey((uint)VK_RETURN, MAPVK_VK_TO_VSC);
            IntPtr keyDownLParam = BuildKeyLParam(scanCode, isKeyUp: false);
            IntPtr keyUpLParam = BuildKeyLParam(scanCode, isKeyUp: true);

            _ = SendMessage(hwnd, WM_KEYDOWN, new IntPtr(VK_RETURN), keyDownLParam);
            _ = SendMessage(hwnd, WM_CHAR, new IntPtr(VK_RETURN), keyDownLParam);
            _ = SendMessage(hwnd, WM_KEYUP, new IntPtr(VK_RETURN), keyUpLParam);
            return true;
        }

        public static bool ClickCenterClient(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out RECT rect))
            {
                return false;
            }

            int x = Math.Max(2, (rect.Right - rect.Left) / 2);
            int y = Math.Max(2, (rect.Bottom - rect.Top) / 2);
            IntPtr lParam = MakeMouseLParam(x, y);

            _ = SendMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
            _ = SendMessage(hwnd, WM_LBUTTONDOWN, new IntPtr(MK_LBUTTON), lParam);
            _ = SendMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
            return true;
        }

        public static int GetTreeItemCount(IntPtr treeHwnd)
        {
            if (treeHwnd == IntPtr.Zero)
            {
                return 0;
            }

            return (int)SendMessage(treeHwnd, TVM_GETCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt64();
        }

        public static IntPtr FindTreeItemByText(IntPtr treeHwnd, string targetText, bool ignoreCase = true)
        {
            if (treeHwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(targetText))
            {
                return IntPtr.Zero;
            }

            StringComparison cmp = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            IntPtr root = SendMessage(treeHwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_ROOT), IntPtr.Zero);
            return FindTreeItemByTextRecursive(treeHwnd, root, targetText, cmp);
        }

        public static bool SelectTreeItem(IntPtr treeHwnd, IntPtr treeItem)
        {
            if (treeHwnd == IntPtr.Zero || treeItem == IntPtr.Zero)
            {
                return false;
            }

            _ = SendMessage(treeHwnd, TVM_ENSUREVISIBLE, IntPtr.Zero, treeItem);
            IntPtr result = SendMessage(treeHwnd, TVM_SELECTITEM, new IntPtr(TVGN_CARET), treeItem);
            return result != IntPtr.Zero;
        }

        public static IReadOnlyList<string> DumpTreeItems(IntPtr treeHwnd, int maxItems = 80)
        {
            var lines = new List<string>();
            if (treeHwnd == IntPtr.Zero || maxItems <= 0)
            {
                return lines;
            }

            IntPtr root = SendMessage(treeHwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_ROOT), IntPtr.Zero);
            DumpTreeItemsRecursive(treeHwnd, root, 0, maxItems, lines);
            return lines;
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

        public static bool SetEditTextLikeUserInput(IntPtr dialog, IntPtr edit, string value)
        {
            if (dialog == IntPtr.Zero || edit == IntPtr.Zero)
            {
                return false;
            }

            SendMessage(dialog, WM_NEXTDLGCTL, edit, new IntPtr(1));
            _ = SetFocus(edit);

            // Select all existing text and replace with new text through edit control semantics.
            SendMessage(edit, EM_SETSEL, IntPtr.Zero, new IntPtr(-1));
            SendMessage(edit, EM_REPLACESEL, new IntPtr(1), value ?? string.Empty);

            int controlId = GetDlgCtrlID(edit);
            IntPtr idAndUpdate = MakeWParam(controlId, EN_UPDATE);
            IntPtr idAndChange = MakeWParam(controlId, EN_CHANGE);
            IntPtr idAndKillFocus = MakeWParam(controlId, EN_KILLFOCUS);

            // Explicitly notify dialog parent because some MFC flows do not react to WM_SETTEXT alone.
            SendMessage(dialog, WM_COMMAND, idAndUpdate, edit);
            SendMessage(dialog, WM_COMMAND, idAndChange, edit);
            SendMessage(dialog, WM_COMMAND, idAndKillFocus, edit);
            return true;
        }

        public static bool SendEnterToEditControl(IntPtr dialog, IntPtr edit)
        {
            if (dialog == IntPtr.Zero || edit == IntPtr.Zero)
            {
                return false;
            }

            // Move focus explicitly to the target edit so Enter is routed through dialog keyboard handling.
            SendMessage(dialog, WM_NEXTDLGCTL, edit, new IntPtr(1));

            IntPtr root = GetAncestor(dialog, GA_ROOT);
            if (root == IntPtr.Zero)
            {
                root = dialog;
            }

            uint currentTid = GetCurrentThreadId();
            uint targetTid = GetWindowThreadProcessId(root, out _);
            bool attached = false;

            try
            {
                if (currentTid != targetTid && targetTid != 0)
                {
                    attached = AttachThreadInput(currentTid, targetTid, true);
                }

                _ = SetForegroundWindow(root);
                _ = SetActiveWindow(root);
                _ = SetFocus(edit);

                uint scanCode = MapVirtualKey((uint)VK_RETURN, MAPVK_VK_TO_VSC);
                IntPtr keyDownLParam = BuildKeyLParam(scanCode, isKeyUp: false);
                IntPtr keyUpLParam = BuildKeyLParam(scanCode, isKeyUp: true);

                _ = PostMessage(edit, WM_KEYDOWN, new IntPtr(VK_RETURN), keyDownLParam);
                _ = PostMessage(edit, WM_CHAR, new IntPtr(VK_RETURN), keyDownLParam);
                _ = PostMessage(edit, WM_KEYUP, new IntPtr(VK_RETURN), keyUpLParam);
                return true;
            }
            finally
            {
                if (attached)
                {
                    _ = AttachThreadInput(currentTid, targetTid, false);
                }
            }
        }

        public static int GetDialogListItemCount(IntPtr dialog)
        {
            if (dialog == IntPtr.Zero)
            {
                return 0;
            }

            int bestCount = 0;
            foreach (IntPtr child in EnumerateChildWindows(dialog))
            {
                string className = GetClassNameRaw(child);
                int count = 0;

                if (string.Equals(className, "ListBox", StringComparison.OrdinalIgnoreCase))
                {
                    count = (int)SendMessage(child, LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt64();
                }
                else if (string.Equals(className, "SysListView32", StringComparison.OrdinalIgnoreCase))
                {
                    count = (int)SendMessage(child, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt64();
                }
                else if (string.Equals(className, "SysTreeView32", StringComparison.OrdinalIgnoreCase))
                {
                    count = (int)SendMessage(child, TVM_GETCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt64();
                }

                if (count > bestCount)
                {
                    bestCount = count;
                }
            }

            return bestCount;
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

        private static IntPtr MakeWParam(int lowWord, int highWord)
        {
            long value = ((long)highWord << 16) | ((long)lowWord & 0xFFFF);
            return new IntPtr(value);
        }

        private static IntPtr BuildKeyLParam(uint scanCode, bool isKeyUp)
        {
            long value = 1L | ((long)scanCode << 16);
            if (isKeyUp)
            {
                value |= 1L << 30;
                value |= 1L << 31;
            }

            return new IntPtr(value);
        }

        private static IntPtr MakeMouseLParam(int x, int y)
        {
            int packed = (x & 0xFFFF) | ((y & 0xFFFF) << 16);
            return new IntPtr(packed);
        }

        private static IntPtr FindTreeItemByTextRecursive(
            IntPtr treeHwnd,
            IntPtr startItem,
            string targetText,
            StringComparison comparison)
        {
            for (IntPtr item = startItem; item != IntPtr.Zero; item = SendMessage(treeHwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_NEXT), item))
            {
                string text = GetTreeItemText(treeHwnd, item);
                if (string.Equals(text, targetText, comparison))
                {
                    return item;
                }

                IntPtr child = SendMessage(treeHwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_CHILD), item);
                if (child == IntPtr.Zero)
                {
                    continue;
                }

                IntPtr found = FindTreeItemByTextRecursive(treeHwnd, child, targetText, comparison);
                if (found != IntPtr.Zero)
                {
                    return found;
                }
            }

            return IntPtr.Zero;
        }

        private static void DumpTreeItemsRecursive(
            IntPtr treeHwnd,
            IntPtr startItem,
            int depth,
            int maxItems,
            List<string> output)
        {
            for (IntPtr item = startItem; item != IntPtr.Zero; item = SendMessage(treeHwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_NEXT), item))
            {
                if (output.Count >= maxItems)
                {
                    return;
                }

                string text = GetTreeItemText(treeHwnd, item);
                output.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "depth={0} hItem=0x{1:X} text='{2}'",
                    depth,
                    item.ToInt64(),
                    text));

                IntPtr child = SendMessage(treeHwnd, TVM_GETNEXTITEM, new IntPtr(TVGN_CHILD), item);
                if (child == IntPtr.Zero)
                {
                    continue;
                }

                DumpTreeItemsRecursive(treeHwnd, child, depth + 1, maxItems, output);
                if (output.Count >= maxItems)
                {
                    return;
                }
            }
        }

        private static string GetTreeItemText(IntPtr treeHwnd, IntPtr treeItem)
        {
            const int maxChars = 512;
            IntPtr textBuffer = IntPtr.Zero;
            IntPtr tvItemPtr = IntPtr.Zero;

            try
            {
                textBuffer = Marshal.AllocHGlobal((maxChars + 1) * 2);

                TVITEMW tvItem = new TVITEMW
                {
                    mask = TVIF_TEXT,
                    hItem = treeItem,
                    pszText = textBuffer,
                    cchTextMax = maxChars
                };

                tvItemPtr = Marshal.AllocHGlobal(Marshal.SizeOf<TVITEMW>());
                Marshal.StructureToPtr(tvItem, tvItemPtr, fDeleteOld: false);

                _ = SendMessage(treeHwnd, TVM_GETITEMW, IntPtr.Zero, tvItemPtr);
                return Marshal.PtrToStringUni(textBuffer) ?? string.Empty;
            }
            finally
            {
                if (tvItemPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(tvItemPtr);
                }

                if (textBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(textBuffer);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TVITEMW
        {
            public uint mask;
            public IntPtr hItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public int iSelectedImage;
            public int cChildren;
            public IntPtr lParam;
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
