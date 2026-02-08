using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UiSnoop.Services;
using UiSnoop.ViewModels;

namespace UiSnoop;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;

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

    private const int DwmaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmaUseImmersiveDarkMode = 20;
    private const int DwmaBorderColor = 34;
    private const int DwmaCaptionColor = 35;
    private const int DwmaTextColor = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
}
