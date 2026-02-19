using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace UiSnoop.ViewModels;

internal sealed class WindowTreeViewModel : ObservableObject
{
    private readonly IntPtr rootHwnd;
    private string statusText = string.Empty;

    public WindowTreeViewModel(IntPtr rootHwnd)
    {
        this.rootHwnd = rootHwnd;
        RefreshCommand = new RelayCommand(Refresh);
        CopyTreeCommand = new RelayCommand(CopyTreeToClipboard);
        Refresh();
    }

    public ObservableCollection<WindowNode> RootNodes { get; } = new();

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand CopyTreeCommand { get; }

    private void Refresh()
    {
        RootNodes.Clear();

        if (rootHwnd == IntPtr.Zero || !IsWindow(rootHwnd))
        {
            StatusText = "Root window is no longer valid.";
            return;
        }

        WindowNode root = BuildNode(rootHwnd);
        BuildChildrenRecursive(root, rootHwnd, depth: 0, maxDepth: 30);
        RootNodes.Add(root);

        int total = CountNodes(root);
        StatusText = $"Root: 0x{rootHwnd.ToInt64():X} — {total} window(s)";
    }

    private static void BuildChildrenRecursive(WindowNode parentNode, IntPtr parentHwnd, int depth, int maxDepth)
    {
        if (depth >= maxDepth)
        {
            return;
        }

        var children = new List<IntPtr>();
        EnumChildWindows(parentHwnd, (child, _) =>
        {
            children.Add(child);
            return true;
        }, IntPtr.Zero);

        foreach (IntPtr child in children)
        {
            if (GetParent(child) != parentHwnd)
            {
                continue;
            }

            WindowNode childNode = BuildNode(child);
            BuildChildrenRecursive(childNode, child, depth + 1, maxDepth);
            parentNode.Children.Add(childNode);
        }
    }

    private static WindowNode BuildNode(IntPtr hwnd)
    {
        string title = GetWindowTextRaw(hwnd);
        string className = GetClassNameRaw(hwnd);
        bool isVisible = IsWindowVisible(hwnd);
        bool isEnabled = IsWindowEnabled(hwnd);

        GetWindowRect(hwnd, out RECT rect);
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;

        string classNN = ComputeClassNN(hwnd, className);

        return new WindowNode
        {
            Handle = hwnd,
            HandleHex = $"0x{hwnd.ToInt64():X}",
            ClassName = className,
            Title = title,
            ClassNN = classNN,
            IsVisible = isVisible,
            IsEnabled = isEnabled,
            RectX = rect.Left,
            RectY = rect.Top,
            RectWidth = w,
            RectHeight = h
        };
    }

    private static string ComputeClassNN(IntPtr hwnd, string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return string.Empty;
        }

        IntPtr root = GetAncestor(hwnd, GA_ROOT);
        if (root == IntPtr.Zero || root == hwnd)
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

        return className + "1";
    }

    private void CopyTreeToClipboard()
    {
        if (RootNodes.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        foreach (WindowNode root in RootNodes)
        {
            AppendNodeLlmFormat(sb, root, depth: 0);
        }

        System.Windows.Clipboard.SetText(sb.ToString());
        StatusText = $"Copied {CountNodes(RootNodes[0])} node(s) to clipboard.";
    }

    private static void AppendNodeLlmFormat(StringBuilder sb, WindowNode node, int depth)
    {
        sb.Append(' ', depth * 2);

        sb.Append('[').Append(node.ClassName).Append(']');

        if (!string.IsNullOrEmpty(node.Title))
        {
            sb.Append(" \"").Append(node.Title).Append('"');
        }

        sb.Append(" — ").Append(node.HandleHex);
        sb.Append("  ClassNN: ").Append(node.ClassNN);
        sb.Append("  Vis: ").Append(node.IsVisible ? "Y" : "N");
        sb.Append("  En: ").Append(node.IsEnabled ? "Y" : "N");
        sb.Append("  Rect: ").Append(node.RectX).Append(',').Append(node.RectY)
          .Append(' ').Append(node.RectWidth).Append('x').Append(node.RectHeight);
        sb.AppendLine();

        foreach (WindowNode child in node.Children)
        {
            AppendNodeLlmFormat(sb, child, depth + 1);
        }
    }

    private static int CountNodes(WindowNode node)
    {
        int count = 1;
        foreach (WindowNode child in node.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }

    private static string GetWindowTextRaw(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassNameRaw(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private const uint GA_ROOT = 2;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
}

internal sealed class WindowNode : ObservableObject
{
    public IntPtr Handle { get; init; }
    public string HandleHex { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ClassNN { get; init; } = string.Empty;
    public bool IsVisible { get; init; }
    public bool IsEnabled { get; init; }
    public int RectX { get; init; }
    public int RectY { get; init; }
    public int RectWidth { get; init; }
    public int RectHeight { get; init; }
    public ObservableCollection<WindowNode> Children { get; } = new();

    public string HeaderText =>
        string.IsNullOrEmpty(Title)
            ? $"[{ClassName}] — {HandleHex}"
            : $"[{ClassName}] \"{Truncate(Title, 40)}\" — {HandleHex}";

    public string DetailText =>
        $"ClassNN: {ClassNN}    Rect: {RectX},{RectY} {RectWidth}x{RectHeight}    Visible: {(IsVisible ? "Yes" : "No")}  Enabled: {(IsEnabled ? "Yes" : "No")}";

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 1)] + "\u2026";
    }
}
