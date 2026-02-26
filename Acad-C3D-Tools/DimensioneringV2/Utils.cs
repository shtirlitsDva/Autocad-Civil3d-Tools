using DimensioneringV2.UI;

using DimensioneringV2.UI.Infrastructure;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.IO;
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
        public static void ClrFile(string fullPathAndName)
        {
            //Clear the output file
            System.IO.File.WriteAllBytes(fullPathAndName, new byte[0]);
        }
        public static void OutputWriter(
            string fullPathAndName, string sr, bool clearFile = false, bool useBOM = true)
        {
            if (clearFile) System.IO.File.WriteAllBytes(fullPathAndName, new byte[0]);

            if (useBOM)
            {
                // Write to output file
                using (StreamWriter w = new StreamWriter(fullPathAndName, true, Encoding.UTF8))
                {
                    w.Write(sr);
                    w.Close();
                }
            }
            else
            {
                // Create UTF-8 encoding without BOM
                var utf8WithoutBom = new System.Text.UTF8Encoding(false);

                // Write to output file
                using (StreamWriter w = new StreamWriter(fullPathAndName, true, utf8WithoutBom))
                {
                    w.Write(sr);
                    w.Close();
                }
            }
        }

        public static void OutputWriter(
            string fullPathAndName, StringBuilder sb, bool clearFile = false, bool useBOM = true) =>
            OutputWriter(fullPathAndName, sb.ToString(), clearFile, useBOM);
    }
}
