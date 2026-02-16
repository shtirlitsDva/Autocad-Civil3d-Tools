using Autodesk.AutoCAD.Windows;
using SheetCreationAutomation.ViewModels;
using System;
using System.Drawing;

namespace SheetCreationAutomation.UI
{
    internal sealed class SheetAutomationPaletteSet : PaletteSet
    {
        public SheetAutomationPaletteSet()
            : base("SheetAutomation", "SCAUI", new Guid("64D4D4EE-A6FA-4034-A055-D017A44604B0"))
        {
            PaletteExceptionGuard.EnsureInitialized();

            Style =
                PaletteSetStyles.ShowAutoHideButton |
                PaletteSetStyles.ShowCloseButton |
                PaletteSetStyles.ShowPropertiesMenu;

            MinimumSize = new Size(380, 280);
            Location = new Point(20, 20);

            var viewFramesVm = new ViewFramesAutomationViewModel();
            var viewFramesControl = new ViewFramesAutomationControl(viewFramesVm);
            var sheetsControl = new SheetsAutomationControl(new SheetsAutomationViewModel());
            var finalizeControl = new FinalizeAutomationControl(new FinalizeAutomationViewModel());
            var debugControl = new DebugVisualTreeControl(new DebugVisualTreeViewModel());

            AddVisual("VIEW FRAMES", viewFramesControl);
            AddVisual("SHEETS", sheetsControl);
            AddVisual("FINALIZE", finalizeControl);
            AddVisual("DEBUG", debugControl);
            Activate(0);
        }
    }
}
