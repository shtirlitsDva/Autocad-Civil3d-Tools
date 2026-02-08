using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using UiSnoop.Models;

namespace UiSnoop.Services;

internal sealed class UiSnoopCaptureService : IUiSnoopCaptureService
{
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public SnoopRenderResult Capture(bool followMouse)
    {
        SnapshotData snapshot = CaptureSnapshot(followMouse);
        return new SnoopRenderResult
        {
            Snapshot = snapshot,
            HumanOutput = BuildTechnicalSnapshotText(snapshot),
            LlmOutput = BuildLlmFriendlyBlock(snapshot),
            JsonOutput = JsonSerializer.Serialize(snapshot, jsonOptions)
        };
    }

    public void CopyToClipboard(string text)
    {
        Clipboard.SetText(text ?? string.Empty);
    }

    private static SnapshotData CaptureSnapshot(bool followMouse)
    {
        _ = GetCursorPos(out POINT cursor);

        IntPtr rawWindowAtPoint = WindowFromPoint(cursor);
        IntPtr rootWindow = rawWindowAtPoint;
        if (rootWindow != IntPtr.Zero)
        {
            IntPtr ancestor = GetAncestor(rootWindow, GA_ROOT);
            if (ancestor != IntPtr.Zero)
            {
                rootWindow = ancestor;
            }
        }

        if (!followMouse)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground != IntPtr.Zero)
            {
                rootWindow = foreground;
                rawWindowAtPoint = foreground;
            }
        }

        IntPtr hwndAtPoint = ResolveControlUnderPoint(rootWindow, cursor, rawWindowAtPoint);

        IntPtr activeWindow = GetForegroundWindow();

        WindowInfo rootInfo = CaptureWindowInfo(rootWindow);
        WindowInfo controlInfo = CaptureWindowInfo(hwndAtPoint);
        WindowInfo activeInfo = CaptureWindowInfo(activeWindow);

        string classNN = GetClassNN(hwndAtPoint);
        MousePositions mouse = CaptureMousePositions(cursor, rootWindow);
        ColorInfo color = CapturePixelColor(cursor);

        TextCollection visibleText = CollectWindowTexts(rootWindow, visibleOnly: true, maxLines: 120);
        TextCollection allText = CollectWindowTexts(rootWindow, visibleOnly: false, maxLines: 200);
        string statusBarText = TryGetStatusBarText(rootWindow);

        UiAutomationInfo uiInfo = CaptureUiAutomationInfo(hwndAtPoint);
        List<HierarchyNode> hierarchy = BuildHierarchy(hwndAtPoint);

        return new SnapshotData
        {
            TimestampLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            FollowMouse = followMouse,
            Mouse = mouse,
            PixelColor = color,
            WindowUnderMouse = rootInfo,
            ControlUnderMouse = controlInfo with { ClassNN = classNN },
            ActiveWindow = activeInfo,
            StatusBarText = statusBarText,
            VisibleText = visibleText,
            AllText = allText,
            UiAutomation = uiInfo,
            Hierarchy = hierarchy
        };
    }

    private static IntPtr ResolveControlUnderPoint(IntPtr rootWindow, POINT screenPoint, IntPtr fallbackHandle)
    {
        IntPtr deepest = GetDeepestChildAtPoint(rootWindow, screenPoint);
        if (deepest != IntPtr.Zero)
        {
            return deepest;
        }

        IntPtr uiaHandle = TryGetAutomationWindowHandleAtPoint(screenPoint);
        if (uiaHandle != IntPtr.Zero)
        {
            return uiaHandle;
        }

        return fallbackHandle;
    }

    private static IntPtr GetDeepestChildAtPoint(IntPtr rootWindow, POINT screenPoint)
    {
        if (rootWindow == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr current = rootWindow;
        for (int i = 0; i < 64; i++)
        {
            POINT clientPoint = screenPoint;
            if (!ScreenToClient(current, ref clientPoint))
            {
                break;
            }

            IntPtr child = RealChildWindowFromPoint(current, clientPoint);
            if (child == IntPtr.Zero || child == current || !IsWindow(child))
            {
                break;
            }

            current = child;
        }

        return current;
    }

    private static IntPtr TryGetAutomationWindowHandleAtPoint(POINT screenPoint)
    {
        try
        {
            AutomationElement element = AutomationElement.FromPoint(new Point(screenPoint.X, screenPoint.Y));
            object handleValue = element.GetCurrentPropertyValue(AutomationElement.NativeWindowHandleProperty, true);
            if (handleValue is int handle && handle != 0)
            {
                return new IntPtr(handle);
            }
        }
        catch
        {
            // ignore UIA hit-test failures and keep Win32 result
        }

        return IntPtr.Zero;
    }

    private static WindowInfo CaptureWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return WindowInfo.Empty;
        }

        string title = GetWindowTextRaw(hwnd);
        string className = GetClassNameRaw(hwnd);
        _ = GetWindowThreadProcessId(hwnd, out uint pid);

        string processName = string.Empty;
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            processName = string.Empty;
        }

        RectInfo rectInfo = GetWindowRectInfo(hwnd);
        RectInfo clientRectInfo = GetClientRectInfo(hwnd);

        return new WindowInfo
        {
            HandleHex = ToHex(hwnd),
            HandleDec = hwnd.ToInt64().ToString(),
            Title = title,
            ClassName = className,
            ProcessId = pid,
            ProcessName = processName,
            RectScreen = rectInfo,
            RectClient = clientRectInfo,
            IsVisible = IsWindowVisible(hwnd),
            IsEnabled = IsWindowEnabled(hwnd)
        };
    }

    private static MousePositions CaptureMousePositions(POINT cursor, IntPtr rootWindow)
    {
        int windowX = 0;
        int windowY = 0;
        int clientX = 0;
        int clientY = 0;

        if (rootWindow != IntPtr.Zero && GetWindowRect(rootWindow, out RECT wr))
        {
            windowX = cursor.X - wr.Left;
            windowY = cursor.Y - wr.Top;
        }

        POINT clientPt = cursor;
        if (rootWindow != IntPtr.Zero && ScreenToClient(rootWindow, ref clientPt))
        {
            clientX = clientPt.X;
            clientY = clientPt.Y;
        }

        return new MousePositions
        {
            Screen = new PointInfo(cursor.X, cursor.Y),
            Window = new PointInfo(windowX, windowY),
            Client = new PointInfo(clientX, clientY)
        };
    }

    private static ColorInfo CapturePixelColor(POINT cursor)
    {
        IntPtr dc = GetDC(IntPtr.Zero);
        if (dc == IntPtr.Zero)
        {
            return new ColorInfo();
        }

        uint raw = GetPixel(dc, cursor.X, cursor.Y);
        _ = ReleaseDC(IntPtr.Zero, dc);

        byte r = (byte)(raw & 0x000000FF);
        byte g = (byte)((raw & 0x0000FF00) >> 8);
        byte b = (byte)((raw & 0x00FF0000) >> 16);

        return new ColorInfo
        {
            Hex = $"{r:X2}{g:X2}{b:X2}",
            Red = r,
            Green = g,
            Blue = b
        };
    }

    private static string GetClassNN(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        string className = GetClassNameRaw(hwnd);
        if (string.IsNullOrWhiteSpace(className))
        {
            return string.Empty;
        }

        IntPtr root = GetAncestor(hwnd, GA_ROOT);
        if (root == IntPtr.Zero)
        {
            root = hwnd;
        }

        if (root == hwnd)
        {
            return className + "1";
        }

        int ordinal = 0;
        bool found = false;
        EnumChildWindows(root, (child, _) =>
        {
            if (string.Equals(GetClassNameRaw(child), className, StringComparison.OrdinalIgnoreCase))
            {
                ordinal++;
                if (child == hwnd)
                {
                    found = true;
                    return false;
                }
            }

            return true;
        }, IntPtr.Zero);

        if (found && ordinal > 0)
        {
            return className + ordinal;
        }

        // Fallback: old parent-scoped ordinal when root-scoped enumeration cannot locate the control.
        IntPtr parent = GetParent(hwnd);
        if (parent == IntPtr.Zero)
        {
            return className + "1";
        }

        ordinal = 0;
        EnumChildWindows(parent, (child, _) =>
        {
            if (GetParent(child) != parent)
            {
                return true;
            }

            if (string.Equals(GetClassNameRaw(child), className, StringComparison.OrdinalIgnoreCase))
            {
                ordinal++;
                if (child == hwnd)
                {
                    return false;
                }
            }

            return true;
        }, IntPtr.Zero);

        if (ordinal <= 0)
        {
            ordinal = 1;
        }

        return className + ordinal;
    }

    private static TextCollection CollectWindowTexts(IntPtr root, bool visibleOnly, int maxLines)
    {
        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (root == IntPtr.Zero)
        {
            return new TextCollection();
        }

        void AddTextForHandle(IntPtr h)
        {
            if (visibleOnly && !IsWindowVisible(h))
            {
                return;
            }

            string text = GetWindowTextRaw(h).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (seen.Add(text))
            {
                lines.Add(text);
            }
        }

        AddTextForHandle(root);
        EnumChildWindows(root, (child, _) =>
        {
            AddTextForHandle(child);
            return lines.Count < maxLines;
        }, IntPtr.Zero);

        return new TextCollection
        {
            Lines = lines,
            Truncated = lines.Count >= maxLines
        };
    }

    private static string TryGetStatusBarText(IntPtr root)
    {
        if (root == IntPtr.Zero)
        {
            return string.Empty;
        }

        IntPtr statusBar = IntPtr.Zero;
        EnumChildWindows(root, (child, _) =>
        {
            string cls = GetClassNameRaw(child);
            if (string.Equals(cls, "msctls_statusbar32", StringComparison.OrdinalIgnoreCase))
            {
                statusBar = child;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        if (statusBar == IntPtr.Zero)
        {
            return string.Empty;
        }

        string text = GetWindowTextRaw(statusBar).Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        try
        {
            AutomationElement element = AutomationElement.FromHandle(statusBar);
            return element.Current.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static UiAutomationInfo CaptureUiAutomationInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return UiAutomationInfo.Empty;
        }

        try
        {
            AutomationElement element = AutomationElement.FromHandle(hwnd);
            var patterns = new List<string>();

            (AutomationPattern Pattern, string Name)[] knownPatterns =
            {
                (InvokePattern.Pattern, "Invoke"),
                (TogglePattern.Pattern, "Toggle"),
                (ValuePattern.Pattern, "Value"),
                (SelectionItemPattern.Pattern, "SelectionItem"),
                (SelectionPattern.Pattern, "Selection"),
                (ExpandCollapsePattern.Pattern, "ExpandCollapse"),
                (WindowPattern.Pattern, "Window"),
                (TextPattern.Pattern, "Text"),
                (RangeValuePattern.Pattern, "RangeValue"),
                (ScrollPattern.Pattern, "Scroll")
            };

            foreach ((AutomationPattern pattern, string name) in knownPatterns)
            {
                if (element.TryGetCurrentPattern(pattern, out _))
                {
                    patterns.Add(name);
                }
            }

            return new UiAutomationInfo
            {
                Name = element.Current.Name ?? string.Empty,
                AutomationId = element.Current.AutomationId ?? string.Empty,
                ClassName = element.Current.ClassName ?? string.Empty,
                FrameworkId = element.Current.FrameworkId ?? string.Empty,
                ControlType = element.Current.ControlType?.ProgrammaticName ?? string.Empty,
                IsEnabled = element.Current.IsEnabled,
                IsOffscreen = element.Current.IsOffscreen,
                SupportedPatterns = patterns
            };
        }
        catch (Exception ex)
        {
            return new UiAutomationInfo
            {
                Error = ex.Message
            };
        }
    }

    private static List<HierarchyNode> BuildHierarchy(IntPtr hwnd)
    {
        var nodes = new List<HierarchyNode>();
        IntPtr current = hwnd;
        while (current != IntPtr.Zero)
        {
            nodes.Add(new HierarchyNode
            {
                HandleHex = ToHex(current),
                HandleDec = current.ToInt64().ToString(),
                ClassName = GetClassNameRaw(current),
                Title = GetWindowTextRaw(current)
            });

            current = GetParent(current);
        }

        nodes.Reverse();
        return nodes;
    }

    private static RectInfo GetWindowRectInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT rect))
        {
            return new RectInfo();
        }

        return new RectInfo
        {
            X = rect.Left,
            Y = rect.Top,
            Width = Math.Max(0, rect.Right - rect.Left),
            Height = Math.Max(0, rect.Bottom - rect.Top)
        };
    }

    private static RectInfo GetClientRectInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetClientRect(hwnd, out RECT rect))
        {
            return new RectInfo();
        }

        POINT origin = new() { X = 0, Y = 0 };
        _ = ClientToScreen(hwnd, ref origin);

        return new RectInfo
        {
            X = origin.X,
            Y = origin.Y,
            Width = Math.Max(0, rect.Right - rect.Left),
            Height = Math.Max(0, rect.Bottom - rect.Top)
        };
    }

    private static string BuildTechnicalSnapshotText(SnapshotData s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Window Identity:");
        sb.AppendLine($"title: {s.WindowUnderMouse.Title}");
        sb.AppendLine($"class_name: {s.WindowUnderMouse.ClassName}");
        sb.AppendLine($"process_name: {s.WindowUnderMouse.ProcessName}");
        sb.AppendLine($"process_id: {s.WindowUnderMouse.ProcessId}");
        sb.AppendLine($"window_handle_hex: {s.WindowUnderMouse.HandleHex}");
        sb.AppendLine($"window_handle_dec: {s.WindowUnderMouse.HandleDec}");
        sb.AppendLine();
        sb.AppendLine("Mouse Position:");
        sb.AppendLine($"screen: {s.Mouse.Screen.X}, {s.Mouse.Screen.Y}");
        sb.AppendLine($"window: {s.Mouse.Window.X}, {s.Mouse.Window.Y}");
        sb.AppendLine($"client: {s.Mouse.Client.X}, {s.Mouse.Client.Y}");
        sb.AppendLine($"pixel_color: #{s.PixelColor.Hex} (R={s.PixelColor.Red:X2} G={s.PixelColor.Green:X2} B={s.PixelColor.Blue:X2})");
        sb.AppendLine();
        sb.AppendLine("Control Under Mouse Position:");
        sb.AppendLine($"title: {s.ControlUnderMouse.Title}");
        sb.AppendLine($"class_name: {s.ControlUnderMouse.ClassName}");
        sb.AppendLine($"class_instance: {s.ControlUnderMouse.ClassNN}");
        sb.AppendLine($"window_handle_hex: {s.ControlUnderMouse.HandleHex}");
        sb.AppendLine($"window_handle_dec: {s.ControlUnderMouse.HandleDec}");
        sb.AppendLine();
        sb.AppendLine("Active Window Position:");
        sb.AppendLine($"screen_rect: x:{s.ActiveWindow.RectScreen.X} y:{s.ActiveWindow.RectScreen.Y} w:{s.ActiveWindow.RectScreen.Width} h:{s.ActiveWindow.RectScreen.Height}");
        sb.AppendLine($"client_rect: x:{s.ActiveWindow.RectClient.X} y:{s.ActiveWindow.RectClient.Y} w:{s.ActiveWindow.RectClient.Width} h:{s.ActiveWindow.RectClient.Height}");
        sb.AppendLine();
        sb.AppendLine("Status Bar Text:");
        sb.AppendLine(string.IsNullOrWhiteSpace(s.StatusBarText) ? "<empty>" : s.StatusBarText);
        sb.AppendLine();
        sb.AppendLine("Visible Text:");
        foreach (string line in s.VisibleText.Lines)
        {
            sb.AppendLine(line);
        }

        if (s.VisibleText.Truncated)
        {
            sb.AppendLine("... (truncated)");
        }

        sb.AppendLine();
        sb.AppendLine("All Text:");
        foreach (string line in s.AllText.Lines)
        {
            sb.AppendLine(line);
        }

        if (s.AllText.Truncated)
        {
            sb.AppendLine("... (truncated)");
        }

        return sb.ToString();
    }

    private static string BuildLlmFriendlyBlock(SnapshotData s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ui_snoop_snapshot:");
        sb.AppendLine($"  timestamp_local: {s.TimestampLocal}");
        sb.AppendLine($"  mode_follow_mouse: {s.FollowMouse}");
        sb.AppendLine("  mouse:");
        sb.AppendLine($"    screen: [{s.Mouse.Screen.X}, {s.Mouse.Screen.Y}]");
        sb.AppendLine($"    window: [{s.Mouse.Window.X}, {s.Mouse.Window.Y}]");
        sb.AppendLine($"    client: [{s.Mouse.Client.X}, {s.Mouse.Client.Y}]");
        sb.AppendLine("    color:");
        sb.AppendLine($"      hex: \"{s.PixelColor.Hex}\"");
        sb.AppendLine($"      rgb: [{s.PixelColor.Red}, {s.PixelColor.Green}, {s.PixelColor.Blue}]");

        AppendWindow(sb, "window_under_mouse", s.WindowUnderMouse);
        AppendWindow(sb, "control_under_mouse", s.ControlUnderMouse);
        sb.AppendLine($"    classnn: \"{Escape(s.ControlUnderMouse.ClassNN)}\"");
        AppendWindow(sb, "active_window", s.ActiveWindow);

        sb.AppendLine("  status_bar_text: \"" + Escape(s.StatusBarText) + "\"");
        sb.AppendLine("  uia_control_under_mouse:");
        sb.AppendLine($"    name: \"{Escape(s.UiAutomation.Name)}\"");
        sb.AppendLine($"    automation_id: \"{Escape(s.UiAutomation.AutomationId)}\"");
        sb.AppendLine($"    class_name: \"{Escape(s.UiAutomation.ClassName)}\"");
        sb.AppendLine($"    framework_id: \"{Escape(s.UiAutomation.FrameworkId)}\"");
        sb.AppendLine($"    control_type: \"{Escape(s.UiAutomation.ControlType)}\"");
        sb.AppendLine($"    is_enabled: {s.UiAutomation.IsEnabled}");
        sb.AppendLine($"    is_offscreen: {s.UiAutomation.IsOffscreen}");
        sb.AppendLine($"    error: \"{Escape(s.UiAutomation.Error)}\"");
        sb.AppendLine("    supported_patterns:");
        foreach (string p in s.UiAutomation.SupportedPatterns)
        {
            sb.AppendLine($"      - \"{Escape(p)}\"");
        }

        sb.AppendLine("  hierarchy_path:");
        foreach (HierarchyNode node in s.Hierarchy)
        {
            sb.AppendLine(
                $"    - {{ hwnd_hex: \"{node.HandleHex}\", hwnd_dec: \"{node.HandleDec}\", class: \"{Escape(node.ClassName)}\", title: \"{Escape(node.Title)}\" }}");
        }

        sb.AppendLine("  visible_text_lines:");
        foreach (string line in s.VisibleText.Lines)
        {
            sb.AppendLine($"    - \"{Escape(line)}\"");
        }

        if (s.VisibleText.Truncated)
        {
            sb.AppendLine("    - \"<truncated>\"");
        }

        sb.AppendLine("  all_text_lines:");
        foreach (string line in s.AllText.Lines)
        {
            sb.AppendLine($"    - \"{Escape(line)}\"");
        }

        if (s.AllText.Truncated)
        {
            sb.AppendLine("    - \"<truncated>\"");
        }

        return sb.ToString();
    }

    private static void AppendWindow(StringBuilder sb, string section, WindowInfo info)
    {
        sb.AppendLine($"  {section}:");
        sb.AppendLine($"    title: \"{Escape(info.Title)}\"");
        sb.AppendLine($"    class: \"{Escape(info.ClassName)}\"");
        sb.AppendLine($"    hwnd_hex: \"{info.HandleHex}\"");
        sb.AppendLine($"    hwnd_dec: \"{info.HandleDec}\"");
        sb.AppendLine($"    pid: {info.ProcessId}");
        sb.AppendLine($"    process: \"{Escape(info.ProcessName)}\"");
        sb.AppendLine($"    visible: {info.IsVisible}");
        sb.AppendLine($"    enabled: {info.IsEnabled}");
        sb.AppendLine("    rect_screen:");
        sb.AppendLine($"      x: {info.RectScreen.X}");
        sb.AppendLine($"      y: {info.RectScreen.Y}");
        sb.AppendLine($"      w: {info.RectScreen.Width}");
        sb.AppendLine($"      h: {info.RectScreen.Height}");
        sb.AppendLine("    rect_client:");
        sb.AppendLine($"      x: {info.RectClient.X}");
        sb.AppendLine($"      y: {info.RectClient.Y}");
        sb.AppendLine($"      w: {info.RectClient.Width}");
        sb.AppendLine($"      h: {info.RectClient.Height}");
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ToHex(IntPtr handle) => $"0x{handle.ToInt64():X}";

    private static string GetWindowTextRaw(IntPtr hwnd)
    {
        var sb = new StringBuilder(2048);
        _ = GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassNameRaw(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        _ = GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private const uint GA_ROOT = 2;

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

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr RealChildWindowFromPoint(IntPtr hWndParent, POINT ptParentClientCoords);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
}
