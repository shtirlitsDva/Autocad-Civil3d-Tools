using System.Threading;

namespace SheetCreationAutomation.UI
{
    internal static class AcContext
    {
        public static SynchronizationContext? Current { get; set; }
    }
}
