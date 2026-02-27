using System;

using Autodesk.AutoCAD.Windows;

using NSPaletteSet.Views;

namespace NSPaletteSet
{
    public class MyPaletteSet : PaletteSet
    {
        public MyPaletteSet()
            : base("NSPalette", "NSPALETTE", new Guid("87374E16-C0DB-4F3F-9271-7A71ED921226"))
        {
            Style = PaletteSetStyles.ShowCloseButton
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowTabForSingle;

            AddVisual("Rør", new PipePaletteView());
        }
    }
}
