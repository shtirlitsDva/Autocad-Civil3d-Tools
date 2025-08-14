using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;


namespace PipeScheduleV2Tests
{
    public class PipeScheduleV2TestsClass : IExtensionApplication
    {
        public void Initialize()
        {
            prdDbg("PipeScheduleV2Tests loaded!");
        }

        public void Terminate()
        {
            
        }

        [CommandMethod("RUNPS2TESTS")]
        public void runps2tests()
        {
            prdDbg("Testing started!");


            prdDbg("Testing ended!");
        }
    }
}
