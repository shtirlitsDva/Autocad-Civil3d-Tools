using System;
using System.Drawing;
using FrameworkElement = System.Windows.FrameworkElement;

using Autodesk.AutoCAD.Windows;

namespace NSLOAD
{
    /// <summary>
    /// The window NSLOADMGR shows: on AutoCAD a PaletteSet, made the first time
    /// it is asked for. The BricsCAD build has its own copy of this file.
    /// </summary>
    internal static class ManagerWindow
    {
        private static readonly Guid PaletteGuid =
            new("A7E3F1B2-9C4D-4E8A-B6D5-2F1A3C7E9B04");

        private static PaletteSet? _palette;
        private static Func<FrameworkElement>? _makeView;

        /// <summary>Called once NSLOAD has read its register and config, which
        /// the view shows. AutoCAD makes the palette later, when it is first shown.</summary>
        public static void Prepare(Func<FrameworkElement> makeView) => _makeView = makeView;

        public static void BringForward()
        {
            if (_palette == null)
            {
                if (_makeView == null) return;

                _palette = new PaletteSet("NSLOAD Manager", PaletteGuid)
                {
                    Size = new Size(400, 500),
                    MinimumSize = new Size(300, 200),
                    DockEnabled = DockSides.Left | DockSides.Right,
                };
                _palette.AddVisual("Plugins", _makeView());
            }
            _palette.Visible = true;
        }

        /// <summary>NSLOAD is unloading: dispose the palette so it does not
        /// survive an unload/reload cycle.</summary>
        public static void Release()
        {
            if (_palette == null) return;
            try
            {
                _palette.Visible = false;
                _palette.Dispose();
            }
            catch (System.Exception ex) { NsLoadDiagnostics.Report("manager palette dispose", ex); }
            _palette = null;
        }
    }
}
