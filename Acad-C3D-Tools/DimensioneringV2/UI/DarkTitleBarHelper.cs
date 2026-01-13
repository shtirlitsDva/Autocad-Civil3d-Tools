using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DimensioneringV2.UI
{
    /// <summary>
    /// Helper to enable dark title bar on Windows 10/11.
    /// </summary>
    public static class DarkTitleBarHelper
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        /// <summary>
        /// Enables dark mode for the window's title bar.
        /// Call this in the window's Loaded event or after the window handle is available.
        /// </summary>
        public static void EnableDarkTitleBar(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int darkMode = 1;
                
                // Try the newer attribute first (Windows 10 20H1+), then fall back
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
                }
            }
            catch
            {
                // Ignore errors on older Windows versions
            }
        }
    }
}
