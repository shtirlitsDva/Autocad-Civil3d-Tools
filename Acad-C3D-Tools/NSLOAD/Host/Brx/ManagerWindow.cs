using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Bricscad.Windows;

using Panel = Bricscad.Windows.Panel;

namespace NSLOAD
{
    /// <summary>
    /// The window NSLOADMGR shows: on BricsCAD a native tool panel. Its
    /// PaletteSet is a compatibility shim that loses auto-hide when docked and
    /// forgets where it was. The AutoCAD build has its own copy of this file.
    /// </summary>
    /// <remarks>
    /// Rules from X:\AutoCAD DRI - 01 Civil 3D\Dev\00 Bricscad porting\ui-panels.md:
    /// the panel is made when NSLOAD loads, so its icon is on the stack from
    /// startup and BricsCAD puts it back where the workspace last had it; the
    /// command only brings it forward. Its name is its identity and its tab
    /// label, and is saved in the workspace, so it never changes. NSLOAD is never
    /// hot-reloaded, so the panel lives for the session and is never removed;
    /// in particular <see cref="Release"/> touches nothing native, because at
    /// exit BricsCAD frees its panels before plugins terminate.
    /// </remarks>
    internal static class ManagerWindow
    {
        private const string PanelName = "NSLOAD";

        private static Panel? _panel;

        /// <summary>Called once NSLOAD has read its register and config, which
        /// the view shows. Makes the panel; BricsCAD defers a bundle's Initialize
        /// until the first drawing opens, so this runs then.</summary>
        public static void Prepare(Func<FrameworkElement> makeView)
        {
            _panel ??= new Panel(PanelName,
                new DockingTemplate(DockSides.Right, "RDOCK", 40), makeView())
            {
                Title = "NSLOAD Manager",
                Icon = GlyphIcon("\uE71D"), // Segoe MDL2 "AllApps"
            };
        }

        /// <summary>Opens the panel, or brings it forward when it is open: hide
        /// then show expands it in an icon or flyout stack and selects its tab
        /// in a tabbed one, as -TOOLPANEL Show does. Show alone does nothing on
        /// an open panel.</summary>
        public static void BringForward()
        {
            if (_panel == null) return;
            if (_panel.Visible) _panel.Visible = false;
            _panel.Visible = true;
        }

        /// <summary>Nothing: the panel lives for the session, and while BricsCAD
        /// quits any call on it reads freed memory.</summary>
        public static void Release() { }

        // A panel icon must be a bitmap (a DrawingImage shows BricsCAD's "P"
        // placeholder), so the glyph is rendered once into one.
        private static ImageSource GlyphIcon(string glyph)
        {
            const int px = 32;
            var text = new FormattedText(glyph, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe MDL2 Assets"), px,
                Brushes.White, 1.0);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                dc.DrawText(text, new Point((px - text.Width) / 2, (px - text.Height) / 2));
            var bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
    }
}
