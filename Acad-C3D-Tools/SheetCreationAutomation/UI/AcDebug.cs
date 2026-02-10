using IntersectUtilities.UtilsCommon;

namespace SheetCreationAutomation.UI
{
    internal static class AcDebug
    {
        public static void Print(string msg = "")
        {
            if (AcContext.Current == null)
            {
                Utils.prdDbg(msg);
                return;
            }

            AcContext.Current.Post(_ => { Utils.prdDbg(msg); }, null);
        }

        public static void Print(object obj)
        {
            if (AcContext.Current == null)
            {
                Utils.prdDbg(obj);
                return;
            }

            AcContext.Current.Post(_ => { Utils.prdDbg(obj); }, null);
        }
    }
}
