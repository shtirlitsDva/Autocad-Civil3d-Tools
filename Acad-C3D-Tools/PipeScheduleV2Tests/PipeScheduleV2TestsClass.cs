using Autodesk.AutoCAD.Runtime;

namespace PipeScheduleV2Tests
{
    public partial class PipeScheduleV2TestsClass : IExtensionApplication
    {
        internal const string RegistryFileName = "PS2_PolyRegistry.csv";
        internal const string ReportsFolderName = "Reports";

        public void Initialize() { IntersectUtilities.UtilsCommon.Utils.prdDbg("PipeScheduleV2Tests loaded!"); }
        public void Terminate() { }
    }
}