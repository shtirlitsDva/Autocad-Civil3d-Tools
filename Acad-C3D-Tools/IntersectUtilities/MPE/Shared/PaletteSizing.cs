using System;
using System.Drawing;
using Autodesk.AutoCAD.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.MPE.Shared
{
    internal static class PaletteSizing
    {
        // Forces a default floating size onto a freshly shown PaletteSet. Call it once, right after
        // the palette is first made visible. Three things conspire to make a plain
        // `Size = ...` not stick, so all three are handled:
        //   1. Size set in the constructor's object initializer is discarded — there is no window to
        //      size yet at construction time, so it falls back to MinimumSize.
        //   2. A docked palette ignores Size (its size comes from the dock strip), so it is undocked
        //      first.
        //   3. AutoCAD finishes realizing/restoring the palette window asynchronously, and that pass
        //      can overwrite a size set synchronously here — so the size is re-applied once on the
        //      next Application.Idle, which lands after the restore pass.
        // Apply it only on first show so a size the user has since dragged is respected.
        public static void ApplyDefault(PaletteSet paletteSet, Size size)
        {
            paletteSet.Dock = DockSides.None;
            paletteSet.Size = size;

            void OnIdle(object? sender, EventArgs e)
            {
                AcadApp.Idle -= OnIdle;
                try
                {
                    if (paletteSet.Size != size)
                    {
                        paletteSet.Size = size;
                    }
                }
                catch
                {
                    // Palette disposed before idle fired — nothing to size.
                }
            }

            AcadApp.Idle += OnIdle;
        }
    }
}
