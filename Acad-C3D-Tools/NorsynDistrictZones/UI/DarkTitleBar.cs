using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NorsynDistrictZones.UI;

/// <summary>
/// Paints a standard WPF window's NATIVE title bar dark (Win10 1809+/Win11) via the DWM
/// immersive-dark-mode attribute. This keeps the OS min/max/close chrome — no custom
/// WindowChrome — so it is a window attribute, not a style, and stays out of Theme.xaml.
/// </summary>
internal static class DarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Hook a window so its title bar renders dark as soon as its HWND exists (before first paint).</summary>
    public static void Apply(Window window)
    {
        if (PresentationSource.FromVisual(window) is not null) Set(window);
        window.SourceInitialized += (_, _) => Set(window);
    }

    private static void Set(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int on = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    }
}
