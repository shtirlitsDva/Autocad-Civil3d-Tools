using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using UiSnoop.Services;
using UiSnoop.ViewModels;
using UiSnoop.Views;

namespace UiSnoop;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private bool isPickMode;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainWindowViewModel(new UiSnoopCaptureService());
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyDarkTitleBar();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        viewModel.Dispose();
    }

    private void OnPickAndTreeClick(object sender, RoutedEventArgs e)
    {
        if (isPickMode)
        {
            return;
        }

        isPickMode = true;
        Cursor = Cursors.Cross;
        Mouse.Capture(this, CaptureMode.Element);
        PreviewMouseLeftButtonDown += OnPickMouseDown;
        PreviewKeyDown += OnPickKeyDown;
    }

    private void OnPickMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ExitPickMode();

        GetCursorPos(out POINT pt);
        IntPtr hit = WindowFromPoint(pt);
        if (hit == IntPtr.Zero)
        {
            return;
        }

        IntPtr root = GetAncestor(hit, GA_ROOT);
        if (root == IntPtr.Zero)
        {
            root = hit;
        }

        IntPtr ownHwnd = new WindowInteropHelper(this).Handle;
        if (root == ownHwnd)
        {
            return;
        }

        var treeWindow = new WindowTreeWindow(root)
        {
            Owner = this
        };
        treeWindow.Show();
    }

    private void OnPickKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ExitPickMode();
        }
    }

    private void ExitPickMode()
    {
        isPickMode = false;
        Cursor = Cursors.Arrow;
        Mouse.Capture(null);
        PreviewMouseLeftButtonDown -= OnPickMouseDown;
        PreviewKeyDown -= OnPickKeyDown;
    }

    private void ApplyDarkTitleBar()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        const int enabled = 1;
        SetDwmIntAttribute(hwnd, DwmaUseImmersiveDarkMode, enabled);
        SetDwmIntAttribute(hwnd, DwmaUseImmersiveDarkModeBefore20H1, enabled);

        // COLORREF values are 0x00BBGGRR.
        SetDwmIntAttribute(hwnd, DwmaCaptionColor, unchecked((int)0x00272623));
        SetDwmIntAttribute(hwnd, DwmaBorderColor, unchecked((int)0x00474040));
        SetDwmIntAttribute(hwnd, DwmaTextColor, unchecked((int)0x00EBE8E8));
    }

    private static void SetDwmIntAttribute(IntPtr hwnd, int attribute, int value)
    {
        _ = DwmSetWindowAttribute(hwnd, attribute, ref value, Marshal.SizeOf<int>());
    }

    private const uint GA_ROOT = 2;
    private const int DwmaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmaUseImmersiveDarkMode = 20;
    private const int DwmaBorderColor = 34;
    private const int DwmaCaptionColor = 35;
    private const int DwmaTextColor = 36;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
}
