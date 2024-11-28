using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    internal class CustomPaletteSet : PaletteSet
    {
        static bool wasVisible;

        public CustomPaletteSet() : base("DimV2", "DIM2MAP", new Guid("75B45F74-C728-432B-AC50-9A5F345A3877"))
        {
            Style =
                PaletteSetStyles.ShowAutoHideButton |
                PaletteSetStyles.ShowCloseButton |
                PaletteSetStyles.ShowPropertiesMenu;
            MinimumSize = new System.Drawing.Size(250, 150);

            AddVisual("MAP", new MainWindow());
            AddVisual("SETTINGS", new SettingsTab());

            // automatically hide the palette while none document is active (no document state)
            var docs = Application.DocumentManager;
            docs.DocumentBecameCurrent += (s, e) =>
                Visible = e.Document == null ? false : wasVisible;
            docs.DocumentCreated += (s, e) =>
                Visible = wasVisible;
            docs.DocumentToBeDeactivated += (s, e) =>
                wasVisible = Visible;
            docs.DocumentToBeDestroyed += (s, e) =>
            {
                wasVisible = Visible;
                if (docs.Count == 1)
                    Visible = false;
            };
        }
    }
}
