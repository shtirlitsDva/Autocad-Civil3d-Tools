using System;
using System.Runtime.InteropServices;

namespace UiSnoop.Services;

internal static class WindowHighlightService
{
    private const int PS_INSIDEFRAME = 6;
    private const int NULL_BRUSH = 5;
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_FRAME = 0x0400;
    private const uint RDW_ALLCHILDREN = 0x0080;

    private const int HighlightWidth = 3;
    private static readonly int HighlightColor = 0x0000FF; // COLORREF: pure red (0x00BBGGRR)

    private static IntPtr lastHighlightedHwnd = IntPtr.Zero;

    public static IntPtr CurrentHighlight => lastHighlightedHwnd;

    public static void Highlight(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        ClearHighlight();

        if (!GetWindowRect(hwnd, out RECT rect))
        {
            return;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        IntPtr hdc = GetWindowDC(hwnd);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            IntPtr pen = CreatePen(PS_INSIDEFRAME, HighlightWidth, HighlightColor);
            IntPtr nullBrush = GetStockObject(NULL_BRUSH);

            IntPtr oldPen = SelectObject(hdc, pen);
            IntPtr oldBrush = SelectObject(hdc, nullBrush);

            Rectangle(hdc, 0, 0, width, height);

            SelectObject(hdc, oldPen);
            SelectObject(hdc, oldBrush);
            DeleteObject(pen);
        }
        finally
        {
            ReleaseDC(hwnd, hdc);
        }

        lastHighlightedHwnd = hwnd;
    }

    public static void ClearHighlight()
    {
        if (lastHighlightedHwnd == IntPtr.Zero)
        {
            return;
        }

        RedrawWindow(lastHighlightedHwnd, IntPtr.Zero, IntPtr.Zero,
            RDW_INVALIDATE | RDW_FRAME | RDW_ALLCHILDREN);

        lastHighlightedHwnd = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, int crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int fnObject);

    [DllImport("gdi32.dll")]
    private static extern bool Rectangle(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);
}
