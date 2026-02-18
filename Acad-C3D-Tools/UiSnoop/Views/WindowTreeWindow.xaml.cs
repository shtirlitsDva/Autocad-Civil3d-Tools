using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using UiSnoop.Services;
using UiSnoop.ViewModels;

namespace UiSnoop.Views;

public partial class WindowTreeWindow : Window
{
    public WindowTreeWindow(IntPtr rootHwnd)
    {
        InitializeComponent();
        DataContext = new WindowTreeViewModel(rootHwnd);
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
        WindowHighlightService.ClearHighlight();
    }

    private void OnTreePreviewMouseMove(object sender, MouseEventArgs e)
    {
        WindowNode? node = FindNodeUnderMouse(e);
        if (node != null)
        {
            if (WindowHighlightService.CurrentHighlight != node.Handle)
            {
                WindowHighlightService.Highlight(node.Handle);
            }
        }
        else
        {
            WindowHighlightService.ClearHighlight();
        }
    }

    private void OnTreeMouseLeave(object sender, MouseEventArgs e)
    {
        WindowHighlightService.ClearHighlight();
    }

    private static WindowNode? FindNodeUnderMouse(MouseEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return null;
        }

        DependencyObject? current = source;
        while (current != null)
        {
            if (current is TreeViewItem item && item.DataContext is WindowNode node)
            {
                // Only match if the mouse is over this item's header area,
                // not over a child item nested below it.
                if (IsMouseOverHeader(item, e))
                {
                    return node;
                }

                return null;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsMouseOverHeader(TreeViewItem item, MouseEventArgs e)
    {
        // The header content is the first row of the TreeViewItem template.
        // We check if the original source is NOT inside a nested TreeViewItem's items host.
        // If the visual source walks up and hits this TreeViewItem first (before any child
        // TreeViewItem), we are on the header.
        DependencyObject? current = e.OriginalSource as DependencyObject;
        while (current != null && current != item)
        {
            if (current is TreeViewItem)
            {
                // Hit a child TreeViewItem before reaching our item — mouse is over a child.
                return false;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return true;
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
