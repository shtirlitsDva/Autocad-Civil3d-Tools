using DimensioneringV2.UI;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2
{
    internal static class Utils
    {
        public static void prtDbg(string msg = "")
        {
            AcContext.Current.Post(_ => { IntersectUtilities.UtilsCommon.Utils.prdDbg(msg); }, null);
        }
        public static void prtDbg(object obj)
        {
            AcContext.Current.Post(_ => { IntersectUtilities.UtilsCommon.Utils.prdDbg(obj); }, null);
        }
    }
}
