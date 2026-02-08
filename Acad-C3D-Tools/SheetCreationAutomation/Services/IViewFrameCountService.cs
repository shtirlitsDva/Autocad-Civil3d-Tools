using Autodesk.AutoCAD.DatabaseServices;

namespace SheetCreationAutomation.Services
{
    internal interface IViewFrameCountService
    {
        int GetViewFrameCount(Database database);
    }
}
